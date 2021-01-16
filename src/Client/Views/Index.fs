module Index

open Fable.React
open Fable.React.Props
open Fulma
open Shared
open Elmish
open Utils
open Api


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



let init (): Model * Cmd<Msg> =
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

let update (msg: Msg) (model: Model): Model * Cmd<Msg> =
    match msg with
    | InvoiceMsg msg ->
        match msg with
        | Invoice.AddInvoice (Finished (inv, Ok id)) ->
            let subModel, cmd = Invoice.update msg model.InvoiceModel
            let inv = { inv with Id = id }

            { model with InvoiceModel = subModel },
            Cmd.batch [ Cmd.ofMsg (InvoicesMsg(Invoices.AddInvoice inv))
                        Cmd.map InvoiceMsg cmd
                        Cmd.ofMsg (LoadTotals Started)]
        | _ ->
            let newSubModel, cmd = Invoice.update msg model.InvoiceModel

            { model with
                  InvoiceModel = newSubModel },
            Cmd.map InvoiceMsg cmd
    | InvoicesMsg msg ->
        match msg with
        | Invoices.RemoveInvoice (inv, Finished (Ok _)) ->
            let subModel, cmd = Invoices.update msg model.InvoicesModel
            { model with InvoicesModel = subModel },Cmd.batch [ Cmd.ofMsg (LoadTotals Started); Cmd.map InvoicesMsg cmd]
        | _ ->
            let newSubModel, cmd = Invoices.update msg model.InvoicesModel

            { model with
                  InvoicesModel = newSubModel },
            Cmd.map InvoicesMsg cmd
    | LoadTotals Started ->
        { model with Totals = InProgress },
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
                  | Error _ -> Resolved Invoice.TotalsDefault },
        Cmd.none



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
                Container.container [] [
                    navBrand
                ]
            ]
        ]

        Hero.body [] [

            Container.container [] [
                Totals.Panel(
                    model.Totals
                    |> Deferred.defaultResolved Invoice.TotalsDefault
                )
                Columns.columns [ Columns.IsCentered ] [
                    Column.column [ Column.Width(Screen.All, Column.IsOneThird) ] [
                        Heading.p [ Heading.Modifiers [ Modifier.TextAlignment(Screen.All, TextAlignment.Centered) ] ] [
                            str <| model.Title
                        ]
                        Invoice.view
                            { Model = model.InvoiceModel
                              Dispatch = dispatch << InvoiceMsg }
                    ]
                    Column.column [ Column.Width(Screen.All, Column.IsHalf) ] [
                        Invoices.view
                            { Model = model.InvoicesModel
                              Dispatch = (dispatch << InvoicesMsg) }
                    ]
                ]
            ]
        ]
    ]
