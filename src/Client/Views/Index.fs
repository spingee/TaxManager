module Index

open Fable.React
open Fable.React.Props
open Fulma
open Shared
open Elmish
open Shared.Invoice
open Utils
open Api
open Common
open FsToolkit.ErrorHandling


type Model =
    { Title: string
      IsLoading: bool
      InvoiceModel: Invoice.Model
      InvoicesModel: Invoices.Model
      Totals: Deferred<Invoice.Totals> }

type Msg =
    | InvoiceMsg of Invoice.Msg
    | InvoicesMsg of Invoices.Msg
    | LoadTotals of AsyncOperationStatus<Result<Invoice.Totals, string>>
    | ReportRequest of SummaryReportType * AsyncOperationStatus<Result<string, string>>



let init () : Model * Cmd<Msg> =
    let invSubModel, invCmd = Invoice.init ()
    let invsSubModel, invsCmd = Invoices.init ()

    let cmd =
        Cmd.batch [ Cmd.map InvoiceMsg invCmd
                    Cmd.map InvoicesMsg invsCmd
                    Cmd.ofMsg (LoadTotals Started) ]

    let model =
        { Title = "Submit invoice data"
          IsLoading = false
          InvoicesModel = invsSubModel
          InvoiceModel = invSubModel
          Totals = HasNotStartedYet }

    model, cmd

let update (msg: Msg) (model: Model) : Model * Cmd<Msg> =
    match msg with
    | InvoiceMsg msg ->
        let newSubModel, cmd, extMsg = Invoice.update msg model.InvoiceModel

        { model with
              InvoiceModel = newSubModel },
        Cmd.batch [ Cmd.map InvoiceMsg cmd
                    match extMsg with
                    | Invoice.InvoiceAdded inv ->
                        Cmd.batch [ Cmd.ofMsg (InvoicesMsg(Invoices.AddInvoice inv))
                                    Cmd.ofMsg (LoadTotals Started) ]
                    | _ -> Cmd.none ]
    | InvoicesMsg msg ->
        let newSubModel, cmd, extMsg = Invoices.update msg model.InvoicesModel

        { model with
              InvoicesModel = newSubModel },
        Cmd.batch [ match extMsg with
                    | Invoices.InvoiceRemoved _ -> Cmd.ofMsg (LoadTotals Started)
                    | _ -> Cmd.none
                    Cmd.map InvoicesMsg cmd ]
    | LoadTotals Started ->
        { model with
              Totals = InProgress(Deferred.toOption model.Totals) },
        Cmd.OfAsync.either
            invoiceApi.getTotals
            ()
            (Finished >> LoadTotals)
            (exceptionToResult >> Finished >> LoadTotals)
    | LoadTotals (Finished result) ->
        { model with
              Totals =
                  match result with
                  | Ok i -> Resolved i
                  | Error e ->
                      Browser.Dom.console.error (e)

                      Resolved(
                          Deferred.toOption model.Totals
                          |> Option.defaultValue Invoice.TotalsDefault
                      ) },
        Cmd.none

    | ReportRequest (t, Started) ->
        { model with
              Totals = InProgress(Deferred.toOption model.Totals) },
        Cmd.OfAsync.either
            invoiceApi.prepareSummaryReportUrl
            t
            (fun x -> ReportRequest (t, Finished x))
            (fun x -> ReportRequest (t, Finished (exceptionToResult x)))
    | ReportRequest (_,Finished result) ->
        result
            |> Result.tee (fun url ->  (Browser.Dom.window.``open`` (url,"_blank")).focus())
            |> ignore
        model
        , toastMessage result



let navBrand =
    Navbar.Brand.div [] [
        Navbar.Item.a [ Navbar.Item.Props [ Href "https://safe-stack.github.io/" ]
                        Navbar.Item.IsActive true ] [
            img [ Src "/favicon.png"; Alt "Logo" ]
        ]
    ]

let view (model: Model) (dispatch: Msg -> unit) =
    Hero.hero [ Hero.Color IsPrimary
                Hero.IsFullHeight
                Hero.Props [ Style [ Background """url("kid2.png") no-repeat center center fixed"""
                                     BackgroundSize "cover" ] ] ] [
        Hero.head [] [
            Navbar.navbar [] [
                Container.container [] [ navBrand ]
            ]
        ]

        Hero.body [ Props [ Style [ AlignItems AlignItemsOptions.FlexStart ] ] ] [

            Container.container [] [
                Totals.Panel(
                        {
                           Totals = model.Totals |> Deferred.defaultResolved Invoice.TotalsDefault
                           ReportRequest = fun r -> dispatch <| Msg.ReportRequest (r,Started)
                        }
                )
                Columns.columns [] [
                    Column.column [ Column.Width(Screen.All, Column.IsOneThird) ] [
                        Heading.p [ Heading.Modifiers [ Modifier.TextAlignment(Screen.All, TextAlignment.Centered) ] ] [
                            str <| model.Title
                        ]
                        Invoice.view
                            { Model = model.InvoiceModel
                              Dispatch = dispatch << InvoiceMsg }
                    ]
                    Column.column [ Column.Width(Screen.All, Column.IsTwoThirds) ] [
                        Invoices.view
                            { Model = model.InvoicesModel
                              Dispatch = dispatch << InvoicesMsg }
                    ]
                ]
            ]
        ]
    ]