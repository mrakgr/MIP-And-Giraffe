module Client.Auth

open System
open Fable.Core
open Fable.Msal

let pciConfig =
    let opts = msalBrowserAuthOptions {
        clientId "8ca7907a-d757-40ea-96a8-9e1a75a99f30"
        authority $"https://login.microsoftonline.com/ec88369d-6c2f-4f15-b0c7-adbe35caec77"
        }
    msalConfiguration { auth opts }

let pci = PublicClientApplication(pciConfig)

[<Import("InteractionRequiredAuthError", from = "@azure/msal-browser")>]
type InteractionRequiredAuthError() =
    inherit Exception()

let redirectConfig = msalRedirectRequest {
    prompt "none"
    scopes [
        "api://c23c5d70-996d-4e55-a458-2940bd2e79b4/Any"
    ]
}

let silentConfig (x : AccountInfo) = msalSilentRequest {
    account x
    scopes [
        "api://c23c5d70-996d-4e55-a458-2940bd2e79b4/Any"
    ]
}

let acquire_token () =
    let login_redirect redirectConfig = pci.loginRedirect(redirectConfig) |> unbox
    promise {
        match Browser.Dom.window.localStorage.getItem("old_account") with
        | null -> return login_redirect redirectConfig
        | acc ->
            let authResult = JS.JSON.parse acc :?> AccountInfo
            try return! pci.acquireTokenSilent (silentConfig authResult)
            with :? InteractionRequiredAuthError -> return login_redirect redirectConfig
    }

open Shared
open Fable.Remoting.Client

let inline private create_proxy' (authResult : AuthenticationResult option) =
    Remoting.createApi ()
    |> Option.foldBack (fun authResult -> Remoting.withAuthorizationHeader $"Bearer %s{authResult.accessToken}") authResult
    |> Remoting.withRouteBuilder Route.builder
    |> Remoting.buildProxy<'api>

open FSharp.Reflection
let inline wrap_proxy (proxy : 'api) =
    let mutable r = proxy
    FSharpType.GetRecordFields(typeof<'api>)
    |> Array.map (fun m ->
        if m.PropertyType |> FSharpType.GetFunctionElements |> snd |> FSharpType.IsFunction then
            failwith "The function must not be nested."
        box (fun x -> async {
            let body () : _ Async = unbox (FSharpValue.GetRecordField(r,m)) x
            try return! body ()
            with :? ProxyRequestException as ex when ex.StatusCode = 401 ->
                let p = promise {
                    let! authResult = acquire_token ()
                    r <- create_proxy' (Some authResult)
                    return! body () |> Async.StartAsPromise
                    }
                return! Async.AwaitPromise p
            }))
    |> fun x -> FSharpValue.MakeRecord(typeof<'api>,x) :?> 'api

let inline private create_proxy_templ access_token = create_proxy' access_token |> wrap_proxy

let create_proxy access_token : ITodosApi = create_proxy_templ access_token