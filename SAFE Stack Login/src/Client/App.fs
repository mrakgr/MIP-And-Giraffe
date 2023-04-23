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

let create_proxy access_token =
    Remoting.createApi ()
    |> Remoting.withAuthorizationHeader $"Bearer {access_token}"
    |> Remoting.withRouteBuilder Route.builder
    |> Remoting.buildProxy<ITodosApi>

let createProgram access_token =
    let todosApi = (create_proxy access_token)

    Program.mkProgram (Index.init todosApi) (Index.update todosApi) Index.view
    #if DEBUG
    |> Program.withConsoleTrace
    #endif
    |> Program.withReactSynchronous "elmish-app"
    #if DEBUG
    |> Program.withDebugger
    #endif
    |> Program.run

open Client.Auth

promise {
    do! pci.initialize()
    let fin (authResult : Fable.Msal.AuthenticationResult) = createProgram authResult.accessToken
    match! pci.handleRedirectPromise () with
    | Some authResult ->
        Browser.Dom.window.sessionStorage.setItem("current_account",authResult.account |> JS.JSON.stringify)
        return fin authResult
    | None ->
        let! authResult = acquire_token()
        return fin authResult
} |> Promise.catchEnd (fun ex ->
        let view =
            Html.div [
                Html.text $"An error happened. Message: %s{ex.Message}"
            ]
        ReactDOM.createRoot(Browser.Dom.document.getElementById "elmish-app").render view
    )