open System
open System.Security.Claims
open System.Threading.Tasks
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Authentication
open Microsoft.Extensions.Hosting
open Giraffe

[<CLIMutable>] type RegisterModel = {Username : string; Password : string; Email : string}
[<CLIMutable>] type LoginModel = {Username : string; Password : string}

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
    
    let login = master "Login" [
        form [_action "/login"; _method "POST"] [
            login_input "Username"
            login_input "Password"
            input [_type "submit"]
        ]
    ]
    
    let register = master "Register" [
        form [_action "/register"; _method "POST"] [
            login_input "Email"
            login_input "Username"
            login_input "Password"
            input [_type "submit"]
        ]
    ]
    
    let user (model : RegisterModel) = master "User" [
        p [] [
            str "Welcome To The User Page"
        ]
        p [] [
            str "Email: "
            str model.Email
        ]
        p [] [
            str "Username:"
            str model.Username
        ]
    ]
        
    
let user_handler : HttpHandler = fun next ctx -> task {
    let json = ctx.GetJsonSerializer()
    match ctx.GetCookieValue("MyCookie") with
    | Some x ->
        let page = json.Deserialize x |> Pages.user
        return! htmlView page next ctx
    | None ->
        return! redirectTo false "/login" next ctx
}
        
    
let login_handler : HttpHandler = fun next ctx -> task {
    let! model = ctx.BindFormAsync<LoginModel>()
    let user = ctx.GetCookieValue("MyCookie")
    match user with
    | Some x ->
        return! redirectTo false "/user" next ctx
    | None ->
        return! htmlView Pages.login next ctx // TODO
}
let register_handler : HttpHandler = fun next ctx -> task {
    let! model = ctx.BindFormAsync<RegisterModel>()
    let claims = [|
        Claim(ClaimTypes.Name, model.Username, ClaimValueTypes.String)
        Claim(ClaimTypes.Name, model.Password, ClaimValueTypes.String)
        Claim(ClaimTypes.Name, model.Username, ClaimValueTypes.String)
    |]
    ctx.SignInAsync()
    return! redirectTo false "/user" next ctx
}
        
let webApp : HttpHandler =
    choose [
        GET >=> choose [
            route "/" >=> htmlView Pages.index
            route "/login" >=> htmlView Pages.login
            route "/register" >=> htmlView Pages.register
            route "/user" >=> user_handler
        ]
        POST >=> choose [
            route "/login" >=> login_handler
            route "/register" >=> register_handler
        ]
        setStatusCode 404 >=> htmlView Pages.error
    ]

[<EntryPoint>]
let main args =
    let builder = WebApplication.CreateBuilder(args)
    builder.Services.AddGiraffe() |> ignore
            
    let app = builder.Build()

    app.UseGiraffe(webApp)

    app.Run()

    0 // Exit code

