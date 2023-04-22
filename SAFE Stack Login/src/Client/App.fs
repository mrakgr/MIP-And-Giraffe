module App

open System
open Elmish
open Elmish.React
open Fable.Core
open Fable.Remoting.Client
open Feliz
open Shared

#if DEBUG
open Elmish.Debug
open Elmish.HMR
#endif

let createProgram access_token =
    let todosApi =
        Remoting.createApi ()
        |> Remoting.withAuthorizationHeader $"Bearer {access_token}"
        |> Remoting.withRouteBuilder Route.builder
        |> Remoting.buildProxy<ITodosApi>

    Program.mkProgram (Index.init todosApi) (Index.update todosApi) Index.view
    #if DEBUG
    |> Program.withConsoleTrace
    #endif
    |> Program.withReactSynchronous "elmish-app"
    #if DEBUG
    |> Program.withDebugger
    #endif
    |> Program.run

open Fable.Msal

let pciConfig =
    let opts = msalBrowserAuthOptions {
        clientId "8ca7907a-d757-40ea-96a8-9e1a75a99f30"
        authority $"https://login.microsoftonline.com/ec88369d-6c2f-4f15-b0c7-adbe35caec77"
        }
    msalConfiguration { auth opts }

let pci = PublicClientApplication(pciConfig)

module Error =
    [<Import("InteractionRequiredAuthError", from = "@azure/msal-browser")>]
    type InteractionRequiredAuthError() =
        inherit Exception()

let redirectConfig = msalRedirectRequest {
    scopes [
        "api://c23c5d70-996d-4e55-a458-2940bd2e79b4/Any"
    ]
}

let silentConfig acc = msalSilentRequest {
    account acc
    scopes [
        "api://c23c5d70-996d-4e55-a458-2940bd2e79b4/Any"
    ]
}

promise {
    let fin (authResult : AuthenticationResult) = createProgram authResult.accessToken
    match! pci.handleRedirectPromise () with
    | Some authResult ->
        Browser.Dom.window.sessionStorage.setItem("current_account",authResult.account |> JS.JSON.stringify)
        return fin authResult
    | None ->
        match Browser.Dom.window.sessionStorage.getItem("current_account") with
        | null -> do! pci.loginRedirect(redirectConfig)
        | acc ->
            let! authResult = pci.acquireTokenSilent (silentConfig (JS.JSON.parse acc :?> AccountInfo))
            return fin authResult
} |> Promise.catchEnd (function
    | :? Error.InteractionRequiredAuthError -> pci.loginRedirect(redirectConfig) |> Promise.start
    | ex ->
        let view =
            Html.div [
                Html.text $"An error happened. Message: %s{ex.Message}"
            ]
        ReactDOM.createRoot(Browser.Dom.document.getElementById "elmish-app").render view
    )
