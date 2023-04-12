open System.Security.Claims
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Authentication
open Microsoft.AspNetCore.Authentication.Cookies
open Microsoft.AspNetCore.Authentication.OpenIdConnect
open Microsoft.Identity.Web
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.DependencyInjection
open Giraffe
open Microsoft.EntityFrameworkCore
open System.ComponentModel.DataAnnotations

[<CLIMutable>] type UserModel = { [<Key>] Email : string; Password : string}

type SimpleDbContext(options) =
    inherit DbContext(options)
    
    [<DefaultValue>] val mutable private users : DbSet<UserModel>
    member this.Users with get() = this.users and set v = this.users <- v

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
    
    let index = master "Index" [
        p [] [
            str "Welcome to my website!"
        ]
        p [] [
            a [_href "/login"] [str "Login"]
        ]
        p [] [
            a [_href "/register"] [str "Register"]
        ]
    ]
        
    let error = master "Error" [
        str "404 - Page Not Found"
    ]
    
    let login_input field_name =
        div [] [
            label [] [str $"{field_name}: "]
            input [_name field_name; _type "text"]
        ]
    
    let login_templ route errors = master "Login" [
        form [_action $"/%s{route}"; _method "POST"] [
            login_input "Email"
            login_input "Password"
            input [_type "submit"]
            yield! errors |> List.map (fun er ->
                p [_style "color: red"] [
                    str $"* %s{er}"
                ]
                ) 
        ]
    ]
    
    let user email = master "User" [
        p [] [
            str "Welcome To The User Page"
        ]
        p [] [
            str "Email: "
            str email
        ]
    ]
    
module Handler =
    let user : HttpHandler = fun next ctx -> task {
        let! result = ctx.AuthenticateAsync()
        if result.Succeeded then
            let p = result.Principal
            return! htmlView (Pages.user (p.FindFirstValue ClaimTypes.Email)) next ctx
        else
            do! ctx.ChallengeAsync()
            return! next ctx
    }
    
    let login : HttpHandler = fun next ctx -> task {
        let! result = ctx.AuthenticateAsync()
        if result.Succeeded then
            return! redirectTo false "user" next ctx
        else
            return! htmlView (Pages.login_templ "login" []) next ctx
    }
    
    module POST =
        let create_principal email =
            let claims = [|
                Claim(ClaimTypes.Email, email, ClaimValueTypes.String)
            |]
            let identity = ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme)
            ClaimsPrincipal(identity)
            
        let login : HttpHandler = fun next ctx -> task {
            let! model = ctx.BindFormAsync<UserModel>()
            let db = ctx.GetService<SimpleDbContext>()
            
            let! user = db.Users.FirstOrDefaultAsync(fun db -> model.Email = db.Email && model.Password = db.Password)
            if isNotNull (box user) then
                do! ctx.SignInAsync(create_principal user.Email)
                return! redirectTo false "/user" next ctx
            else
                return! htmlView (Pages.login_templ "login" ["The email or the password is not correct."]) next ctx
        }
        
        let register : HttpHandler = fun next ctx -> task {
            let! model = ctx.BindFormAsync<UserModel>()
            
            let db = ctx.GetService<SimpleDbContext>()
            match! db.Users.AnyAsync(fun db -> model.Email = db.Email) with
            | false ->
                db.Users.Add(model) |> ignore
                let! _ = db.SaveChangesAsync()
                
                do! ctx.SignInAsync(create_principal model.Email)
                return! redirectTo false "/user" next ctx
            | true ->
                return! htmlView (Pages.login_templ "register" ["The email is already registered."]) next ctx
        }
        
let webApp : HttpHandler =
    choose [
        GET >=> choose [
            route "/" >=> htmlView Pages.index
            route "/login" >=> Handler.login
            route "/register" >=> htmlView (Pages.login_templ "register" [])
            route "/user" >=> Handler.user
        ]
        POST >=> choose [
            route "/login" >=> Handler.POST.login
            route "/register" >=> Handler.POST.register
        ]
        setStatusCode 404 >=> htmlView Pages.error
    ]
   
[<EntryPoint>]
let main args =
    let builder = WebApplication.CreateBuilder(args)
    builder.Services.AddGiraffe() |> ignore
    builder.Services.AddAuthentication()
        .AddCookie(fun opts ->
            opts.LoginPath <- PathString "/login"
            )
        // .AddMicrosoftIdentityWebApp(fun x ->
        //     // x.Instance <- ""
        //     x.ClientId <- "f01cee69-7a9d-47ac-a1df-bc285ab72811"
        //     x.TenantId <- "ec88369d-6c2f-4f15-b0c7-adbe35caec77"
        //     )
    |> ignore
        
    builder.Services.AddDbContext<SimpleDbContext>(fun opts ->
        opts.UseInMemoryDatabase("SimpleDb") |> ignore
        ) |> ignore
            
    let app = builder.Build()

    app.UseGiraffe(webApp)

    app.Run()

    0 // Exit code

