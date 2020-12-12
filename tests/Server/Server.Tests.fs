module Server.Tests


open Expecto
open Server

let server = testList "Server" [
    testCase "Adding valid Todo" <| fun _ ->
        let invoices =
            async {
                return! Server.invoiceApi.getInvoices()
            } |> Async.RunSynchronously
        Expect.isOk(invoices) "Should be ok"

]

let all =
    testList "All"
        [
            server
        ]

[<EntryPoint>]
let main _ = runTests defaultConfig all