module Index

open Elmish
open Fable.Remoting.Client
open Shared



type Model =
    { Input: Invoice }


type Msg =    
    | AddInvoice
    | AddedInvoice of Invoice
    | SetInput of Invoice

let todosApi =
    Remoting.createApi()
    |> Remoting.withRouteBuilder Route.builder
    |> Remoting.buildProxy<ITodosApi>

let invoiceApi =
    Remoting.createApi()
    |> Remoting.withRouteBuilder Route.builder
    |> Remoting.buildProxy<IInvoiceApi>

let init(): Model * Cmd<Msg> =    
    let model =
        { Input = { Rate = uint16 6000
                    ManDays = 20uy
                    Month = { Month = 1uy
                              Year = uint16 2020 }
                  }
        }
    let cmd = Cmd.none//Cmd.OfAsync.perform todosApi.getTodos () GotTodos
    model, cmd

let update (msg: Msg) (model: Model): Model * Cmd<Msg> =
    match msg with   
    | AddedInvoice invoice ->
        model, Cmd.none
    | AddInvoice ->
        let cmd = Cmd.OfAsync.perform invoiceApi.addInvoice model.Input AddedInvoice
        model ,cmd
    | SetInput invoice ->
        {model with Input = invoice} ,Cmd.none

open Fable.React
open Fable.React.Props
open Fulma

let navBrand =
    Navbar.Brand.div [ ] [
        Navbar.Item.a [
            Navbar.Item.Props [ Href "https://safe-stack.github.io/" ]
            Navbar.Item.IsActive true
        ] [
            img [
                Src "/favicon.png"
                Alt "Logo"
            ]
        ]
    ]

let containerBox (model : Model) (dispatch : Msg -> unit) =
    Box.box' [ ] [
        //Content.content [ ] [
        //    Content.Ol.ol [ ] [
        //        for todo in model.Todos ->
        //            li [ ] [ str todo.Description ]
        //    ]
        //]
        Field.div [ Field.IsGrouped ] [            
            Control.p [ Control.IsExpanded ] [
                Label.label [ ] [ str "Man-day rate" ]
                Input.text [
                  Input.Value (model.Input.Rate.ToString())
                  Input.Placeholder "Man-day rate in CZK"
                  Input.OnChange (fun x -> SetInput {model.Input with Rate = uint16 x.Value } |> dispatch)
                ]
            ]           
        ]
        Field.div [ Field.IsGrouped ] [           
            Control.p [ Control.IsExpanded ] [
                Label.label [ ] [ str "Month of year" ]
                Input.text [
                  Input.Value (model.Input.Month.Month.ToString())
                  Input.Placeholder "Month of year"
                  Input.OnChange (fun x -> SetInput {model.Input with Month = { model.Input.Month with Year = uint16 x.Value  } } |> dispatch)
                ]
            ]            
        ]
        Field.div [ Field.IsGrouped ] [           
            Control.p [ Control.IsExpanded ] [
                Label.label [ ] [ str "Year" ]
                Input.text [
                  Input.Value (model.Input.Month.Year.ToString())
                  Input.Placeholder "Year"
                  Input.OnChange (fun x -> SetInput {model.Input with Month = { model.Input.Month with Year = uint16 x.Value  } } |> dispatch)
                ]
            ]            
        ]
        Field.div [ Field.IsGrouped ] [           
            Control.p [ Control.IsExpanded ] [
                Label.label [ ] [ str "Total number of man days" ]
                Input.text [
                  Input.Value (model.Input.ManDays.ToString())
                  Input.Placeholder "Total number of man days"
                  Input.OnChange (fun x -> SetInput {model.Input with ManDays = uint8 x.Value } |> dispatch)
                ]
            ]            
        ]
        Field.div [ Field.IsGrouped ] [           
            Control.p [ ] [
                Button.a [
                    Button.Color IsPrimary
                    //Button.Disabled (Todo.isValid model.Input |> not)
                    Button.OnClick (fun _ -> dispatch AddInvoice)
                ] [
                    str "Add"
                ]
            ]
        ]
    ]

let view (model : Model) (dispatch : Msg -> unit) =
    Hero.hero [
        Hero.Color IsPrimary
        Hero.IsFullHeight
        Hero.Props [
            Style [
                Background """linear-gradient(rgba(0, 0, 0, 0.5), rgba(0, 0, 0, 0.5)), url("https://unsplash.it/1200/900?random") no-repeat center center fixed"""
                BackgroundSize "cover"
            ]
        ]
    ] [
        Hero.head [ ] [
            Navbar.navbar [ ] [
                Container.container [ ] [ navBrand ]
            ]
        ]

        Hero.body [ ] [
            Container.container [ ] [
                Column.column [
                    Column.Width (Screen.All, Column.Is6)
                    Column.Offset (Screen.All, Column.Is3)
                ] [
                    Heading.p [ Heading.Modifiers [ Modifier.TextAlignment (Screen.All, TextAlignment.Centered) ] ] [ str "Submit invoice data" ]
                    containerBox model dispatch
                ]
            ]
        ]
    ]

