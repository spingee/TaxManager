[<RequireQualifiedAccess>]
module SearchBox


open Elmish
open Fable.React
open Fulma
open Thoth.Elmish
open System
open Fable.React.Props
open Fable.Core.JsInterop
open Utils
open Common

type State =
    | Initial
    | IsTyping
    | StoppedTyping

type Model =
    { Debouncer: Debouncer.State
      UserInput: string
      Result: Deferred<string array>
      ActiveIndex: int option
      SelectedText: string option
      State: State
      Search: string -> Async<Result<string list, string>>
       }

let private getActiveItem model =
    match model.ActiveIndex, model.Result with
    | Some i,Resolved arr -> Some arr.[i]
    | _,_ -> None


let private isActive model =
    match model.Result with
    | Resolved arr -> arr.Length > 0
    | _ -> false



type Direction =
    | Up
    | Down

type Msg =
    | DebouncerSelfMsg of Debouncer.SelfMessage<Msg>
    | ChangeValue of string
    | EndOfInput
    | SelectText of string
    | MoveList of Direction
    | Close
    | Search of AsyncOperationStatus<Result<string list, string>>

let init search =
    { Debouncer = Debouncer.create ()
      UserInput = ""
      SelectedText = None
      Result = HasNotStartedYet
      ActiveIndex = None
      State = Initial
      Search = search },
    Cmd.none

let update msg model =
    match msg with
    | ChangeValue newValue ->
        let (debouncerModel, debouncerCmd) =
            model.Debouncer
            |> Debouncer.bounce (TimeSpan.FromSeconds 1.) "user_input" EndOfInput

        { model with
              UserInput = newValue
              State = IsTyping
              Debouncer = debouncerModel },
        Cmd.batch [ Cmd.map DebouncerSelfMsg debouncerCmd ]

    | DebouncerSelfMsg debouncerMsg ->
        let (debouncerModel, debouncerCmd) =
            Debouncer.update debouncerMsg model.Debouncer

        { model with
              Debouncer = debouncerModel },
        debouncerCmd

    | EndOfInput ->
        //        let (debouncerModel, debouncerCmd) =
        //            model.Debouncer
        //            |> Debouncer.bounce (TimeSpan.FromSeconds 2.5) "reset_demo" Reset


        //        { model with State = State.StoppedTyping
        //                     Debouncer = debouncerModel }, Cmd.batch [ Cmd.map DebouncerSelfMsg debouncerCmd ]

        { model with
              State = StoppedTyping },
        Cmd.ofMsg(Search Started)

    | Close ->
        { model with
              Result = HasNotStartedYet
              ActiveIndex = None
              State = Initial },
        Cmd.none
    | SelectText str ->
        { model with
              UserInput = str
              Result = HasNotStartedYet
              ActiveIndex = None
              State = Initial },
        Cmd.none
    | MoveList dir ->
        let index =
            match dir, model.ActiveIndex, model.Result with
            | _, _, (InProgress _|HasNotStartedYet| Resolved [||]) -> None
            | Up, Some i, Resolved result -> Some <| (i + result.Length - 1) % result.Length
            | Down, Some i, Resolved result -> Some <| (i + 1) % result.Length
            | Up, None, Resolved result -> Some <| result.Length - 1
            | Down, None, _ -> Some <| 0


        { model with ActiveIndex = index }, Cmd.none
    | Search Started ->
        model,
        Cmd.OfAsync.either
            model.Search
            model.UserInput (Finished >> Search)
            (exceptionToResult >> Finished >> Search)
    | Search (Finished result) ->
        match result with
        | Ok res -> {model with Result = Resolved (Array.ofList res)},Cmd.none
        | _ -> model,Cmd.none


type Props = { Model: Model; Dispatch: Msg -> unit }

let private elmishView name render =
    FunctionComponent.Of(render, name, equalsButFunctions)

let view =
    elmishView "SearchBox"
    <| fun { Model = model; Dispatch = dispatch } ->


        Dropdown.dropdown [ Dropdown.IsActive(isActive model); Dropdown.Props [ Style [ Display DisplayOptions.Block ] ] ] [
            div [] [
                //Control.Option.IsLoading model.Loading
                Control.div [ Control.IsExpanded] [
                    Input.input [ Input.Option.Placeholder "Enter query ..."
                                  Input.OnChange(fun ev -> dispatch (ChangeValue (ev.Value)))
                                  Input.Value model.UserInput
                                  match model.SelectedText with
                                  | Some s -> Input.Option.Value s
                                  | _ -> ()
                                  Input.Props [ OnKeyUp(fun ev ->
                                                    match ev.key with
                                                    | "ArrowDown" ->
                                                         if (isActive model) then  dispatch (MoveList Down)
                                                         else dispatch (EndOfInput)
                                                    | "ArrowUp" ->
                                                         if (isActive model) then dispatch (MoveList Up)
                                                         else dispatch (EndOfInput)
                                                    | "Enter" ->
                                                        match model.ActiveIndex,model.Result with
                                                        | Some i,Resolved result -> dispatch (SelectText(result.[i]))
                                                        | _,_ -> ()
                                                    | "Escape" -> if (isActive model) then dispatch Close

                                                    | _ -> ())
                                                OnBlur (fun _ -> dispatch Close)] ]
                ]
            ]

            Dropdown.menu [ Props [ Role "menu" ] ] [
                Dropdown.content [] [
                    yield!
                        model.Result |>
                            Deferred.map (fun res ->
                                res
                                |> Array.mapi (fun i s ->
                                    let isActive =
                                        model.ActiveIndex
                                        |> Option.map (fun ai -> ai = i)
                                        |> Option.defaultValue false

                                    a [ Href "#"
                                        Class
                                            (match isActive with
                                             | false -> "dropdown-item"
                                             | true -> "dropdown-item is-active")
                                        OnClick(fun ev ->
                                            ev.preventDefault ()
                                            dispatch (SelectText ev.target?textContent)) ] [
                                        str s
                                    ])
                                )
                            |> Deferred.defaultResolved  [|Html.none|]
                ]
            ]
        ]
