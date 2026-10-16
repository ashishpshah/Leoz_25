# Project Detail permissions: UAT checklist

Run with `Permissions:Mode = Dynamic` and the seeded defaults (no vendor overrides). Every row shows the **expected** result, which equals the old behaviour except where marked ★ (intentional change, see `docs/permissions.md`).
Log in as: Vendor, Customer, and one Employee per designation (Manager, Co-ordinator, Site Supervisor, Site Engineer, Purchaser, Architect, Site Vendor). The employee must be mapped to the project.

Legend: ✔ visible / allowed, ✘ hidden / denied.

## 1. Project Detail boxes (VIEW)

| Box | Vendor | Customer | Manager | Co-ord | Site Sup | Site Eng | Purchaser | Architect | Site Vendor |
|---|---|---|---|---|---|---|---|---|---|
| EPM | ✔ | ✔ | ✘ | ✘ | ✘ | ✘ | ✘ | ✘ | ✘ |
| F3D, DRW | ✔ | ✔ | ✔ | ✔ | ✔ | ✔ | ✘ | ✔ | ✔ |
| WCLTSC, MB | ✔ | ✔ | ✔ | ✔ | ✔ | ✔ | ✘ | ✔ | ✘ |
| SITE, WPEND, WCOMP | ✔ | ✔ | ✔ | ✔ | ✔ | ✔ | ✘ | ✘ | ✘ |
| MPRO | ✔ | ✔ | ✔ | ✔ | ✔ | ✔ | ✔ | ✘ | ✘ |

(Employee rows come from LOV `ProjectDetailTypeAccess` of the local database; check the Module Access screen if your data differs.)

## 2. Document modules (EPM, F3D, DRW, WCLTSC, MB)

| Check | Expected |
|---|---|
| "Add new record" EPM / DRW / F3D / MB (no file yet) | Vendor, Customer, Manager, Co-ord, Site Eng, Architect |
| "Add new record" WCLTSC | Vendor only |
| Edit pencil EPM / WCLTSC (uploaded, latest) | Vendor, Manager (not Customer: UI rule unchanged) |
| F3D / DRW / MB pencil and "Upload new version" ★ | everyone with EDIT: pencil (not Customer) and button |
| Approve / Reject buttons (status Uploaded) | Vendor, Customer, Manager, Co-ord |
| Approved / Rejected record | cannot be edited (message) |
| Delete button | unchanged |

## 3. Site Updates (Daily Update)

| Check | Expected |
|---|---|
| Daily Update menu page | opens for everyone with SITE VIEW; others are redirected to the dashboard ★ |
| "Add new record" button | Co-ordinator only |
| Action (pencil) column | Co-ordinator only |
| Save new / edited entry | Co-ordinator of the vendor, active, mapped to the project |
| Co-ordinator not mapped to the project, inactive, or of another vendor | denied |
| Edit entry with a changed `ProjectId` (dev tools) | "You do not have access…" |
| Images (eye) / file download | everyone with SITE VIEW and access to the project ★ |
| Direct URL `Project/GetData`, `Partial_AddEditForm_DailyUpdate`, `ViewDocument_DailyUpdateFile` as an employee without SITE | empty / nothing / 404 ★ |
| Delete file button | unchanged |

## 4. Pending Selection & Work (WPEND)

| Check | Expected |
|---|---|
| "Add new record" | Vendor, Manager, Co-ord, Site Eng, Architect (never Customer) |
| Edit pencil | Manager (employee table) |
| Complete / Reject | Vendor, Customer, Manager |
| Delete button | unchanged (Manager) |

## 5. Material Procurement (MPRO)

| Check | Expected |
|---|---|
| "Add new record" | everyone except Architect and Purchaser |
| Order quantity, Status (Ordered / Rejected), edit pencil | Manager |
| Receive button / Receive quantity | Co-ordinator |
| Delete button | unchanged (Manager) |

## 6. Work Completion Report (WCOMP)

| Check | Expected |
|---|---|
| "Add new record", edit pencil | Manager |
| Approve form / Approve, Reject buttons | Customer |
| Approve / Reject by direct call to `Save_Doc_Status?Type=WC` as Manager / Vendor | denied ★ |
| Delete button | unchanged (Manager, Pending) |

## 7. Module Access screen

| Check | Expected |
|---|---|
| Super Admin / Admin | all 10 subjects; edits change the system default |
| Vendor | designations only; edits apply to own company; "Reset to default" removes them |
| Customer / Employee | menu not shown; direct URL redirects to dashboard |
| Change a right, then reload the affected user's Project Detail (no re-login) | new right applies within 60 s |
| Phone width (< 768 px) | one card per module, toggles per action, no horizontal page scroll |
| Unsaved change + switch subject | confirmation asked |
| `PermissionAudit` | one row per changed cell |
