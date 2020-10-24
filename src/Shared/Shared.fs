namespace Shared

open System
open System.Text.RegularExpressions


type Todo = { Id: Guid; Description: string }

module Invoice =
    type VatId = internal VatId of string

    let createVatId str =
        let legit =
            Regex("^[A-Z]{2}[A-Z0-9]+$").IsMatch(str)

        match legit with
        | true -> Ok(VatId str)
        | false -> Error("Vat id has wrong format.")

    type Customer =
        { IdNumber: uint
          VatId: VatId
          Name: string }

    type Invoice =
        { Id: Guid
          ManDays: uint8
          Rate: uint16
          AccountingPeriod: DateTime
          Customer: Customer }

    type IInvoiceApi =
        { addInvoice: Invoice -> Async<Result<string, string>>
          getCustomers: unit -> Async<Result<Customer list, string>> }




module Todo =
    let isValid (description: string) =
        String.IsNullOrWhiteSpace description |> not

    let create (description: string) =
        { Id = Guid.NewGuid()
          Description = description }

module Route =
    let builder typeName methodName = sprintf "/api/%s/%s" typeName methodName

type ITodosApi =
    { getTodos: unit -> Async<Todo list>
      addTodo: Todo -> Async<Todo> }
