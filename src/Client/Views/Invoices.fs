[<RequireQualifiedAccess>]
module Invoices

open System
open Elmish
open Fable.React
open Fulma
open Shared
open Shared.Invoice
open Utils
open Api
open Fable.FontAwesome
open Common

type Model =
    { Errors: Result<string, string list> option
      Invoices: Deferred<Invoice list>
      RemovingInvoice: Invoice option }

type Msg =
    | AddInvoice of Invoice
    | HandleException of Exception
    | LoadInvoices of AsyncOperationStatus<Result<Invoice list, string>>
    | RemoveInvoice of Invoice * AsyncOperationStatus<Result<unit, string>>

type ExtMsg =
    | NoOp
    | InvoiceRemoved of Invoice

let init (): Model * Cmd<Msg> =

    let cmd =
        Cmd.batch [ Cmd.ofMsg (LoadInvoices Started) ]

    let model =
        { Errors = None
          Invoices = HasNotStartedYet
          RemovingInvoice = None }

    model, cmd

let update (msg: Msg) (model: Model): Model * Cmd<Msg> * ExtMsg =
    match msg with
    | AddInvoice inv ->
        let invs =
            match model.Invoices with
            | Resolved invs -> inv :: invs
            | _ -> [ inv ]

        { model with Invoices = Resolved invs }, Cmd.none, NoOp
    | HandleException exn ->
        { model with
              Errors = Some(Error [ exn.Message ]) },
        Cmd.none, NoOp
    | LoadInvoices Started ->
        { model with Invoices = InProgress None },
        Cmd.OfAsync.either
            invoiceApi.getInvoices
            ()
            (Finished >> LoadInvoices)
            (exceptionToResult >> Finished >> LoadInvoices)
            , NoOp
    | LoadInvoices (Finished result) ->
        { model with
              Invoices =
                  match result with
                  | Ok i -> Resolved i
                  | Error _ -> Resolved []
              Errors =
                  match result with
                  | Ok _ -> None
                  | Error s -> Some(Error [ s ]) },
        Cmd.none, NoOp
    | RemoveInvoice (inv, Started) ->
        { model with
              RemovingInvoice = Some inv },
        Cmd.OfAsync.either
            invoiceApi.removeInvoice
            inv.Id
            (fun x -> RemoveInvoice(inv, Finished(x)))
            (fun x -> RemoveInvoice(inv, Finished(exceptionToResult x)))
            , NoOp
    | RemoveInvoice (inv, Finished (Ok _)) ->
        { model with
              RemovingInvoice = None
              Errors = Some (Ok "Success")
              Invoices =
                  (model.Invoices
                   |> Deferred.map (fun i -> i |> List.filter (fun x -> x <> inv))) },
        Cmd.none, InvoiceRemoved inv
    | RemoveInvoice (inv, Finished (Error r)) ->
        { model with
              RemovingInvoice = None
              Errors = Some (Error [r]) },
        Cmd.none, NoOp

type Props = { Model: Model; Dispatch: Msg -> unit }

let elmishView name render =
    FunctionComponent.Of(render, name, equalsButFunctions)

let view =
    elmishView "InvoiceList"
    <| fun { Model = model; Dispatch = dispatch } ->
        Box.box' [] [
            Table.table [] [
                thead [] [
                    tr [] [
                        th [] [ str "Period" ]
                        th [] [ str "Rate" ]
                        th [] [ str "Man Days" ]
                        th [] [ str "Total" ]
                        th [] [ str "VAT" ]
                        th [] [ str "Total VAT" ]
                        th [] [ str "Customer" ]
                        th [] [ str "Action" ]
                    ]
                ]
                tbody [] [
                    yield!
                        model.Invoices
                        |> Deferred.map (fun invs ->
                            invs
                            |> List.sortByDescending (fun x -> x.AccountingPeriod)
                            |> List.map (fun i ->
                                tr [] [
                                    let total = (int i.ManDays) * (int i.Rate)
                                    let vat = i.Vat |> Option.map (fun v -> (total/ 100 * (int v))) |> Option.defaultValue 0
                                    td [] [
                                        str
                                        <| sprintf "%i/%i" i.AccountingPeriod.Year i.AccountingPeriod.Month
                                    ]
                                    td [] [ str <| i.Rate.ToString() ]
                                    td [] [ str <| i.ManDays.ToString() ]
                                    td [] [
                                        str
                                        <| (sprintf "%i" total)
                                    ]
                                    td [] [ ofInt vat ]
                                    td [] [
                                        str
                                        <| (sprintf "%i" (i.Vat |> Option.map (fun v -> total + (total/ 100 * (int v))) |> Option.defaultValue total ))
                                    ]
                                    td [] [
                                        str <| i.Customer.Name.ToString()
                                    ]
                                    td [] [
                                        let inProgress =
                                            model.RemovingInvoice
                                            |> Option.map (fun ri -> ri = i)
                                            |> Option.defaultValue false

                                        Delete.delete [ Delete.Modifiers [ Modifier.BackgroundColor IsDanger
                                                                           Modifier.IsHidden(Screen.All, inProgress) ]
                                                        Delete.OnClick(fun _ -> dispatch (RemoveInvoice(i, Started))) ] []

                                        Icon.icon [ Icon.Modifiers [ Modifier.IsHidden(Screen.All, not inProgress) ] ] [
                                            Fa.i [ Fa.Pulse; Fa.Solid.Spinner ] []
                                        ]
                                    ]
                                ]))
                        |> Deferred.defaultResolved []
                ]
            ]
            notification model.Errors
        ]
