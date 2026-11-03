# Security Policy

## Supported versions

The latest released image (`ghcr.io/st0o0/njord:latest`) receives security
updates. Older tags are not maintained, so pin to a `MAJOR.MINOR` tag and update
regularly.

## Reporting a vulnerability

Please report security issues **privately** via GitHub's
[private vulnerability reporting](https://github.com/st0o0/njord/security/advisories/new)
(Security > Advisories > *Report a vulnerability*). Do **not** open a public
issue for security problems.

You can expect an initial response within a few days. Once a fix is available it
is released and the advisory is published.

## Scope

njord is a .NET weather intelligence service that polls the Open-Meteo API and
serves data via gRPC. It stores no credentials beyond its own configuration.
Vulnerabilities in .NET runtime or NuGet dependencies should be reported
upstream; when a fix is available the image is rebuilt against it.
