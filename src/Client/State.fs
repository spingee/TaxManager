module State

open Elmish
open Fable.Remoting.Client
open Shared
open Shared.Invoice
open System
open Types
open Result.ResultEx

type Msg =
    | AddInvoice
    | AddedInvoice of Result<string, string>
    | SetRate of string
    | SetAccPeriod of DateTime
    | SetMandays of string
    | HandleException of Exception

let invoiceApi =
    Remoting.createApi ()
    |> Remoting.withRouteBuilder Route.builder
    |> Remoting.buildProxy<IInvoiceApi>

let init (): Model * Cmd<Msg> =
    let model =
        { Input =
              { Rate = Validated.success "6000" <| uint16 6000
                ManDays = Validated.success "20" 20uy
                AccountingPeriod = DateTime.Now.AddMonths(-1) }
          Title = "Submit invoice data"
          Result = None
          IsLoading = false }

    let cmd = Cmd.none //Cmd.OfAsync.perform todosApi.getTodos () GotTodos
    model, cmd

let update (msg: Msg) (model: Model): Model * Cmd<Msg> =
    match msg with
    | AddedInvoice result ->
        { model with
              Result = Some result
              IsLoading = false },
        Cmd.none
    | AddInvoice ->
        let inv =
            result {
                let! vatId = createVatId "CZ26775794"

                return { Id = Guid.NewGuid()
                         Customer =
                             { Name = "Principal engineering s.r.o."
                               IdNumber = uint 26775794
                               VatId = vatId }
                         Rate = model.Input.Rate.Parsed.Value
                         ManDays = model.Input.ManDays.Parsed.Value
                         AccountingPeriod = model.Input.AccountingPeriod }
            }

        match inv with
        | Ok inv ->
            let model =
                { model with
                      IsLoading = true
                      Result = None }

            let cmd =
                Cmd.OfAsync.either invoiceApi.addInvoice inv AddedInvoice HandleException

            model, cmd
        | Error str -> { model with Result = Some(Error str) }, Cmd.none
    | SetRate v ->
        let rate =
            match UInt16.TryParse v with
            | true, parsed -> Validated.success v parsed
            | false, _ -> Validated.failure v

        { model with
              Input = { model.Input with Rate = rate } },
        Cmd.none
    | SetAccPeriod v ->
        { model with
              Input =
                  { model.Input with
                        AccountingPeriod = v } },
        Cmd.none
    | SetMandays v ->
        let md =
            match Byte.TryParse v with
            | true, parsed -> Validated.success v parsed
            | false, _ -> Validated.failure v

        { model with
              Input = { model.Input with ManDays = md } },
        Cmd.none
    | HandleException exn ->
        { model with
              IsLoading = false
              Result = Some(Error exn.Message) },
        Cmd.none
