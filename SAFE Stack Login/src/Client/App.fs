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
        clientId AzureAD.config.ClientId
        authority AzureAD.config.Authority
        }
    msalConfiguration { auth opts }

let pci = PublicClientApplication(pciConfig)

module Error =
    [<Import("InteractionRequiredAuthError", from = "@azure/msal-browser")>]
    type InteractionRequiredAuthError() =
        inherit Exception()

promise {
    let fin (authResult : AuthenticationResult) = createProgram authResult.accessToken
    match! pci.handleRedirectPromise () with
    | Some authResult ->
        Browser.Dom.window.sessionStorage.setItem("current_account",authResult.account |> JS.JSON.stringify)
        return fin authResult
    | None ->
        match Browser.Dom.window.sessionStorage.getItem("current_account") with
        | null -> do! pci.loginRedirect()
        | acc ->
            let c = msalSilentRequest {account (JS.JSON.parse acc :?> AccountInfo)}
            let! authResult = pci.acquireTokenSilent c
            return fin authResult
} |> Promise.catchEnd (function
    | :? Error.InteractionRequiredAuthError -> pci.loginRedirect() |> Promise.start
    | ex ->
        let view =
            Html.div [
                Html.text $"An error happened. Message: %s{ex.Message}"
            ]
        ReactDOM.createRoot(Browser.Dom.document.getElementById "elmish-app").render view
    )
