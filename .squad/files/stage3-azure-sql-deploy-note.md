# Stage 3 — Azure SQL Deploy Note

After the `AddIdentityTables` migration is applied to LocalDB, run the following to apply it to Azure SQL:

```bash
dotnet ef database update \
  --project src/TravelRequestWF.Infrastructure \
  --startup-project src/TravelRequestWF.Web \
  --connection "Server=<azure-sql-server>.database.windows.net;Database=TravelRequestWF;User Id=<user>;Password=<password>;Encrypt=True;TrustServerCertificate=False;"
```

Replace `<azure-sql-server>`, `<user>`, and `<password>` with the actual Azure SQL credentials from the Key Vault / connection string in the App Service configuration.

This migration adds: `AspNetUsers` (with `EmployeeId` FK), `AspNetRoles`, `AspNetUserRoles`, `AspNetUserClaims`, `AspNetUserLogins`, `AspNetUserTokens`, `AspNetRoleClaims`.
