module App

open Client
open Elmish
open Elmish.React
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
    match! pci.handleRedirectPromise () with
    | Some authResult ->
        do! LocalForage.setItem "old_account" authResult.account
        return createProgram (Some authResult)
    | None ->
        return createProgram None

} |> Promise.catchEnd (fun ex ->
        let view =
            Html.div [
                Html.text $"An error happened. Message: %s{ex.Message}"
            ]
        ReactDOM.createRoot(Browser.Dom.document.getElementById "elmish-app").render view
    )

