[<RequireQualifiedAccess>]
module AutoComplete

open Fable.Core.JsInterop
open Fable.React
open Fable.React.Props
open FSharp.Control
open Fable.Reaction
open Fulma
open Browser.Dom


type Props = {
    DebounceTimeout: int
    Search: string -> Async<Result<string list, string>>
    Dispatch:  string -> unit
}


// The model holds data that you want to keep track of while the application is running
// in this case, we are keeping track of a counter
// we mark it as optional, because initially it will not be available from the client
// the initial value will be requested from server
type Model = {
    Result: string list
    Loading: bool
    SelectedText: string option
}

// The Msg type defines what events/actions can occur while the application is running
// the state of the application changes *only* in reaction to these events
type Msg =
    | KeyboardEvent of Browser.Types.KeyboardEvent
    | Loading
    | QueryResult of Result<string list, string>
    | Close of string

    static member asKeyboardEvent = function
        | KeyboardEvent ev -> Some ev
        | _ -> None

// defines the initial state and initial command (= side-effect) of the application
let init () : Model = {
    Result = []
    Loading = false
    SelectedText = None
}

// The update function computes the next state of the application based on the current state and the incoming events/messages
// It can also run side-effects (encoded as commands) like calling the server via Http.
// these commands in turn, can dispatch messages to which the update function will react.
let update (currentModel: Model) (msg: Msg) : Model =
    let model =
        match msg with
        | QueryResult (Ok entries) ->
            { Result = entries; Loading = false ; SelectedText = None }
        | Loading ->
            console.log(sprintf "=============>%A" Loading)
            { currentModel with Loading = true ; SelectedText = None  }
        | Close s ->
            console.log(sprintf "=============>%s" s)
            { Result = []; Loading = false; SelectedText = Some s }
        | _ -> currentModel
    model

let view (props: Props) (model: Model) (dispatch : Msg -> unit) =
    let active (result : string list) =
        Dropdown.Option.IsActive (result.Length > 0)

    Dropdown.dropdown [ active model.Result ] [
        div [] [
            Control.div [ Control.Option.IsLoading model.Loading ] [
                Input.input [ Input.Option.Placeholder "Enter query ..."
                              Input.Option.Props [ OnKeyUp (KeyboardEvent >> dispatch)]
                              match model.SelectedText with
                              | Some s -> Input.Option.Value s
                              | _ -> ()
                            ]
            ]
        ]

        Dropdown.menu [ Props [ Role "menu" ]] [
            Dropdown.content [] [
                for item in model.Result do
                    yield a [ Href "#"
                              Class "dropdown-item"
                              OnClick (fun ev ->
                                            ev.preventDefault()
                                            props.Dispatch ev.target?textContent
                                            dispatch (Close ev.target?textContent)
                                            ) ] [
                        str item
                    ]
            ]
        ]
    ]

let stream (props: Props) model msgs =
    let targetValue (ev: Browser.Types.KeyboardEvent) : string =
        try
            let target = !!ev.target?value : string
            target.Trim ()
        with _ -> ""

    let terms =
        msgs
        |> AsyncRx.choose Msg.asKeyboardEvent       // Event to Msg
        |> AsyncRx.map targetValue                  // Map keyboard event to input value
        |> AsyncRx.filter (fun term -> term.Length >= 0)
        |> AsyncRx.debounce props.DebounceTimeout   // Pause for 750ms
        |> AsyncRx.distinctUntilChanged             // Only if the value has changed

    let loading =
        terms
        |> AsyncRx.filter (fun x -> x.Length > 0)
        |> AsyncRx.map (fun _ -> Loading)
    let empty =
        terms
        |> AsyncRx.filter (fun x -> x.Length = 0)
        |> AsyncRx.map (fun _ -> Close "")

    let results =
        terms
        |> AsyncRx.filter (fun x -> x.Length > 0)
        |> AsyncRx.flatMapLatest (props.Search >> AsyncRx.ofAsync)
        |> AsyncRx.catch (sprintf "%A" >> Error >> AsyncRx.single)
        |> AsyncRx.map QueryResult

    results
    |> AsyncRx.merge loading
    |> AsyncRx.merge empty
    |> AsyncRx.tag "msgs"

let textBox =
    let initialModel = init ()

    FunctionComponent.Of(fun (props : Props) ->
        let model = Hooks.useReducer(update, initialModel)
        let dispatch, msgs = Reaction.useStatefulStream(model.current, model.update, stream props)

        view props model.current dispatch
    )