# syntax=docker/dockerfile:1@sha256:4edf897a3ffa55b89f906fc8cc78afdb3f1834cc9c7083565e611a8a7d5fe99e

FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0-noble@sha256:212fe4cb74627d5adf9817a4088906ae3edff1bd853bb27565d4b46731efe61e AS build
ARG TARGETARCH
ARG VERSION=0.0.0-dev
WORKDIR /src

COPY protos/ /protos/
COPY src/global.json src/Directory.Build.props src/Directory.Packages.props src/Njord.slnx ./
COPY src/Njord.Budget/Njord.Budget.csproj Njord.Budget/
COPY src/Njord.Persistence/Njord.Persistence.csproj Njord.Persistence/
COPY src/Njord.Messages/Njord.Messages.csproj Njord.Messages/
COPY src/Njord.Core/Njord.Core.csproj Njord.Core/
COPY src/Njord.OpenMeteo/Njord.OpenMeteo.csproj Njord.OpenMeteo/
COPY src/Njord.Pipeline/Njord.Pipeline.csproj Njord.Pipeline/
COPY src/Njord.Grpc/Njord.Grpc.csproj Njord.Grpc/
COPY src/Njord.Polling/Njord.Polling.csproj Njord.Polling/
COPY src/Njord.Features/Njord.Features.csproj Njord.Features/
COPY src/Njord/Njord.csproj Njord/
RUN dotnet restore Njord/Njord.csproj -a ${TARGETARCH}

COPY src/Njord.Budget/ Njord.Budget/
COPY src/Njord.Persistence/ Njord.Persistence/
COPY src/Njord.Messages/ Njord.Messages/
COPY src/Njord.Core/ Njord.Core/
COPY src/Njord.OpenMeteo/ Njord.OpenMeteo/
COPY src/Njord.Pipeline/ Njord.Pipeline/
COPY src/Njord.Grpc/ Njord.Grpc/
COPY src/Njord.Polling/ Njord.Polling/
COPY src/Njord.Features/ Njord.Features/
COPY src/Njord/ Njord/
RUN dotnet publish Njord/Njord.csproj -c Release -a ${TARGETARCH} -o /app /p:Version=${VERSION} /p:ContinuousIntegrationBuild=true

FROM mcr.microsoft.com/dotnet/runtime-deps:10.0-noble@sha256:f87d6824cd56fa4ad117d06461e4eb0058f46a159872dbcc4a836a72efdd1bec AS prep
RUN mkdir -p /data && chown 1654:1654 /data

FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled-extra@sha256:a9298ccc39c6258ae71228479e2f3f8d2ceb6639a9927262a4b0ff7d319a0e7e
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
