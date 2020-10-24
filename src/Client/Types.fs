module Types

open Shared.Invoice
open System

type Validated<'t> = { Raw: string; Parsed: Option<'t> }

type CustomerInput ={
    IdNumber: Validated<uint>
    VatId: Validated<VatId>
    Name: Validated<string>
 }

type InvoiceInput =
    { ManDays: Validated<uint8>
      Rate: Validated<uint16>
      AccountingPeriod: DateTime
      Customer: CustomerInput }

let isValid input =
    let { ManDays = mandays; Rate = rate; CustomerId = customerId } = input
    match (mandays.Parsed, rate.Parsed, customerId.Parsed) with
    | Some _, Some _, Some _ -> true
    | _, _, _ -> false


type Model =
    { Input: InvoiceInput
      Title: string
      Result: Result<string, string> option
      IsLoading: bool
      CreatingCustomer: bool
      Customers: Customer list }

module Validated =
    let createEmpty (): Validated<_> = { Raw = ""; Parsed = None }
    let success raw value: Validated<_> = { Raw = raw; Parsed = Some value }
    let failure raw: Validated<_> = { Raw = raw; Parsed = None }
