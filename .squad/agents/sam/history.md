# Sam — History

## Project Context

- **Project:** TravelRequestWF — a Web Application built on .NET 10 Razor Pages, with Azure SQL Database, and likely some Azure Functions and MS Power Automate flows.
- **Owner:** Jorgito
- **Team cast:** Lord of the Rings universe (Aragorn, Legolas, Gandalf, Merry, Sam, Pippin)

## Learnings

### Stage 5 — Power Automate Flows (2026-08-13)

- **Non-blocking HTTP trigger design:** Both flows are "fire and forget" from the .NET side — the app POSTs and gets HTTP 202 Accepted immediately. No polling or response parsing needed in `PowerAutomateNotificationService`.
- **Same payload shape for both flows:** Aragorn locked a single canonical JSON contract (11 fields, PascalCase) shared by Flow A and Flow B. This simplifies Gandalf's implementation — one DTO class, two URLs.
- **null Comments handling:** `Comments` can be null (e.g., on Approve without a comment). Handled in Flow B's email body with Power Automate's `if(empty(triggerBody()?['Comments']), '(No comments provided)', triggerBody()?['Comments'])` expression. This avoids a blank table cell which looks broken in email clients.
- **EventType disambiguates flows:** Aragorn added `EventType` to the payload (`"Submitted"`, `"Resubmitted"`, `"Approved"`, `"Rejected"`, `"Returned"`). Used in email subjects for clarity. Flow A routes Submit/Resubmit; Flow B routes Approve/Reject/Return — the flow selection happens in .NET before the POST, not inside Power Automate.
- **Schema generation tip:** Power Automate's "Generate from sample" fails silently on `null` values — use a string for `Comments` during schema generation; null is fine at runtime once the schema is set.
- **URL security:** Flow HTTP trigger URLs contain a SAS signature. Treat as secrets. Document to store in Azure App Service Application Settings for production, not committed to source control.
