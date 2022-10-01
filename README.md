# Tax manager
My hobby project - evidence my earnings and taxes, invoice generator, reports etc.
Written in fullstack F# based on SAFE-Stack template

## How to run
run **dotnet tool install**\
run **CreateDockerImage.ps1**

+ ### Mac
    docker run --name=taxmanager -d --restart always -v C:/Users/janst/OneDrive/Dokumenty/Faktury:/app/db -v C:/Users/janst/Desktop:/app/output  -p 8086:8085 spingee/taxmanager:latest
+ ### Windows
    docker run --name=taxmanager -v /Users/janstrnad/OneDrive/Dokumenty/Faktury/:/app/db -v /Users/janstrnad/Desktop:/app/output  -p 8086:8085 -d spingee/taxmanager