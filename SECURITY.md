# Security Policy

## Supported Versions

| Version | Supported          |
| ------- | ------------------ |
| 0.x     | :white_check_mark: |

Only the latest release on `main` receives security fixes.

## Reporting a Vulnerability

**Do not open a public issue for security vulnerabilities.**

Instead, please use [GitHub Security Advisories](https://github.com/st0o0/njord/security/advisories/new) to report the vulnerability privately. This ensures the report stays confidential until a fix is available.

If you prefer email, contact the maintainer at **claude@schloots.net** with:

- A description of the vulnerability
- Steps to reproduce or a proof of concept
- The affected version(s)

You can expect an initial response within **72 hours**. Once confirmed, a fix will be prioritized and released as soon as possible.

## Security Measures

This project employs the following automated security practices:

- **Dependency scanning** — Weekly Trivy scans on dependencies and Docker image (see `.github/workflows/security.yml`)
- **Dependabot** — Automated dependency updates for NuGet and GitHub Actions
- **Container hardening** — Non-root runtime user, minimal base image (`mcr.microsoft.com/dotnet/aspnet`)
- **No secrets in code** — Configuration via environment variables; MQTT credentials are never committed

## Scope

njord is a self-hosted service running on the user's own infrastructure. The security scope covers:

- **In scope**: code vulnerabilities, dependency issues, container security, MQTT credential handling, gRPC endpoint exposure
- **Out of scope**: Home Assistant configuration, Mosquitto broker hardening, host OS security, network-level concerns

## Disclosure Policy

- Confirmed vulnerabilities will be fixed in a patch release
- A GitHub Security Advisory will be published after the fix is available
- Credit will be given to the reporter (unless they prefer to remain anonymous)
