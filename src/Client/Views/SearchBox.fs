[<RequireQualifiedAccess>]
module SearchBox


open System.Data
open Elmish
open Fable.React
open Fulma
open Thoth.Elmish
open System
open Fable.React.Props
open Fable.Core.JsInterop
open Browser.Dom


type State =
    | Initial
    | IsTyping
    | StoppedTyping

type Model =
    { Debouncer: Debouncer.State
      UserInput: string
      Dictionary: string list
      Result: string array
      ActiveIndex: int option
      SelectedText: string option
      State: State }

let private getActiveItem model =
    match model.ActiveIndex with
    | None -> None
    | Some i -> Some model.Result.[i]

let private isActive model =
    model.Result.Length > 0


type Direction =
    | Up
    | Down

type Msg =
    | DebouncerSelfMsg of Debouncer.SelfMessage<Msg>
    | ChangeValue of string
    | EndOfInput
    | Reset
    | SelectText of string
    | MoveList of Direction
    | Close

let init () =
    { Debouncer = Debouncer.create ()
      UserInput = ""
      Dictionary =
          [ "lol"
            "stvol"
            "asdja"
            "asdas adasd"
            "alol"
            "bstvol"
            "basdja"
            "aaasdas adasd" ]
      SelectedText = None
      Result = [||]
      ActiveIndex = None
      State = State.Initial },
    Cmd.none

let update msg model =
    match msg with
    | ChangeValue newValue ->
        let (debouncerModel, debouncerCmd) =
            model.Debouncer
            |> Debouncer.bounce (TimeSpan.FromSeconds 1.) "user_input" EndOfInput

        { model with
              UserInput = newValue
              State = State.IsTyping
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
              State = State.StoppedTyping
              Result =
                  (model.Dictionary
                   |> List.filter (fun s -> s.StartsWith(model.UserInput)))
                  |> Array.ofList },
        Cmd.none

    | Close ->
        { model with
              Result = [||]
              ActiveIndex = None
              State = State.Initial },
        Cmd.none
    | SelectText str ->
        { model with
              UserInput = str
              Result = [||]
              ActiveIndex = None
              State = State.Initial },
        Cmd.none
    | MoveList dir ->
        let index =
            match dir, model.ActiveIndex, model.Result with
            | _, _, [||] -> None
            | Up, Some i, _ -> Some <| (i + model.Result.Length - 1) % model.Result.Length
            | Down, Some i, _ -> Some <| (i + 1) % model.Result.Length
            | Up, None, _ -> Some <| model.Result.Length - 1
            | Down, None, _ -> Some <| 0


        { model with ActiveIndex = index }, Cmd.none

type Props = { Model: Model; Dispatch: Msg -> unit }

let private elmishView name render =
    FunctionComponent.Of(render, name, equalsButFunctions)

let view =
    elmishView "SearchBox"
    <| fun { Model = model; Dispatch = dispatch } ->


        Dropdown.dropdown [ Dropdown.Option.IsActive(isActive model) ] [
            div [] [
                //Control.Option.IsLoading model.Loading
                Control.div [] [
                    Input.input [ Input.Option.Placeholder "Enter query ..."
                                  Input.OnChange(fun ev -> dispatch (ChangeValue ev.Value))
                                  Input.Value model.UserInput
                                  match model.SelectedText with
                                  | Some s -> Input.Option.Value s
                                  | _ -> ()
                                  Input.Props [ OnKeyUp(fun ev ->
                                                    match ev.key with
                                                    | "ArrowDown" ->
                                                         if (isActive model) then  dispatch (MoveList Down)
                                                         else dispatch EndOfInput
                                                    | "ArrowUp" ->
                                                         if (isActive model) then dispatch (MoveList Up)
                                                         else dispatch EndOfInput
                                                    | "Enter" ->
                                                        match model.ActiveIndex with
                                                        | Some i -> dispatch (SelectText(model.Result.[i]))
                                                        | _ -> ()
                                                    | "Escape" -> if (isActive model) then dispatch Close

                                                    | _ -> ()) ] ]
                ]
            ]

            Dropdown.menu [ Props [ Role "menu" ] ] [
                Dropdown.content [] [
                    yield!
                        model.Result
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
                ]
            ]
        ]
