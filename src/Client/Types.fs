module Types
open Shared
open System

type Validated<'t> =
    {  Raw : string
       Parsed : Option<'t> }

type InvoiceInput = {
    ManDays: Validated<uint8>
    Rate: Validated<uint16>
    AccountingPeriod: DateTime
}

let isValid input =
    let {InvoiceInput.ManDays = mandays; Rate = rate} = input
    match (mandays.Parsed,rate.Parsed) with
    |Some _,Some _ -> true
    |_,_ -> false


type Model =
    { Input: InvoiceInput
      Title: string
      Result: Result<string, string> option }

module Validated =
    let createEmpty() : Validated<_> =
        { Raw = ""; Parsed = None }

    let success raw value : Validated<_> =
        { Raw = raw; Parsed = Some value }

    let failure raw : Validated<_> =
        { Raw = raw; Parsed = None }


