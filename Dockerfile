FROM mcr.microsoft.com/dotnet/aspnet:6.0-alpine AS runtime

#install font for document converter
RUN apk --no-cache add msttcorefonts-installer fontconfig && \
    update-ms-fonts && \
    fc-cache -f && \
    #timezone
    apk add --no-cache tzdata && \
    #.net globalization runtime
    apk add --no-cache icu-libs &&\
    #fixes some error with System.Drawing used by document converter
    apk add libgdiplus --no-cache --repository http://dl-3.alpinelinux.org/alpine/edge/testing/ --allow-untrusted

# Disable the invariant mode (set in base image)
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false

COPY /deploy .
WORKDIR /
EXPOSE 8085
ENV TZ=Europe/Prague
ENTRYPOINT ["dotnet", "Server.dll"]
