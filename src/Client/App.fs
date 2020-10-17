module App

open Elmish
open Elmish.React
open Fable.Core.JsInterop

importAll "flatpickr/dist/themes/dark.css"
importAll "flatpickr/dist/plugins/monthSelect/style.css"


#if DEBUG
open Elmish.Debug
open Elmish.HMR
#endif



Program.mkProgram State.init State.update Index.view
#if DEBUG
|> Program.withConsoleTrace
#endif
|> Program.withReactSynchronous "elmish-app"
#if DEBUG
|> Program.withDebugger
#endif
|> Program.run
