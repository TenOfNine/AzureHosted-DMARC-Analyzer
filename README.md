# DMARC Analyzer

A self-hosted DMARC report analyzer, deployed as an Azure App Service. It ingests RUA/RUF reports
from Exchange Online shared mailboxes via Microsoft Graph, and goes beyond just showing what a
report claims: it independently re-checks each sending domain's **current** SPF record and DKIM
selectors against every reported source IP, to catch drift between what a report says and what's
actually authorized today.

The same codebase is redeployable per organization — Entra ID app registration, monitored
domains, shared mailboxes, and retention are all configured through an in-app setup wizard, not
baked into the deployment template.

## Architecture

```
src/
  DmarcAnalyzer.Core            Pure domain logic: RFC 7489 XML parsing, RFC 7208 SPF evaluation,
                                 DKIM selector checking, verified-sender classification.
                                 No Azure/EF/Graph dependency — fully unit-testable offline.
  DmarcAnalyzer.Infrastructure  EF Core (Azure SQL), Microsoft Graph client, Key Vault secret
                                 store, DNS resolution, background ingestion + retention jobs.
  DmarcAnalyzer.Web             ASP.NET Core Razor Pages: setup wizard, dashboard, settings.
tests/DmarcAnalyzer.Tests       xUnit tests against fakes (no network access needed).
tools/GrantSqlAccess            Small deploy-time utility granting the Web App's managed identity
                                 access to Azure SQL (Bicep can't create database users).
infra/                          Bicep: App Service, Azure SQL (AAD-only auth), Key Vault, App
                                 Insights — parameterized generically for reuse per deployment.
.github/workflows/               CI (build/test/format) and CD (OIDC → Bicep → migrate → deploy).
docs/                           Deployment guide and the one manual Exchange Online step.
```

See [`docs/deployment.md`](docs/deployment.md) for how to stand up a new instance, and
[`docs/exchange-application-access-policy.md`](docs/exchange-application-access-policy.md) for
scoping the Graph app registration to only the mailboxes it should read.

## How it works

1. A background job polls each configured shared mailbox via Graph delta query, extracts DMARC
   aggregate report attachments (zip/gzip/raw XML), and parses them (RFC 7489).
2. Each report is matched to a monitored domain by its own `policy_published/domain` — not by
   which mailbox it arrived in, since one mailbox commonly serves several domains and vice versa.
3. For every record (source IP), the domain's **live** SPF record is re-evaluated against that IP
   (full RFC 7208: recursive includes, redirect, CIDR matching, the 10-lookup limit) and compared
   against what the report itself claimed — a mismatch is flagged as a discrepancy. DKIM selectors
   are checked for continued DNS presence, flagging ones that have since been rotated or revoked.
4. The dashboard shows per-domain pass-rate trends and a per-record breakdown with a
   "verified sender" badge (DMARC-aligned pass, or an admin-curated allowlist entry).
5. Report data older than a configurable retention window is purged automatically.

## Development

```bash
dotnet build
dotnet test
```

Running the app locally requires a reachable SQL Server and a real Azure Key Vault your local
identity has access to — see [`docs/deployment.md`](docs/deployment.md#local-development).
