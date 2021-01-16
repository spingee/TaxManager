[<RequireQualifiedAccess>]
module Totals

open Shared.Invoice
open Fable.React
open Fable.React.Props
open Fulma
open Utils

let Panel =
    FunctionComponent.Of
        (fun (props: Totals) ->
            Level.level [] [
                Level.item [ Level.Item.HasTextCentered ] [
                    div [] [
                        Level.heading [ Modifiers [ Modifier.TextColor IsPrimary ] ] [
                            str <| sprintf "Last quarter (%s)" props.LastQuarter.TimeRange
                        ]

                        Level.title [] [
                            str <| formatDecimal 2 props.LastQuarter.Value
                        ]
                    ]
                ]
                Level.item [ Level.Item.HasTextCentered ] [
                    div [] [
                        Level.heading [ Modifiers [ Modifier.TextColor IsPrimary ] ] [
                            str <| sprintf "Last quarter VAT (%s)" props.LastQuarterVat.TimeRange
                        ]

                        Level.title [] [
                            str <|  formatDecimal 2 props.LastQuarterVat.Value
                        ]
                    ]
                ]
                Level.item [ Level.Item.HasTextCentered ] [
                    div [] [
                        Level.heading [ Modifiers [ Modifier.TextColor IsPrimary ] ] [
                            str <| sprintf "Last year (%s)" props.LastYear.TimeRange
                        ]

                        Level.title [] [
                            str <| formatDecimal 2 props.LastYear.Value
                        ]
                    ]
                ]
            ])
// Field.div [ Field.IsGrouped
//             Field.IsGroupedMultiline ] [
//     Control.div [] [
//         Tag.list [ Tag.List.HasAddons ] [
//             Tag.tag [Tag.Color IsDark] [ str <| "Last quarter" ]
//             Tag.tag [ Tag.Color IsPrimary ] [
//                 str <| props.LastQuarterTotal.ToString()
//             ]
//         ]
//     ]
//     Control.div [] [
//         Tag.list [ Tag.List.HasAddons ] [
//             Tag.tag [Tag.Color IsDark] [ str <| "Last quarter VAT" ]
//             Tag.tag [ Tag.Color IsPrimary ] [
//                 str <| props.LastQuarterTotalVat.ToString()
//             ]
//         ]
//     ]
//     Control.div [] [
//         Tag.list [ Tag.List.HasAddons ] [
//             Tag.tag [Tag.Color IsDark] [ str <| "Last year" ]
//             Tag.tag [ Tag.Color IsPrimary ] [
//                 str <| props.LastYearTotal.ToString()
//             ]
//         ]
//     ]
// ])
