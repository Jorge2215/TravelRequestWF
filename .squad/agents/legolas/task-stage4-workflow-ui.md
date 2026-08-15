# Legolas — Task Brief: Stage 4 Workflow UI
**Assigned by:** Aragorn  
**Date:** 2026-08-12T20:04:41-03:00  
**Branch:** `dev`

---

## Your Scope

You own ALL `.cshtml` markup for the 5 workflow pages. Gandalf owns the `.cshtml.cs` code-behind and services. The property names and handler names below come from Gandalf's brief and are authoritative — bind your Razor forms to these exact names.

**Soft dependency:** You can build markup now. A final combined `dotnet build` requires both your markup and Gandalf's code-behind to be merged. Same pattern as Stage 3 — build your side cleanly and flag any binding mismatches you find.

---

## Design Conventions

- Bootstrap 5 (already in the project via `_Layout.cshtml`) — use Bootstrap classes.
- Status badges: use `badge` + contextual color classes:
  - Pending → `badge bg-warning text-dark`
  - Approved → `badge bg-success`
  - Rejected → `badge bg-danger`
  - Returned → `badge bg-secondary`
- All forms use `asp-` tag helpers (`asp-for`, `asp-page-handler`, `asp-route-id`).
- Validation: use `<span asp-validation-for="...">` and `<div asp-validation-summary="ModelOnly">`.
- Date inputs: `type="date"`, bound to `DateOnly` properties.
- File upload: `enctype="multipart/form-data"`, `<input type="file" asp-for="Documents" multiple>`.

---

## 1. Pages/Employee/Submit.cshtml

**Purpose:** Employee submits a new travel request.

**Model properties to bind:**
- `Model.Destination` (string)
- `Model.StartDate` (DateOnly)
- `Model.EndDate` (DateOnly)
- `Model.Purpose` (string)
- `Model.Documents` (List<IFormFile>, multiple file upload)
- `Model.ErrorMessage` (string?) — display as alert if set
- `Model.SuccessMessage` (string?) — display as success alert if set

**Layout:**
```
<h2>Submit Travel Request</h2>

[Error alert if ErrorMessage set]
[Success alert if SuccessMessage set]

<form method="post" enctype="multipart/form-data">
  <div class="mb-3">
    <label asp-for="Destination">Destination</label>
    <input asp-for="Destination" class="form-control" />
    <span asp-validation-for="Destination" class="text-danger" />
  </div>
  <div class="mb-3">
    <label asp-for="StartDate">Start Date</label>
    <input asp-for="StartDate" type="date" class="form-control" />
    <span asp-validation-for="StartDate" class="text-danger" />
  </div>
  <div class="mb-3">
    <label asp-for="EndDate">End Date</label>
    <input asp-for="EndDate" type="date" class="form-control" />
    <span asp-validation-for="EndDate" class="text-danger" />
  </div>
  <div class="mb-3">
    <label asp-for="Purpose">Purpose</label>
    <textarea asp-for="Purpose" class="form-control" rows="4"></textarea>
    <span asp-validation-for="Purpose" class="text-danger" />
  </div>
  <div class="mb-3">
    <label asp-for="Documents">Supporting Documents (optional)</label>
    <input asp-for="Documents" type="file" class="form-control" multiple />
  </div>
  <button type="submit" class="btn btn-primary">Submit Request</button>
  <a asp-page="/Employee/Index" class="btn btn-secondary ms-2">Cancel</a>
</form>
```

---

## 2. Pages/Employee/Index.cshtml

**Purpose:** Employee sees their own travel requests list.

**Model properties:**
- `Model.Requests` (IReadOnlyList<TravelRequest>)

**Layout:**
```
<h2>My Travel Requests</h2>
<a asp-page="/Employee/Submit" class="btn btn-primary mb-3">+ New Request</a>

[If Requests is empty: info alert "You have no travel requests yet."]

[Table with columns: Destination | Start Date | End Date | Status | Submitted | Actions]
[Each row: request.Destination | request.StartDate | request.EndDate | status badge | request.SubmittedAt | <a asp-page="/Employee/Detail" asp-route-id="@request.Id">View</a>]
```

Status badge helper: render a `<span class="badge ...">` with appropriate color per status (see conventions above).

---

## 3. Pages/Employee/Detail.cshtml

**Route:** `@page "{id:int}"`

**Purpose:** Employee views a single request, its documents, audit trail, and can resubmit if Returned.

**Model properties:**
- `Model.Request` (TravelRequest?) — if null, show "Not found" message
- `Model.CanResubmit` (bool)
- `Model.ErrorMessage` (string?)

**Layout:**
```
<h2>Request Detail</h2>
<a asp-page="/Employee/Index" class="btn btn-secondary mb-3">← Back to My Requests</a>

[If ErrorMessage: danger alert]

[Card or panel showing: Destination, Start/End Date, Purpose, Status badge, Submitted date, Approver name (Request.Approver?.Name)]

<!-- Documents section -->
<h4>Documents</h4>
[If no documents: "No documents attached."]
[Table: FileName | Download link (href=BlobUrl, target="_blank")]

<!-- Audit Trail section -->
<h4>Audit Trail</h4>
[Table: Action | Actor | Timestamp | Details — ordered by Timestamp ascending]
[Render Request.AuditLogEntries — include only entries where TravelRequestId is set (these are request-level entries)]

<!-- Resubmit section — only if CanResubmit -->
[If Model.CanResubmit:]
<form method="post" asp-page-handler="Resubmit" asp-route-id="@Model.Request.Id">
  <p class="text-muted">This request was returned for more information. Review the audit trail and resubmit when ready.</p>
  <button type="submit" class="btn btn-warning">Resubmit Request</button>
</form>
```

---

## 4. Pages/Manager/Index.cshtml

**Purpose:** Manager sees pending requests assigned to them.

**Model properties:**
- `Model.Requests` (IReadOnlyList<TravelRequest>)

**Layout:**
```
<h2>Pending Requests for Review</h2>

[If Requests is empty: info alert "No pending requests assigned to you."]

[Table: Employee | Destination | Start Date | End Date | Status | Submitted | Actions]
[Each row: request.Employee?.Name | ... | status badge | <a asp-page="/Manager/Review" asp-route-id="@request.Id">Review</a>]
```

Note: Show ALL requests (not just Pending) in the list so the manager can see historical decisions too — filter to Pending only by default with a note, or show all with status badges. **Decision: show all, not just Pending** so manager has full visibility. Pending rows get a "Review" link; non-Pending rows get a "View" link (same Review page, but action buttons will be hidden by the page since status is not Pending).

---

## 5. Pages/Manager/Review.cshtml

**Route:** `@page "{id:int}"`

**Purpose:** Manager views a request in full and takes action (Approve/Reject/Return).

**Model properties:**
- `Model.Request` (TravelRequest?)
- `Model.Comments` (string?) — bound via `asp-for`
- `Model.ErrorMessage` (string?)

**Layout:**
```
<h2>Review Travel Request</h2>
<a asp-page="/Manager/Index" class="btn btn-secondary mb-3">← Back to Requests</a>

[If ErrorMessage: danger alert]

[Card: Employee name, Destination, Dates, Purpose, Status badge, Submitted date]

<!-- Documents -->
<h4>Documents</h4>
[Same as Detail page pattern]

<!-- Audit Trail -->
<h4>Audit Trail</h4>
[Same as Detail page pattern]

<!-- Action section — only show if Status == Pending -->
[If Model.Request?.Status == TravelRequestStatus.Pending:]
<h4>Decision</h4>
<div class="mb-3">
  <label asp-for="Comments">Comments (optional)</label>
  <textarea asp-for="Comments" class="form-control" rows="3"></textarea>
</div>
<div class="d-flex gap-2">
  <form method="post" asp-page-handler="Approve" asp-route-id="@Model.Request.Id">
    <input type="hidden" asp-for="Comments" />
    <button type="submit" class="btn btn-success">✓ Approve</button>
  </form>
  <form method="post" asp-page-handler="Reject" asp-route-id="@Model.Request.Id">
    <input type="hidden" asp-for="Comments" />
    <button type="submit" class="btn btn-danger">✗ Reject</button>
  </form>
  <form method="post" asp-page-handler="Return" asp-route-id="@Model.Request.Id">
    <input type="hidden" asp-for="Comments" />
    <button type="submit" class="btn btn-warning">↩ Return for More Info</button>
  </form>
</div>
```

**Implementation note on Comments across 3 forms:** Since Comments is a shared textarea and each action is a separate `<form>`, pass Comments via hidden inputs in each form — OR restructure as a single form with multiple submit buttons using `asp-page-handler`. Recommend: single `<form>` with three submit buttons using the `name="handler"` pattern (asp-page-handler on button), which avoids duplicating the Comments textarea:
```html
<form method="post" asp-route-id="@Model.Request.Id">
  <textarea asp-for="Comments" ...></textarea>
  <button type="submit" asp-page-handler="Approve" class="btn btn-success">Approve</button>
  <button type="submit" asp-page-handler="Reject" class="btn btn-danger">Reject</button>
  <button type="submit" asp-page-handler="Return" class="btn btn-warning">Return</button>
</form>
```

---

## Namespaces / Using Directives

Ensure `_ViewImports.cshtml` already has:
```
@using TravelRequestWF.Infrastructure.Entities
@using TravelRequestWF.Infrastructure.Enums  (or wherever TravelRequestStatus lives)
```

If not, add them. Do NOT add `@using` inline on every page — use `_ViewImports.cshtml`.

---

## Coordination with Gandalf

- Handler names: `"Approve"`, `"Reject"`, `"Return"` → `OnPostApproveAsync`, `OnPostRejectAsync`, `OnPostReturnAsync`
- Handler name: `"Resubmit"` → `OnPostResubmitAsync`
- Property names are fixed as listed in each section above.
- If you encounter a property name that doesn't match what Gandalf implemented, flag it immediately rather than silently renaming.

---

## Final Check

After markup is done: run `dotnet build` (once Gandalf's code-behind is also in place). Zero errors expected. The markup pages should compile against the PageModel properties Gandalf defined.
