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
        .AddMicrosoftIdentityWebApp(fun x ->
            x.Instance <- AzureAD.config.Instance
            x.TenantId <- AzureAD.config.TenantId
            x.ClientId <- AzureAD.config.ClientId
            )
        |> ignore

    let app = builder.Build()

    app
        .UseCookiePolicy(CookiePolicyOptions(Secure = CookieSecurePolicy.Always))
        .Use(Func<HttpContext,RequestDelegate,Task>(fun ctx next -> task {
            let! r = ctx.AuthenticateAsync()
            if r.Succeeded then
                return! next.Invoke(ctx)
            else
                return! ctx.ForbidAsync()
        }))
        .UseFileServer()
        .UseGiraffe(webApp)

    app.Run()

    0 // Exit code
