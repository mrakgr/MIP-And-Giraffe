module Client.Auth

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

let acquire_token () =
    let login_redirect redirectConfig = pci.loginRedirect(redirectConfig) |> unbox
    promise {
        match Browser.Dom.window.sessionStorage.getItem("current_account") with
        | null -> return login_redirect redirectConfig
        | acc ->
            try
                return! pci.acquireTokenSilent (silentConfig (JS.JSON.parse acc :?> AccountInfo))
            with
                :? Error.InteractionRequiredAuthError -> return login_redirect redirectConfig
    }