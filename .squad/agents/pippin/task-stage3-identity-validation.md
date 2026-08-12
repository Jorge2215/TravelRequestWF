# Pippin — Task Brief: Stage 3 Validation — Auth & Role-Based Access

**Assigned to:** Pippin (Tester)  
**Stage:** 3 — Authentication & Roles  
**Branch:** `dev` (read-only testing; no code changes unless trivially fixing a broken test assertion)  
**Date assigned:** 2026-08-11T23:06:00-03:00  
**Start condition:** Gandalf AND Legolas have both completed their tasks and pushed to `dev`.

---

## Context

Stage 3 adds ASP.NET Identity with local accounts. The seeded test users are:

| Email | Password | Role |
|---|---|---|
| employee1@test.com | Employee1!Pass | Employee |
| employee2@test.com | Employee2!Pass | Employee |
| manager1@test.com | Manager1!Pass | Manager |
| manager2@test.com | Manager2!Pass | Manager |

Pages requiring authentication:
- `/Employee/Index`, `/Employee/Submit`, `/Employee/Detail` — requires role **Employee**
- `/Manager/Index`, `/Manager/Review` — requires role **Manager**

Cookie config: `LoginPath = /Account/Login`, `AccessDeniedPath = /Account/AccessDenied`

---

## Your Tasks

### 1. Pull Latest dev & Build

```bash
git checkout dev
git pull origin dev
dotnet build TravelRequestWF.sln
```

Confirm build succeeds before testing.

### 2. Start the App

```bash
cd TravelRequestWF.Web
dotnet run
```

Note the URL (likely `https://localhost:5001` or `http://localhost:5000`).

### 3. Execute All Test Cases

Work through every test case in the table below. Record actual results.

### 4. Produce Test Results Document

Write your documented test results to `.squad/files/stage3-auth-test-results.md`.

---

## Test Cases

### TC-01: Unauthenticated access to Employee page
- **Steps:** Open browser, navigate to `/Employee/Index` without logging in
- **Expected:** Redirect to `/Account/Login?ReturnUrl=%2FEmployee%2FIndex`
- **Pass criteria:** Login page is displayed; `/Employee/Index` not accessible

### TC-02: Unauthenticated access to Manager page
- **Steps:** Navigate to `/Manager/Index` without logging in
- **Expected:** Redirect to `/Account/Login`
- **Pass criteria:** Login page is displayed

### TC-03: Login with Employee credentials
- **Steps:** Navigate to `/Account/Login`, enter `employee1@test.com` / `Employee1!Pass`, submit
- **Expected:** Redirect to `/` (or returnUrl); navbar shows "Hello, employee1@test.com" with "Employee" badge
- **Pass criteria:** Successful login, correct display name

### TC-04: Login with invalid credentials
- **Steps:** Navigate to `/Account/Login`, enter `employee1@test.com` / `WrongPassword`, submit
- **Expected:** Login page redisplays with error "Invalid credentials" (or equivalent)
- **Pass criteria:** No redirect; error message visible

### TC-05: Employee can access Employee pages
- **Steps:** Login as employee1@test.com, navigate to `/Employee/Index`, `/Employee/Submit`
- **Expected:** Pages load successfully (200 OK)
- **Pass criteria:** No redirect, pages render

### TC-06: Employee cannot access Manager pages
- **Steps:** Login as employee1@test.com, navigate to `/Manager/Index`
- **Expected:** Redirect to `/Account/AccessDenied` (or 403)
- **Pass criteria:** AccessDenied page shown; Manager content not visible

### TC-07: Login with Manager credentials
- **Steps:** Logout, navigate to `/Account/Login`, enter `manager1@test.com` / `Manager1!Pass`, submit
- **Expected:** Redirect to `/`; navbar shows "Hello, manager1@test.com" with "Manager" badge
- **Pass criteria:** Successful login, correct role badge

### TC-08: Manager can access Manager pages
- **Steps:** Login as manager1@test.com, navigate to `/Manager/Index`, `/Manager/Review`
- **Expected:** Pages load (200 OK)
- **Pass criteria:** No redirect

### TC-09: Manager cannot access Employee pages
- **Steps:** Login as manager1@test.com, navigate to `/Employee/Submit`
- **Expected:** Redirect to `/Account/AccessDenied`
- **Pass criteria:** AccessDenied page shown

### TC-10: Logout flow
- **Steps:** Login as any user, click Logout button in nav
- **Expected:** Session cleared; redirect to `/` or login page; navbar shows Login/Register links
- **Pass criteria:** No longer authenticated; restricted pages redirect to login again

### TC-11: Registration creates Employee-role user
- **Steps:** Navigate to `/Account/Register`, register new user `newuser@test.com` / `NewUser1!Pass`
- **Expected:** Account created, logged in automatically, role = Employee (default for self-registration)
- **Pass criteria:** Navbar shows user logged in with Employee badge; can access `/Employee/Index`

### TC-12: Return URL preserved after login
- **Steps:** (Unauthenticated) navigate to `/Employee/Submit` → redirected to Login → login as employee1@test.com
- **Expected:** Redirected back to `/Employee/Submit` after successful login
- **Pass criteria:** ReturnUrl parameter honored

### TC-13: employee2@test.com login
- **Steps:** Login as employee2@test.com / Employee2!Pass
- **Expected:** Successful login, Employee role
- **Pass criteria:** Same as TC-03

### TC-14: manager2@test.com login
- **Steps:** Login as manager2@test.com / Manager2!Pass
- **Expected:** Successful login, Manager role
- **Pass criteria:** Same as TC-07

---

## Output Document Format

Write `.squad/files/stage3-auth-test-results.md` with this structure:

```markdown
# Stage 3 Auth — Test Results

**Tester:** Pippin  
**Date:** <date>  
**Build:** dev branch, commit <sha>  
**App URL:** <url used>

## Summary

| Total | Passed | Failed | Blocked |
|---|---|---|---|
| 14 | ? | ? | ? |

## Results

| TC | Description | Expected | Actual | Status | Notes |
|---|---|---|---|---|---|
| TC-01 | Unauthenticated → Employee page | Redirect to Login | ... | PASS/FAIL | |
...
```

For any FAIL, include:
- Exact actual behavior observed
- Screenshot description or error message text
- Whether it's a Gandalf issue (backend) or Legolas issue (UI)

---

## Acceptance Criteria

- [ ] All 14 TCs executed and documented
- [ ] TC-01 through TC-10 all PASS (core auth flows)
- [ ] TC-11 through TC-14 PASS (all seeded users + registration)
- [ ] Results file written to `.squad/files/stage3-auth-test-results.md`
- [ ] Any failures clearly attributed (Gandalf/Legolas/config) with enough detail to fix

---

## If You Find Bugs

- Document in the results file with repro steps
- Do NOT fix Gandalf's backend code directly
- Do NOT fix Legolas's UI code directly
- Flag clearly so the responsible agent can fix and you re-test
