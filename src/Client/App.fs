module App

open Elmish
open Elmish.React
open Fable.Core.JsInterop

importAll "bulma/css/bulma.min.css"
importAll "flatpickr/dist/themes/dark.css"
importAll "flatpickr/dist/plugins/monthSelect/style.css"


#if DEBUG
open Elmish.Debug
open Elmish.HMR
#endif



Program.mkProgram Index.init Index.update Index.view
#if DEBUG
|> Program.withConsoleTrace
#endif
|> Program.withReactSynchronous "elmish-app"
#if DEBUG
|> Program.withDebugger
#endif
|> Program.run
