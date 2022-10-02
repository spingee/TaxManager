FROM mcr.microsoft.com/dotnet/sdk:6.0 as build

#WORKDIR /workspace
#COPY . .
#ENTRYPOINT "/bin/sh"

# Install node
RUN curl -sL https://deb.nodesource.com/setup_16.x | bash
RUN apt-get update && apt-get install -y nodejs

WORKDIR /workspace
COPY [".config/", "."]
RUN dotnet tool restore


COPY ["package.json", "."]
COPY ["package-lock.json", "."]
RUN npm install

COPY ["global.json", "."]
COPY ["paket.lock", "."]
COPY ["paket.dependencies", "."]
COPY ["src/Server/Server.fsproj", "src/Server/"]
COPY ["src/Server/paket.references", "src/Server/"]
COPY ["src/Shared/Shared.fsproj", "src/Shared/"]
#COPY ["src/Shared/paket.references", "Shared/"]
COPY ["src/Client/Client.fsproj", "src/Client/"]
COPY ["src/Client/paket.references", "src/Client/"]

#
#RUN dotnet restore src/Shared/Shared.fsproj
#RUN dotnet restore src/Server/Server.fsproj
#RUN dotnet restore src/Client/Client.fsproj

RUN dotnet paket restore --project src/Client/Client.fsproj
RUN dotnet paket restore --project src/Server/Server.fsproj

COPY . .

RUN dotnet fable src/Client -o src/Client/output -s --run npm run build
RUN cd src/Server && dotnet publish -c release -o ../../deploy


FROM mcr.microsoft.com/dotnet/aspnet:6.0 AS final
#install font for document converter
RUN apt-get update \
&& apt-get install -y --allow-unauthenticated libc6-dev \
libgdiplus \
libx11-dev \
&& rm -rf /var/lib/apt/lists/*

# Disable the invariant mode (set in base image)
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false

COPY --from=build /workspace/deploy /app
WORKDIR /app
EXPOSE 8085
ENV TZ=Europe/Prague
ENTRYPOINT ["dotnet", "Server.dll"]