[<RequireQualifiedAccess>]
module Invoices

open System
open Elmish
open Fable.React
open Fable.React.Props
open Fulma
open Shared.Invoice
open Utils
open Api
open Fable.FontAwesome
open Common
open Fable.Remoting.Client
open Elmish.SweetAlert

let private pageSize = 20

type Model =
    { Errors: Result<string, string list> option
      Invoices: Deferred<Invoice list>
      RemovingInvoice: Invoice option
      CurrentPage: int
      Total: int }

type Msg =
    | AddInvoice of Invoice
    | HandleException of Exception
    | LoadInvoices of AsyncOperationStatus<Result<Invoice list * int, string>>
    | RemoveInvoiceConfirm of Invoice
    | RemoveInvoice of Invoice * AsyncOperationStatus<Result<unit, string>>
    | Paginate of int

type ExtMsg =
    | NoOp
    | InvoiceRemoved of Invoice

let paginateCmd pageNumber pageSize =
    Cmd.OfAsync.either
        (fun _ -> invoiceApi.getInvoices pageNumber pageSize)
        ()
        (Finished >> LoadInvoices)
        (exceptionToResult >> Finished >> LoadInvoices)

let init () : Model * Cmd<Msg> =

    let cmd =
        Cmd.batch [ Cmd.ofMsg (LoadInvoices Started) ]

    let model =
        { Errors = None
          Invoices = HasNotStartedYet
          RemovingInvoice = None
          CurrentPage = 1
          Total = 0 }

    model, cmd

let update (msg: Msg) (model: Model) : Model * Cmd<Msg> * ExtMsg =
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
        Cmd.none,
        NoOp
    | LoadInvoices Started ->
        { model with
              Invoices = InProgress None },
        paginateCmd 1 pageSize,
        NoOp
    | LoadInvoices (Finished result) ->
        { model with
              Invoices =
                  match result with
                  | Ok i -> Resolved(fst i)
                  | Error _ -> model.Invoices
              Total =
                  match result with
                  | Ok i -> snd i
                  | Error _ -> model.Total
              Errors =
                  match result with
                  | Ok _ -> None
                  | Error s -> Some(Error [ s ]) },
        (match result with
         | Ok _ -> Cmd.none
         | Error r -> toastMessage <| Error r),
        NoOp
    | RemoveInvoiceConfirm inv ->
        let handleConfirm =
            function
            | ConfirmAlertResult.Confirmed -> RemoveInvoice(inv, Started)
            | ConfirmAlertResult.Dismissed reason -> unbox null

        let confirmAlert =
            ConfirmToastAlert("Are you sure?", handleConfirm)
                .Title("Warning")
                .ShowCloseButton(false)
                .Type(AlertType.Warning)
                .ShowCloseButton(true)
                .Timeout(10000)

        model, SweetAlert.Run(confirmAlert), NoOp
    | RemoveInvoice (inv, Started) ->
        { model with
              RemovingInvoice = Some inv },
        Cmd.OfAsync.either
            invoiceApi.removeInvoice
            inv.Id
            (fun x -> RemoveInvoice(inv, Finished(x)))
            (fun x -> RemoveInvoice(inv, Finished(exceptionToResult x))),
        NoOp
    | RemoveInvoice (inv, Finished (Ok _)) ->
        { model with
              RemovingInvoice = None
              Errors = Some(Ok "Success")
              Invoices =
                  (model.Invoices
                   |> Deferred.map (fun i -> i |> List.filter (fun x -> x <> inv))) },
        toastMessage <| Ok 1,
        InvoiceRemoved inv
    | RemoveInvoice (inv, Finished (Error r)) ->
        { model with
              RemovingInvoice = None
              Errors = Some(Error [ r ]) },
        toastMessage <| Error r,
        NoOp
    | Paginate page ->
        let page =
            if (page < 1) then
                1
            else if ((page - 1) * pageSize > model.Total) then
                (page - 1)
            else
                page

        { model with CurrentPage = page }, paginateCmd page pageSize, NoOp

type Props = { Model: Model; Dispatch: Msg -> unit }

let private elmishView name render =
    FunctionComponent.Of(render, name, equalsButFunctions)

let view =
    elmishView "InvoiceList"
    <| fun { Model = model; Dispatch = dispatch } ->
        Box.box' [] [
            //h4 [][ str (model.Total.ToString())]
            Table.table [ Table.IsFullWidth ] [
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
                        |> Deferred.map
                            (fun invs ->
                                invs
                                |> List.sortByDescending (fun x -> x.AccountingPeriod)
                                |> List.map
                                    (fun i ->
                                        let manDay,rate =
                                            getManDayAndRate i
                                            |> function
                                                | Some (m,r) -> m.ToString(), r.ToString()
                                                | None -> "N/A", "N/A"
                                        tr [] [
                                            let total = getTotal i
                                            let vat = getVatAmount i
                                            let totalVat = getTotalWithVat i

                                            let iAccountingPeriod = i.AccountingPeriod.ToLocalTime()
                                            td [] [
                                                str
                                                <| sprintf "%i/%i" iAccountingPeriod.Year iAccountingPeriod.Month
                                            ]

                                            td [] [ str <| manDay ]
                                            td [] [ str <| rate ]

                                            td [] [
                                                str
                                                <| (sprintf "%s" (formatDecimal 2 <| decimal total))
                                            ]

                                            td [] [
                                                str
                                                <| sprintf "%s" (formatDecimal 2 <| decimal vat)
                                            ]

                                            td [] [
                                                str
                                                <| (sprintf "%s" (formatDecimal 2 <| decimal totalVat))
                                            ]

                                            td [] [
                                                str <| i.Customer.Name.ToString()
                                            ]

                                            td [] [
                                                let inProgress =
                                                    model.RemovingInvoice
                                                    |> Option.map (fun ri -> ri = i)
                                                    |> Option.defaultValue false


                                                a [ Href(sprintf "/api/generateInvoiceDocument/%O" i.Id)
                                                    Title "Download excel" ] [
                                                    Icon.icon [ Icon.Modifiers [ Modifier.TextColor IsPrimary ] ] [
                                                        Fa.i [ Fa.Regular.FileExcel ] []
                                                    ]
                                                ]

                                                a [ OnClick
                                                        (fun e ->
                                                            e.preventDefault ()
                                                            dispatch (RemoveInvoiceConfirm i))
                                                    Href "javascript:void(0)"
                                                    Title "Delete invoice" ] [
                                                    Icon.icon [ Icon.Modifiers [ Modifier.TextColor IsDanger ] ] [
                                                        match inProgress with
                                                        | false -> Fa.i [ Fa.Solid.TrashAlt ] []
                                                        | true -> Fa.i [ Fa.Pulse; Fa.Solid.Spinner ] []
                                                    ]
                                                ]
                                            ]
                                        ]))
                        |> Deferred.defaultResolved []
                ]
                tfoot [] [
                    tr [] [
                        th [ ColSpan 8 ] [
                            let isLast =
                                model.CurrentPage * pageSize >= model.Total

                            let max =
                                System.Math.Ceiling((decimal model.Total) / (decimal pageSize))
                                |> int


                            Pagination.pagination [] [
                                Pagination.Previous.a [ Props [ Disabled(model.CurrentPage <= 1)
                                                                OnClick
                                                                    (fun e -> dispatch <| Paginate(model.CurrentPage - 1)) ] ] [
                                    str "Previous"
                                ]

                                Pagination.Next.a [ Props [ Disabled isLast
                                                            OnClick(fun e -> dispatch <| Paginate(model.CurrentPage + 1)) ] ] [
                                    str "Next"
                                ]
                                if (max >= 5) then
                                    let middle = max / 2
                                    let middleLower = middle - 1
                                    let middleHigher = middle + 1

                                    Pagination.list [] [
                                        Pagination.Link.a [ Pagination.Link.Props [ OnClick
                                                                                      (fun e -> dispatch <| Paginate(1))  ]
                                                            Pagination.Link.Current(1 = model.CurrentPage) ] [
                                            str (1l.ToString())
                                        ]
                                        Pagination.ellipsis []
                                        Pagination.Link.a [ Pagination.Link.Props [ OnClick
                                                                                      (fun e ->
                                                                                          dispatch
                                                                                          <| Paginate(middleLower)) ]
                                                            Pagination.Link.Current(middleLower = model.CurrentPage) ] [
                                            str (middleLower.ToString())
                                        ]
                                        Pagination.Link.a [ Pagination.Link.Props [ OnClick
                                                                                      (fun e ->
                                                                                          dispatch <| Paginate(middle)) ]
                                                            Pagination.Link.Current(middle = model.CurrentPage) ] [
                                            str (middle.ToString())
                                        ]
                                        Pagination.Link.a [ Pagination.Link.Props [ OnClick
                                                                                      (fun e ->
                                                                                          dispatch
                                                                                          <| Paginate(middleHigher)) ]
                                                            Pagination.Link.Current(middleHigher = model.CurrentPage) ] [
                                            str (middleHigher.ToString())
                                        ]
                                        Pagination.ellipsis []
                                        Pagination.Link.a [ Pagination.Link.Props [ OnClick
                                                                                      (fun e ->
                                                                                          dispatch <| Paginate(max)) ]
                                                            Pagination.Link.Current(max = model.CurrentPage) ] [
                                            str (max.ToString())
                                        ]
                                    ]
                                else
                                    Pagination.list [] [
                                        for x in 1 .. max do
                                            yield
                                                Pagination.Link.a [ Pagination.Link.Props [ OnClick
                                                                                              (fun e ->
                                                                                                  dispatch
                                                                                                  <| Paginate(x)) ]
                                                                    Pagination.Link.Current(x = model.CurrentPage) ] [
                                                    str (x.ToString())
                                                ]
                                    ]
                            ]
                        ]
                    ]
                ]
            ]
        ]