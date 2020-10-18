module Index

open Fable.React
open Fable.React.Props
open Fulma
open Fable.Core.JsInterop
open State
open Types
open Fable.FontAwesome

let monthSelectPlugin (x: obj): obj -> obj =
    importDefault "flatpickr/dist/plugins/monthSelect"

let navBrand =
    Navbar.Brand.div [] [
        Navbar.Item.a [ Navbar.Item.Props [ Href "https://safe-stack.github.io/" ]
                        Navbar.Item.IsActive true ] [
            img [ Src "/favicon.png"; Alt "Logo" ]
        ]
    ]


let createCustomerModal model dispatch =
    Modal.modal [ Modal.IsActive model.CreatingCustomer ] [
        Modal.background [] []
        Modal.Card.card [] [
            Modal.Card.head [] [
                Modal.Card.title [] [
                    str "Create customer"
                ]
                Delete.delete [ Delete.OnClick(fun _ -> dispatch EndCreateCustomer) ] []
            ]
            Modal.Card.body [] [

                Field.div [ Field.IsGrouped ] [
                    Control.p [ Control.IsExpanded ] [
                        Label.label [] [ str "Name" ]
                        Input.text [ Input.OnChange(fun x -> SetRate x.Value |> dispatch) ]
                    ]
                ]
                Field.div [ Field.IsGrouped ] [
                    Control.p [ Control.IsExpanded ] [
                        Label.label [] [
                            str "Identification number"
                        ]
                        Input.text [ Input.OnChange(fun x -> SetRate x.Value |> dispatch) ]
                    ]
                ]
                Field.div [ Field.IsGrouped ] [
                    Control.p [ Control.IsExpanded ] [
                        Label.label [] [ str "VAT Id" ]
                        Input.text [ Input.OnChange(fun x -> SetRate x.Value |> dispatch) ]
                    ]
                ]
            ]
            Modal.Card.foot [] [
                Button.button [ Button.Color IsSuccess
                                Button.OnClick(fun _ -> dispatch EndCreateCustomer) ] [
                    str "Create"
                ]
                Button.button [ Button.OnClick(fun _ -> dispatch EndCreateCustomer) ] [
                    str "Cancel"
                ]
            ]
        ]
    ]

let containerBox (model: Model) (dispatch: Msg -> unit) =
    Box.box' [] [
        fieldset [ ReadOnly model.IsLoading ] [
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
                                 Input.OnChange(fun x -> SetMandays x.Value |> dispatch) ]
                ]
            ]
            Field.div [ Field.IsExpanded ] [
                Label.label [] [ str "Customer" ]
                Field.p [ Field.HasAddons ] [
                    Control.div [ Control.IsExpanded ] [
                        Select.select [ Select.IsFullWidth ] [
                            select [ OnChange(fun e -> dispatch <| SetCustomer e.Value) ] [
                                yield option [ Value("") ] [ str "" ]
                                for c in model.Customers ->
                                    option [ Value(c.Id)
                                             Selected(model.Input.CustomerId.Parsed.Value = c.Id) ] [
                                        str c.Name
                                    ]
                            ]
                        ]
                    ]
                    Control.div [] [
                        Button.a [ Button.Color IsPrimary
                                   Button.OnClick(fun _ -> dispatch BeginCreateCustomer) ] [
                            Icon.icon [] [
                                Fa.i [ Fa.Solid.Plus ] []
                            ]
                        ]
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
        createCustomerModal model dispatch
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
