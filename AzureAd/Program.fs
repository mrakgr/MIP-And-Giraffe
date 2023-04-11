open System.Security.Claims
open Microsoft.AspNetCore.Authentication.Cookies
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Authentication
open Microsoft.AspNetCore.Authentication.OpenIdConnect
open Microsoft.Identity.Web
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.DependencyInjection
open Giraffe
open Microsoft.EntityFrameworkCore
open System.ComponentModel.DataAnnotations

module Pages =
    open Giraffe.ViewEngine
    
    let master title_str body_list =
        html [] [
            head [] [
                title [] [
                    str title_str
                ]
            ]
            body [] body_list
        ]
    
    let index name = master "Index" [
        p [] [
            str $"Welcome %s{name}! You've logged in successfully!"
        ]
    ]
        
    let error = master "Error" [
        str "404 - Page Not Found"
    ]
    
module Handler =
    let index : HttpHandler = fun next ctx -> task {
        let! x = ctx.AuthenticateAsync()
        if x.Succeeded then
            return! htmlView (Pages.index x.Principal.Identity.Name) next ctx
        else
            do! ctx.ChallengeAsync()
            return! next ctx
    }
        
let webApp : HttpHandler =
    choose [
        GET >=> choose [
            route "/" >=> Handler.index
        ]
        setStatusCode 404 >=> htmlView Pages.error
    ]
   
[<EntryPoint>]
let main args =
    let builder = WebApplication.CreateBuilder(args)
    builder.Services.AddGiraffe() |> ignore
    builder.Services
        .AddAuthentication(OpenIdConnectDefaults.AuthenticationScheme)
        .AddMicrosoftIdentityWebApp(builder.Configuration.GetSection("AzureAd"))
          |> ignore

    let app = builder.Build()

    app.UseGiraffe(webApp)

    app.Run()

    0 // Exit code

