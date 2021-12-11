#temp till Fake .net6 support
Set-Location $PSScriptRoot

Remove-Item ./deploy/* -Recurse -Force

npm install .

Push-Location ./src/Server

dotnet publish -c Release -o "../../deploy"

Set-Location ../Client

dotnet fable -o output -s --run webpack --config ../../webpack.config.js

Pop-Location

$version = dotnet minver;
docker build -t spingee/taxmanager:$version  -t spingee/taxmanager:latest .