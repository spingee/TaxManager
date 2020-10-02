module Server

open Fable.Remoting.Server
open Fable.Remoting.Giraffe
open Saturn
open System
open Shared
open InvoiceExcel

type Storage() =
    let todos = ResizeArray<_>()

    member __.GetTodos() = List.ofSeq todos

    member __.AddTodo(todo: Todo) =
        if Todo.isValid todo.Description then
            todos.Add todo
            Ok()
        else
            Error "Invalid todo"

let storage = Storage()

storage.AddTodo(Todo.create "Create new SAFE project")
|> ignore

storage.AddTodo(Todo.create "Write your app")
|> ignore

storage.AddTodo(Todo.create "Ship it !!!")
|> ignore

let todosApi =
    { getTodos = fun () -> async { return storage.GetTodos() }
      addTodo =
          fun todo ->
              async {
                  match storage.AddTodo todo with
                  | Ok () -> return todo
                  | Error e -> return failwith e
              } }

let invoiceApi =
    { addInvoice =
          fun invoice ->
              async {
                  try
                    let outputFile = sprintf "C:\\Users\\SpinGee\\Desktop\\Faktura - %i-%02i-01.xlsx" invoice.AccountingPeriod.Year invoice.AccountingPeriod.Month
                    createExcelInvoice outputFile invoice
                    return Ok "Success"
                  with ex ->
                    return Error <| sprintf "%s" ex.Message

              } }

let webApp =
    Remoting.createApi ()
    |> Remoting.withRouteBuilder Route.builder
    //|> Remoting.fromValue todosApi
    |> Remoting.fromValue invoiceApi
    |> Remoting.buildHttpHandler

let app =
    application {
        url "http://0.0.0.0:8085"
        use_router webApp
        memory_cache
        use_static "public"
        use_gzip
    }

run app
