// Learn more about F# at http://fsharp.org

open System
open InvoiceExcel
open Shared

[<EntryPoint>]
let main argv =
    createExcelInvoice "lol.xlsx"{ AccountingPeriod = {Month = uint8 5;Year = uint16 2020 }; ManDays = uint8 15 ;  Rate = uint16 6500} |> ignore
    0 
