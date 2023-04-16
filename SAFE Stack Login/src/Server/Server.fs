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

let webApp =
    Remoting.createApi ()
    |> Remoting.withRouteBuilder Route.builder
    |> Remoting.fromValue todosApi
    |> Remoting.buildHttpHandler

[<EntryPoint>]
let main args =
    let builder = WebApplication.CreateBuilder(args)
    builder.Services.AddGiraffe() |> ignore

    // if builder.Environment.IsDevelopment() then
    builder.Services.AddCors() |> ignore

    builder.Services
        .AddAuthentication(OpenIdConnectDefaults.AuthenticationScheme)
        .AddMicrosoftIdentityWebApp(builder.Configuration.GetSection("AzureAd"))
        |> ignore

    let app = builder.Build()

    // if app.Environment.IsDevelopment() then
    app.UseCors(fun opts ->
        opts.WithOrigins("http://localhost:8080").AllowCredentials().AllowAnyHeader().AllowAnyMethod() |> ignore
        ) |> ignore

    app.UseAuthentication()
        .Use(Func<HttpContext,RequestDelegate,Task>(fun ctx next -> task {
            if ctx.User = null || ctx.User.Identity.IsAuthenticated = false then
                return! ctx.ChallengeAsync()
            else
                return! next.Invoke(ctx)
        }))
        .UseGiraffe(webApp)

    app.Run()

    0 // Exit code