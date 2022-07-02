#temp till Fake .net6 support
Set-Location $PSScriptRoot

$version = dotnet minver;
docker build -t spingee/taxmanager:$version  -t spingee/taxmanager:latest .