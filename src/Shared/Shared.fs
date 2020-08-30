namespace Shared

open System

type Todo =
    { Id : Guid
      Description : string }

type AccountingPeriod =
    { Month : uint8
      Year : uint8 }
    
type Invoice =
    { ManDays : uint8
      Rate: uint16
      Month: AccountingPeriod }

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
    { getTodos : unit -> Async<Todo list>
      addTodo : Todo -> Async<Todo> }

type IInvoiceApi =
    { addInvoice : Invoice -> Async<Invoice> }
