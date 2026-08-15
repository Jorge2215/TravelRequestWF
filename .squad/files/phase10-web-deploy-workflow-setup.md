# Phase 10 — Web Deploy Workflow Setup Guide

**File:** `.github/workflows/deploy-web.yml`  
**Purpose:** Automatically build and deploy `TravelRequestWF.Web` to Azure App Service on every push to `main`.

---

## Prerequisites

Before the pipeline will run successfully, you need to configure **one repo Variable** and **one repo Secret** in GitHub.

---

## Step 1 — Get the Publish Profile from the Azure Portal

1. Sign in to the [Azure Portal](https://portal.azure.com).
2. Navigate to your **App Service** resource (the Web App you just created for `TravelRequestWF.Web`).
3. In the top command bar, click **"Get publish profile"** — this downloads a `.PublishSettings` file to your computer.
4. Open that file in any text editor (Notepad, VS Code, etc.).
5. **Select all** and **copy** the entire XML content — you'll paste it as a GitHub secret in the next step.

---

## Step 2 — Add the Publish Profile as a GitHub Repo Secret

1. Go to your GitHub repository → **Settings** tab.
2. In the left sidebar, click **Secrets and variables** → **Actions**.
3. Make sure you're on the **Secrets** tab.
4. Click **"New repository secret"**.
5. Name: `AZURE_WEBAPP_PUBLISH_PROFILE`
6. Value: paste the full XML content you copied in Step 1.
7. Click **"Add secret"**.

---

## Step 3 — Add the App Service Name as a Repo Variable

1. Still in **Settings → Secrets and variables → Actions**.
2. Click the **Variables** tab.
3. Click **"New repository variable"**.
4. Name: `AZURE_WEBAPP_NAME`
5. Value: the exact name of your Azure App Service resource (e.g. `travelrequestwf-web`).  
   This is the name shown at the top of the App Service blade in the Azure Portal.
6. Click **"Add variable"**.

---

## Step 4 — Merge `dev` → `main` to Activate the Pipeline

The workflow file currently lives on `dev` (Merry authored it there, per team convention — `main` is Jorgito's merge target).

Once you're ready to go live:
- Merge the `dev` branch into `main` (or cherry-pick just the workflow file).
- Any subsequent push or merge to `main` will automatically trigger the workflow.

---

## What Happens on Each Push to `main`

```
push to main
    → checkout code
    → setup .NET 10 SDK
    → dotnet restore
    → dotnet build --configuration Release
    → dotnet publish src/TravelRequestWF.Web/TravelRequestWF.Web.csproj -c Release -o ./publish
    → azure/webapps-deploy@v3  (deploys ./publish to your App Service)
```

The deploy step uses the publish profile to authenticate with Azure — no service principal or Azure login step required.

---

## Checklist Before Going Live

- [ ] Azure App Service created (App Service Plan + Web App targeting .NET 10)
- [ ] `AZURE_WEBAPP_NAME` repo variable set
- [ ] `AZURE_WEBAPP_PUBLISH_PROFILE` repo secret set
- [ ] App Service Configuration (Application Settings) contains all required keys:
  - `ConnectionStrings:DefaultConnection` — Azure SQL connection string
  - `AzureStorage:ConnectionString` — Azure Storage connection string
  - `PowerAutomate:FlowASubmissionUrl` — real Flow A URL
  - `PowerAutomate:FlowBStatusChangeUrl` — real Flow B URL
- [ ] Pending EF Core migrations applied to Azure SQL (see Aragorn's Phase 10 audit)
- [ ] `dev` merged into `main` to activate the pipeline

---

*Created by Merry — 2026-08-15 | Phase 10*
