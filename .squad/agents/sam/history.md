# Sam — History

## Project Context

- **Project:** TravelRequestWF — a Web Application built on .NET 10 Razor Pages, with Azure SQL Database, and likely some Azure Functions and MS Power Automate flows.
- **Owner:** Jorgito
- **Team cast:** Lord of the Rings universe (Aragorn, Legolas, Gandalf, Merry, Sam, Pippin)

## Learnings

### Phase 9 — Flow C: Daily Pending Requests Digest (2026-08-15)

- **Array payload pattern is new:** Flow C's payload differs fundamentally from Flow A/B — it contains a nested array (`PendingRequests`). The "Use sample payload to generate schema" feature correctly generates an array-of-objects schema when given two array items in the sample. Ensure both array elements are included in the sample to get the full schema; one element is technically enough but two is safer for Power Automate's heuristic.
- **Apply to each loop naming:** The Power Automate designer may rename the loop action (e.g., `Apply_to_each_1`). The `items(...)` expression inside the loop references the action's internal name — if the name differs, expressions referencing `items('Apply_to_each')` will fail at runtime. Guide warns Jorgito to re-insert dynamic tokens from the picker if the name differs.
- **`string(length(...))` for subject field:** The `length()` expression returns integer; the Subject field expects string. Omitting `string()` wrapper causes a type coercion error at runtime. This must be called out explicitly in the guide.
- **`varEmailBody` variable scope:** The Initialize variable, Apply to each loop, and all Append to string variable actions must be at the correct nesting level. The close-table Append must be outside the loop. Guide includes explicit callout on nesting.
- **Integer RequestId:** Unlike Phase 5 (where `RequestId` was sent as a string representation of an int), Phase 9's Merry sends `RequestId` as an actual integer in the JSON. Power Automate schema generator types it as `integer`. This is fine — it renders correctly in the HTML table without any cast needed.
- **"Is HTML" toggle required:** Must be turned on for the digest email. Without it, the email body shows raw HTML tags. Documented in the guide and in the troubleshooting table.
- **config key convention:** Used the colon-separated key `PowerAutomate:FlowCDailyDigestUrl` in `local.settings.json` Values section for Azure Functions — this is the Functions-compatible way to express hierarchical config (colons work in `local.settings.json`; double-underscores are the environment variable equivalent for Azure App Settings).

### Stage 5 — Power Automate Flows (2026-08-13)

- **Non-blocking HTTP trigger design:** Both flows are "fire and forget" from the .NET side — the app POSTs and gets HTTP 202 Accepted immediately. No polling or response parsing needed in `PowerAutomateNotificationService`.
- **Same payload shape for both flows:** Aragorn locked a single canonical JSON contract (11 fields, PascalCase) shared by Flow A and Flow B. This simplifies Gandalf's implementation — one DTO class, two URLs.
- **null Comments handling:** `Comments` can be null (e.g., on Approve without a comment). Handled in Flow B's email body with Power Automate's `if(empty(triggerBody()?['Comments']), '(No comments provided)', triggerBody()?['Comments'])` expression. This avoids a blank table cell which looks broken in email clients.
- **EventType disambiguates flows:** Aragorn added `EventType` to the payload (`"Submitted"`, `"Resubmitted"`, `"Approved"`, `"Rejected"`, `"Returned"`). Used in email subjects for clarity. Flow A routes Submit/Resubmit; Flow B routes Approve/Reject/Return — the flow selection happens in .NET before the POST, not inside Power Automate.
- **Schema generation tip:** Power Automate's "Generate from sample" fails silently on `null` values — use a string for `Comments` during schema generation; null is fine at runtime once the schema is set.
- **URL security:** Flow HTTP trigger URLs contain a SAS signature. Treat as secrets. Document to store in Azure App Service Application Settings for production, not committed to source control.
- **Docs correction (2026-08-13):** Replaced misleading UUID-style RequestId samples in .squad/files/stage5-power-automate-setup.md with integer-as-string examples (e.g. "1006"); RequestId is an int PK cast to string by Gandalf's code.
