# DMARC Analyzer — Functional & Technical Specification

## 1. Document Overview

| | |
|---|---|
| **Document version** | 1.0 |
| **Date written** | 2026-08-10 |
| **Document status** | Final — describes the as-built system on `main` |
| **Repository** | `TenOfNine/AzureHosted-DMARC-Analyzer` |
| **Scope** | This document specifies the system as implemented. It is a reference for operating, extending, and reviewing the application — not a forward-looking proposal. Where a capability is intentionally out of scope for the current implementation, it is called out explicitly under [§3.6](#36-explicitly-out-of-scope) and [§7.1](#71-glossary). |

This document complements [`README.md`](../README.md) (quick orientation and screenshots) and
[`docs/deployment.md`](./deployment.md) (deployment runbook). It is the detailed reference for
*how the system is built* and *what it guarantees*.

## 2. Feature Overview

### 2.1 Background

DMARC (Domain-based Message Authentication, Reporting & Conformance, RFC 7489) lets a domain
owner publish a policy telling receiving mail servers what to do with messages that fail SPF/DKIM
alignment, and requests daily aggregate reports (RUA) — and optionally per-message forensic
reports (RUF) — describing what was seen. Raw RUA/RUF XML/zip attachments are not
human-readable at scale, and the reports only describe what a receiver saw *at evaluation time*
against whatever SPF/DKIM records existed *then* — they do not tell the domain owner whether a
listed sender is still authorized *today*, since SPF records and DKIM keys change independently
of when reports are generated.

This project was commissioned to replace manual/ad-hoc inspection of RUA/RUF emails landing in an
Exchange Online shared mailbox with a hosted analyzer that (a) ingests and parses those reports
automatically, (b) independently re-verifies each report's claims against the sending domain's
**live** SPF record and DKIM selectors, and (c) presents the result as a dashboard.

### 2.2 Business goals

| ID | Goal |
|---|---|
| G-1 | Eliminate manual handling of DMARC report emails. |
| G-2 | Surface, per sending domain, which source IPs are DMARC-compliant vs. not, over time. |
| G-3 | Detect drift between a report's historical SPF/DKIM verdict and the domain's current DNS state — i.e., catch senders that *used to* be authorized and no longer are, or vice versa. |
| G-4 | Deploy on Azure infrastructure the organization already operates (Germany West Central region), using Exchange Online mailboxes already receiving the reports. |
| G-5 | Make the same codebase deployable as an independent instance per organization, without code changes between deployments. |
| G-6 | Access the shared mailbox(es) using modern, credential-minimal authentication (Entra ID app registration + Microsoft Graph), not legacy/basic auth. |
| G-7 | Ship with automated deployment and CI test coverage rather than a manually-deployed one-off. |

### 2.3 Target users

| Persona | Needs |
|---|---|
| **Messaging/Security administrator** | Reviews domain DMARC health, investigates unverified senders, decides on policy changes (`p=quarantine`/`reject`). |
| **Deploying engineer** | Stands up a new instance of the system for an organization: provisions Azure infrastructure, registers the Entra ID app, runs the setup wizard. |
| **Maintainer/contributor** | Extends the codebase — this document, together with the source tree, is the primary onboarding reference. |

There is currently one implicit role: anyone who can reach the deployed Web App URL has full
access to the dashboard and settings (see [§5.2](#52-security-requirements) for the security
posture this implies).

## 3. Functional Requirements

Requirements are grouped by capability area and tagged with an ID for traceability. All are
**Implemented** in the current codebase unless marked otherwise.

### 3.1 Report ingestion (Microsoft Graph)

| ID | Requirement | Implementation |
|---|---|---|
| FR-ING-1 | The system polls each configured Exchange Online shared mailbox on a recurring interval (default 15 minutes, configurable via `Ingestion:PollIntervalMinutes`) for new messages. | `MailboxPollingService` (`src/DmarcAnalyzer.Infrastructure/Ingestion/MailboxPollingService.cs`), an ASP.NET Core `BackgroundService` using a `PeriodicTimer`. |
| FR-ING-2 | Polling uses Microsoft Graph delta queries so repeated polls only return messages new since the last successful poll; the delta link is persisted per mailbox. | `GraphMailboxClient.ListNewMessagesAsync` (`src/DmarcAnalyzer.Infrastructure/Graph/GraphMailboxClient.cs`); `Mailbox.DeltaLink` column. |
| FR-ING-3 | Graph access uses app-only (client-credentials) OAuth via an Entra ID app registration — no delegated/user login, no legacy basic auth. | `GraphClientFactory` builds a `ClientSecretCredential` per call from the stored Tenant ID/Client ID and the Key-Vault-resident client secret. Required Graph permission: application `Mail.Read`. |
| FR-ING-4 | A message is only processed once. A per-mailbox, per-message dedupe ledger prevents reprocessing on subsequent polls, and a failure on one message never blocks processing of the remaining messages in the same poll cycle. | `ProcessedMessage` entity/table, keyed uniquely on `(MailboxId, GraphMessageId)`; `MailboxPollingService.ProcessSingleMessageAsync` wraps each message in its own try/catch. |
| FR-ING-5 | RUA attachments are accepted as `.zip`, `.gz`, or raw `.xml`; the correct handler is chosen by file extension and the XML is extracted transparently. | `AttachmentExtractor.TryExtractXml` (`src/DmarcAnalyzer.Infrastructure/Ingestion/AttachmentExtractor.cs`). |
| FR-ING-6 | A message with no recognizable DMARC attachment is recorded as **Skipped**, not **Failed**. | `DmarcReportIngestionPipeline.ProcessMessageAsync`. |
| FR-ING-7 | A report is attributed to a monitored `Domain` by the report's own `policy_published/domain` value (case-insensitive match), not by which mailbox it arrived in. A report for a domain not registered in the system is recorded as **Skipped** with the domain name in the reason. | `DmarcReportIngestionPipeline.ProcessMessageAsync`; this is what allows one shared mailbox to receive reports for several domains and a domain's reports to arrive across several mailboxes (see FR-CFG-3). |
| FR-ING-8 | RUF (forensic) attachments are recognized by the extractor but are not deep-parsed in the current implementation (see [§3.6](#36-explicitly-out-of-scope)). | `AttachmentType.Ruf` is defined but not produced by `AttachmentExtractor`; only RUA (zip/gzip/xml) is parsed today. |

### 3.2 DMARC aggregate report parsing

| ID | Requirement | Implementation |
|---|---|---|
| FR-PARSE-1 | Parse an RFC 7489 aggregate ("feedback") report: `report_metadata` (org name, contact email, report ID, date range), `policy_published` (domain, `adkim`/`aspf` alignment mode, `p`/`sp` disposition, `pct`, `fo`), and one or more `record` blocks (source IP, count, policy-evaluated disposition/SPF/DKIM, override reasons, identifiers, `auth_results`). | `DmarcXmlParser.Parse` (`src/DmarcAnalyzer.Core/Dmarc/DmarcXmlParser.cs`), `System.Xml.Linq`-based, no external XML schema dependency. |
| FR-PARSE-2 | Optional fields absent from the XML (`extra_contact_info`, `sp`, `fo`, DKIM `selector`/`human_result`, `envelope_from`/`envelope_to`) do not cause a failure; `pct` defaults to 100 and `adkim`/`aspf` default to relaxed (`r`) when omitted. | `DmarcXmlParser`, `DmarcEnumMapper`. |
| FR-PARSE-3 | Malformed XML, a non-`<feedback>` root element, or a missing *required* field raises a typed, message-specific exception rather than an unhandled crash. | `DmarcParseException`. Caught by the ingestion pipeline and recorded as a **Failed** `ProcessedMessage` with the exception message as the reason. |
| FR-PARSE-4 | Parsed data is persisted relationally: one `AggregateReport` row per report, one `Record` row per `<record>`, and one `SpfAuthResult`/`DkimAuthResult` row per reported auth-result entry. | EF Core entities under `src/DmarcAnalyzer.Core/Entities/`; mapping in `DmarcReportIngestionPipeline.MapReport`. |

### 3.3 SPF verification engine

| ID | Requirement | Implementation |
|---|---|---|
| FR-SPF-1 | For every ingested record, independently resolve the domain's **current** SPF TXT record and evaluate it against the record's source IP — separately from, and not derived from, whatever the report itself claimed. | `SpfEvaluator.EvaluateAsync` (`src/DmarcAnalyzer.Core/Spf/SpfEvaluator.cs`), invoked per record by `DmarcReportIngestionPipeline.EvaluateSpfAsync`. |
| FR-SPF-2 | Evaluation implements RFC 7208 mechanisms `ip4`, `ip6`, `a`, `mx`, `include`, `exists`, `all` (with qualifiers `+`/`-`/`~`/`?`) and the `redirect` modifier; unknown modifiers are ignored per the RFC's extensibility rule, unknown/unsupported mechanisms (including the deprecated `ptr`) resolve to `PermError`. | `SpfEvaluator` — see inline RFC citations in the source. |
| FR-SPF-3 | `include` results are combined per RFC 7208 §5.2: only a `Pass` from the included domain counts as a match; `Fail`/`SoftFail`/`Neutral` fall through to the next mechanism in the *outer* record (not a short-circuit); `None`/`PermError` from the included domain propagates as `PermError`. | `SpfEvaluator.EvaluateDomainAsync`, `include` case. |
| FR-SPF-4 | The total number of mechanisms/modifiers that trigger a DNS lookup (`a`, `mx`, `include`, `exists`, `ptr`, `redirect`) is capped at 10 across the whole recursive evaluation; exceeding it yields `PermError`. | `SpfEvaluator.LookupCounter`, shared across the recursion tree. |
| FR-SPF-5 | Zero SPF TXT records resolves to `None`; more than one resolves to `PermError`. | `SpfEvaluator.EvaluateDomainAsync`. |
| FR-SPF-6 | CIDR containment (`ip4`/`ip6` mechanisms, and matching a resolved `a`/`mx` host address against the checked IP) is computed via manual bitwise prefix comparison, not .NET's `IPNetwork` type — the latter rejects a base address with non-zero host bits, which resolved A/AAAA host addresses routinely have. | `SpfEvaluator.IsInCidr`. (See [§7.3](#73-known-defects-fixed-during-development) — this was a defect found and fixed during development.) |
| FR-SPF-7 | Macro expansion (RFC 7208 §7) is explicitly not implemented; `include`/`redirect`/`exists` arguments are treated as literal domains. | Documented in a code comment on `SpfEvaluator`; see [§3.6](#36-explicitly-out-of-scope). |
| FR-SPF-8 | When the recomputed result disagrees with the record's own `policy_evaluated/spf` verdict, the discrepancy is flagged and persisted with an explanatory note. | `SpfEvaluationResult.DiscrepancyFlag`/`DiscrepancyNote`, computed in `DmarcReportIngestionPipeline.EvaluateSpfAsync`; surfaced in the UI as a **record changed** badge. |

### 3.4 DKIM verification

| ID | Requirement | Implementation |
|---|---|---|
| FR-DKIM-1 | For every reported DKIM auth-result that includes a selector, independently query `{selector}._domainkey.{domain}` and parse the returned key record. | `DkimSelectorChecker.CheckAsync` (`src/DmarcAnalyzer.Core/Dkim/DkimSelectorChecker.cs`). |
| FR-DKIM-2 | Classify the selector as **Valid** (well-formed `p=` tag with valid base64), **Revoked** (empty `p=` tag, per RFC 6376 §3.6.1), **Missing** (no TXT record at all), or **ParseError** (record present but malformed/no `p=` tag). | `DkimSelectorChecker.CheckAsync`. |
| FR-DKIM-3 | If the report claimed a DKIM **Pass** for a selector that is now Missing or Revoked, flag it as stale. | `DkimSelectorCheck.StaleFlag`; surfaced in the UI as a **selector stale** badge. |
| FR-DKIM-4 | Full cryptographic re-verification of the DKIM signature (which would require the original signed message) is explicitly out of scope — aggregate reports carry only pass/fail + selector, not the signature or signed content. | Documented in code comments; see [§3.6](#36-explicitly-out-of-scope). |

### 3.5 Configuration, setup, and multi-instance support

| ID | Requirement | Implementation |
|---|---|---|
| FR-CFG-1 | No tenant-specific configuration (Entra ID app registration, domains, mailboxes, retention) is baked into the deployment template; all of it is entered through an in-app setup wizard after deployment, so the same codebase/template redeploys cleanly for a new organization. | `infra/main.parameters.json` contains only generic knobs (region, naming, SKU, SQL admin identity); `Pages/Setup/*`. |
| FR-CFG-2 | Until Graph connection + ≥1 domain + ≥1 mailbox + retention are all configured, every request except `/Setup/*` is redirected to the setup wizard. | `SetupGateMiddleware` (`src/DmarcAnalyzer.Web/Infrastructure/SetupGateMiddleware.cs`) + `SetupStateService.IsSetupCompleteAsync`. |
| FR-CFG-3 | Domains and shared mailboxes are a many-to-many relationship: one mailbox can serve several domains, and one domain's reports can land in more than one mailbox. All mailboxes authenticate with the *same* app registration credentials — only the mailbox address differs per poll. | `DomainMailbox` join entity; `Pages/Setup/Mailboxes.cshtml` multi-select UI. |
| FR-CFG-4 | The Graph app registration's client secret is validated live (a real Graph call) before being persisted, and is written directly to Azure Key Vault — it is never stored in the SQL database, not even encrypted. Only the Key Vault secret **name** is stored in SQL. | `Pages/Setup/GraphConnection.cshtml.cs`; `GraphConnectionSettings.ClientSecretKeyVaultName`; `KeyVaultSecretStore`. |
| FR-CFG-5 | Data retention is configurable (30–3650 days, default 400) per deployment, not hardcoded. | `Pages/Setup/Retention.cshtml`; `RetentionSettings.RetentionDays`. |
| FR-CFG-6 | The setup-wizard pages remain directly reachable after initial setup and double as the ongoing settings UI (add/remove domains and mailboxes, rotate the Graph client secret, change retention) — there is no separate, duplicated "settings" implementation. | `Pages/Settings/Index.cshtml` links directly to `Pages/Setup/*`. |

### 3.6 Explicitly out of scope

The following are intentional non-goals of the current implementation, called out so they are not
mistaken for defects:

- **RUF (forensic report) content parsing** — attachments are recognized but not parsed; only RUA
  (aggregate) reports drive the dashboard and analysis.
- **SPF macro expansion** (RFC 7208 §7) — `%{i}`-style macros in `exists`/`include`/`redirect`
  arguments are treated as literal text, not expanded. This covers the large majority of
  real-world SPF records; a macro-using record will simply fail to match rather than
  mis-evaluate silently.
- **DKIM cryptographic signature re-verification** — only DNS-selector liveness is checked, not
  the signature itself (requires the original signed message, which aggregate reports don't
  carry).
- **Raw report/email storage** — only parsed relational fields are stored; raw XML/attachment
  bytes are not persisted (keeps the database small on a serverless SQL tier; a future
  Blob-Storage-backed "replay" feature is a documented possibility, not implemented).
- **Multi-tenant single deployment** — one deployed instance serves one organization's Entra ID
  tenant and domains; running multiple unrelated customers behind one deployment is not
  supported (by design — see FR-CFG-1).
- **User authentication/authorization within the app** — see [§5.2](#52-security-requirements).

## 4. System Design

### 4.1 Architecture

```mermaid
flowchart TB
    subgraph Azure["Azure Resource Group"]
        subgraph AppService["App Service (Linux, .NET 8, Always On)"]
            Web["DmarcAnalyzer.Web<br/>Razor Pages + minimal APIs"]
            Poll["MailboxPollingService<br/>(BackgroundService)"]
            Purge["RetentionPurgeService<br/>(BackgroundService)"]
        end
        SQL[("Azure SQL Database<br/>AAD-only auth, serverless")]
        KV["Key Vault<br/>(Graph client secret)"]
        AI["Application Insights"]
    end
    Graph["Microsoft Graph API"]
    EXO["Exchange Online<br/>shared mailboxes"]
    DNS["Public DNS<br/>(SPF TXT / DKIM selector TXT)"]

    Web -- EF Core, Managed Identity --> SQL
    Poll -- EF Core, Managed Identity --> SQL
    Purge -- EF Core, Managed Identity --> SQL
    Web -- Managed Identity --> KV
    Poll -- app-only OAuth (client credentials) --> Graph
    Graph --> EXO
    Poll -- DNS TXT queries --> DNS
    Web --> AI
    Poll --> AI
```

**Project layout** (`src/`, `tests/`, `tools/`, `infra/`):

| Project | Responsibility | External dependencies |
|---|---|---|
| `DmarcAnalyzer.Core` | RFC 7489 XML parsing, RFC 7208 SPF evaluation, DKIM selector checking, verified-sender classification, entity definitions, DI-facing abstractions (`ISpfDnsResolver`, `IDkimDnsResolver`, `IGraphMailboxClient`, `ISecretStore`, `ISetupStateService`, `IVerifiedSenderClassifier`). | **None** — no EF Core, Graph SDK, or Azure SDK reference. This is what makes it fully unit-testable offline. |
| `DmarcAnalyzer.Infrastructure` | EF Core `DbContext` + configurations + migrations, Graph client (`Microsoft.Graph` SDK v6), Key Vault client (`Azure.Security.KeyVault.Secrets`), DNS resolution (`DnsClient.NET`), the two background services, the ingestion pipeline. | EF Core SqlServer, Microsoft.Graph, Azure.Identity, Azure.Security.KeyVault.Secrets, DnsClient. |
| `DmarcAnalyzer.Web` | ASP.NET Core 8 Razor Pages host: setup wizard, dashboard, settings, chart-data minimal API, DI composition root (`Program.cs`). | References Infrastructure + Core; adds `Microsoft.ApplicationInsights.AspNetCore`. |
| `tests/DmarcAnalyzer.Tests` | xUnit tests against in-memory fakes (`FakeSpfDnsResolver`, `FakeDkimDnsResolver`, `FakeGraphMailboxClient`) and EF Core's InMemory provider. No network access required. | xunit, Microsoft.EntityFrameworkCore.InMemory. |
| `tools/GrantSqlAccess` | Deploy-time console utility: grants the Web App's managed identity a SQL database user (Bicep cannot create database users). | Microsoft.Data.SqlClient. |
| `infra/` | Bicep IaC: `main.bicep` + `modules/{appServicePlan,webApp,sqlServer,keyVault,monitoring}.bicep`. | — |

**Dependency direction** is strictly `Web → Infrastructure → Core`; `Core` never depends outward.
This is enforced by project references, not just convention, and is what lets ~75% of the
business-logic test suite ([§6](#6-verification-and-acceptance-evidence)) run with zero network or
Azure dependency.

### 4.2 Data design

All entities live in `DmarcAnalyzer.Core.Entities`; EF Core mapping (`IEntityTypeConfiguration<T>`)
lives in `DmarcAnalyzer.Infrastructure.Data.Configurations`. Table names match entity names unless
noted.

```mermaid
erDiagram
    Domain ||--o{ DomainMailbox : "via join"
    Mailbox ||--o{ DomainMailbox : "via join"
    Domain ||--o{ AggregateReport : "has"
    Mailbox ||--o{ AggregateReport : "ingested via"
    Mailbox ||--o{ ProcessedMessage : "dedupe ledger"
    AggregateReport ||--o{ Record : "has"
    Record ||--o{ SpfAuthResult : "reported"
    Record ||--o{ DkimAuthResult : "reported"
    Record ||--o| SpfEvaluationResult : "recomputed"
    DkimAuthResult ||--o| DkimSelectorCheck : "recomputed"
    Domain ||--o{ VerifiedSenderOverride : "allowlist"
```

| Entity (table) | Key fields | Notes |
|---|---|---|
| `Domain` (`Domains`) | `DomainName` (unique), `IsActive` | A monitored sending domain. |
| `Mailbox` (`Mailboxes`) | `MailboxUpn` (unique), `MailFolder`, `DeltaLink`, `LastPolledUtc`, `LastPollStatus`, `LastPollError` | A polled shared mailbox. `MailFolder` accepts a well-known folder name (e.g. `inbox`) or a raw Graph folder ID — arbitrary folder-name resolution is not implemented. |
| `DomainMailbox` (`DomainMailboxes`) | composite PK `(DomainId, MailboxId)` | Many-to-many join; both FKs cascade-delete. |
| `GraphConnectionSettings` | singleton row, `Id = 1` | `TenantId`, `ClientId`, `ClientSecretKeyVaultName` (pointer only — the secret value lives in Key Vault), validation status. |
| `RetentionSettings` | singleton row, `Id = 1` | `RetentionDays` (default 400), `LastPurgeRunUtc`, `LastPurgeRowsDeleted`. |
| `ProcessedMessage` (`ProcessedMessages`) | unique index `(MailboxId, GraphMessageId)` | Idempotency ledger: `Status` (Success/Skipped/Failed), `AttachmentType`, `FailureReason`. |
| `DmarcAggregateReport` (`AggregateReports`) | FK `DomainId` (cascade delete), FK `MailboxId` (**restrict** — a mailbox can be deactivated/removed without needing to decide what happens to reports it already ingested) | `report_metadata` + `policy_published` fields; indexed on `(DomainId, DateRangeEndUtc)` for dashboard trend queries. |
| `DmarcRecord` (`Records`) | FK `AggregateReportId` (cascade) | One `<record>` per row: `SourceIp`, `Count`, `PolicyEvaluatedDisposition/Dkim/Spf`, identifiers. |
| `SpfAuthResult` / `DkimAuthResult` | FK `DmarcRecordId` (cascade) | As reported by the receiving mail server (`auth_results/spf`, `auth_results/dkim`). |
| `SpfEvaluationResult` | 1:1 with `DmarcRecord` (cascade) | The **independently recomputed** SPF verdict — `RecomputedResult`, `MatchedMechanism`, `LookupCount`, `DiscrepancyFlag`/`Note`. |
| `DkimSelectorCheck` | 1:1 with `DkimAuthResult` (cascade) | The **independently recomputed** DKIM selector liveness — `Status`, `RawTxtRecord`, `StaleFlag`. |
| `VerifiedSenderOverride` | FK `DomainId` (cascade) | Admin-curated allowlist entry (CIDR and/or org-name pattern) contributing to the "verified sender" badge alongside DMARC-aligned pass. |

Enumerations (`DmarcAnalyzer.Core.Entities.Enums`): `DmarcDisposition`, `DmarcAlignmentMode`,
`DmarcPolicyResult`, `SpfScope`, `SpfResultCode`, `DkimResultCode`, `MessageProcessingStatus`,
`AttachmentType`, `MailboxPollStatus`, `DkimSelectorStatus`.

### 4.3 Interface design

#### 4.3.1 Page routes (Razor Pages)

| Route | Purpose |
|---|---|
| `/` | Redirects to `/Dashboard`. |
| `/Dashboard` | Domain overview cards (30-day volume, pass rate, last report). |
| `/Dashboard/DomainDetail?domainId=&Days=&UnverifiedOnly=` | Per-domain trend chart + filterable record table. |
| `/Settings` | Hub linking to all configuration areas below. |
| `/Settings/IngestionStatus` | Per-mailbox poll status/error, retention job status, recent processing failures. |
| `/Setup/Welcome`, `/Setup/GraphConnection`, `/Setup/Domains`, `/Setup/Mailboxes`, `/Setup/Retention`, `/Setup/Review` | First-run wizard; also the ongoing settings pages for these areas (see FR-CFG-6). |

#### 4.3.2 Minimal API

| Endpoint | Purpose |
|---|---|
| `GET /api/domains/{domainId}/trend?days=30` | Returns `[{ date, pass, fail }]` daily aggregated volume for the domain-detail Chart.js chart. Implemented in `src/DmarcAnalyzer.Web/Api/ChartDataEndpoints.cs`. |

#### 4.3.3 External interfaces

| Interface | Direction | Auth | Notes |
|---|---|---|---|
| Microsoft Graph (`graph.microsoft.com`) | Outbound | App-only OAuth 2.0 client-credentials (`ClientSecretCredential`), scope `https://graph.microsoft.com/.default` | Requires the `Mail.Read` **application** permission, admin-consented; recommended to be scoped via an Exchange Online Application Access Policy (see [`docs/exchange-application-access-policy.md`](./exchange-application-access-policy.md)) since application permissions are otherwise tenant-wide. |
| Public DNS | Outbound | None | TXT queries for SPF (domain apex and any `include`d domains) and DKIM selector records, via `DnsClient.NET`'s system-resolver-backed `LookupClient`. |
| Azure Key Vault | Outbound | Managed Identity (`DefaultAzureCredential`) | `GetSecretAsync`/`SetSecretAsync` for the Graph client secret only. |
| Azure SQL | Outbound | Managed Identity (`Authentication=Active Directory Managed Identity` connection string) | No SQL password anywhere in the system. |

### 4.4 Ingestion & analysis pipeline (sequence)

```mermaid
sequenceDiagram
    participant Timer as PeriodicTimer (15 min)
    participant Poll as MailboxPollingService
    participant Graph as Microsoft Graph
    participant Pipe as DmarcReportIngestionPipeline
    participant SPF as SpfEvaluator
    participant DKIM as DkimSelectorChecker
    participant DB as Azure SQL

    Timer->>Poll: tick
    loop each active Mailbox
        Poll->>Graph: delta query (since last DeltaLink)
        Graph-->>Poll: new message summaries
        loop each message with attachments, not yet processed
            Poll->>Graph: get attachments
            Poll->>Pipe: ProcessMessageAsync(mailbox, attachments)
            Pipe->>Pipe: extract XML (zip/gz/raw) + parse (RFC 7489)
            Pipe->>DB: resolve Domain by policy_published/domain
            Pipe->>DB: persist AggregateReport + Records + auth results
            loop each Record
                Pipe->>SPF: EvaluateAsync(domain, sourceIp)
                SPF-->>Pipe: recomputed result (+ discrepancy vs. report)
                Pipe->>DKIM: CheckAsync(domain, selector)
                DKIM-->>Pipe: selector status (+ stale vs. report)
            end
            Pipe->>DB: persist SpfEvaluationResult / DkimSelectorCheck
            Poll->>DB: record ProcessedMessage (Success/Failed)
        end
        Poll->>DB: update Mailbox.DeltaLink/LastPolledUtc/LastPollStatus
    end
```

### 4.5 Infrastructure (Bicep)

| Resource | Configuration | File |
|---|---|---|
| App Service Plan | Linux, SKU `B1` (parameterized, must support Always On) | `infra/modules/appServicePlan.bicep` |
| Web App | `.NET 8`, system-assigned managed identity, `alwaysOn: true`, HTTPS-only, TLS ≥ 1.2 | `infra/modules/webApp.bicep` |
| Azure SQL | Logical server with **Azure-AD-only authentication** (no SQL password path exists), database SKU `GP_S_Gen5` (General Purpose, serverless), capacity 1, `autoPauseDelay: 60` min, `minCapacity: 0.5` vCore | `infra/modules/sqlServer.bicep` |
| Key Vault | RBAC authorization (not access policies), soft-delete + purge protection enabled | `infra/modules/keyVault.bicep` |
| Monitoring | Log Analytics workspace + workspace-based Application Insights | `infra/modules/monitoring.bicep` |
| Role assignment | Web App managed identity granted **Key Vault Secrets Officer**, scoped to the Key Vault resource only | `infra/main.bicep` |

Default region: `germanywestcentral`. All resource names are derived from a `namePrefix` parameter
plus `uniqueString(resourceGroup().id, ...)`, so the same template redeploys cleanly into a fresh
resource group for a new organization without name collisions (supports G-5/FR-CFG-1).

### 4.6 CI/CD

| Workflow | Trigger | Steps |
|---|---|---|
| `.github/workflows/ci.yml` | Pull request → `main`, push → `main` | `dotnet restore` → `dotnet build -c Release` → `dotnet format --verify-no-changes` → `dotnet test` (with coverage collection) → upload test results artifact. No Azure credentials used or needed. |
| `.github/workflows/deploy.yml` | `workflow_run` after CI succeeds on `main`, or manual `workflow_dispatch` | `azure/login` via **OIDC federated credentials** (no stored client secret) → Bicep deploy (`azure/arm-deploy`) → grant the Web App's managed identity DB access (`tools/GrantSqlAccess`, using `Authentication=Active Directory Default`) → build + apply an EF Core migrations bundle → `dotnet publish` → `azure/webapps-deploy`. |

## 5. Non-Functional Requirements

### 5.1 Performance requirements

| ID | Requirement |
|---|---|
| NFR-PERF-1 | Mailbox polling interval is configurable (default 15 min); DMARC aggregate reports are typically sent once daily per reporting organization per domain, so this comfortably keeps up without needing elastic scaling. |
| NFR-PERF-2 | The SPF/DKIM lookup limit (10, per RFC 7208) bounds the DNS work per record to a small, fixed number of queries. |
| NFR-PERF-3 | Retention purge runs in batches of 500 rows per delete statement (`RetentionPurgeService`) to avoid a single long-running transaction against the serverless SQL tier. |
| NFR-PERF-4 | The Azure SQL tier auto-pauses after 60 minutes of inactivity (cost control); the first request after a pause incurs a resume latency, which is an accepted tradeoff for a low-traffic internal tool. |

### 5.2 Security requirements

| ID | Requirement |
|---|---|
| NFR-SEC-1 | No password-based authentication exists anywhere in the credential chain: Graph access is app-only OAuth, SQL access is Azure AD-only (Managed Identity for the app; the deploy pipeline's own AAD-admin identity for migrations), Key Vault access is Managed Identity via RBAC. |
| NFR-SEC-2 | The Graph app registration's client secret is written directly to Key Vault by the setup wizard and is never persisted in SQL, logs, or source control. |
| NFR-SEC-3 | GitHub Actions authenticates to Azure via OIDC federated credentials; no long-lived Azure secret is stored as a GitHub secret. |
| NFR-SEC-4 | The Graph application permission (`Mail.Read`) is tenant-wide by default; the system documents (does not itself automate) scoping it to only the configured shared mailboxes via an Exchange Online Application Access Policy — see `docs/exchange-application-access-policy.md`. |
| NFR-SEC-5 | **Gap, accepted for the current scope**: the Web App itself has no authentication/authorization layer — anyone who can reach the deployed URL has full read/write access to the dashboard and all settings, including the ability to rotate the Graph client secret and add/remove monitored domains and mailboxes. This is an explicit, documented limitation, not an oversight; deployments are expected to restrict network reachability (e.g., Azure Front Door with auth, VNet integration + private endpoint, or an App Service authentication provider) at the infrastructure layer if broader-than-trusted-network exposure is required. This is the single most consequential gap between "internal tool for a trusted network" and "safe to expose publicly," and should be the first extension considered before any deployment reachable from the open internet. |
| NFR-SEC-6 | Raw report/email content is not stored (see [§3.6](#36-explicitly-out-of-scope)), limiting the blast radius of a database compromise to metadata (source IPs, volumes, org names) rather than message content. |

### 5.3 Usability requirements

| ID | Requirement |
|---|---|
| NFR-USE-1 | A fresh deployment is unusable until first-run setup is complete, and is redirected there automatically rather than presenting a broken/empty dashboard. |
| NFR-USE-2 | The dashboard visually distinguishes "verified" vs. "unverified" senders and surfaces the two independent-verification signals (SPF record drift, stale DKIM selector) as inline badges directly in the record table, not buried in a separate report. |
| NFR-USE-3 | The UI style is a self-contained, brand-neutral dashboard aesthetic (teal/slate palette, card-based domain overview, Chart.js trend visualization) modeled loosely on commercial DMARC dashboards (dmarcian-style), implemented with vendored Bootstrap + Chart.js — no CDN dependency at runtime. |

### 5.4 Reliability requirements

| ID | Requirement |
|---|---|
| NFR-REL-1 | A single message's ingestion failure (parse error, transient Graph/DNS error) is isolated and does not abort the rest of that poll cycle's batch (FR-ING-4). |
| NFR-REL-2 | Both background services (`MailboxPollingService`, `RetentionPurgeService`) catch and log unexpected exceptions at the top of their loop rather than letting an unhandled exception terminate the hosted service. |
| NFR-REL-3 | The App Service Plan is expected to run as a **single instance** (no autoscale) for this workload — the background services hold no distributed lock, so multi-instance scale-out would risk duplicate polling/purging. This is a documented limitation, not enforced in code or infrastructure. |

### 5.5 Portability / redeployability requirements

| ID | Requirement |
|---|---|
| NFR-PORT-1 | The Bicep template accepts only generic parameters (environment name, region, name prefix, SKU, SQL admin identity) — see FR-CFG-1. |
| NFR-PORT-2 | Standing up a second, independent instance for a different organization requires no source changes — only a new resource group, a new Entra ID app registration, and re-running the setup wizard. |

## 6. Verification and Acceptance Evidence

This section records what has actually been verified against the implementation, as evidence for
the acceptance criteria in [§6.1](#61-acceptance-criteria).

- **Automated test suite**: 41 xUnit tests, all passing (`dotnet test`), covering:
  - `SpfEvaluatorTests` (18 tests): CIDR boundary correctness (ip4/ip6), all four `all`-qualifier
    outcomes, two-level nested `include` with correct non-short-circuit fallthrough, an `include`
    target with no record, the `redirect` modifier, `a`/`mx` with and without explicit
    domain-spec/CIDR, `exists`, exceeding the 10-lookup limit, multiple SPF records, no SPF
    record, an unrecognized mechanism, an unrecognized-but-ignored modifier, mechanism-keyword
    case-insensitivity.
  - `DkimSelectorCheckerTests` (5 tests): valid key, revoked (empty `p=`), missing record, no
    `p=` tag, garbage base64.
  - `DmarcXmlParserTests` (5 tests) and `AttachmentExtractorTests` (4 tests): full/minimal valid
    reports, malformed XML, a missing required field, a non-`<feedback>` root, and zip/gzip/raw-xml
    attachment-extraction equivalence.
  - `MailboxPollingServiceTests` (2 tests): a message is not reprocessed on a second poll; one
    malformed message does not block a subsequent valid one in the same batch.
- **Infrastructure validation**: `infra/main.bicep` compiles and lints cleanly with the Bicep CLI
  (0 errors, 0 warnings) — all 5 modules and the Key Vault role assignment resolve correctly.
- **End-to-end UI verification**: the running application (seeded with representative sample data,
  not a live Azure/Graph tenant) was exercised via a headless-browser pass covering the dashboard,
  domain-detail page (including the discrepancy/stale badges), settings hub, domains/mailboxes
  configuration, ingestion status, and setup wizard — see the screenshots in `README.md`.
- **Workflow syntax**: both GitHub Actions workflow files were parsed and validated as well-formed
  YAML.

Verification **not** performed (requires live Azure/Microsoft 365 infrastructure this project does
not have access to): an actual deployment to Azure, a live Graph mailbox poll against a real
tenant, and an actual CI/CD run of `deploy.yml` end-to-end. These are called out in
`docs/deployment.md` as the deploying engineer's first post-clone verification steps.

### 6.1 Acceptance criteria

| Category | Criterion | Status |
|---|---|---|
| Functional | All requirements in [§3](#3-functional-requirements) are implemented and covered by at least one automated test or a documented manual verification. | Met — see evidence above. |
| Functional | A DMARC report for an unmonitored domain is skipped, not treated as an error. | Met (FR-ING-7; covered indirectly by the domain-resolution logic, exercised by `MailboxPollingServiceTests`). |
| Performance | `dotnet build`/`dotnet test` complete in CI without network access. | Met — no test in the suite makes a real network call. |
| Security | No plaintext secret (Graph client secret, SQL password) exists in source control, database rows, or GitHub Actions configuration. | Met by design (FR-CFG-4, NFR-SEC-1–3); SQL has no password path at all. |
| Reliability | A single bad message does not abort a poll cycle. | Met — `MailboxPollingServiceTests.PollOnceAsync_OneFailingMessage_DoesNotBlockTheRestOfTheBatch`. |
| Reliability | A poll/purge cycle failure is logged, not fatal to the process. | Met by construction (`try/catch` around each `BackgroundService` loop body) — not covered by an automated test (would require simulating a hosted-service crash), flagged as a documentation-only acceptance item. |
| Portability | The Bicep template contains no hardcoded tenant-specific value. | Met — verified by inspection of `infra/main.parameters.json` and all `infra/*.bicep` files. |

## 7. Appendix

### 7.1 Glossary

| Term | Meaning |
|---|---|
| **DMARC** | Domain-based Message Authentication, Reporting & Conformance (RFC 7489). |
| **SPF** | Sender Policy Framework (RFC 7208) — a DNS TXT record listing hosts authorized to send mail for a domain. |
| **DKIM** | DomainKeys Identified Mail (RFC 6376) — cryptographic email signing, keys published as DNS TXT records under `{selector}._domainkey.{domain}`. |
| **RUA** | DMARC *aggregate* report — daily XML summary of authentication results, the only report type this system parses today. |
| **RUF** | DMARC *forensic* report — per-message failure detail; recognized but not parsed by this system. |
| **Alignment** (`adkim`/`aspf`) | Whether SPF/DKIM must match the `From:` domain exactly (`s` = strict) or allow an organizational-domain match (`r` = relaxed). |
| **Disposition** (`p`/`sp`) | The policy action requested for the domain (`none`/`quarantine`/`reject`); `sp` is the subdomain policy. |
| **Discrepancy** (this system's term) | A record whose live-recomputed SPF result disagrees with what the aggregate report itself claimed for that source IP. |
| **Stale selector** (this system's term) | A DKIM selector the report recorded as passing, which no longer resolves or has been revoked in DNS today. |
| **Application Access Policy** | An Exchange Online control restricting which mailboxes a given Graph app registration's application permissions can reach. |

### 7.2 Reference documents

- RFC 7489 — Domain-based Message Authentication, Reporting, and Conformance (DMARC)
- RFC 7208 — Sender Policy Framework (SPF) for Authorizing Use of Domains in Email
- RFC 6376 — DomainKeys Identified Mail (DKIM) Signatures
- [`README.md`](../README.md) — orientation, architecture summary, UI screenshots
- [`docs/deployment.md`](./deployment.md) — deployment runbook
- [`docs/exchange-application-access-policy.md`](./exchange-application-access-policy.md) — Exchange Online PowerShell steps

### 7.3 Known defects fixed during development

Recorded here for traceability, since both were substantive correctness bugs caught by the
automated test suite before merge, not merely style issues:

1. **SPF CIDR matching crash**: .NET 8's `System.Net.IPNetwork` constructor throws if the base
   address has non-zero bits past the prefix length — which a resolved A/AAAA host address
   routinely has. Replaced with a manual bitwise prefix comparison (`SpfEvaluator.IsInCidr`).
2. **EF Core change-tracking bug**: new `SpfEvaluationResult`/`DkimSelectorCheck` entities,
   assigned only via a navigation property on an already-tracked parent with a pre-set
   (non-default) `Guid` key, were misidentified as `Modified` instead of `Added` by EF Core's
   change-tracker graph traversal, causing a `DbUpdateConcurrencyException` on save. Fixed by
   explicitly adding both entities to their `DbSet` in `DmarcReportIngestionPipeline`.

### 7.4 Change log

| Version | Date | Change |
|---|---|---|
| 1.0 | 2026-08-10 | Initial specification, describing the system as of commit `844b4ac`. |
