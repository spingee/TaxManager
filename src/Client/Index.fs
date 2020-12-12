module Index

open Fable.React
open Fable.React.Props
open Fulma
open Fable.Core.JsInterop
open State
open Types
open Fable.FontAwesome
open Utils

let monthSelectPlugin (x: obj): obj -> obj =
    importDefault "flatpickr/dist/plugins/monthSelect"

let navBrand =
    Navbar.Brand.div [] [
        Navbar.Item.a [ Navbar.Item.Props [ Href "https://safe-stack.github.io/" ]
                        Navbar.Item.IsActive true ] [
            img [ Src "/favicon.png"; Alt "Logo" ]
        ]
    ]

let createTextField label validated (options: Input.Option list)  =
    Field.div [ Field.IsGrouped ] [
        Control.p [ Control.IsExpanded ] [
            Label.label [] [ str label ]
            Input.text
                ([ match validated.Parsed with
                   | Error []
                   | Ok _ -> ()
                   | Error _ -> Input.Color IsDanger
                   Input.Value(validated.Raw)]
                 @ options)
            match validated.Parsed with
            | Error []
            | Ok _ -> div [] []
            | Error (e :: _) ->
                Help.help [ Help.Color IsDanger ] [
                    str e
                ]
        ]
    ]

let errorHelp validated =
    match validated.Parsed with
    | Error []
    | Ok _ -> div [] []
    | Error (e :: _) ->
        Help.help [ Help.Color IsDanger ] [
            str e
        ]


let invoicesTable model (dispatch: Msg -> unit) =
    Box.box' [] [
        Table.table [] [
            thead [] [
                tr [] [
                    th [] [ str "Period" ]
                    th [] [ str "Rate" ]
                    th [] [ str "Man Days" ]
                    th [] [ str "Customer" ]
                    th [] [ str "Action" ]
                ]
            ]
            tbody [] [
                yield! model.Invoices
                       |> Deferred.map (fun invs ->
                           invs
                           |> List.sortByDescending (fun x -> x.AccountingPeriod)
                           |> List.map (fun i ->
                               tr [] [
                                   td [] [
                                       str
                                       <| sprintf "%i/%i" i.AccountingPeriod.Year i.AccountingPeriod.Month
                                   ]
                                   td [] [ str <| i.Rate.ToString() ]
                                   td [] [ str <| i.ManDays.ToString() ]
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
    ]

let createCustomerModal model (dispatch: Msg -> unit) =
    Modal.modal [ Modal.IsActive model.CreatingCustomer ] [
        let custInput = model.CustomerInput
        Modal.background [] []
        Modal.Card.card [] [
            Modal.Card.head [] [
                Modal.Card.title [] [
                    str "Create customer"
                ]
                Delete.delete [ Delete.OnClick(fun _ -> dispatch <| EndCreateCustomer false) ] []
            ]
            Modal.Card.body [] [
                createTextField "Name" custInput.Name
                    [ Input.Placeholder "Name of customer"
                      Input.OnChange(fun x -> SetCustomerName x.Value |> dispatch) ]
                createTextField "Identification number" custInput.IdNumber
                    [ Input.Placeholder "Identification number"
                      Input.OnChange(fun x -> SetCustomerIdNumber x.Value |> dispatch) ]
                createTextField "VAT Id" custInput.VatId
                    [ Input.Placeholder "VAT Id"
                      Input.OnChange(fun x -> SetCustomerVatId x.Value |> dispatch) ]
                createTextField "Address" custInput.Address
                    [ Input.Placeholder "Address"
                      Input.OnChange(fun x -> SetCustomerAddress x.Value |> dispatch) ]
                createTextField "Note" custInput.Note
                    [ Input.Placeholder "Note"
                      Input.OnChange(fun x -> SetCustomerNote x.Value |> dispatch) ]

            ]
            Modal.Card.foot [] [
                Button.button [ Button.Color IsSuccess
                                Button.Disabled
                                <| not (Customer.isValid custInput)
                                Button.OnClick(fun _ -> dispatch <| EndCreateCustomer true) ] [
                    str "Create"
                ]
                Button.button [ Button.OnClick(fun _ -> dispatch <| EndCreateCustomer false) ] [
                    str "Cancel"
                ]
            ]
        ]
    ]

let containerBox (model: Model) (dispatch: Msg -> unit) =
    Box.box' [] [
        fieldset [ ReadOnly model.IsLoading ] [
            createTextField "Man-day rate" model.InvoiceInput.Rate
                [ Input.Placeholder "Man-day rate in CZK"
                  Input.OnChange(fun x -> SetRate x.Value |> dispatch) ]

            Field.div [ Field.IsGrouped ] [
                Control.p [ Control.IsExpanded ] [
                    Label.label [] [ str "Month of year" ]
                    Flatpickr.flatpickr [ Flatpickr.OnChange(SetAccPeriod >> dispatch)
                                          Flatpickr.Value
                                              (System.DateTime
                                                  (int model.InvoiceInput.AccountingPeriod.Year,
                                                   int model.InvoiceInput.AccountingPeriod.Month,
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
            createTextField "Total number of man days" model.InvoiceInput.ManDays
                [ Input.Placeholder "Total number of man days"
                  Input.OnChange(fun x -> SetMandays x.Value |> dispatch) ]
            createTextField "Order number" model.InvoiceInput.OrderNumber
                [ Input.Placeholder "Order number"
                  Input.OnChange(fun x -> SetOrderNumber x.Value |> dispatch) ]

            Field.div [ Field.IsExpanded ] [
                Label.label [] [ str "Customer" ]
                Field.p [ Field.HasAddons ] [
                    Control.div [ Control.IsExpanded ] [
                        Select.select [ Select.IsFullWidth
                                        Select.IsLoading(model.Customers = InProgress) ] [
                            select [ OnChange(fun e -> dispatch <| SelectCustomer e.Value) ] [
                                match model.SelectedCustomer with
                                | None -> yield option [ Value("") ] [ str "" ]
                                | _ -> ()
                                match model.Customers with
                                | Resolved custs ->
                                    for i = 0 to custs.Length - 1 do
                                        let c = custs.[i]
                                        yield option [ Value(i)
                                                       Selected(model.SelectedCustomer = Some c) ] [
                                                  str <| sprintf "%s (%i)" c.Name c.IdNumber
                                              ]
                                | _ -> ()
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
                               Button.Disabled(isModelValid model |> not)
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
                Hero.Props
                    [ Style [ Background """url("kid2.png") no-repeat center center fixed"""
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

                                (match res with
                                 | Ok s -> str s
                                 | Error [ one ] -> str (one)
                                 | Error e ->
                                     Content.content [] [
                                         ul [] [
                                             for s in e do
                                                 li [] [ str s ]
                                         ]
                                     ])
                            ]
                        | None _ ->
                            Notification.notification [ Notification.Modifiers
                                                            [ Modifier.IsInvisible(Screen.All, true) ] ] []
                    ]
                    Column.column [ Column.Width(Screen.All, Column.IsHalf) ] [
                        invoicesTable model dispatch
                    ]
                ]
            ]
        ]
    ]
