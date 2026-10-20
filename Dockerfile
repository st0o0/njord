# syntax=docker/dockerfile:1@sha256:4edf897a3ffa55b89f906fc8cc78afdb3f1834cc9c7083565e611a8a7d5fe99e

FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0-noble@sha256:0d99ebf1ccad15037b5c98c9fe8979d6b8dbac23812feae2277a0b277b5c5ffa AS build
ARG TARGETARCH
ARG VERSION=0.0.0-dev
WORKDIR /src

COPY protos/ /protos/
COPY src/global.json src/Directory.Build.props src/Directory.Packages.props src/Njord.slnx ./
COPY src/Njord.Forecast/Njord.Forecast.csproj Njord.Forecast/
COPY src/Njord.Budget/Njord.Budget.csproj Njord.Budget/
COPY src/Njord.Persistence/Njord.Persistence.csproj Njord.Persistence/
COPY src/Njord.Messages/Njord.Messages.csproj Njord.Messages/
COPY src/Njord.Core/Njord.Core.csproj Njord.Core/
COPY src/Njord.OpenMeteo/Njord.OpenMeteo.csproj Njord.OpenMeteo/
COPY src/Njord.Pipeline/Njord.Pipeline.csproj Njord.Pipeline/
COPY src/Njord.Grpc/Njord.Grpc.csproj Njord.Grpc/
COPY src/Njord.Mqtt/Njord.Mqtt.csproj Njord.Mqtt/
COPY src/Njord.Weather/Njord.Weather.csproj Njord.Weather/
COPY src/Njord.AirQuality/Njord.AirQuality.csproj Njord.AirQuality/
COPY src/Njord.Flood/Njord.Flood.csproj Njord.Flood/
COPY src/Njord.Marine/Njord.Marine.csproj Njord.Marine/
COPY src/Njord/Njord.csproj Njord/
RUN dotnet restore Njord/Njord.csproj -a ${TARGETARCH}

COPY src/Njord.Forecast/ Njord.Forecast/
COPY src/Njord.Budget/ Njord.Budget/
COPY src/Njord.Persistence/ Njord.Persistence/
COPY src/Njord.Messages/ Njord.Messages/
COPY src/Njord.Core/ Njord.Core/
COPY src/Njord.OpenMeteo/ Njord.OpenMeteo/
COPY src/Njord.Pipeline/ Njord.Pipeline/
COPY src/Njord.Grpc/ Njord.Grpc/
COPY src/Njord.Mqtt/ Njord.Mqtt/
COPY src/Njord.Weather/ Njord.Weather/
COPY src/Njord.AirQuality/ Njord.AirQuality/
COPY src/Njord.Flood/ Njord.Flood/
COPY src/Njord.Marine/ Njord.Marine/
COPY src/Njord/ Njord/
RUN dotnet publish Njord/Njord.csproj -c Release -a ${TARGETARCH} -o /app /p:Version=${VERSION} /p:ContinuousIntegrationBuild=true

FROM mcr.microsoft.com/dotnet/runtime-deps:10.0-noble@sha256:da5846024d0008b48259b53a8953d39381fe5b7ec99ef1449ba4ba927f201490 AS prep
RUN mkdir -p /data && chown 1654:1654 /data

FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled-extra@sha256:3ed6558056b54d5c9dfefb7ff473728e022ff2bf1d976cbf8ee07b05ec594374
ARG VERSION=0.0.0-dev
LABEL org.opencontainers.image.title="njord" \
      org.opencontainers.image.description="Multi-domain weather intelligence for Home Assistant" \
      org.opencontainers.image.version="${VERSION}" \
      org.opencontainers.image.source="https://github.com/st0o0/njord" \
      org.opencontainers.image.documentation="https://github.com/st0o0/njord#readme"
WORKDIR /app
COPY --from=build --chown=$APP_UID /app .
COPY --from=prep --chown=$APP_UID /data /app/data
VOLUME /app/data
EXPOSE 8080 8081
ENTRYPOINT ["dotnet", "Njord.dll"]
