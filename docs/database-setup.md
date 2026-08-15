# Database Setup — TravelRequestWFDb

## Local Development (LocalDB)

Migrations are applied automatically when you run `dotnet ef database update` locally using the `appsettings.Development.json` connection string (LocalDB). No additional steps needed for dev.

## Azure SQL — Production Setup

Once Jorgito provides the real Azure SQL credentials, run the following command to apply all pending migrations against the live database:

```powershell
dotnet ef database update `
  --project src/TravelRequestWF.Infrastructure `
  --startup-project src/TravelRequestWF.Web `
  --connection "Server=<your-azure-sql-server>.database.windows.net;Database=TravelRequestWFDb;User Id=<your-user>;Password=<your-password>;Encrypt=True;TrustServerCertificate=False;"
```

Replace `<your-azure-sql-server>`, `<your-user>`, and `<your-password>` with the real values from Azure Portal → SQL Server → Connection strings.

> ⚠️ **Never commit real credentials.** Use environment variables or Azure Key Vault for production secrets. The `appsettings.json` placeholder is intentional — override it via environment variable `ConnectionStrings__DefaultConnection` on the App Service.

## Migration Reference

| Migration | Description |
|---|---|
| `InitialCreate` | Creates Employees, TravelRequests, RequestDocuments, AuditLogEntries tables with all FK constraints |

## Adding Future Migrations

```powershell
dotnet ef migrations add <MigrationName> `
  --project src/TravelRequestWF.Infrastructure `
  --startup-project src/TravelRequestWF.Web `
  --output-dir Migrations
```

Always run migrations in source control. Never apply manual schema changes directly to Azure SQL.
