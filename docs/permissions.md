# Project Detail module permissions

Who may **view** each Project Detail module and **add / edit / approve / reject / complete / order / receive** in it is stored in the database and edited on the **Module Access** screen (Configuration → Module Access). Delete rights are **not** part of this system: every delete endpoint and delete button keeps its original hard-coded check.

## Modules and actions

| Code | Module | Actions |
|---|---|---|
| EPM | Estimate & Payment Terms | VIEW, ADD, EDIT, APPROVE, REJECT |
| F3D | Final 3D | VIEW, ADD, EDIT, APPROVE, REJECT |
| DRW | Drawings | VIEW, ADD, EDIT, APPROVE, REJECT |
| WCLTSC | Client Scope of Work & Material Specifications | VIEW, ADD, EDIT, APPROVE, REJECT |
| MB | Mood Board | VIEW, ADD, EDIT, APPROVE, REJECT |
| SITE | Site Updates (Daily Update) | VIEW, ADD, EDIT |
| WPEND | Pending Selection & Work | VIEW, ADD, EDIT, COMPLETE, REJECT |
| MPRO | Material Procurement | VIEW, ADD, EDIT, ORDER, REJECT, RECEIVE |
| WCOMP | Work Completion Report | VIEW, ADD, EDIT, APPROVE, REJECT |

F3D, DRW and MB keep only the latest version, so a re-upload is an EDIT.

## Subjects

| Login | Subject type | Subject code |
|---|---|---|
| Vendor | ACTOR | VENDOR |
| Customer | ACTOR | CUSTOMER |
| Employee | DESIGNATION | `LOV.DesignationCode` of the employee's USER_TYPE: MANAGER, COORDINATOR, SITE_SUPERVISOR, SITE_ENGINEER, PURCHASER, ARCHITECT, SITE_VENDOR, OTHER |
| Admin / Super Admin | none | no module rights (same as before) |

If a USER_TYPE row has no `DesignationCode`, the old description rules (`ProjectAccess.ResolveDesignation`) are used and a warning is logged.

## Tables (scripts in `Infra/Sql`)

| Table | Purpose |
|---|---|
| `ProjectModule` | The 9 modules (`IsActive = 0` hides a module from everyone) |
| `PermissionAction` | VIEW, ADD, EDIT, APPROVE, REJECT, COMPLETE, ORDER, RECEIVE |
| `ProjectModuleAction` | Which actions exist per module (columns of the matrix) |
| `ProjectModulePermission` | One row per subject × module × action. `VendorId NULL` = system default, otherwise that vendor's override. `IsAllowed`, `Scope` (`ALL` / `OWN`). **Missing row = not allowed.** |
| `PermissionVersion` | Single row, bumped on every change; logged-in users rebuild their rights on the next request (cached 60 s per server) |
| `PermissionAudit` | Old / new value of every changed cell, who and when |
| `LOV.DesignationCode` | Canonical designation of each USER_TYPE row |
| `SchemaScriptLog` | Which numbered scripts have run |

Effective right = vendor override (if active) else system default else denied.
`Scope = OWN` is honoured for SITE EDIT (only entries the user created or last modified); the seed uses `ALL` everywhere.

`Role.ProjectDetailTypeAccess` and `LOV.ProjectDetailTypeAccess` are deprecated (columns kept; Legacy mode still reads the LOV one).

## Mode (`appsettings` → `Permissions:Mode`)

| Mode | Decides | Use |
|---|---|---|
| `Legacy` (default in `appsettings.json`) | Old hard-coded rules | Production until Shadow is clean |
| `Shadow` | Old rules; database rights are evaluated too and every difference is logged (`Permissions - Shadow mismatch`, once per request and check) | At least one week in production |
| `Dynamic` (`appsettings.Development.json`) | Database rights | Target |

Rollback = set `Legacy`; no schema rollback is needed.

All in-scope decisions go through `ProjectAccess.Check(legacyResult, module, action)`; views and controllers use `ProjectAccess.CanView(module)` for box / endpoint visibility.

### Intentional differences in Dynamic mode
1. `Save_Doc_Status` type WC (work completion approve / reject) requires WCOMP APPROVE / REJECT (Customer). Before, there was no role check.
2. Daily Update page, list (`GetData`), form and image download require SITE VIEW on the server. Before, only the Project Detail box was hidden.
3. F3D / DRW / MB screen: everyone with EDIT sees both the edit pencil (not Customer) and "Upload new version". Before, Vendor / Manager saw only the pencil and Co-ordinator / Site Engineer / Architect only the button. The server already allowed both.

Expected Shadow log entries caused by (3): `F3D|DRW|MB.EDIT` for Co-ordinator, Site Engineer, Architect, Customer, Vendor and Manager on the document screen.

## Local database

```powershell
.\Infra\Sql\run-local.ps1                         # apply new scripts to LeozApp (skips applied ones)
.\Infra\Sql\run-local.ps1 -CreateTestDb -FromBackup "<path to LeozApp .bak>"   # (re)create LeozApp_Test
.\Infra\Sql\run-local.ps1 -BuildDeployAll         # regenerate deploy_all.sql after adding a script
dotnet test Leoz_25.Tests                          # needs LeozApp_Test
```

Scripts refuse any database other than `LeozApp` / `LeozApp_Test`, and skip themselves when already logged in `SchemaScriptLog`.

## Production deployment
1. Back up the production database.
2. Open `Infra/Sql/deploy_all.sql`, set `@Target` to the production database name, run the whole file (re-runnable). **Run it before deploying the code**: login reads `LOV.DesignationCode`.
3. Deploy the code with `Permissions:Mode = Legacy` (the host overrides `ConnectionStrings__DataConnection` through its environment settings).
4. Switch to `Shadow`, review the log for at least a week, fix rights on the Module Access screen.
5. Switch to `Dynamic`.
6. After two stable weeks, remove the legacy arguments of `ProjectAccess.Check`, the shadow helper and the legacy `ProjectAccess` members that only `Check` uses. **Keep** `CanModify`, `CanUpdateMaterial`, `Is(MANAGER)` and `HasModule`: delete endpoints still use them.

## Adding a module
1. New numbered script (e.g. `005_Module_XYZ.sql`, same header as the others): insert into `ProjectModule`, `ProjectModuleAction` (its actions; add a `PermissionAction` row first if the action is new), and the default `ProjectModulePermission` rows (`VendorId NULL`, allowed rows only); bump `PermissionVersion`; log in `SchemaScriptLog`.
2. Run `run-local.ps1`, then `run-local.ps1 -BuildDeployAll`.
3. Add the constant to `PermissionModules` and a box in `ProjectDetail.cshtml` guarded by `ProjectAccess.CanView("XYZ")`.
4. Guard its endpoints with `ProjectAccess.Can("XYZ", action)` (no legacy rule exists, so `Check` is not needed) plus the usual `IsProjectAllowed`.
5. The Module Access matrix shows the new row automatically.
