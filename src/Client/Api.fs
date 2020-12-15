module Api

open Fable.Remoting.Client
open Shared
open Shared.Invoice

let invoiceApi =
    Remoting.createApi ()
    |> Remoting.withRouteBuilder Route.builder
    |> Remoting.buildProxy<IInvoiceApi>