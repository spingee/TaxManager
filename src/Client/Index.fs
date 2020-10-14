module Index

open Elmish
open Fable.Remoting.Client
open Shared
open System
open Types




type Msg =
    | AddInvoice
    | AddedInvoice of Result<string, string>
    | SetRate of string
    | SetAccPeriod of DateTime
    | SetMandays of string
    | HandleError of Exception

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
        let invoice: Shared.Invoice =
            { Rate = model.Input.Rate.Parsed.Value
              ManDays = model.Input.ManDays.Parsed.Value
              AccountingPeriod =
                  { Month = uint8 model.Input.AccountingPeriod.Month
                    Year = uint16 model.Input.AccountingPeriod.Year } }

        let model = { model with IsLoading = true }

        let cmd =
            Cmd.OfAsync.either invoiceApi.addInvoice invoice AddedInvoice HandleError

        model, cmd
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
    | HandleError exn ->
        { model with
              IsLoading = false
              Result = Some(Error exn.Message) },
        Cmd.none

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
        fieldset [ Disabled model.IsLoading ] [
            //Content.content [ ] [
            //    Content.Ol.ol [ ] [
            //        for todo in model.Todos ->
            //            li [ ] [ str todo.Description ]
            //    ]
            //]
            Field.div [ Field.IsGrouped ] [
                Control.p [ Control.IsExpanded ] [
                    Label.label [] [ str "Man-day rate" ]
                    Input.text [ Input.Value(model.Input.Rate.Raw)
                                 Input.Placeholder "Man-day rate in CZK"
                                 Input.OnChange(fun x -> SetRate x.Value |> dispatch) ]
                ]
            ]
            Field.div [ Field.IsGrouped ] [
                Control.p [ Control.IsExpanded ] [
                    Label.label [] [ str "Month of year" ]
                    Flatpickr.flatpickr [ Flatpickr.OnChange(SetAccPeriod >> dispatch)
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
                    Input.text [ Input.Value(model.Input.ManDays.Raw)
                                 Input.Placeholder "Total number of man days"
                                 Input.OnChange(fun x -> SetMandays x.Value |> dispatch)

                                  ]
                ]
            ]
            Field.div [ Field.IsGrouped ] [
                Control.p [] [
                    Button.a [ Button.Color IsPrimary
                               Button.IsLoading model.IsLoading
                               Button.Disabled(isValid model.Input |> not)
                               Button.OnClick(fun _ -> dispatch AddInvoice) ] [
                        str "Add"
                    ]
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
                Columns.columns [ Columns.IsCentered ] [
                    Column.column [ Column.Width(Screen.All, Column.IsOneThird) ] [
                        Heading.p [ Heading.Modifiers [ Modifier.TextAlignment(Screen.All, TextAlignment.Centered) ] ] [
                            str <| model.Title
                        ]
                        containerBox model dispatch
                        match model.Result with
                        | Some res ->
                            Notification.notification [ Notification.Color
                                                            (match res with
                                                             | Ok _ -> IsSuccess
                                                             | Error _ -> IsDanger) ] [
                                str
                                    (match res with
                                     | Ok s
                                     | Error s -> s)
                            ]
                        | None _ ->
                            Notification.notification [ Notification.Modifiers [ Modifier.IsInvisible(Screen.All, true) ] ] []
                    ]
                ]
            ]
        ]
    ]
