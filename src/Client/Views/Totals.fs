[<RequireQualifiedAccess>]
module Totals

open Shared.Invoice
open Fable.React
open Fable.React.Props
open Fulma
open Utils
open Fable.FontAwesome

type Report = | Annual

type Props =
    { Totals: Totals
      ReportRequest: SummaryReportType -> unit }

let Panel =
    FunctionComponent.Of
        (fun (props: Props) ->
            div [ Style [ MarginBottom 20 ] ] [
                    Level.level [] [
                        Level.item [ Level.Item.HasTextCentered ] [
                            div [] [
                                Level.heading [ Modifiers [ Modifier.TextColor IsPrimary ] ] [
                                    str <| sprintf "Last quarter (%s)" props.Totals.LastQuarter.TimeRange
                                ]

                                Level.title [] [
                                    str <| formatDecimal 2 props.Totals.LastQuarter.Value
                                ]
                            ]
                        ]
                        Level.item [ Level.Item.HasTextCentered ] [
                            div [] [
                                Level.heading [ Modifiers [ Modifier.TextColor IsPrimary ] ] [
                                    str
                                    <| sprintf "Last quarter VAT (%s)" props.Totals.LastQuarterVat.TimeRange
                                ]

                                Level.title [] [
                                    str <| formatDecimal 2 props.Totals.LastQuarterVat.Value
                                    a [ Href "#"
                                        Title "Send Tax Announcement report to and open financial office form"
                                        OnClick
                                            (fun e ->
                                                e.preventDefault ()
                                                props.ReportRequest QuartalVatAnnounce) ] [
                                        Icon.icon [ Icon.Modifiers [ Modifier.TextColor IsInfo ] ] [
                                            Fa.i [ Fa.Solid.Building; Fa.Size Fa.FaExtraSmall ] []
                                        ]
                                    ]
                                    a [ Href "#"
                                        Title "Send Tax report to and open financial office form"
                                        OnClick
                                            (fun e ->
                                                e.preventDefault ()
                                                props.ReportRequest QuartalVat) ] [
                                        Icon.icon [ Icon.Modifiers [ Modifier.TextColor IsSuccess ] ] [
                                            Fa.i [ Fa.Solid.Building; Fa.Size Fa.FaExtraSmall ] []
                                        ]
                                    ]
                                ]
                            ]
                        ]
                        Level.item [ Level.Item.HasTextCentered ] [
                            div [] [
                                Level.heading [ Modifiers [ Modifier.TextColor IsPrimary ] ] [
                                    str <| sprintf "Last year (%s)" props.Totals.LastYear.TimeRange
                                ]

                                Level.title [] [
                                    str <| formatDecimal 2 props.Totals.LastYear.Value
                                    a [ Href "#"
                                        Title "Send Annual report to and open financial office form"
                                        OnClick
                                            (fun e ->
                                                e.preventDefault ()
                                                props.ReportRequest AnnualTax) ] [
                                        Icon.icon [ Icon.Modifiers [ Modifier.TextColor IsInfo ] ] [
                                            Fa.i [ Fa.Solid.Building; Fa.Size Fa.FaExtraSmall ] []
                                        ]
                                    ]
                                ]
                            ]
                        ]
                    ]
                    Level.level [] [
                        Level.item [ Level.Item.HasTextCentered ] [
                            div [] [
                                Level.heading [ Modifiers [ Modifier.TextColor IsPrimary ] ] [
                                    str <| sprintf "Current quarter (%s)" props.Totals.CurrentQuarter.TimeRange
                                ]

                                Level.title [] [
                                    str <| formatDecimal 2 props.Totals.CurrentQuarter.Value
                                ]
                            ]
                        ]
                        Level.item [ Level.Item.HasTextCentered ] [
                            div [] [
                                Level.heading [ Modifiers [ Modifier.TextColor IsPrimary ] ] [
                                    str
                                    <| sprintf "Current quarter VAT (%s)" props.Totals.CurrentQuarterVat.TimeRange
                                ]

                                Level.title [] [
                                    str <| formatDecimal 2 props.Totals.CurrentQuarterVat.Value
                                ]
                            ]
                        ]
                        Level.item [ Level.Item.HasTextCentered ] [
                            div [] [
                                Level.heading [ Modifiers [ Modifier.TextColor IsPrimary ] ] [
                                    str <| sprintf "Current year (%s)" props.Totals.CurrentYear.TimeRange
                                ]

                                Level.title [] [
                                    str <| formatDecimal 2 props.Totals.CurrentYear.Value
                                ]
                            ]
                        ]
                    ]
            ]
        )