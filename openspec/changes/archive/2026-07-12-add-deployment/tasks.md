## 1. Host switch to WebApplication

- [x] 1.1 Change `src/Njord/Njord.csproj` SDK from `Microsoft.NET.Sdk` to `Microsoft.NET.Sdk.Web`
- [x] 1.2 Update `src/Njord/Program.cs`: replace `Host.CreateApplicationBuilder` with `WebApplication.CreateBuilder`, add `builder.Services.AddHealthChecks()`, build with `builder.Build()`, call `app.MapHealthChecks("/healthz")` before `app.RunAsync()`
- [x] 1.3 Add `ASPNETCORE_URLS=http://+:8080` as `ENV` in `Dockerfile` (default health port)
- [x] 1.4 Change `Dockerfile` runtime base image from `mcr.microsoft.com/dotnet/runtime:10.0-noble-chiseled` to `mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled`
- [x] 1.5 Add `EXPOSE 8080` to `Dockerfile`

## 2. Health endpoint tests

- [x] 2.1 Add `Microsoft.AspNetCore.Mvc.Testing` to `src/Njord.Tests/Njord.Tests.csproj` and `src/Directory.Packages.props`
- [x] 2.2 Create `src/Njord.Tests/Health/HealthEndpointSpec.cs` — sealed class, `[Fact(Timeout = 5000)]`, BDD-style names: test that `GET /healthz` returns 200 with body `Healthy`, test that `GET /other` returns 404

## 3. Docker Compose

- [x] 3.1 Create `docker-compose.yml` at repo root: single `njord` service, `image: ghcr.io/st0o0/njord:latest`, `restart: unless-stopped`, environment variables for `Njord__Mqtt__Host`, `Njord__Mqtt__Username`, `Njord__Mqtt__Password`, `Njord__Locations__0__Name`, `Njord__Locations__0__Latitude`, `Njord__Locations__0__Longitude`, port mapping for 8080

## 4. Fix outdated descriptions

- [x] 4.1 Update `Dockerfile` label `org.opencontainers.image.description` from "Kachelmann" to "Open-Meteo weather API → MQTT bridge for Home Assistant"
- [x] 4.2 Update `.github/workflows/release.yml` index annotation description from "Kachelmann" to "Open-Meteo weather API → MQTT bridge for Home Assistant"
- [x] 4.3 Update `.github/workflows/dev-build.yml` index annotation description similarly

## 5. CI smoke test

- [x] 5.1 Investigate the current smoke test in `.github/workflows/ci.yml` — determine why it expects stdout `"njord"` from `docker run --rm njord:ci` and whether it ever passed
- [x] 5.2 Fix or replace the smoke test so it works with the WebApplication host (e.g. start the container with a valid config via env vars, verify `/healthz` responds, or add a `--version` entrypoint that prints the version and exits)

## 6. Validation

- [x] 6.1 Run `dotnet build Njord.slnx` from `src/` — must compile cleanly
- [x] 6.2 Run `dotnet run --project Njord.Tests/Njord.Tests.csproj` from `src/` — all tests green including new health endpoint specs
- [x] 6.3 Verify existing tests still pass (no regressions from the host switch)
