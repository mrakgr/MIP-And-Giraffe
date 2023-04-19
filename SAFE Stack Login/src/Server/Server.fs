module Server

open System
open System.Threading.Tasks
open Fable.Remoting.Server
open Fable.Remoting.Giraffe

open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Http
open Microsoft.AspNetCore.Authentication
open Microsoft.Identity.Web
open Microsoft.AspNetCore.Authentication.OpenIdConnect
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.DependencyInjection
open Giraffe

open Shared

module Storage =
    let todos = ResizeArray()

    let addTodo (todo: Todo) =
        if Todo.isValid todo.Description then
            todos.Add todo
            Ok()
        else
            Error "Invalid todo"

    do
        addTodo (Todo.create "Create new SAFE project")
        |> ignore

        addTodo (Todo.create "Write your app") |> ignore
        addTodo (Todo.create "Ship it !!!") |> ignore

let todosApi =
    { getTodos = fun () -> async { return Storage.todos |> List.ofSeq }
      addTodo =
        fun todo ->
            async {
                return
                    match Storage.addTodo todo with
                    | Ok () -> todo
                    | Error e -> failwith e
            } }

let webApp : HttpHandler =
    Remoting.createApi ()
    |> Remoting.withRouteBuilder Route.builder
    |> Remoting.fromValue todosApi
    |> Remoting.buildHttpHandler

[<EntryPoint>]
let main _ =
    let builder = WebApplication.CreateBuilder(WebApplicationOptions(
        WebRootPath = "public"
        ))

    builder.Services
        .AddGiraffe()
        .AddAuthentication(OpenIdConnectDefaults.AuthenticationScheme)
        .AddMicrosoftIdentityWebApp(builder.Configuration.GetSection("AzureAd"))
        |> ignore

    let app = builder.Build()

    app
        .UseForwardedHeaders()
        .UseCookiePolicy(CookiePolicyOptions(Secure = CookieSecurePolicy.Always))
        .UseAuthentication()
        .Use(Func<HttpContext,RequestDelegate,Task>(fun ctx next -> task {
            if ctx.User = null || ctx.User.Identity.IsAuthenticated = false then
                return! ctx.ChallengeAsync()
            else
                return! next.Invoke(ctx)
        }))
        .UseFileServer()
        .UseGiraffe(webApp)

    app.Run()

    0 // Exit code
