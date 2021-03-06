#r "paket: groupref build //"
#load "./.fake/build.fsx/intellisense.fsx"
#r "netstandard"

open Fake.Core
open Fake.DotNet
open Fake.IO
open Farmer
open Farmer.Builders


Target.initEnvironment ()

let sharedPath = Path.getFullName "./src/Shared"
let serverPath = Path.getFullName "./src/Server"
let clientPath = Path.getFullName "./src/Client"
let deployDir = Path.getFullName "./deploy"
let sharedTestsPath = Path.getFullName "./tests/Shared"
let serverTestsPath = Path.getFullName "./tests/Server"

let dockerImageName =
    Environment.environVarOrDefault "DockerImageName" "taxmanager"

let dockerUser =
    Environment.environVarOrDefault "DockerUser" "spingee"
// let dockerPassword = Environment.environVarOrDefault "DockerPassword"
// let dockerLoginServer = Environment.environVarOrDefault "DockerLoginServer"


let npm args workingDir =
    let npmPath =
        match ProcessUtils.tryFindFileOnPath "npm" with
        | Some path -> path
        | None ->
            "npm was not found in path. Please install it and make sure it's available from your path. "
            + "See https://safe-stack.github.io/docs/quickstart/#install-pre-requisites for more info"
            |> failwith

    let arguments =
        args |> String.split ' ' |> Arguments.OfArgs

    Command.RawCommand(npmPath, arguments)
    |> CreateProcess.fromCommand
    |> CreateProcess.withWorkingDirectory workingDir
    |> CreateProcess.ensureExitCode
    |> Proc.run
    |> ignore

let dotnet cmd workingDir =
    let result =
        DotNet.exec (DotNet.Options.withWorkingDirectory workingDir) cmd ""

    if result.ExitCode <> 0
    then failwithf "'dotnet %s' failed in %s" cmd workingDir

Target.create "Clean" (fun _ -> Shell.cleanDir deployDir)

Target.create "InstallClient" (fun _ -> npm "install" ".")

Target.create "Bundle" (fun _ ->
    dotnet (sprintf "publish -c Release -o \"%s\"" deployDir) serverPath
    dotnet "fable --run webpack" clientPath)

Target.create "Azure" (fun _ ->
    let web =
        webApp {
            name "Api"
            zip_deploy "deploy"
        }

    let deployment =
        arm {
            location Location.WestEurope
            add_resource web
        }

    deployment
    |> Deploy.execute "Api" Deploy.NoParameters
    |> ignore)

Target.create "Run" (fun _ ->
    //can be done with webpack: webpack-dev-server --open
    let openBrowser =
        async {
            System.Threading.Thread.Sleep(5000)
            Proc.run
            <| CreateProcess.fromRawCommandLine "cmd" "/C start http://localhost:8080/"
            |> ignore
        }

    dotnet "build" sharedPath
    [ async { dotnet "watch run" serverPath }
      async { dotnet "fable watch --sourceMaps --run webpack-dev-server --open" clientPath }
      //openBrowser
      ]
    |> Async.Parallel
    |> Async.RunSynchronously
    |> ignore)

Target.create "CreateDockerImage" (fun _ ->
    let minVer =
        CreateProcess.fromRawCommand "dotnet" ["minver"]
        |> CreateProcess.redirectOutput
        |> CreateProcess.mapResult (fun r-> r.Output.Trim())
        |> Proc.run
    // let minVer =
    //     DotNet.exec id "minver" ""
    let result =
        CreateProcess.fromRawCommandLine "docker"
        <| $"build -t {dockerUser}/{dockerImageName}:{minVer.Result} -t {dockerUser}/{dockerImageName}:latest ."
        |> Proc.run

    if result.ExitCode <> 0 then failwith "Docker build failed")

Target.create "RunTests" (fun _ ->
    dotnet "build" sharedTestsPath
    [ async { dotnet "watch run" serverTestsPath }
      async { npm "run test:live" "." } ]
    |> Async.Parallel
    |> Async.RunSynchronously
    |> ignore)

open Fake.Core.TargetOperators

"Clean"
==> "InstallClient"
==> "Bundle"
==> "Azure"

"Clean" ==> "InstallClient" ==> "Run"

"Clean" ==> "InstallClient" ==> "RunTests"

"Clean"
==> "InstallClient" ==> "Bundle" ==> "CreateDockerImage"

Target.runOrDefaultWithArguments "Bundle"
