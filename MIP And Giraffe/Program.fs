open System.Security.Claims
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Authentication
open Microsoft.AspNetCore.Authentication.Cookies
open Microsoft.AspNetCore.Authentication.OpenIdConnect
open Microsoft.Identity.Web
open Microsoft.Identity.Web.UI
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.DependencyInjection
open Giraffe
open Microsoft.EntityFrameworkCore
open System.ComponentModel.DataAnnotations

[<CLIMutable>] type RegisterModel = { [<Key>] Username : string; Password : string; Email : string}
[<CLIMutable>] type LoginModel = {Username : string; Password : string}

type SimpleDbContext(options) =
    inherit DbContext(options)
    
    [<DefaultValue>] val mutable private users : DbSet<RegisterModel>
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
    
    let login errors = master "Login" [
        form [_action "/login"; _method "POST"] [
            login_input "Username"
            login_input "Password"
            input [_type "submit"]
            yield! errors |> List.map (fun er ->
                p [_style "color: red"] [
                    str $"* %s{er}"
                ]
                ) 
        ]
    ]
    
    let register errors = master "Register" [
        form [_action "/register"; _method "POST"] [
            login_input "Email"
            login_input "Username"
            login_input "Password"
            input [_type "submit"]
            yield! errors |> List.map (fun er ->
                p [_style "color: red"] [
                    str $"* %s{er}"
                ]
                ) 
        ]
    ]
    
    let user username email = master "User" [
        p [] [
            str "Welcome To The User Page"
        ]
        p [] [
            str "Email: "
            str email
        ]
        p [] [
            str "Username:"
            str username
        ]
    ]
    
module Handler =
    let user : HttpHandler = fun next ctx -> task {
        let! result = ctx.AuthenticateAsync()
        if result.Succeeded then
            let p = result.Principal
            match p.FindFirstValue(ClaimTypes.NameIdentifier), p.FindFirstValue(ClaimTypes.Email) with
            | null, _ | _, null -> return! failwith "The claim types are invalid."
            | username, email -> return! htmlView (Pages.user username email) next ctx
        else
            do! ctx.ChallengeAsync()
            return! next ctx
    }

    let create_principal username email =
        let claims = [|
            Claim(ClaimTypes.NameIdentifier, username, ClaimValueTypes.String)
            Claim(ClaimTypes.Email, email, ClaimValueTypes.String)
        |]
        let identity = ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme)
        ClaimsPrincipal(identity)
        
    let login : HttpHandler = fun next ctx -> task {
        let! model = ctx.BindFormAsync<LoginModel>()
        let db = ctx.GetService<SimpleDbContext>()
        
        let! user = db.Users.FirstOrDefaultAsync(fun db -> model.Username = db.Username && model.Password = db.Password)
        if isNotNull (box user) then
            do! ctx.SignInAsync(create_principal user.Username user.Email)
            return! redirectTo false "/user" next ctx
        else
            return! htmlView (Pages.login ["The username or the password is not correct."]) next ctx
    }
    
    let register : HttpHandler = fun next ctx -> task {
        let! model = ctx.BindFormAsync<RegisterModel>()
        
        let db = ctx.GetService<SimpleDbContext>()
        match! db.Users.AnyAsync(fun db -> model.Email = db.Email || model.Username = db.Username) with
        | false ->
            db.Users.Add(model) |> ignore
            let! _ = db.SaveChangesAsync()
            
            do! ctx.SignInAsync(create_principal model.Username model.Email)
            return! redirectTo false "/user" next ctx
        | true ->
            return! htmlView (Pages.register ["The email or the username are already registered."]) next ctx
    }
        
let webApp : HttpHandler =
    choose [
        GET >=> choose [
            route "/" >=> htmlView Pages.index
            route "/login" >=> htmlView (Pages.login [])
            route "/register" >=> htmlView (Pages.register [])
            route "/user" >=> Handler.user
        ]
        POST >=> choose [
            route "/login" >=> Handler.login
            route "/register" >=> Handler.register
        ]
        setStatusCode 404 >=> htmlView Pages.error
    ]
   
[<EntryPoint>]
let main args =
    let builder = WebApplication.CreateBuilder(args)
    builder.Services.AddGiraffe() |> ignore
    builder.Services.AddAuthentication()
        // .AddCookie(fun opts ->
        //     opts.LoginPath <- PathString "/login"
        //     // opts.ReturnUrlParameter <- ""
        //     )
        .AddMicrosoftIdentityWebApp(fun x ->
            // x.Instance <- ""
            x.ClientId <- "f01cee69-7a9d-47ac-a1df-bc285ab72811"
            x.TenantId <- "ec88369d-6c2f-4f15-b0c7-adbe35caec77"
            )
    |> ignore
        
    builder.Services.AddDbContext<SimpleDbContext>(fun opts ->
        opts.UseInMemoryDatabase("SimpleDb") |> ignore
        ) |> ignore
            
    let app = builder.Build()

    app.UseGiraffe(webApp)

    app.Run()

    0 // Exit code

