# Deploying to Azure

This is a reference architecture for running PhysioTrac on Azure using
services Microsoft offers under its Business Associate Agreement (BAA) for
covered entities/business associates. **Confirm the current list of
BAA-eligible services directly with Microsoft (the Microsoft Trust Center's
HIPAA documentation) before you build on this** -- which services are
covered, and under what configuration, changes over time and is a legal
question, not a technical one this document can answer for you. Signing
the BAA itself is also on you; Azure doesn't do it automatically just
because you provisioned a covered service. See
[HIPAA_CHECKLIST.md](HIPAA_CHECKLIST.md) for the full list of what using
BAA-eligible services does *not*, by itself, get you.

## Reference architecture

| Concern | Service |
|---|---|
| `PhysioTrac.Api` / `PhysioTrac.Web` containers | Azure Container Apps (or App Service for Containers) |
| Database | Azure SQL Database |
| Secrets (connection strings, `Seed__DemoPassword`, etc.) | Azure Key Vault |
| TLS termination / public entry point | Azure Front Door or Application Gateway |
| Logging | Azure Monitor / Log Analytics (Serilog already writes structured JSON -- see [Logging](../README.md#logging)) |
| Static frontend (`frontend/`) | Azure Static Web Apps, or the same Container Apps environment |
| Container images | Azure Container Registry |

## Provisioning outline

1. **Resource group.** One per environment (`physiotrac-prod`,
   `physiotrac-staging`), so blast radius and cost are scoped and you can
   tear down a whole environment cleanly.

2. **Azure SQL Database.** Provision at a tier with automated backups and
   the point-in-time retention window you need (see
   [BACKUP_RESTORE.md](BACKUP_RESTORE.md)). Enable
   **Microsoft Entra-only authentication** if you want to avoid a
   standing SQL `sa`/app-login password entirely; otherwise store the
   connection string's password in Key Vault, never in App
   Settings/environment variables directly. Enable
   **Transparent Data Encryption** (on by default for new databases) for
   encryption at rest, and require **TLS 1.2+** for connections (default).

3. **Azure Container Registry.** Push the images the existing Dockerfiles
   already build (`src/PhysioTrac.Api/Dockerfile`,
   `src/PhysioTrac.Web/Dockerfile`, `frontend/Dockerfile`):

   ```bash
   az acr build --registry <your-acr-name> --image physiotrac-api:latest -f src/PhysioTrac.Api/Dockerfile .
   az acr build --registry <your-acr-name> --image physiotrac-web:latest -f src/PhysioTrac.Web/Dockerfile .
   az acr build --registry <your-acr-name> --image physiotrac-frontend:latest -f frontend/Dockerfile frontend
   ```

4. **Key Vault.** Store `ConnectionStrings__Default` and
   `Seed__DemoPassword` (leave this one unset/empty in real environments --
   see [Demo data](../README.md#demo-data), it only seeds under
   `ASPNETCORE_ENVIRONMENT=Development`) as secrets. Grant the Container
   Apps' managed identity `get`/`list` access via an access policy or RBAC
   role -- never a shared Key Vault access key in an environment variable.

5. **Container Apps environment.** One environment holding both the `api`
   and `web` container apps (they were already built to talk to each other
   this way in Docker Compose). Configure:
   - `ASPNETCORE_ENVIRONMENT=Production`
   - `ConnectionStrings__Default` sourced from the Key Vault secret
     reference (Container Apps supports this natively), not a plain env var
   - Ingress: `web`/`api` internal-only if Front Door is the sole public
     entry point; external ingress with the platform's managed TLS
     certificate otherwise
   - Min replicas >= 1 (avoid a cold-start on every request for a clinical
     app) with autoscale rules on CPU/HTTP concurrency

6. **Front Door / Application Gateway.** Public HTTPS entry point, routing
   to the Web app and the API. This is also where you'd add a WAF policy.

7. **Log Analytics.** Point Container Apps' diagnostic settings at a
   workspace. Serilog's rolling file sink (`logs/physiotrac-*.log` --
   see [Program.cs](../src/PhysioTrac.Api/Program.cs)) still writes inside
   the container; Container Apps' built-in log collection captures stdout,
   so also route Serilog to the console sink in production, or ship the
   file sink's output via an agent, so logs survive container restarts.

## What this guide does not cover

DNS/domain setup, CI/CD wiring specifics beyond what
[.github/workflows/ci.yml](../.github/workflows/ci.yml) already builds and
tests, cost management, and multi-region/DR topology -- add these
deliberately for your own environment rather than copying defaults here.
