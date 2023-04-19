namespace Shared

module AzureAD =
    let config = {|
        Instance = "https://login.microsoftonline.com/"
        TenantId = "ec88369d-6c2f-4f15-b0c7-adbe35caec77"
        ClientId = "fd12b58b-6c39-4f86-ba02-98e6d7ee5eb5"
        CallbackPath = "/signin-oidc"
        SignedOutCallbackPath = "/signout-oidc"
        |}

open System

type Todo = { Id: Guid; Description: string }

module Todo =
    let isValid (description: string) =
        String.IsNullOrWhiteSpace description |> not

    let create (description: string) =
        { Id = Guid.NewGuid()
          Description = description }

module Route =
    let builder typeName methodName =
        sprintf "/api/%s/%s" typeName methodName

type ITodosApi =
    { getTodos: unit -> Async<Todo list>
      addTodo: Todo -> Async<Todo> }