# syntax=docker/dockerfile:1

# CI cross-compiles via `dotnet publish -r <rid>` and passes the
# published output as build context. No SDK needed here.

FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled
LABEL org.opencontainers.image.title="njord" \
      org.opencontainers.image.description="Multi-model weather intelligence for Home Assistant" \
      org.opencontainers.image.source="https://github.com/st0o0/njord" \
      org.opencontainers.image.documentation="https://github.com/st0o0/njord#readme"
WORKDIR /app
COPY --chown=$APP_UID . .
RUN mkdir -p /app/data
VOLUME /app/data
EXPOSE 8080 8081
ENTRYPOINT ["dotnet", "Njord.dll"]
