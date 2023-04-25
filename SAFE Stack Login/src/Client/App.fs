module App

open Elmish
open Elmish.React
open Fable.Core
open Feliz

open Client.Auth

#if DEBUG
open Elmish.Debug
open Elmish.HMR
#endif

let createProgram access_token =
    let todosApi = create_proxy access_token

    Program.mkProgram (Index.init todosApi) (Index.update todosApi) Index.view
    #if DEBUG
    |> Program.withConsoleTrace
    #endif
    |> Program.withReactSynchronous "elmish-app"
    #if DEBUG
    |> Program.withDebugger
    #endif
    |> Program.run

promise {
    do! pci.initialize()
    let fin (authResult : Fable.Msal.AuthenticationResult) = createProgram authResult
    match! pci.handleRedirectPromise () with
    | Some authResult ->
        Browser.Dom.window.localStorage.setItem("old_account",authResult.account |> JS.JSON.stringify)
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