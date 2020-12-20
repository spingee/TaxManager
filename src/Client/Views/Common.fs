module Common

open Utils
open Fable.Core
open Fulma
open Fable.React


[<Erase>]
type Html =
    static member inline none : ReactElement = unbox null

let elmishView name render = FunctionComponent.Of(render, name, equalsButFunctions)

let inline createTextField label validated (options: Input.Option list)  =
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
            | Ok _ -> Html.none
            | Error (e :: _) ->
                Help.help [ Help.Color IsDanger ] [
                    str e
                ]
        ]
    ]
let inline createTextAreaField label validated (options: Textarea.Option list)  =
    Field.div [ Field.IsGrouped ] [
        Control.p [ Control.IsExpanded ] [
            Label.label [] [ str label ]
            Textarea.textarea
                ([ match validated.Parsed with
                   | Error []
                   | Ok _ -> ()
                   | Error _ -> Textarea.Color IsDanger
                   Textarea.Value(validated.Raw)] @ options)
                []
            match validated.Parsed with
            | Error []
            | Ok _ -> Html.none
            | Error (e :: _) ->
                Help.help [ Help.Color IsDanger ] [
                    str e
                ]

        ]
    ]
let notification =
    function

    | Some res ->
        p []
            [Notification.notification [ Notification.Color
                                            (match res with
                                             | Ok _ -> IsSuccess
                                             | Error _ -> IsDanger) ] [

                (match res with
                 | Ok s -> str s
                 | Error [ one ] -> str (one)
                 | Error e ->
                     Content.content [] [
                             for s in e do
                                 p [] [ str s ]
                     ])
            ]]
    | None _ -> Html.none