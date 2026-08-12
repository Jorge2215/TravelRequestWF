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

### 2026-08-12 — Stage 4 Workflow UI

- Ran parallel with Gandalf who owns the .cshtml.cs code-behind. The brief specified exact property/handler names — bind against those regardless of whether Gandalf's code has landed.
- Gandalf's Stage 4 stubs existed on `dev` but were empty shells (no `[BindProperty]` properties at all). The build produced 56 CS1061 errors — every single one was "model does not contain definition" due to missing PageModel properties, not markup errors. Zero false-positives on the markup side.
- The `TravelRequest` entity uses `AuditLog` (not `AuditLogEntries`) as the navigation property name — always verify entity field names in `Entities/*.cs` before writing markup, don't assume from the brief.
- Status badge colors: Pending=bg-warning text-dark, Approved=bg-success, Rejected=bg-danger, Returned=bg-secondary (not bg-info — brief says bg-secondary for Returned).
- Manager/Review.cshtml: used a single `<form>` with three `asp-page-handler` buttons (Approve/Reject/Return) sharing one `Comments` textarea — this is cleaner than three separate forms with duplicate hidden inputs.
- `_ViewImports.cshtml` needed `@using TravelRequestWF.Infrastructure.Entities` added so all pages can use `TravelRequestStatus` enum and entity types without per-page `@using` directives.
- WIP commit + push done. Coordinator should re-invoke Legolas once Gandalf's PageModel properties land to run the final combined build verification.

