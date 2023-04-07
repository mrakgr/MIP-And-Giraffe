open System
open Microsoft.AspNetCore.Builder
open Microsoft.Extensions.Hosting
open Giraffe

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
        str "Hello World!"
    ]
        
    let error = master "Error" [
        str "404 - Page Not Found"
    ]
        
let webApp : HttpHandler =
    choose [
        route "/" >=> htmlView Pages.index
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

