FROM mcr.microsoft.com/dotnet/aspnet:5.0 AS runtime
COPY /deploy .
WORKDIR /
EXPOSE 8085
ENTRYPOINT ["dotnet", "Server.dll"]
