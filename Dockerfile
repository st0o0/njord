# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG VERSION=0.0.0-dev
WORKDIR /src
# Restore first so the NuGet layer caches independently of source changes
COPY src/global.json src/Directory.Build.props src/Directory.Packages.props ./
COPY src/Njord/Njord.csproj Njord/
RUN dotnet restore Njord/Njord.csproj
COPY src/Njord/ Njord/
RUN dotnet publish Njord/Njord.csproj -c Release -o /app --no-restore -p:Version=${VERSION} \
    && mkdir -p /app/data

FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled AS runtime
LABEL org.opencontainers.image.title="njord" \
      org.opencontainers.image.description="Open-Meteo weather API to MQTT bridge for Home Assistant" \
      org.opencontainers.image.source="https://github.com/st0o0/njord" \
      org.opencontainers.image.documentation="https://github.com/st0o0/njord#readme"
WORKDIR /app
VOLUME /app/data
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
COPY --from=build /app .
ENTRYPOINT ["dotnet", "Njord.dll"]
