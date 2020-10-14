// Learn more about F# at http://fsharp.org

open System
open InvoiceExcel
open Shared
open GemBox.Spreadsheet

[<EntryPoint>]
let main argv =
    SpreadsheetInfo.SetLicense("FREE-LIMITED-KEY")
    createExcelAndPdfInvoice "lol"{ AccountingPeriod = {Month = uint8 5;Year = uint16 2020 }; ManDays = uint8 15 ;  Rate = uint16 6500} |> ignore
    0
