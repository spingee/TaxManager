FROM mcr.microsoft.com/dotnet/aspnet:6.0 AS runtime

#install font for document converter
#RUN apt-get update; apt-get install -y fontconfig fonts-liberation
RUN sed -i'.bak' 's/$/ contrib/' /etc/apt/sources.list
RUN apt-get update; apt-get install -y ttf-mscorefonts-installer fontconfig
RUN fc-cache -f -v
#fixes some error with System.Drawing used by document converter
RUN apt-get install libgdiplus -y

COPY /deploy .
WORKDIR /
EXPOSE 8085
ENV TZ=Europe/Prague
ENTRYPOINT ["dotnet", "Server.dll"]
