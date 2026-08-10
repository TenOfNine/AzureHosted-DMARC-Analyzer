// Grants the Web App's system-assigned managed identity read/write access to the DMARC Analyzer
// database. Bicep cannot create SQL database users/role memberships, so this runs as a deploy pipeline
// step instead (see .github/workflows/deploy.yml). Reads the SQL from
// infra/post-deploy-sql-grant.sql.tmpl so the grant statements have a single source of truth.
//
// Usage: dotnet run -- <sqlServerFqdn> <databaseName> <webAppName> [templatePath]
//
// Connects using "Authentication=Active Directory Default", which resolves credentials through
// Microsoft.Data.SqlClient's built-in Azure.Identity integration — in the deploy pipeline this picks up
// the OIDC federated identity azure/login@v2 already established, without needing any secret here. The
// principal running this (the deploy pipeline's identity) must be the SQL server's Azure AD
// administrator, since only the AAD admin can create other database users.

using Microsoft.Data.SqlClient;

if (args.Length < 3)
{
    Console.Error.WriteLine("Usage: dotnet run -- <sqlServerFqdn> <databaseName> <webAppName> [templatePath]");
    return 1;
}

var sqlServerFqdn = args[0];
var databaseName = args[1];
var webAppName = args[2];
var templatePath = args.Length > 3 ? args[3] : Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "infra", "post-deploy-sql-grant.sql.tmpl");

if (!File.Exists(templatePath))
{
    Console.Error.WriteLine($"Template not found: {templatePath}");
    return 1;
}

var sqlTemplate = await File.ReadAllTextAsync(templatePath);
var sql = sqlTemplate.Replace("__WEBAPP_NAME__", webAppName, StringComparison.Ordinal);

var connectionString = $"Server=tcp:{sqlServerFqdn},1433;Database={databaseName};Authentication=Active Directory Default;Encrypt=True;";

await using var connection = new SqlConnection(connectionString);
await connection.OpenAsync();

// Executed as a single batch (not split on ';') because the template's IF/BEGIN/END block would
// otherwise be cut apart into invalid fragments — CREATE USER/ALTER ROLE don't require being the
// only statement in a batch, so the whole template is valid as one command.
await using var command = new SqlCommand(sql, connection);
await command.ExecuteNonQueryAsync();

Console.WriteLine($"Granted database access to '{webAppName}' on {sqlServerFqdn}/{databaseName}.");
return 0;
