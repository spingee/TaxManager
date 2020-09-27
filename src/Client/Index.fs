module Index

open Elmish
open Fable.Remoting.Client
open Shared
open System


type Model = { Input: Invoice; Title: string }


type Msg =
    | AddInvoice
    | AddedInvoice of Invoice
    | SetInput of Invoice

let todosApi =
    Remoting.createApi ()
    |> Remoting.withRouteBuilder Route.builder
    |> Remoting.buildProxy<ITodosApi>

let invoiceApi =
    Remoting.createApi ()
    |> Remoting.withRouteBuilder Route.builder
    |> Remoting.buildProxy<IInvoiceApi>

let init (): Model * Cmd<Msg> =
    let model =
        { Input =
              { Rate = uint16 6000
                ManDays = 20uy
                AccountingPeriod =
                    { Month = uint8 DateTime.Now.Month
                      Year = uint16 DateTime.Now.Year } }
          Title = "Submit invoice data" }

    let cmd = Cmd.none //Cmd.OfAsync.perform todosApi.getTodos () GotTodos
    model, cmd

let update (msg: Msg) (model: Model): Model * Cmd<Msg> =
    match msg with
    | AddedInvoice invoice -> { model with Input = invoice }, Cmd.none
    | AddInvoice ->
        let cmd =
            Cmd.OfAsync.perform invoiceApi.addInvoice model.Input AddedInvoice

        model, cmd
    | SetInput invoice -> { model with Input = invoice }, Cmd.none

open Fable.React
open Fable.React.Props
open Fulma
open Fable.Core.JsInterop

let monthSelectPlugin (x: obj): obj -> obj =
    importDefault "flatpickr/dist/plugins/monthSelect"

let navBrand =
    Navbar.Brand.div [] [
        Navbar.Item.a [ Navbar.Item.Props [ Href "https://safe-stack.github.io/" ]
                        Navbar.Item.IsActive true ] [
            img [ Src "/favicon.png"; Alt "Logo" ]
        ]
    ]



let containerBox (model: Model) (dispatch: Msg -> unit) =
    Box.box' [] [
        //Content.content [ ] [
        //    Content.Ol.ol [ ] [
        //        for todo in model.Todos ->
        //            li [ ] [ str todo.Description ]
        //    ]
        //]
        Field.div [ Field.IsGrouped ] [
            Control.p [ Control.IsExpanded ] [
                Label.label [] [ str "Man-day rate" ]
                Input.text [ Input.Value(model.Input.Rate.ToString())
                             Input.Placeholder "Man-day rate in CZK"
                             Input.OnChange(fun x ->
                                 SetInput
                                     { model.Input with
                                           Rate = uint16 x.Value }
                                 |> dispatch) ]
            ]
        ]
        Field.div [ Field.IsGrouped ] [
            Control.p [ Control.IsExpanded ] [
                Label.label [] [ str "Month of year" ]
                Flatpickr.flatpickr [ Flatpickr.OnChange(fun x ->
                                          SetInput
                                              { model.Input with
                                                    AccountingPeriod =
                                                        { model.Input.AccountingPeriod with
                                                              Month = uint8 x.Month
                                                              Year = uint16 x.Year } }
                                          |> dispatch)
                                      Flatpickr.Value
                                          (System.DateTime
                                              (int model.Input.AccountingPeriod.Year,
                                               int model.Input.AccountingPeriod.Month,
                                               1))
                                      Flatpickr.DateFormat "F Y"
                                      Flatpickr.custom
                                          "plugins"
                                          [| monthSelectPlugin
                                              ({| shorthand = true
                                                  dateFormat = "F Y"
                                                  altFormat = "F Y" |}) |]

                                          true
                                      Flatpickr.Locale Flatpickr.Locales.czech
                                      Flatpickr.ClassName "input" ]
            ]
        ]

        Field.div [ Field.IsGrouped ] [
            Control.p [ Control.IsExpanded ] [
                Label.label [] [
                    str "Total number of man days"
                ]
                Input.text [ Input.Value(model.Input.ManDays.ToString())
                             Input.Placeholder "Total number of man days"
                             Input.OnChange(fun x ->
                                 SetInput
                                     { model.Input with
                                           ManDays = uint8 x.Value }
                                 |> dispatch) ]
            ]
        ]
        Field.div [ Field.IsGrouped ] [
            Control.p [] [
                Button.a [ Button.Color IsPrimary
                           //Button.Disabled (Todo.isValid model.Input |> not)
                           Button.OnClick(fun _ -> dispatch AddInvoice) ] [
                    str "Add"
                ]
            ]
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

        Hero.body [] [
            Container.container [] [
                Column.column [ Column.Width(Screen.All, Column.Is4)
                                Column.Offset(Screen.All, Column.Is4) ] [
                    Heading.p [ Heading.Modifiers [ Modifier.TextAlignment(Screen.All, TextAlignment.Centered) ] ] [
                        str <| model.Title
                    ]
                    containerBox model dispatch
                ]
            ]
        ]
    ]
