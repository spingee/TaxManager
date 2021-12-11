REM temporary cmd till fake will have .net6 support

@echo off
call npm install
cd ./src/Client
start dotnet fable watch -o output -s --run webpack-dev-server --open --config ../../webpack.config.js
cd ../..
start dotnet watch run --project .\src\Server\Server.fsproj

