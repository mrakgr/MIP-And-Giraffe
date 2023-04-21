module App

open Elmish
open Elmish.React
open Fable.Core
open Feliz
open Feliz.React.Msal
open Feliz.React.Msal.Hooks
open Shared

#if DEBUG
open Elmish.Debug
open Elmish.HMR
#endif

let client : IPublicClientApplication =
    let auth : Auth = { clientId = Shared.AzureAD.config.ClientId
                        authority = $"https://login.microsoftonline.com/{AzureAD.config.TenantId}"
                        knownAuthorities = [|"login.live.com"|]
                        redirectUri = null
                        postLogoutRedirectUri = null }
    let cache : Cache = { cacheLocation = "sessionStorage"
                          storeAuthStateInCookie = false }
    let config : MsalConfig = { auth = auth
                                cache = cache }
    createClient config

type AuthResult = {
    acquireToken : InteractionType * AuthenticationRequest option -> obj
    login : InteractionType * AuthenticationRequest option -> unit
    result : obj
    error : obj
}

[<ReactComponent>]
let Main() =
    let x : AuthResult = useMsalAuthentication(InteractionType.Redirect,JS.undefined,JS.undefined)
    // let tok = x.acquireToken(InteractionType.Redirect,None)

    let ctx = useMsal()

    Html.div [
        Html.p "Anyone can see this paragraph"
        AuthenticatedTemplate.create [
            AuthenticatedTemplate.children [
                Html.p $"Signed in as: {ctx.accounts |> Array.tryHead |> Option.map (fun x -> x.username)}"
            ]
        ]

        UnauthenticatedTemplate.create [
            UnauthenticatedTemplate.children [
                Html.p "No users are signed in."
            ]
        ]
    ]

[<ReactComponent>]
let App () =
    MsalProvider.create[
        MsalProvider.instance client
        MsalProvider.children[
            Main()
        ]
    ]

open Browser.Dom
ReactDOM.createRoot(document.getElementById("elmish-app")).render(App())


// Program.mkProgram Index.init Index.update Index.view
// #if DEBUG
// |> Program.withConsoleTrace
// #endif
// |> Program.withReactSynchronous "elmish-app"
// #if DEBUG
// |> Program.withDebugger
// #endif
// |> Program.run