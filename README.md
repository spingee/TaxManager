# Tax manager
My hobby project - evidence my earnings and taxes, invoice generator, reports etc.
Written in fullstack F# based on SAFE-Stack template

## How to run
run **dotnet tool install**
run **CreateDockerImage.ps1**

```docker run --name=taxmanager -v {path to dir with db file}:/app/db -v {path where to publish generated invoice documents}:/app/output  -p 8086:80 -d spingee/taxmanager```