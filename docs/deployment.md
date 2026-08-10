# Deploying a new instance

This deployment template is generic — the same repository can be deployed once per
organization/customer into a fresh Azure resource group, with all tenant-specific configuration
(Entra ID app registration, monitored domains, shared mailboxes, retention) captured afterward
through the in-app setup wizard rather than baked into the infrastructure template.

## 1. Prerequisites

- An Azure subscription and a resource group to deploy into (region: Germany West Central by
  default — `germanywestcentral`).
- Permission to create an Entra ID app registration and a federated credential on it.
- Permission to set the initial Azure AD administrator on the Azure SQL logical server (this can
  be your own account, or — recommended for CI/CD — a group that includes the deploy pipeline's
  identity; see step 3).

## 2. Create the Entra ID app registration used by the app itself (Graph access)

This is the app registration DMARC Analyzer uses at runtime to read shared mailboxes via
Microsoft Graph. It is **separate** from the app registration used for GitHub Actions deployment
(step 3).

1. Entra ID portal → App registrations → New registration. Any name/redirect URI (none needed —
   this is a daemon/client-credentials app).
2. API permissions → Add a permission → Microsoft Graph → **Application permissions** →
   `Mail.Read` → Add, then **Grant admin consent**.
3. Certificates & secrets → New client secret. Copy the secret value — you'll enter it once in the
   app's setup wizard (Graph Connection step) after the first deploy; it is written to Key Vault
   at that point and never stored anywhere else.
4. Note the **Tenant ID**, **Client ID**, and the secret value for the setup wizard.
5. Follow [`exchange-application-access-policy.md`](./exchange-application-access-policy.md) to
   scope this app registration to only the shared mailboxes it should be able to read.

## 3. Set up GitHub Actions OIDC deployment (a *different*, second app registration)

This one authenticates the GitHub Actions workflow to Azure — no client secret is stored in
GitHub; it uses federated (OIDC) credentials instead.

1. Create another app registration (e.g. "dmarc-analyzer-deploy"), or reuse an existing
   CI/CD one.
2. Certificates & secrets → Federated credentials → Add credential → GitHub Actions deploying
   Azure resources. Fill in your GitHub org/repo, and either `Environment` (if using a GitHub
   Environment) or `Branch: main`.
3. Grant this app registration's service principal **Contributor** (or a narrower custom role
   covering the resource types in `infra/`) on the target resource group.
4. Also make it the Azure SQL server's Azure AD administrator — either directly (set
   `sqlAdminAadObjectId`/`sqlAdminAadLogin` in `infra/main.parameters.json` to this service
   principal), or via an AAD group it belongs to. This is required because only the SQL AAD admin
   can create the database user for the Web App's managed identity and apply EF Core migrations —
   see `infra/post-deploy-sql-grant.sql.tmpl` and `.github/workflows/deploy.yml`.
5. In the GitHub repository, add:
   - **Secrets**: `AZURE_CLIENT_ID`, `AZURE_TENANT_ID`, `AZURE_SUBSCRIPTION_ID` (all
     non-sensitive identifiers — safe under OIDC, no client secret needed).
   - **Variables**: `AZURE_RESOURCE_GROUP` (must already exist), `AZURE_WEBAPP_NAME` is **not**
     set manually — Bicep generates a unique Web App name; instead the deploy workflow reads it
     from the Bicep deployment outputs automatically. Only `AZURE_RESOURCE_GROUP` (and optionally
     `AZURE_SQL_DATABASE_NAME`, default `DmarcAnalyzer`) need to be set as repository variables.

## 4. Fill in `infra/main.parameters.json`

Only generic, non-secret values:

```json
{
  "namePrefix": { "value": "dmarc-contoso" },
  "sqlAdminAadObjectId": { "value": "<object id of the deploy service principal or AAD group>" },
  "sqlAdminAadLogin": { "value": "<its display name/UPN>" }
}
```

## 5. First deploy

Push to `main` (after CI passes) or run the **Deploy** workflow manually
(`workflow_dispatch`) from the Actions tab. This:

1. Deploys `infra/main.bicep` (App Service, Azure SQL, Key Vault, Application Insights).
2. Grants the Web App's managed identity database access (`tools/GrantSqlAccess`).
3. Builds and applies EF Core migrations (`dotnet ef migrations bundle`).
4. Publishes and deploys the app to the Web App.

## 6. Complete the setup wizard

Browse to the deployed Web App's URL — you'll land on the setup wizard automatically. Walk
through: Graph connection (Tenant ID/Client ID/secret from step 2), domains, shared mailboxes,
retention. The mailbox polling background job picks up new reports on its next cycle (default:
every 15 minutes, configurable via the `Ingestion:PollIntervalMinutes` app setting).

## Local development

You'll need a reachable SQL Server (e.g. a local SQL Server container, or `(localdb)` on
Windows) and, to complete the Graph Connection step of the setup wizard locally, a real Azure Key
Vault (`KeyVault:Uri` in `appsettings.Development.json` or user-secrets) that your local Azure
identity (`az login`) has `Key Vault Secrets Officer` on — the app has no local-secret-storage
fallback by design, since the client secret must never be stored anywhere but Key Vault.

```bash
dotnet ef database update --project src/DmarcAnalyzer.Infrastructure --startup-project src/DmarcAnalyzer.Infrastructure
dotnet run --project src/DmarcAnalyzer.Web
```
