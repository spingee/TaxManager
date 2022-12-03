#temp till Fake .net6 support
Set-Location $PSScriptRoot

npm install .

Push-Location ./src/Shared

dotnet build

Set-Location ../Client

Start-Process -NoNewWindow -FilePath dotnet -ArgumentList "fable watch -o output -s --run webpack-dev-server --mode development --open --config ../../webpack.config.js"


Pop-Location

dotnet watch run --project ./src/Server/Server.fsproj