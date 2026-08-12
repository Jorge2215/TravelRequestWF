# Legolas — History

## Project Context

- **Project:** TravelRequestWF — a Web Application built on .NET 10 Razor Pages, with Azure SQL Database, and likely some Azure Functions and MS Power Automate flows.
- **Owner:** Jorgito
- **Team cast:** Lord of the Rings universe (Aragorn, Legolas, Gandalf, Merry, Sam, Pippin)

## Learnings

### 2026-08-11 — Stage 3 Identity UI

- Gandalf's `ApplicationUser` class landed at `TravelRequestWF.Infrastructure.Identity.ApplicationUser` — always check `.squad/agents/gandalf/task-stage3-identity-backend.md` for the exact namespace before writing Identity UI code.
- Even when a parallel agent hasn't "pushed" yet, their work can already be in the local working tree (committed locally but not remote-pushed). Always run `git pull` AND check with `glob **/ApplicationUser.cs` before assuming the dependency is absent.
- The `@inject` directive for `SignInManager<ApplicationUser>` belongs in `_Layout.cshtml` body (after `<body>`), not in `<head>` — Razor processes it fine wherever it appears in the view but placing it inside the component that uses it keeps things clean.
- Self-registered users are auto-assigned the `Employee` role in `Register.cshtml.cs`. Role assignment to `Manager` is manual/admin-only for this PoC — document this in register page comments.
- `dotnet build TravelRequestWF.slnx` succeeded cleanly (0 errors, 0 warnings) after Gandalf's Identity backend changes were present locally.

