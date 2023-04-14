open System
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Authentication
open Microsoft.AspNetCore.Identity
open Microsoft.AspNetCore.Identity.EntityFrameworkCore
open Microsoft.Identity.Web
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.DependencyInjection
open Giraffe
open Microsoft.EntityFrameworkCore

[<CLIMutable>] type LoginModel = { Email : string; Password : string}
[<CLIMutable>] type RegisterModel = { Email : string; UserName : string; Password : string}

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
        form [_action $"/login"; _method "POST"] [
            login_input "Email"
            login_input "Password"
            input [_type "submit"]
            yield! errors |> List.map (fun er ->
                p [_style "color: red"] [
                    str $"* %s{er}"
                ]
                )
            div [] [
                a [_href "login-azuread"] [str "Login With Azure AD"]
            ]
        ]
    ]
    
    let register errors = master "Login" [
        form [_action $"/register"; _method "POST"] [
            login_input "Email"
            login_input "UserName"
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
        p [] [
            a [_href "/logout"] [str "Logout"]
        ]
    ]
    
module ExternalSchema =
    let Azure = "AzureAD_OIDC"
    let All = [Azure]
    
module Handler =
    let user : HttpHandler = fun next ctx -> task {
        let user = ctx.User
        return! htmlView (Pages.user user.Identity.Name) next ctx
    }
    
    let login : HttpHandler = fun next ctx -> task {
        if ctx.User.Identity.IsAuthenticated then
            return! redirectTo false "user" next ctx
        else
            return! htmlView (Pages.login []) next ctx
    }
    
    let login_azuread : HttpHandler = fun next ctx -> task {
        let signinManager = ctx.GetService<SignInManager<IdentityUser>>()
        let authProps = signinManager.ConfigureExternalAuthenticationProperties(ExternalSchema.Azure,"/user")
        do! ctx.ChallengeAsync(ExternalSchema.Azure,authProps)
        return! next ctx
    }
    
    let logout : HttpHandler = fun next ctx -> task {
        ctx.Request.Cookies |> Seq.iter (fun (KeyValue(k,_)) -> ctx.Response.Cookies.Delete(k))
        return! redirectTo false "/" next ctx
    }
    
    module POST =
        let login : HttpHandler = fun next ctx -> task {
            let! model = ctx.BindFormAsync<LoginModel>()
            let signinManager = ctx.GetService<SignInManager<IdentityUser>>()
            let userManager = ctx.GetService<UserManager<IdentityUser>>()
            let! user = userManager.FindByEmailAsync(model.Email)
            let! result = signinManager.PasswordSignInAsync(user,model.Password,true,false)
            if result.Succeeded then
                return! redirectTo false "/user" next ctx
            else
                return! htmlView (Pages.login ["The username or the password is not correct."]) next ctx
        }
        
        let register : HttpHandler = fun next ctx -> task {
            let! model = ctx.BindFormAsync<RegisterModel>()
            let user = IdentityUser(Email = model.Email, UserName = model.UserName)
            
            let userManager = ctx.GetService<UserManager<IdentityUser>>()
            let! result = userManager.CreateAsync(user,model.Password)
            if result.Succeeded then
                let signinManager = ctx.GetService<SignInManager<IdentityUser>>()
                do! signinManager.SignInAsync(user,true)
                return! redirectTo false "/user" next ctx
            else
                return! htmlView (Pages.register (result.Errors |> Seq.map (fun x -> x.Description) |> Seq.toList)) next ctx
        }
       
let webApp : HttpHandler =
    let mustBeLoggedIn = requiresAuthentication (redirectTo false "/login")
    let auth_3rd_party : HttpHandler = fun next ctx -> 
        let rec loop = function
            | x :: xs -> task {
                let! r = ctx.AuthenticateAsync(x)
                if r.Succeeded then
                    ctx.User <- r.Principal
                    return! next ctx
                else
                    return! loop xs
                }
            | [] -> next ctx
            
        if ctx.User = null || ctx.User.Identity.IsAuthenticated = false then
            loop ExternalSchema.All
        else
            next ctx
    
    auth_3rd_party >=> choose [
        GET >=> choose [
            route "/" >=> htmlView Pages.index
            route "/login" >=> Handler.login
            route "/login-azuread" >=> Handler.login_azuread
            route "/logout" >=> Handler.logout
            route "/register" >=> htmlView (Pages.register [])
            route "/user" >=> mustBeLoggedIn >=> Handler.user
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
    
    builder.Services
        .AddAuthentication()
        .AddMicrosoftIdentityWebApp(builder.Configuration.GetSection("AzureAd"),ExternalSchema.Azure,"CookieAzureAD")
        |> ignore
    
    builder.Services
        .AddIdentity<IdentityUser,IdentityRole>(fun options ->
            // Password settings
            options.Password.RequireDigit   <- true
            options.Password.RequiredLength <- 8
            options.Password.RequireNonAlphanumeric <- false
            options.Password.RequireUppercase <- true
            options.Password.RequireLowercase <- false

            // Lockout settings
            options.Lockout.DefaultLockoutTimeSpan  <- TimeSpan.FromMinutes 30.0
            options.Lockout.MaxFailedAccessAttempts <- 10

            // User settings
            options.User.RequireUniqueEmail <- true
            )
        .AddEntityFrameworkStores<IdentityDbContext<IdentityUser>>()
        .AddDefaultTokenProviders()
    |> ignore
    
    builder.Services.AddDbContext<IdentityDbContext<IdentityUser>>(fun opts ->
        opts.UseInMemoryDatabase("SimpleDb") |> ignore
        ) |> ignore
    
    let app = builder.Build()
    
    app.UseAuthentication()
        .UseGiraffe(webApp)

    app.Run()

    0 // Exit code

