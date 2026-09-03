# HRMS System – Change Log

> **Last updated:** 2026-08-24  
> **Branch:** main  
> **Stack:** ASP.NET Core 8, Razor Pages, Entity Framework Core, ASP.NET Core Identity, Clean Architecture

---

## Architecture Overview

The project follows Clean Architecture with four layers:

| Layer | Project | Purpose |
|---|---|---|
| Domain | `HRMS.Domain` | Entities, no dependencies |
| Application | `HRMS.Application` | Business logic, services, CQRS commands/queries, ViewModels |
| Infrastructure | `HRMS.Infrastructure` | EF Core DbContext, Identity (`ApplicationUser`), Migrations |
| UI | `HRMS.UI` | Razor Pages — thin PageModels only, no business logic |

---

## Change 1 — Clean Architecture Setup (CQRS for Settings)

**Files created in `HRMS.Application`:**

```
HRMS.Application/Common/Result.cs
HRMS.Application/Branches/Commands/CreateBranchCommand.cs
HRMS.Application/Branches/Commands/CreateBranchCommandHandler.cs
HRMS.Application/Branches/Commands/EditBranchCommand.cs
HRMS.Application/Branches/Commands/EditBranchCommandHandler.cs
HRMS.Application/Departments/Commands/CreateDepartmentCommand.cs
HRMS.Application/Departments/Commands/CreateDepartmentCommandHandler.cs
HRMS.Application/Departments/Commands/EditDepartmentCommand.cs
HRMS.Application/Departments/Commands/EditDepartmentCommandHandler.cs
HRMS.Application/Designations/Commands/CreateDesignationCommand.cs
HRMS.Application/Designations/Commands/CreateDesignationCommandHandler.cs
HRMS.Application/Designations/Commands/EditDesignationCommand.cs
HRMS.Application/Designations/Commands/EditDesignationCommandHandler.cs
HRMS.Application/Entity/Commands/ICommand.cs
HRMS.Application/Entity/Commands/ICommandHandler.cs
HRMS.Application/Entity/Queries/IQuery.cs
HRMS.Application/Entity/Queries/IQueryHandler.cs
```

**Pattern used:**
```csharp
// Interface
public interface ICommand<TResult> { }
public interface ICommandHandler<TCommand, TResult> where TCommand : ICommand<TResult>
{
    Task<TResult> HandleAsync(TCommand command);
}

// Result wrapper
public record Result(bool Success, string? Error = null);
```

Settings PageModels (`Settings/Branches/Create`, `Edit`, `Settings/Departments/...`, `Settings/Designations/...`) now call command handlers via DI instead of containing inline EF Core logic.

---

## Change 2 — Business Logic Migrated to Application Layer (Services)

### 2a. ViewModels moved out of UI

**Deleted from `HRMS.UI/Models/`:**
- `ResignationRequestViewModel.cs`
- `TransferRequestViewModel.cs`
- `TerminationRequestViewModel.cs`
- `DeathRequestViewModel.cs`

**Created in `HRMS.Application/Models/`** (same content, namespace changed):
- `ResignationRequestViewModel.cs` — namespace `HRMS.Application.Models`
- `TransferRequestViewModel.cs` — namespace `HRMS.Application.Models`
- `TerminationRequestViewModel.cs` — namespace `HRMS.Application.Models`, includes `TerminationStatusEnum`
- `DeathRequestViewModel.cs` — namespace `HRMS.Application.Models`

### 2b. Services moved out of UI

**Deleted from `HRMS.UI/Services/`:**
- `NotificationService.cs`
- `ResignationService.cs`
- `TransferRequestService.cs`
- `TerminationService.cs`
- `DeathService.cs`

**Created in `HRMS.Application/Services/`** (identical logic, namespace changed):
- `NotificationService.cs` — namespace `HRMS.Application.Services`
- `ResignationService.cs` — namespace `HRMS.Application.Services`
- `TransferRequestService.cs` — namespace `HRMS.Application.Services`
- `TerminationService.cs` — namespace `HRMS.Application.Services`, **new method added:**
  ```csharp
  public (bool Valid, string? Error) ValidateTerminationDates(
      DateTime? initiationDate, DateTime? effectiveDate)
  ```
- `DeathService.cs` — namespace `HRMS.Application.Services`

### 2c. Validation attribute moved

**Created:**
- `HRMS.Application/Validation/FutureDateAttribute.cs` — namespace `HRMS.Application.Validation`

### 2d. Namespace references updated across all files

- **Bulk replaced** in all `.cs` and `.cshtml` files:
  - `HRMS.UI.Models` → `HRMS.Application.Models`
  - `HRMS.UI.Services` → `HRMS.Application.Services`
  - `HRMS.UI.Validation` → `HRMS.Application.Validation`
- **`HRMS.UI/Pages/_ViewImports.cshtml`** — changed `@using HRMS.UI.Models` → `@using HRMS.Application.Models`
- `.cshtml` views using fully-qualified `HRMS.UI.Models.TerminationStatusEnum` were updated to `HRMS.Application.Models.TerminationStatusEnum` (affected: `Termination/Details.cshtml`, `Termination/Requests.cshtml`, `Transfer/Separation.cshtml`)

---

## Change 3 — PageModels Slimmed Down

Business logic removed from three PageModels; they now delegate to Application layer services.

### `HRMS.UI/Pages/Resignation/Apply.cshtml.cs`

- **Removed:** inline 14-day notice period validation
- **Removed:** `SaveRequestAsync` private helper
- Both `OnPostSaveAsync` and `OnPostSubmitAsync` now call:
  ```csharp
  var (success, error, id) = await _resignationService.CreateResignationAsync(
      CurrentUser.Email!, CurrentUser.FullName, CurrentUser.EpfNumber,
      CurrentUser.Branch, CurrentUser.Department, CurrentUser.Designation,
      Input.ReasonForResignation, Input.EffectiveDate, Input.AdditionalRemarks,
      Input.HasOutstandingLoans, Input.IsLoanGuarantor, Input.HasOverridePermission,
      Input.ObligationDetails, submitNow: true/false);
  ```
- `UploadDocumentsAsync` kept in UI (IFormFile handling belongs in UI layer)

### `HRMS.UI/Pages/Transfer/Apply.cshtml.cs`

- **Removed:** inline date range validation (preferred date must be ≥ today, ≤ 90 days)
- **Removed:** same-branch check inline code
- **Removed:** TransferRequestViewModel construction inline
- `OnPostAsync` now calls:
  ```csharp
  var (success, error, _) = await _transferService.ApplyTransferAsync(
      targetEmail, Input.EmployeeName, Input.EpfNumber,
      Input.CurrentBranch, Input.CurrentDesignation, Input.Department,
      Input.RequestedBranch, Input.Reason, Input.PreferredDate,
      user.Email!, userRole, joiningDate,
      documentData, documentFileName, documentContentType);
  ```

### `HRMS.UI/Pages/Termination/CreateRequest.cshtml.cs`

- **Removed:** `ValidateDates()` private method
- **Removed:** `GetSelectedEmployeeAsync()` private method
- **Removed:** `ApplicationDbContext` dependency (constructor no longer needs it)
- Now calls:
  ```csharp
  var (valid, dateError) = _terminationService.ValidateTerminationDates(
      Input.InitiationDate, Input.EffectiveTerminationDate);
  if (!valid) { ModelState.AddModelError("Input.EffectiveTerminationDate", dateError!); return Page(); }
  ```

---

## Change 4 — Role-Based Access Control (Employee Management)

### `HRMS.UI/Pages/Employees/Create.cshtml.cs`

- **Changed authorization** from:
  ```csharp
  [Authorize(Roles = "HR Manager,Area Manager,Branch Manager")]
  ```
  to:
  ```csharp
  [Authorize(Roles = "HR Manager")]
  ```
- **Reason:** Only HR Managers should be able to create employee profiles.

### `HRMS.UI/Pages/Employees/Index.cshtml`

- **Create Employee button** — now hidden unless the user is HR Manager:
  ```html
  @if (User.IsInRole("HR Manager"))
  {
      <a asp-page="Create" ...>Create New Employee</a>
  }
  ```
- **"Drafted Records" tab** — now hidden from Area Manager and Branch Manager:
  ```html
  @if (!User.IsInRole("Area Manager") && !User.IsInRole("Branch Manager"))
  {
      <!-- Drafts tab nav + content -->
  }
  ```

### `HRMS.UI/Pages/Employees/Index.cshtml.cs`

Added **branch scoping** so each role only sees employees in their area:

```csharp
int? scopedBranchId = null;
List<int>? amBranchIds = null;
var currentUser = await _userManager.GetUserAsync(User);

if (User.IsInRole("HR Manager") && currentUser?.EmployeeId != null)
{
    var hrEmployee = await _context.Employees.FindAsync(currentUser.EmployeeId.Value);
    scopedBranchId = hrEmployee?.BranchId;
}
else if (User.IsInRole("Branch Manager") && !string.IsNullOrWhiteSpace(currentUser?.Branch))
{
    var branch = await _context.Branches.FirstOrDefaultAsync(b => b.Name == currentUser.Branch);
    scopedBranchId = branch?.Id;
}
else if (User.IsInRole("Area Manager") && !string.IsNullOrWhiteSpace(currentUser?.ManagedBranches))
{
    amBranchIds = currentUser.ManagedBranches
        .Split(',', StringSplitOptions.RemoveEmptyEntries)
        .Select(s => int.TryParse(s.Trim(), out var id) ? id : 0)
        .Where(id => id > 0).ToList();
}
```

The same filter is applied to:
- Active employees query
- Draft employees query
- Pending documents query

**Scope summary by role:**

| Role | Sees |
|---|---|
| HR Manager | Employees in their own branch (via `EmployeeId → Employee.BranchId`) |
| Branch Manager | Employees in their branch (via `ApplicationUser.Branch` name → `Branch.Id`) |
| Area Manager | Employees in all managed branches (via `ApplicationUser.ManagedBranches` CSV) |
| Admin | All employees globally |

### `HRMS.UI/Pages/Employees/Details.cshtml.cs`

Added **access guard** to prevent direct URL access to out-of-scope employee profiles:

```csharp
if (User.IsInRole("Area Manager"))
{
    var currentUser = await _userManager.GetUserAsync(User);
    var allowed = ParseManagedBranches(currentUser?.ManagedBranches);
    if (allowed != null && !allowed.Contains(emp.BranchId))
        return Forbid();
}
else if (User.IsInRole("Branch Manager"))
{
    var currentUser = await _userManager.GetUserAsync(User);
    if (!string.IsNullOrWhiteSpace(currentUser?.Branch) &&
        !string.Equals(emp.Branch?.Name, currentUser.Branch, StringComparison.OrdinalIgnoreCase))
        return Forbid();
}
```

Added helper:
```csharp
private static List<int>? ParseManagedBranches(string? csv)
```

---

## Change 5 — Dashboard Total Employees (Scoped by Role)

### `HRMS.UI/Pages/Index.cshtml.cs`

- **Added** `UserManager<ApplicationUser>` injection
- **Changed** `TotalEmployees` from a global count to a role-scoped count:
  - Uses the same branch-scoping pattern as `Employees/Index.cshtml.cs`
  - Duty accounts (`NIC = "DUTY-ACC"`) and draft employees (`Status = "Draft"`) are always excluded
  - Admin / unrecognised roles get the global count

```csharp
var countQuery = _context.Employees.Where(e => e.Status != "Draft" && e.NIC != "DUTY-ACC");
if (scopedBranchId.HasValue)
    countQuery = countQuery.Where(e => e.BranchId == scopedBranchId.Value);
else if (amBranchIds != null)
    countQuery = countQuery.Where(e => amBranchIds.Contains(e.BranchId));
TotalEmployees = await countQuery.CountAsync();
```

---

## Key Data Model Notes (for context)

### `ApplicationUser` (Identity)

| Property | Type | Used by |
|---|---|---|
| `EmployeeId` | `int?` | HR Manager — links to their `Employee` record to determine branch |
| `Branch` | `string` | HR Manager / Branch Manager — stores branch name as string |
| `ManagedBranches` | `string?` | Area Manager — CSV of branch IDs e.g. `"1,3,5"` |
| `FullName` | `string` | All roles |
| `EpfNumber` | `string` | All roles |
| `Designation` | `string` | All roles |
| `Department` | `string?` | All roles |

### `Employee` entity key fields

| Property | Notes |
|---|---|
| `BranchId` | FK to `Branch` |
| `Status` | `"Active"`, `"Draft"`, `"Terminated"`, etc. Draft employees in Employee table have `Status = "Draft"` |
| `NIC` | Duty accounts have `NIC = "DUTY-ACC"` — always excluded from counts |
| `EmployeeType` | `"Permanent"`, `"Probationary"`, `"Intern"` |
| `ProbationPeriodMonths` | Months; alert fires 30 days before end |
| `InternPeriodMonths` | Months; alert fires 30 days before end |

### `DraftEmployee` entity

Separate table from `Employee`. Records in this table are unconfirmed employee profiles. HR Managers see a "Drafted Records" tab; Area Managers and Branch Managers do not.

---

---

## Change 6 — Standardized Duty Accounts & Username/Email Architecture

### Key Implementations:
- **Unified Naming & Credentials System**:
  - **Admin**: `admin` / `admin@kanrich.lk`
  - **HR Manager**: `hrmanager` / `hrmanager@kanrich.lk`
  - **HR Officer**: `hro.<name>` / `hro.<name>@kanrich.lk` (e.g. `hro.perera`)
  - **Branch Manager**: `bm.<branch>` / `bm.<branch>@kanrich.lk` (e.g. `bm.colombo`)
  - **Area Manager**: `am.<area>` / `am.<area>@kanrich.lk` (e.g. `am.ratnapura`)
  - **Department Head**: `dh.<department><branch>` / `dh.<department><branch>@kanrich.lk` (e.g. `dh.financebalangoda`)
  - **Employees**: Username `<surname><initials>.<yy>` / Manual Email input (e.g. `pererajd@kanrich.lk`)
- **Duty Account UI Simplification**:
  - Removed Email displays from Duty Account lists and edit forms in `Pages/Admin/DutyAccounts/`.
  - Excluded duty accounts (`NIC == "DUTY-ACC"`) and management duty roles from employee-facing lists, transfer processes, and directory metrics.

---

## Change 7 — Multi-Stage Transfer Pipeline & Role Scoping

### Pipeline Definition:
1. **Stage 1 (Application / Initiation)**:
   - Self-application at `/Transfer/Apply` (restricted exclusively to `Employee` role).
   - HR initiation at `/HRManager/InitiateTransfer` (restricted to `HR Manager` & `HR Officer`).
2. **Stage 2 (Department Head Review)**:
   - Current Department Head reviews and approves at `/DepartmentHead/ReviewTransfer`. Status $\rightarrow$ `DeptHeadApproved`.
3. **Stage 3 (Dual Branch Managers Parallel Review)**:
   - Current Branch Manager reviews at `/BranchManager/ReviewTransfer` (`CurrentBMApproved`).
   - Target Branch Manager reviews at `/BranchManager/ReviewTransfer` (`TargetBMApproved`).
   - Both approve $\rightarrow$ Status becomes `BothBMsApproved`.
4. **Stage 4 (Current Area Manager Review)**:
   - Current Area Manager reviews at `/AreaManager/ReviewTransfer`. Status $\rightarrow$ `AreaManagerApproved`.
   - Inter-area rule: Only the originating Area Manager reviews.
5. **Stage 5 (Current Branch HR Officer Finalization)**:
   - Originating Branch HR Officer finalizes the transfer at `/HRManager/ReviewTransfer`. Status $\rightarrow$ `FullyApproved`.

---

## Change 8 — HR Officer Transfer Initiation Enhancements

### `Pages/HRManager/InitiateTransfer.cshtml` & `.cs`
- **Branch-First Selection**:
  - HR Officers are presented with their assigned branches (`ManagedBranches` / `Branch`) in the "Current Branch" dropdown.
  - Central HR Managers see all branches.
- **Dynamic Branch Employee Loading**:
  - Selecting a branch fires AJAX endpoint `OnGetEmployeesByBranchAsync(branchId)`.
  - Duty accounts (`NIC == "DUTY-ACC"` and administrative role users) are strictly filtered out.
- **EPF & Name Search Dropdown**:
  - Searchable dropdown menu allowing instant filtering by **EPF number** or **Employee Name**.
- **Employee Summary & Target Branch Filter**:
  - Live summary card displaying Avatar, Name, EPF, Designation, Department, Branch, and Service Duration.
  - Target Branch selection dynamically omits the employee's current branch.

---

## Change 9 — Current Branch HR Officer Finalization Enforcement

### `Pages/HRManager/ReviewTransfers.cshtml.cs` & `ReviewTransfer.cshtml.cs`
- **Queue Filtering**: `ReviewTransfersModel.OnGetAsync()` scopes `FinalizationQueue` so that `HR Officer` users only see transfer requests where `CurrentBranch` matches their assigned branches (`ManagedBranches` or `Branch`).
- **Clearance Validation**: `ReviewTransferModel.OnPostAsync()` enforces server-side validation ensuring that an `HR Officer` cannot approve/reject a transfer unless `CurrentBranch` is within their assigned branches.
- **HR Manager Oversight**: Central `HR Manager` retains global oversight to finalize any transfer company-wide.

---

## Change 10 — Authorization & View Fixes

### `Pages/Transfer/Details.cshtml.cs` & `Details.cshtml`
- Added `Department Head` and `HR Officer` to authorized roles in `DetailsModel.OnGetAsync()`, fixing the "Access Denied" error for Department Heads inspecting previously reviewed transfers.
- Added multi-identifier ownership checks (`UserName`, `Email`, `EPFNumber`, `FullName`) so employees can access `/Transfer/Details` and view their transfer progress stepper without HTTP 403 Forbidden errors.
- Made the "Back" button role-aware across all management dashboards.

---

## Change 11 — System Admin Separation Access Restriction

### Key Enforcement:
- Completely removed the `Admin` role from all Separation Management workflows, endpoints, and dashboards:
  - **Transfer**: `Transfer/Details`, `HRManager/InitiateTransfer`, `HRManager/ReviewTransfers`, `HRManager/ReviewTransfer`
  - **Termination**: `ApprovalQueue`, `CreateRequest`, `Details`, `EditRequest`, `Requests`, `ReviewTermination`, `TerminationReport`, `DownloadDocument`
  - **Resignation**: `ReviewResignations`, `ReviewResignation`
  - **Death Process**: `ReviewDeathRequests`, `ReviewDeath`
  - **Hub**: `Separation/Dashboard`
- Admin accounts are strictly focused on system configurations, settings, duty accounts, branch/dept administration, and leave/attendance settings.

---

## Change 12 — Fixed Employee List Loading in HR Officer Transfer Initiation

### `Pages/HRManager/InitiateTransfer.cshtml.cs` & `InitiateTransfer.cshtml`
- **Root Cause**:
  - `GetDutyAccountEmailsAsync()` was previously querying all user records with `EmployeeId == null` and treating their email as a duty account email, causing real employees whose identity accounts had `EmployeeId == null` to be filtered out.
  - In addition, the query was filtering strictly on `Status == "Active"`, which excluded employees with other valid non-draft statuses (e.g. `Permanent`, `Probationary`, `Confirmed`).
- **Fixes Applied**:
  - Replaced overly broad query with `GetDutyAccountExclusionsAsync()`, which scopes duty account exclusions strictly to users holding duty roles (`Admin`, `HR Manager`, `HR Officer`, `Branch Manager`, `Area Manager`, `Department Head`) and records with `NIC == "DUTY-ACC"`.
  - Updated the employee query across `OnGetEmployeesByBranchAsync`, `OnPostAsync`, and `LoadAssignedBranchesAndDataAsync` to retrieve all valid active employees (`e.Status != "Draft" && e.Status != "Terminated" && e.Status != "Resigned"`).
  - Enhanced client-side JavaScript in `bindEmployeeOptions` and `updateEmployeeSummary` to handle all property casings and ensure reliable rendering and summary card display upon selection.

---

## Change 13 — Employee Designation and Department Resolution & Dropdown Formatting

### `Pages/HRManager/InitiateTransfer.cshtml.cs` & `InitiateTransfer.cshtml`
- **Root Cause of "N/A" Display**:
  - If an employee record had `DesignationId == null` (or had not yet been formally linked to a foreign key record in the `Designations` table), the projection previously defaulted to `"N/A"`.
  - The client-side template string `${fullName} (${epf}) - ${designation}` then appended `- N/A` directly into the dropdown text and summary card.
- **Fixes Applied**:
  - Implemented automatic fallback resolution across `OnGetEmployeesByBranchAsync`, `LoadAssignedBranchesAndDataAsync`, and `OnPostAsync`: if `e.Designation` is null, the system automatically checks the user's `ApplicationUser.Designation` and `ApplicationUser.Department` properties.
  - Formatted the dropdown text dynamically: displays `${fullName} (${epf}) - ${designation}` when a designation is present, and gracefully formats as `${fullName} (${epf})` or `${fullName} (${epf}) - ${department}` without showing "N/A".
  - Formatted the employee summary card to display a clean default (e.g. `General Staff`) rather than raw "N/A".

---

## Change 14 — Mandatory Designation and Required Fields Enforcement on Employee Creation

### `Pages/Employees/Create.cshtml.cs` & `Create.cshtml`
- **Root Cause**:
  - `NewEmployee.DesignationId` was defined as a nullable `int?`, and `ModelState.Remove("NewEmployee.Designation")` removed navigation validation without checking if a valid non-zero `DesignationId` was provided.
  - Because `<form novalidate>` is present on the page, native browser validation on `<select required>` was not blocking submission unless explicitly evaluated in custom JavaScript.
- **Fixes Applied**:
  - **Server-Side Validation**: Enforced strict validation in `OnPostAsync()` for `DesignationId` (`!NewEmployee.DesignationId.HasValue || NewEmployee.DesignationId.Value <= 0`), `DepartmentId`, `BranchId`, `EmployeeType`, `Sex`, `FullName`, `Initials`, `ResidentialAddress`, `EPFNumber`, `ETFNumber`, `NIC`, and `DateJoined`.
  - **Client-Side Validation**: Added individual field-level error spans, live change/blur listeners, and pre-submit validation checks for `Designation` and all other compulsory fields, displaying clear error messages and preventing submission until a valid option is selected.
  - **Cascading Dropdowns on Reload**: Updated `LoadDropdownsAsync()` so that if validation fails on post, department and designation dropdowns remain populated according to the previously selected Branch and Department.

---

## Change 15 — Fixed Department Head Transfer Review Queue & Initial Status Labeling

### `Pages/DepartmentHead/ReviewTransfers.cshtml.cs`, `ReviewTransfer.cshtml.cs`, `TransferRequestService.cs`, `InitiateTransfer.cshtml.cs` & `Pages/Transfer/Details.cshtml`
- **Root Cause**:
  - **Queue Resolution**: When a Department Head logged in (e.g. `dh.financebalangoda`), `_userManager.GetUserAsync(User)` in cookie-based auth with custom usernames could fail to resolve the user or had empty `user.Branch` / `user.Department` properties. If empty, `GetRequestsForDeptHeadAsync("", "")` immediately returned 0 items.
  - **Scope Fallback**: If `user.Branch` or `user.Department` was not directly present on `ApplicationUser`, it was not falling back to the linked duty employee record (`user.EmployeeId` $\rightarrow$ `Employee.Branch.Name` / `Employee.Department.Name`).
  - **Detail Page Authorization**: `DepartmentHead/ReviewTransfer.cshtml.cs` previously performed exact case-sensitive string equality checks (`!=`), causing 404 Not Found on minor spacing or casing differences.
  - **Initiate Transfer Department Resolution**: If an existing employee record had an unlinked department navigation property, `DepartmentId` was not queried to resolve `Departments.FindAsync()`, which caused `Department` to be stored as `""` or `"General"` and stranded the request from matching the Department Head.
  - **Details Page Status Badge**: In `Pages/Transfer/Details.cshtml`, `TransferStatus.Pending` (the initial state awaiting Department Head) was mistakenly labeled `"Pending HR Review"` instead of `"Awaiting Department Head"`.
- **Fixes Applied**:
  - Added robust multi-step current user and branch/department scope resolution across `DepartmentHead/ReviewTransfers.cshtml.cs`, `ReviewTransfer.cshtml.cs`, and `TransferRequestService.cs` (falling back through `ApplicationUser`, username lookup, and `Employee.Branch`/`Employee.Department`).
  - Updated `GetRequestsForDeptHeadAsync` and `GetReviewedByDeptHeadAsync` to use case-insensitive, whitespace-trimmed matching so records are never skipped.
  - Updated `InitiateTransfer.cshtml.cs` to include `DepartmentId` and `DesignationId` with fallback DB entity lookup before submitting.
  - Updated `Pages/Transfer/Details.cshtml` status switch so `TransferStatus.Pending` displays `<span class="k-badge k-badge-pending">Awaiting Department Head</span>`.

---

## Change 16 — Scoped Designation Selection to Department & Complete Sri Lankan NIC Validation & Auto-Fill

### `Pages/Employees/Create.cshtml.cs` & `Pages/Employees/Create.cshtml`
- **Root Cause**:
  - **Designation Dropdown Scoping**: `OnGetDesignationsByDepartmentAsync()` and `LoadDropdownsAsync()` previously had a fallback (`if (!designations.Any())`) that loaded all company-wide designations whenever a department was selected or on initial form rendering.
  - **NIC Validation**: Previous validation only checked basic regex or flawed year string matching without full day-of-year and gender parsing according to official Sri Lankan NIC rules.
- **Fixes Applied**:
  - **Department-Scoped Designations**:
    - Updated `OnGetDesignationsByDepartmentAsync(int departmentId)` in `Create.cshtml.cs` to query strictly `_context.DepartmentDesignations.Where(dd => dd.DepartmentId == departmentId)` without returning global designations.
    - Updated `LoadDropdownsAsync()` in `Create.cshtml.cs` so `DesignationList` only loads designations assigned to `NewEmployee.DepartmentId`.
    - Updated `Create.cshtml` JavaScript cascade: when a department is selected, it fetches and populates only designations mapped to that specific department (`DepartmentDesignations`).
  - **Comprehensive Sri Lankan NIC Parsing & Validation (Client & Server)**:
    - Implemented full Sri Lankan NIC algorithm:
      - **Old Format (9 digits + `V`/`X`)**: First 2 digits = birth year (born before 2000 $\rightarrow$ `1900 + YY`), digits 3-5 = day of year from Jan 1st (`001-366` for Male, `501-866` for Female where `dayNum - 500` is the day count).
      - **New Format (12 digits)**: First 4 digits = full birth year (`YYYY`), digits 5-7 = day of year from Jan 1st (`001-366` for Male, `501-866` for Female).
    - **Calculates Exact Date of Birth & Gender**: Converts day of year (1-366) to exact calendar date `YYYY-MM-DD` and identifies gender.
    - **Smart Auto-Fill & Cross-Check**:
      - Automatically populates Date of Birth (`dobInput`) and Gender (`sexSelect`) upon entering/changing NIC.
      - Validates day digits range (`001-366` for Male / `501-866` for Female) and flags invalid days.
      - Accurately cross-checks user-entered Date of Birth and Gender against the parsed NIC values, displaying clear discrepancy messages if conflicting.
      - Mirror implementation added in server-side `Create.cshtml.cs` with `ParseSriLankanNic()` ensuring consistent backend enforcement.

---

## Change 17 — Fixed Employee Creation Form Validations, Cascades & Unmapped Fields

### `Pages/Employees/Create.cshtml.cs` & `Pages/Employees/Create.cshtml`
- **Root Cause**:
  - **JavaScript Parsing Runtime Error**: `parseSriLankanNIC()` had a casing error (`monthDays.Length` vs `monthDays.length`) which threw an unhandled exception when evaluating dates, halting all subsequent form validation handlers.
  - **Premature NIC Validation**: `validateNIC()` was executing immediately on every keystroke (`input` event), flashing red validation errors before users finished typing their 10 or 12 digit NIC numbers.
  - **Unmapped Field**: An extraneous `Date of Designation` input field was present in the form UI with a required marker, but had no backing database property on the `Employee` domain entity.
  - **Empty Dropdowns on Unmapped Branches**: When selecting a branch without explicit `BranchDepartments` records in the database, the department and designation dropdowns rendered completely blank instead of allowing available departments and designations.
- **Fixes Applied**:
  - **Fixed JavaScript Runtime & Validation Engine**: Corrected `monthDays.length` and streamlined all event listeners (`blur`, `input`, `change`, `submit`) ensuring every client-side validation executes smoothly without silent exceptions.
  - **Smooth Real-Time Auto-Fill**: Updated NIC input listeners to parse and auto-fill Date of Birth and Gender as soon as a 10 or 12 character string is entered, reserving error messages for field blur or final submit.
  - **Removed Unmapped `Date of Designation` Field**: Cleaned up the form layout to only include valid employee entity fields (`Date Joined` and `Date Confirmed`).
  - **Resilient Branch & Department Cascades**: Enhanced `OnGetDepartmentsByBranchAsync`, `OnGetDesignationsByDepartmentAsync`, and `LoadDropdownsAsync` to gracefully fall back and always load valid options so dropdowns never get stuck empty.

---

## Change 18 — Comprehensive Transfer Notifications Broadcasting for Approvers, Officers, and Employees

### `HRMS.Application/Services/TransferRequestService.cs`
- **Root Cause**:
  - In `TransferRequestService.cs`, notifications were previously dispatched only to the initiator (`request.RequestedBy`) or employee email upon final approval.
  - When a transfer request was initiated or progressed through stages (Stage 1 Creation $\rightarrow$ Stage 2 Dept Head $\rightarrow$ Stage 3 Branch Managers $\rightarrow$ Stage 4 Area Manager $\rightarrow$ Stage 5 HR Finalization), no notifications were generated for the next reviewing authority (e.g. Department Heads, Branch Managers, Area Managers, or HR Officers).
  - Reviewers had to manually browse their queue pages without receiving in-app notification bell alerts.
- **Fixes Applied**:
  - **Dynamic Role & Scope Resolvers**:
    - `GetDepartmentHeadUserIdentifiersAsync(branch, dept)`: Finds duty and employee accounts with `"Department Head"` role scoped to the exact branch and department.
    - `GetBranchManagerUserIdentifiersAsync(branch)`: Finds duty and employee accounts with `"Branch Manager"` role matching the specified branch.
    - `GetAreaManagerUserIdentifiersAsync(branch1, branch2)`: Finds `"Area Manager"` accounts assigned to the managed branches or company-wide.
    - `GetHROfficerUserIdentifiersAsync(branch1, branch2)`: Finds `"HR Officer"` accounts assigned to manage the origin or target branch.
    - `GetHRManagerUserIdentifiersAsync()`: Finds all `"HR Manager"` accounts.
    - `GetEmployeeUserIdentifiersAsync(email, epf, requestedBy)`: Finds user accounts matching the employee email, EPF number, or initiator username.
    - `SendNotificationsAsync(...)`: Safely dispatches in-app notifications with deduplicated recipients and direct action URLs.
  - **Full Workflow Multi-Recipient Notification Broadcasting**:
    - **Stage 1 (Creation)**: Dispatches notification to the responsible Department Head (`/DepartmentHead/ReviewTransfer/{id}`), assigned HR Officer(s), and the Employee.
    - **Stage 2 (Department Head Review)**:
      - *If Approved*: Alerts Current Branch Manager (`/BranchManager/ReviewTransfer/{id}`), Target Branch Manager (`/BranchManager/ReviewTransfer/{id}`), HR Officers, and the Employee.
      - *If Rejected*: Alerts the Employee and HR Officers with rejection reason.
    - **Stage 3 (Branch Manager Reviews)**:
      - *When Both BMs Approve*: Escalates notification to Area Manager (`/AreaManager/ReviewTransfer/{id}`), Department Head, both Branch Managers, HR Officers, and the Employee.
      - *When One BM Approves*: Alerts the other pending Branch Manager and HR Officers/Employee.
      - *When Either BM Rejects*: Alerts Department Head, other Branch Manager, HR Officers, and the Employee.
    - **Stage 4 (Area Manager Review)**:
      - *If Approved*: Alerts HR Managers / HR Officers (`/HRManager/ReviewTransfer/{id}`), Department Head, both Branch Managers, and the Employee.
      - *If Rejected*: Alerts Department Head, both Branch Managers, HR Officers, and the Employee.
    - **Stage 5 (HR Finalization)**:
      - *If Approved*: Alerts Employee, Department Head, Current & Target Branch Managers, Area Manager, and HR Officers.
      - *If Rejected*: Alerts Employee, Department Head, Branch Managers, Area Manager, and HR Officers.

---

## Change 19 — Resignation Workflow: Multi-Department Head Branch Approval Chain, Stepper & Notification System

### 1. Domain & Persistence (`HRMS.Domain` & `HRMS.Infrastructure`)
- **`HRMS.Domain/Entities/Resignation/ResignationDepartmentReview.cs` [NEW]**:
  - Entity tracking branch-specific Department Head clearance reviews: `ResignationRequestId`, `DepartmentName`, `ReviewerUserId`, `ReviewerName`, `ReviewerEmail`, `Status` ("Pending", "Approved", "Rejected"), `Comments`, `ReviewDate`.
- **`HRMS.Domain/Entities/Resignation/ResignationRequest.cs` [MODIFIED]**:
  - Added navigation collection `public ICollection<ResignationDepartmentReview> DepartmentReviews`.
  - Updated `ResignationStatus` enum: `Draft = 0`, `SubmittedForApproval = 1` (Pending Dept Heads), `DeptHeadRejected = 2`, `DeptHeadsApproved = 3` (Awaiting Branch Manager), `BMApproved = 4`, `BMRejected = 5`, `AMApproved = 6`, `AMRejected = 7`, `HRApproved = 8`, `HRRejected = 9`, `Completed = 10`.
- **`HRMS.Infrastructure/Persistence/ApplicationDbContext.cs` [MODIFIED]**:
  - Registered `DbSet<ResignationDepartmentReview> ResignationDepartmentReviews`.

### 2. Application Layer (`HRMS.Application`)
- **`HRMS.Application/Models/ResignationRequestViewModel.cs` [MODIFIED]**:
  - Added `ResignationDepartmentReviewViewModel` and collection property `DepartmentReviews`.
  - Added helper properties `TotalDeptHeadsCount`, `DeptHeadsApprovedCount`, `DeptHeadsPendingCount`, `DeptHeadsRejectedCount`, and `AreAllDeptHeadsApproved`.
  - Updated `ResignationStatusEnum` and status badges.
- **`HRMS.Application/Services/ResignationService.cs` [MODIFIED]**:
  - Implemented multi-stage approval logic:
    - `InitializeDepartmentReviewsAsync`: Automatically discovers all active departments associated with the branch (via `BranchDepartments` and active `Department Head` users in that branch) and initializes individual pending review records.
    - `GetPendingForDeptHeadAsync(branch, dept)` & `GetReviewedByDeptHeadAsync(branch, dept)`: Filters resignation requests in the Department Head's branch.
    - `DeptHeadReviewAsync`: Records the department's approval/rejection. If any Department Head rejects $\rightarrow$ status becomes `DeptHeadRejected`. When all Department Heads in the branch approve $\rightarrow$ transitions to `DeptHeadsApproved` and automatically notifies the Branch Manager.
    - `GetPendingForBranchManagerAsync(branch)`: Filters to requests where all Department Heads have approved.
    - `BranchManagerApproveAsync` & `BranchManagerRejectAsync`: Transitions status and notifies Area Managers, Department Heads, HR Officers, and Employee.
    - `GetPendingForAreaManagerAsync(managedBranchIds, branch)` & `AreaManagerApproveAsync`/`RejectAsync`: Transitions status and notifies assigned HR Officers, BM, DHs, and Employee.
    - `GetPendingForHRManagerAsync(managedBranchIds)` & `HRManagerApproveAsync`/`RejectAsync`: Enables assigned HR Officers (and HR Managers) to finalize resignations, generates the official acceptance letter, and prepares for account deactivation.
  - Implemented dynamic role/scope resolvers:
    - `GetDepartmentHeadUserIdentifiersAsync(branch, dept)`
    - `GetBranchManagerUserIdentifiersAsync(branch)`
    - `GetAreaManagerUserIdentifiersAsync(branch)`
    - `GetHROfficerUserIdentifiersAsync(branch)`
    - `GetHRManagerUserIdentifiersAsync()`
    - `GetEmployeeUserIdentifiersAsync(email, epf)`
    - `SendNotificationsAsync(...)` with direct URLs (`/DepartmentHead/ReviewResignation/{id}`, `/BranchManager/ReviewResignation/{id}`, `/AreaManager/ReviewResignation/{id}`, `/HRManager/ReviewResignation/{id}`, `/Resignation/Details/{id}`, `/Resignation/AcceptanceLetter/{id}`).

### 3. User Interface (`HRMS.UI`)
- **`Pages/DepartmentHead/ReviewResignations.cshtml` & `.cs` [NEW]**:
  - Resignation review queue for Department Heads showing pending and reviewed branch resignation requests with approval counters.
- **`Pages/DepartmentHead/ReviewResignation.cshtml` & `.cs` [NEW]**:
  - Resignation review detail page with employee details, obligations, reason, other Department Head reviews, documents, and an Approve/Reject form with mandatory comments.
- **`Pages/Separation/Dashboard.cshtml` [MODIFIED]**:
  - Added dedicated Department Head quick cards for Transfers (`/DepartmentHead/ReviewTransfers`) and Resignations (`/DepartmentHead/ReviewResignations`).
- **`Pages/BranchManager/ReviewResignations.cshtml.cs` & `ReviewResignation.cshtml` [MODIFIED]**:
  - Filtered to branch-specific requests and ensured requests are only reviewable when all Department Heads in the branch have approved (`DeptHeadsApproved`).
  - Added Department Head review summary cards.
- **`Pages/AreaManager/ReviewResignations.cshtml.cs` & `ReviewResignation.cshtml` [MODIFIED]**:
  - Filtered to `user.ManagedBranches` and displayed full Department Head and Branch Manager review cards.
- **`Pages/HRManager/ReviewResignations.cshtml.cs` & `ReviewResignation.cshtml` [MODIFIED]**:
  - Scoped to `user.ManagedBranches` for HR Officers (or global for HR Managers) and displayed all prior review feedback.
- **`Pages/Resignation/Details.cshtml` & `Details.cshtml.cs` [MODIFIED]**:
  - Enhanced 5-stage interactive Approval Timeline (Submitted $\rightarrow$ Department Heads $\rightarrow$ Branch Manager $\rightarrow$ Area Manager $\rightarrow$ HR Officer $\rightarrow$ Completed).
  - Added dedicated Department Head reviews breakdown showing each department's review status, reviewer name, date, and remarks.
- **`Pages/Transfer/Separation.cshtml` [MODIFIED]**:
  - Added direct "View Status" action link in the resignation table leading directly to `/Resignation/Details?id={id}`.
- **`Program.cs` [MODIFIED]**:
  - Added automatic MySQL table creation startup script for `ResignationDepartmentReviews` table with cascading foreign key referencing `ResignationRequests.Id`.
- **Query Optimization (`ResignationService.cs`)**:
  - Configured `.AsSplitQuery()` on all multi-collection queries including both `Documents` and `DepartmentReviews` to eliminate Cartesian product expansion and EF Core query warnings.

---

## Change 20 — Resolved Department Head Resignation Visibility, Auto-Repair & Separation Navigation Tabs

### Root Cause
1. **Department Head Sidebar Route Mismatch**: In `_Layout.cshtml`, the "Separation" sidebar item was hard-coded to route `Department Head` accounts to `/DepartmentHead/ReviewTransfers` (which only displayed transfers) instead of the central Separation Hub `/Separation/Dashboard`.
2. **Missing In-Page Separation Tabs**: When a Department Head was on the review pages, there was no tab navigation between **Transfers** and **Resignations**, making resignations inaccessible from the Separation tab.
3. **Empty Review Auto-Repair**: If a resignation request was submitted when `ResignationDepartmentReviews` had not yet been created, `r.DepartmentReviews` had 0 records, causing `r.DepartmentReviews.Any(...)` in `GetPendingForDeptHeadAsync` to evaluate to false for all Department Heads.

### Fixes Applied
1. **Separation Navigation Hub for Department Heads (`_Layout.cshtml`)**:
   - Updated the sidebar "Separation" link for `Department Head` to route to `/Separation/Dashboard` (the Separation Management Hub), aligning with all management roles.
2. **Unified Separation Tabs on Review Pages (`ReviewTransfers.cshtml` & `ReviewResignations.cshtml`)**:
   - Added instant top navigation tabs (**Transfers** & **Resignations**) with active states and a direct button back to the Separation Hub.
3. **Auto-Repair & Resilient Fuzzy Matching (`ResignationService.cs`)**:
   - Enhanced `GetPendingForDeptHeadAsync` to automatically detect and initialize branch department reviews for any pending resignation that lacks review records (`!r.DepartmentReviews.Any()`).
   - Added `MatchBranch` and `MatchDept` helpers to ensure robust matching across variations (e.g., "Finance" vs "Finance Department", "Balangoda" vs "Balangoda Branch").
   - Updated `ReviewResignations.cshtml.cs` to reliably retrieve pending and reviewed requests.
---

## Change 21 — Unified Separation Management Hub with Role-Tailored Tabs & Modern Review Experience

### Changes Implemented
1. **Unified Multi-Topic Separation Hub (`Pages/Separation/Dashboard.cshtml` & `Dashboard.cshtml.cs`)**:
   - Transformed `/Separation/Dashboard` into the full interactive tabbed Separation Hub matching the user's preferred layout.
   - Dynamically loads and switches between separation topics per role:
     - **Department Head**: `Transfers`, `Resignations`
     - **Branch Manager**: `Transfers`, `Resignations`, `Death Process`
     - **Area Manager**: `Transfers`, `Terminations`, `Resignations`
     - **HR Manager & HR Officer**: `Transfers`, `Terminations`, `Resignations`, `Death Process`
2. **Interactive Top Navigation Tabs & Quick Counts**:
   - Styled pill tabs with real-time pending approval counter badges.
   - Context-aware action buttons:
     - `+ Initiate Death Request` for Branch Managers, HR Managers, and HR Officers.
     - `+ Initiate Transfer` for HR Managers and HR Officers.
     - `+ Create Termination` for HR Managers and HR Officers.
3. **Pending Approvals Queue & Modern "All Caught Up!" Card**:
   - Renders pending requests table with employee avatars, designation, dates, status badges, and direct review buttons.
   - When no pending requests exist, renders the clean card with a green checkmark circle and informative message.
4. **Recently Reviewed Section**:
   - Displays historical decisions (`✓ Approved`, `✕ Rejected`), review dates, and direct links to view request details.

---

## Change 22 — Resolved Employee Summary Card Details on HR Initiate Transfer

### Root Cause
In `Pages/HRManager/InitiateTransfer.cshtml`, the client-side JavaScript function `updateEmployeeSummary(emp)` referenced `fullName` and `epf` without declaring them as variables (`const fullName = ...`, `const epf = ...`), causing a `ReferenceError` during selection and preventing the employee details from populating the summary box.

### Fix Applied
- Declared `fullName` and `epf` properly in `updateEmployeeSummary(emp)` in `Pages/HRManager/InitiateTransfer.cshtml`.
- Now, when an HR Manager or HR Officer selects an employee from the dropdown, their initials, full name, EPF, designation, department, branch, years of service, date joined, and email are populated immediately in the summary preview card.

---

## Change 23 — 5-Stage Termination Approval Workflow & Notification Engine

### Summary
Engineered the complete 5-Stage Termination Workflow mirroring the resignation clearance architecture, with multi-stage approval routing, department clearances per branch, role-based review interfaces, and an automated notification dispatch engine across all stages.

### Workflow Stages
1. **Stage 1 (HR Initiation)**: HR Officer/Manager initiates termination request via `/Termination/CreateRequest`. Automatically initializes clearance records for all departments in the employee's branch (`TerminationDepartmentReview`) and sends notification alerts to all branch Department Heads (`/DepartmentHead/ReviewTermination/{id}`).
2. **Stage 2 (Branch Department Heads Clearance)**: Parallel reviews by Department Heads in the employee's branch via `/DepartmentHead/ReviewTerminations` and `/DepartmentHead/ReviewTermination/{id}`. Once all Department Heads approve, status transitions to `DeptHeadsApproved` and notifies the Branch Manager.
3. **Stage 3 (Branch Manager Review)**: Branch Manager reviews via `/BranchManager/ReviewTerminations` and `/BranchManager/ReviewTermination/{id}`. Upon approval, transitions to `BMApproved` and notifies the Area Manager.
4. **Stage 4 (Area Manager Review)**: Area Manager reviews via `/AreaManager/ReviewTerminations` and `/AreaManager/ReviewTermination/{id}`. Upon approval, transitions to `AMApproved` and notifies the HR Officer.
5. **Stage 5 (HR Officer Finalization & Financial Clearance)**: Finalized via `/Termination/ReviewTermination?id={id}`. Completes financial clearance, marks employee record status as `Terminated` in the database, and broadcasts completion notifications to the employee and all stakeholders.

### Files Created / Modified
- **`HRMS.Domain/Entities/Termination/TerminationDepartmentReview.cs` [NEW]**: Entity modeling branch department clearances for termination requests.
- **`HRMS.Domain/Entities/Termination/TerminationRequest.cs` [MODIFIED]**: Added `DepartmentReviews` collection, BM/AM/HR review properties, and updated `TerminationRequestStatus` lifecycle enum.
- **`HRMS.Infrastructure/Persistence/ApplicationDbContext.cs` [MODIFIED]**: Added `DbSet<TerminationDepartmentReview> TerminationDepartmentReviews`.
- **`Program.cs` [MODIFIED]**: Added database auto-migration script to create `TerminationDepartmentReviews` table and ensure all stage columns exist on `TerminationRequests`.
- **`HRMS.Application/Models/TerminationRequestViewModel.cs` [MODIFIED]**: Added `TerminationDepartmentReviewViewModel`, progress counters, and status badges.
- **`HRMS.Application/Services/TerminationService.cs` [MODIFIED]**: Implemented 5-stage methods, branch department discovery, transition handlers, and notification dispatch helpers.
- **`Pages/DepartmentHead/ReviewTerminations.cshtml` & `.cs` [NEW]**: Department Head termination queue.
- **`Pages/DepartmentHead/ReviewTermination.cshtml` & `.cs` [NEW]**: Department Head termination decision interface.
- **`Pages/BranchManager/ReviewTerminations.cshtml` & `.cs` [NEW]**: Branch Manager termination queue.
- **`Pages/BranchManager/ReviewTermination.cshtml` & `.cs` [NEW]**: Branch Manager termination decision interface.
- **`Pages/AreaManager/ReviewTerminations.cshtml` & `.cs` [NEW]**: Area Manager termination queue.
- **`Pages/AreaManager/ReviewTermination.cshtml` & `.cs` [NEW]**: Area Manager termination decision interface.
- **`Pages/Termination/ReviewTermination.cshtml` & `.cs` [MODIFIED]**: HR Officer finalization & financial clearance interface.
- **`Pages/Termination/Details.cshtml` & `.cs` [MODIFIED]**: 5-stage progress timeline and branch department clearances status breakdown.
- **`Pages/Separation/Dashboard.cshtml` & `.cs` [MODIFIED]**: Integrated Terminations tab with role-tailored approval queues and links.

---

## Change 24 — Duty Account Exclusion & Branch-Scoped Employee Filtering on Termination Initiation

### Root Cause
In `Pages/Termination/CreateRequest.cshtml.cs`, `PopulateEmployeesAsync()` fetched all employees and all Identity accounts across the entire database without:
1. Excluding administrative/management duty accounts (roles: `Admin`, `HR Manager`, `HR Officer`, `Branch Manager`, `Area Manager`, `Department Head`, and `e.NIC == "DUTY-ACC"`).
2. Scoping the employee selection to the HR Officer's assigned branches (`user.ManagedBranches` or `user.Branch`).

### Fix Applied
- **`Pages/Termination/CreateRequest.cshtml.cs` [MODIFIED]**:
  - Implemented `GetDutyAccountExclusionsAsync()` to dynamically query and exclude all duty account IDs, emails, usernames, and EPF numbers.
  - Added branch-scoping logic: when logged in as an `HR Officer`, the query filters employees strictly to those belonging to the HR Officer's assigned branches.
  - Excluded employees with status `Draft`, `Terminated`, or `Resigned`.
  - Maintained complete independence from transfer and resignation files.

---

## Change 25 — EPF & Name Live Search Filter for Termination Request Initiation

### Summary
Added real-time employee search and instant live filtering by EPF number or employee name on the termination creation page.

### Fixes Applied
- **`Pages/Termination/CreateRequest.cshtml` [MODIFIED]**:
  - Added dedicated EPF & Name search input box with live matching badge (`X found`).
  - Added client-side fuzzy filter `filterEmployeeDropdown()` that automatically filters dropdown options by EPF number or employee name.
  - Auto-selects employee and displays the summary preview card immediately upon exact match on EPF or single search result.
  - Maintained complete independence from transfer and resignation files.

---

## Change 26 — Embedded Searchable Combobox Inside Employee Selection Dropdown

### Summary
Transformed the employee selection field on `/Termination/CreateRequest` into an embedded searchable combobox with an internal search bar located directly inside the dropdown popup menu.

### Fixes Applied
- **`Pages/Termination/CreateRequest.cshtml` [MODIFIED]**:
  - Implemented an embedded searchable dropdown popup with a sticky internal search box (`Type EPF or name...`).
  - Each item in the dropdown list displays the employee's initial avatar, full name, EPF badge, designation, department, and branch.
  - Features real-time filtering, keyboard navigation (Enter, Arrow keys, Escape), outside-click auto-close, and instant employee summary preview card updates.
  - Preserved the hidden native `<select>` for ASP.NET Razor model binding and validation.
  - Maintained complete independence from transfer and resignation files.

---

## Change 27 — Updated Navigation Route to Separation Hub for Termination Requests

### Summary
Updated the navigation routes, back links, and cancellation flows on the termination request pages to route directly back to the Separation Hub with the Terminations tab active (`/Separation/Dashboard?ActiveTab=Terminations`).

### Fixes Applied
- **`Pages/Termination/CreateRequest.cshtml` & `.cs` [MODIFIED]**:
  - Updated "Back to Separation Hub" link and Cancel button to navigate to `/Separation/Dashboard?ActiveTab=Terminations`.
  - Updated `OnPostSaveDraftAsync` and `OnPostSubmitAsync` redirect routes to `/Separation/Dashboard?ActiveTab=Terminations`.
- **`Pages/Termination/Details.cshtml` [MODIFIED]**:
  - Updated top back link to route back to `/Separation/Dashboard?ActiveTab=Terminations`.
- **`Pages/Termination/EditRequest.cshtml` [MODIFIED]**:
  - Updated back link to route back to `/Separation/Dashboard?ActiveTab=Terminations`.
  - Maintained complete independence from transfer and resignation files.

---

## Change 29 — Employee Death Process 3-Stage Workflow & Separation Hub Integration

### Summary
Implemented a clean 3-stage lifecycle for the employee death separation process with branch-scoped initiation, multi-tier reviews, multi-stakeholder notifications, and automatic system closure without touching transfer, resignation, or termination files:
1. **Branch Manager Initiation**: Initiates via `/DeathProcess/Apply` with embedded searchable dropdown (filtered to active branch employees, excluding duty accounts and departed employees). Submission immediately records Branch Manager approval and advances request directly to Area Manager review (`BMApproved`), notifying the Area Manager.
2. **Area Manager Review & Confirmation**: Reviews via `/AreaManager/ReviewDeath/{id}`. Can confirm/approve (`AMApproved`, notifying HR Manager) or reject (`AMRejected`, notifying Branch Manager).
3. **HR Manager Finalization**: Finalizes via `/HRManager/ReviewDeath/{id}`. Can finalize/complete (`Completed`), which deactivates the user identity login account, marks `Employee.Status = "Deceased"`, stops payroll, triggers finance clearance, and notifies both Branch Manager and Area Manager.
4. **Separation Hub Integration**: Updated `/Separation/Dashboard` with the "Death" tab for Branch Manager, Area Manager, and HR Manager/Officer, showing real-time role-tailored pending review queues with "Review →" / "Finalize →" actions and a comprehensive historical reviewed records table.

### Files Modified
- **`HRMS.Application/Services/DeathService.cs` [MODIFIED]**:
  - Configured `SubmitRequestAsync` so Branch Manager initiation directly marks status as `BMApproved` and dispatches notification to the Area Manager.
  - Implemented branch-scoped query methods: `GetAllPendingForAMAsync(branchIds, branchName)`, `GetReviewedForAMAsync(branchIds, branchName)`, `GetReviewedForBMAsync(branch)`, and `GetReviewedForHRAsync()`.
  - Implemented `AMApproveAsync` (transitions to `AMApproved` and notifies HR) and `AMRejectAsync` (transitions to `AMRejected` and notifies BM).
  - Implemented `HRManagerApproveAsync` (transitions to `Completed`, deactivates user login, sets `Employee.Status = "Deceased"`, stops payroll, triggers finance clearance, and notifies BM & AM) and `HRManagerRejectAsync`.
- **`Pages/DeathProcess/Apply.cshtml` & `.cs` [MODIFIED]**:
  - Scoped employee selection to the Branch Manager's assigned branch with duty account exclusions (`Admin`, `HR Manager`, `HR Officer`, `Branch Manager`, `Area Manager`, `Department Head`, and `NIC == "DUTY-ACC"`).
  - Added embedded searchable combobox inside the dropdown with live EPF/name filtering and auto-fill summary card.
  - Linked back and cancel actions to `/Separation/Dashboard?ActiveTab=Death`.
- **`Pages/AreaManager/ReviewDeath.cshtml` & `.cs` [MODIFIED]**:
  - Integrated 3-stage visual journey timeline, review confirmation form, and back/redirect routing to `/Separation/Dashboard?ActiveTab=Death`.
- **`Pages/HRManager/ReviewDeath.cshtml` & `.cs` [MODIFIED]**:
  - Integrated 3-stage visual journey timeline, HR finalization and system closure form, and back/redirect routing to `/Separation/Dashboard?ActiveTab=Death`.
- **`Pages/BranchManager/ReviewDeath.cshtml` & `.cs` [MODIFIED]**:
  - Updated route parameterization and back/redirect routing to `/Separation/Dashboard?ActiveTab=Death`.
- **`Pages/Separation/Dashboard.cshtml` & `.cs` [MODIFIED]**:
  - Added `"Death"` to `AvailableTabs` for Area Manager.
  - Populated `PendingDeathRequests` and `ReviewedDeathRequests` for Branch Manager, Area Manager, and HR Manager/Officer.
  - Rendered pending review queue with role-specific review links and the reviewed records history table.

---

## Change 30 — Restricted Initiate Death Request Button to Branch Manager Only

### Summary
Restricted the "Initiate Death Request" action button and access to only the Branch Manager role:
- **`Pages/Separation/Dashboard.cshtml` [MODIFIED]**:
  - Updated the action button condition so the "+ Initiate Death Request" button only renders when `Model.ActiveTab == "Death" && User.IsInRole("Branch Manager")`. It is hidden for HR Manager, HR Officer, and Area Manager.
- **`Pages/DeathProcess/Apply.cshtml.cs` [MODIFIED]**:
  - Updated `[Authorize(Roles = "Branch Manager")]` to enforce page-level authorization exclusively for Branch Managers.

---

## Change 31 — Death Process Notification System Integration & Review Pages Redesign

### Summary
1. **Multi-Recipient Notification Dispatching (`DeathService.cs`)**:
   - Resolved notification delivery failure by querying active identity users belonging to specific roles (`Area Manager`, `HR Manager`, `HR Officer`) through `_context.UserRoles`, `_context.Roles`, and `_context.Users`.
   - Added helper methods `NotifyRoleUsersAsync` and `NotifyUserByEmailAsync` to create individual notifications in `_context.Notifications` with direct target URLs.
   - Configured end-to-end notification triggers:
     - **Branch Manager Initiation**: Dispatches notifications with direct links (`/AreaManager/ReviewDeath/{id}`) to all Area Managers.
     - **Area Manager Confirmation**: Dispatches notifications with direct links (`/HRManager/ReviewDeath/{id}`) to all HR Managers and HR Officers, and notifies the Branch Manager.
     - **Area Manager Rejection**: Notifies the Branch Manager with rejection comments.
     - **HR Manager Finalization**: Deactivates login account, halts payroll, marks status as Deceased, and sends finalization notifications to the Branch Manager and Area Manager.
     - **HR Manager Rejection**: Sends rejection notifications to the Branch Manager and Area Manager.

2. **Modern Review Pages UI Redesign (`ReviewDeath.cshtml`)**:
   - Overhauled `Pages/AreaManager/ReviewDeath.cshtml`, `Pages/HRManager/ReviewDeath.cshtml`, and `Pages/BranchManager/ReviewDeath.cshtml`:
     - Clean top navigation with "← Back to Separation Hub" link (`/Separation/Dashboard?ActiveTab=Death`).
     - Modern Hero Employee Banner with colored initial avatar, EPF pill, Department, Branch, and dynamic status badge.
     - 2-Column Responsive Layout:
       - **Left column**: Incident & Nominee details card, Financial Liabilities & Obligations card (with color-coded warnings), and Mandatory Attached Proofs card with PDF badge and direct download buttons.
       - **Right column**: Interactive Review Decision Card with review remarks textarea, confirmation (`#10823c`) and reject (`#ef4444`) actions, and a 3-Stage Visual Approval Journey Timeline (Branch Manager → Area Manager → HR Manager) displaying timestamps, status icons, and reviewer comments.
   - Fully preserved the strict lock on transfer, resignation, and termination files.

---

## Change 32 — Configured EF Core Query Splitting Behavior

### Summary
Configured global query splitting behavior (`QuerySplittingBehavior.SplitQuery`) on the MySQL database context in `Program.cs`. This optimizes performance for queries loading multiple related collections (`Include`) and eliminates the EF Core 20504 MultipleCollectionIncludeWarning advisory warnings in the terminal output.

### Files Modified
- **`HRMS.UI/Program.cs` [MODIFIED]**:
  - Added `mySqlOptions => mySqlOptions.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery)` to the `AddDbContext<ApplicationDbContext>` setup.

---

## Change 33 — Restricted System Admin Access from Attendance & Attendance Dashboard

### Summary
Removed all access permissions and roles for System Admin (`Admin`) from the Attendance module and Attendance & Leave Hub:
- **`HRMS.UI/Pages/Attendance/Dashboard.cshtml.cs` [MODIFIED]**: Added explicit `Forbid()` guard check when `User.IsInRole("Admin")`.
- **`HRMS.UI/Pages/Attendance/Dashboard.cshtml` [MODIFIED]**: Removed `Admin` role checks from all attendance and leave action cards.
- **`HRMS.UI/Pages/Attendance/Index.cshtml.cs` [MODIFIED]**: Added `Forbid()` guard check for `Admin` role, and removed `Admin` from manager filtering logic, branch lists, and department scopes.
---

## Change 34 — Transferred Biometric Log Creation Permissions to Branch Manager

### Summary
Assigned biometric log entry and CSV import creation permissions exclusively to the Branch Manager role:
---

## Change 35 — Added Excel (.xlsx) and Hardware Device Format Support for Biometric Imports

### Summary
Enhanced the Biometric Log Importer to support both native Excel (`.xlsx`) and `.csv` files matching real-world biometric device export layouts (such as `Transaction.xlsx`):
- **`HRMS.UI/Pages/BiometricLogs/Create.cshtml.cs` [MODIFIED]**:
  - Implemented built-in OpenXML/ZIP `.xlsx` reader using `System.IO.Compression.ZipArchive` and `System.Xml.Linq.XDocument`.
  - Added support for biometric machine exports containing `ID` (mapped directly to `Employee.Id`), `Date`, `Time`, `Device Serial No.`, `Device Name`, and `Punch State`.
  - Maintained backward compatibility with legacy CSV formats (`UserID`, `VerifyTime`, `VerifyState`, `DeviceID`).
  - Added branch-level scoping to ensure logs are only processed for employees within the Branch Manager's branch.
- **`HRMS.UI/Pages/BiometricLogs/Create.cshtml` [MODIFIED]**: Updated file picker to `accept=".csv,.xlsx"` with clear instructions.
---

## Change 36 — Added Direct Import Biometric Logs Navigation for Branch Manager

### Summary
Added direct, prominent navigation links and action cards for Branch Managers to import biometric logs:
- **`HRMS.UI/Pages/Attendance/Dashboard.cshtml` [MODIFIED]**: Added a dedicated **"Import Biometric Logs"** card linking directly to `/BiometricLogs/Create` for Branch Managers.
- **`HRMS.UI/Pages/Attendance/Index.cshtml` [MODIFIED]**: Updated top action button to **"Import / Add Biometric Logs"** for quick access directly from the attendance logs explorer.
---

## Change 37 — Removed Dedicated Import Tile from Attendance & Leave Hub

### Summary
Removed the redundant standalone "Import Biometric Logs" tile from [`/Attendance/Dashboard`](file:///c:/Users/RISINU/Documents/Uom/Sem%203/Software%20Project/Merge/MM/HRMS.UI/Pages/Attendance/Dashboard.cshtml). Branch Managers access the log upload and creation features through the standard **"Biometric Logs"** tile or via the **"Import / Add Biometric Logs"** button on the **Attendance Records** page.
- **`HRMS.UI/Pages/Attendance/Dashboard.cshtml` [MODIFIED]**: Removed the extra tile.
---

## Change 38 — Enabled Flexible Employee ID Parsing in Biometric Importer

### Summary
Enhanced the employee ID parser in the biometric importer to support both numeric and alphanumeric prefixed formats (`0032`, `E0032`, `EMP0032`, `32`):
- **`HRMS.UI/Pages/BiometricLogs/Create.cshtml.cs` [MODIFIED]**: Automatically cleans and extracts numeric ID values so prefixes like `E` or `EMP` and leading zeros map seamlessly to the system's integer `Employee.Id`.
---

## Change 39 — Fixed Excel Fractional Serial Time & Date Parsing in Biometric Importer

### Summary
Resolved the issue where Excel `.xlsx` files with raw decimal/serial timestamps (e.g. `0.3298611111111111` for `07:55:00` in `Transaction_sample_clean.xlsx`) were failing standard string date parsing:
- **`HRMS.UI/Pages/BiometricLogs/Create.cshtml.cs` [MODIFIED]**:
  - Implemented `TryParseBiometricDateTime` to handle Excel fractional day times (`TimeSpan.FromDays(fraction)`), OLE Automation date numbers, and text timestamps seamlessly.
  - Added descriptive flash message reporting both successfully imported punch count and any skipped entries due to branch filtering or duplicate punches.
---

## Change 40 — Configured Role-Based Attendance Access Scopes & Filter Controls

### Summary
Configured role-scoped attendance viewing and multi-level filter controls according to organizational hierarchy:
- **`HRMS.UI/Pages/Attendance/Index.cshtml.cs` [MODIFIED]**:
  - **HR Manager**: Access to attendance records across **all branches** and departments, with full Branch and Department filters.
  - **HR Officer & Area Manager**: Scoped to their **assigned branches** (`currentUser.ManagedBranches`), with Assigned Branch and Department filters.
  - **Branch Manager**: Scoped to their **assigned branch** (`currentUser.Branch`), with Department filter to filter within their branch.
  - **Department Head**: Scoped strictly to employees within their **assigned department and branch**.
  - **Employee**: Scoped strictly to their own attendance records.
- **`HRMS.UI/Pages/Attendance/Index.cshtml` [MODIFIED]**:
  - Updated branch dropdown to dynamically display `-- All Branches --` (for HR Manager) vs `-- All Assigned Branches --` (for Area Manager/HR Officer).
  - Improved responsive filter card grid alignment.
---

## Change 41 — Configured Biometric Logs Role Scopes, Filters & Latest Log Ordering

### Summary
Configured role-scoped viewing and multi-level filter controls on the Biometric Logs explorer and history pages:
- **`HRMS.UI/Pages/BiometricLogs/Index.cshtml.cs` [MODIFIED]**:
  - Granted access to `Department Head, Branch Manager, Area Manager, HR Officer, HR Manager`.
  - **HR Manager**: View biometric logs across **all branches** and departments, with Branch and Department filter dropdowns.
  - **HR Officer & Area Manager**: Scoped to their **assigned branches** (`currentUser.ManagedBranches`), with Assigned Branch and Department filter dropdowns.
  - **Branch Manager**: Scoped to their **assigned branch** (`currentUser.Branch`), with Department filter dropdown.
  - **Department Head**: Scoped strictly to employees within their **assigned department and branch**.
  - Removed artificial 7-day cutoff filter and ordered queries by `LogDateTime DESC, Id DESC` to guarantee the **latest logs** always appear at the top.
- **`HRMS.UI/Pages/BiometricLogs/Index.cshtml` [MODIFIED]**:
  - Added Branch, Department, Employee, and Date Range filters to the filter card.
  - Added Branch & Department column badges to the raw logs table.
- **`HRMS.UI/Pages/BiometricLogs/History.cshtml.cs` [MODIFIED]**:
  - Aligned authorization and organizational role-scoping across historical logs.
---

## Change 42 — Enabled Biometric Logs Tile for HR Officer & Department Head on Hub

### Summary
Updated the Biometric Logs feature card on the Attendance & Leave Management Hub ([`/Attendance/Dashboard`](file:///c:/Users/RISINU/Documents/Uom/Sem%203/Software%20Project/Merge/MM/HRMS.UI/Pages/Attendance/Dashboard.cshtml)) so that HR Officers and Department Heads can access the Biometric Logs explorer:
- **`HRMS.UI/Pages/Attendance/Dashboard.cshtml` [MODIFIED]**: Added `HR Officer` and `Department Head` to the role visibility check for the Biometric Logs tile.
---

## Change 43 — Seeded & Enabled Managerial Designations on Employee Accounts

### Summary
Enabled official managerial designations (**Branch Manager**, **Area Manager**, **Department Head**) on employee account creation and editing:
- **`HRMS.UI/Program.cs` [MODIFIED]**:
  - Added automatic database seeding for core designations: `Branch Manager`, `Area Manager`, and `Department Head`.
  - Removed any automatic department seeding so all departments remain exclusively user-managed via Settings.
- **`HRMS.UI/Pages/Employees/Create.cshtml.cs` [MODIFIED]**:
  - Updated `OnGetDesignationsByDepartmentAsync(departmentId)` to dynamically include `Branch Manager`, `Area Manager`, and `Department Head` across any selected department.
  - Updated `LoadDropdownsAsync()` so initial and edit-mode dropdowns include `Branch Manager`, `Area Manager`, and `Department Head`.
  - Expanded `RoleList` for `Admin` and `HR Manager` to include: `Employee`, `Department Head`, `Branch Manager`, `Area Manager`, and `Admin`.
---

## Change 44 — Configured Managerial Department for Branch & Area Managers

### Summary
Added the standard **`Managerial`** department and integrated it across all branch department lists for employee onboarding:
- **`HRMS.UI/Program.cs` [MODIFIED]**:
  - Seeded the standard **`Managerial`** department and linked it to all branches in `BranchDepartments`.
- **`HRMS.UI/Pages/Employees/Create.cshtml.cs` [MODIFIED]**:
  - Ensured `Managerial` is always available in `OnGetDepartmentsByBranchAsync` and `LoadDropdownsAsync` for every selected branch.
- **Workflow Guide for High-Role Employee Accounts**:
  - **Branch Manager**: Branch = *[Assigned Branch]* $\rightarrow$ Department = **`Managerial`** $\rightarrow$ Designation = **`Branch Manager`**
  - **Area Manager**: Branch = **`Head Office`** $\rightarrow$ Department = **`Managerial`** $\rightarrow$ Designation = **`Area Manager`**
  - **Department Head**: Branch = *[Assigned Branch]* $\rightarrow$ Department = *[Specific Department e.g. Finance]* $\rightarrow$ Designation = **`Department Head`**
---

## Change 45 — Enforced Single Branch Manager & Department Head Profile Constraints

### Summary
Prevented duplicate employee profiles for Branch Managers and Department Heads at both client-side and server-side:
- **`HRMS.UI/Pages/Employees/Create.cshtml.cs` [MODIFIED]**:
  - In `OnPostAsync`: Added validation ensuring that no more than one active **Branch Manager** employee profile can exist per branch, and no more than one active **Department Head** profile can exist per branch + department combination (excluding drafts, terminations, resignations, and duty accounts).
  - Added AJAX endpoint `OnGetCheckDesignationAvailabilityAsync(branchId, departmentId, designationId, employeeId)` for real-time validation.
- **`HRMS.UI/Pages/Employees/Create.cshtml` [MODIFIED]**:
  - Integrated `checkDesignationAvailability()` to trigger on branch, department, or designation change, displaying instant warnings if a managerial profile already exists.
---

## Change 46 — Restricted Managerial Employee Account Creation Exclusively to HR Manager

### Summary
Restricted the creation and assignment of managerial employee profiles and designations (**Branch Manager**, **Area Manager**, **Department Head**, and the **`Managerial`** department) exclusively to the **HR Manager** (and Admin):
- **`HRMS.UI/Pages/Employees/Create.cshtml.cs` [MODIFIED]**:
  - In `OnPostAsync`: Added authorization check blocking non-HR-Managers from creating or assigning managerial designations (`Branch Manager`, `Area Manager`, `Department Head`).
  - In `LoadDropdownsAsync()`, `OnGetDepartmentsByBranchAsync()`, and `OnGetDesignationsByDepartmentAsync()`:
    - Managerial designations (`Branch Manager`, `Area Manager`, `Department Head`) are filtered out for standard HR Officers.
    - The `Managerial` department is hidden from standard HR Officers.
    - Managerial system roles (`Branch Manager`, `Area Manager`, `Department Head`) are only selectable by HR Manager / Admin.
---

## Change 47 — Added Create Employee Quick Action Button to Dashboard

### Summary
Added a prominent **Create Employee** action card to the Dashboard Overview ([`/Index`](file:///c:/Users/RISINU/Documents/Uom/Sem%203/Software%20Project/Merge/MM/HRMS.UI/Pages/Index.cshtml)) for the **HR Manager** (as well as HR Officers and Admins):
- **`HRMS.UI/Pages/Index.cshtml` [MODIFIED]**: Added `Create Employee` (`/Employees/Create`) to the **Quick Actions** row on the main dashboard.
- **`HRMS.UI/Pages/Employees/Index.cshtml`**: The `Create Employee` button is also directly available on the Employee Directory tab bar.
---

## Change 48 — Automatic System Role & Designation Synchronization

### Summary
Implemented seamless two-way auto-synchronization between **System Role**, **Designation**, and **Department** on the Employee Creation form ([`/Employees/Create`](file:///c:/Users/RISINU/Documents/Uom/Sem%203/Software%20Project/Merge/MM/HRMS.UI/Pages/Employees/Create.cshtml)):
- **`HRMS.UI/Pages/Employees/Create.cshtml` [MODIFIED]**:
  - Selecting **System Role = `Branch Manager`**: Automatically sets Department to **`Managerial`** and Designation to **`Branch Manager`**.
  - Selecting **System Role = `Area Manager`**: Automatically sets Branch to **`Head Office`**, Department to **`Managerial`**, and Designation to **`Area Manager`**.
  - Selecting **System Role = `Department Head`**: Automatically sets Designation to **`Department Head`**.
  - Conversely, selecting any of these titles in the Designation dropdown automatically syncs the System Role.
- **`HRMS.UI/Pages/Employees/Create.cshtml.cs` [MODIFIED]**:
  - Added pre-validation resolution in `OnPostAsync` so if a managerial role is submitted, the matching designation, department, and branch are resolved automatically if not explicitly chosen.
---

## Change 49 — Defaulted Employee Type to Permanent

### Summary
Configured the **Employee Type** field on the Employee Creation page ([`/Employees/Create`](file:///c:/Users/RISINU/Documents/Uom/Sem%203/Software%20Project/Merge/MM/HRMS.UI/Pages/Employees/Create.cshtml)) to be pre-selected as **`Permanent`** by default:
- **`HRMS.UI/Pages/Employees/Create.cshtml.cs` [MODIFIED]**: Initialized `NewEmployee.EmployeeType = "Permanent"` on initial load and draft recovery fallback.
---

## Change 50 — Locked Employee Type to Permanent for Managerial Roles

### Summary
Enforced that managerial employee profiles (**Branch Manager**, **Area Manager**, **Department Head**) must be **`Permanent`** and cannot be changed to Intern or Probationary:
- **`HRMS.UI/Pages/Employees/Create.cshtml` [MODIFIED]**:
  - Added `updateEmployeeTypeLock()` to automatically lock/disable the Employee Type dropdown to **`Permanent`** whenever a managerial role or designation is selected.
  - Automatically unlocks the Employee Type field when standard `Employee` role is selected.
- **`HRMS.UI/Pages/Employees/Create.cshtml.cs` [MODIFIED]**:
  - Enforced `NewEmployee.EmployeeType = "Permanent"` on the backend in `OnPostAsync` whenever creating or updating a managerial role.
---

## Change 51 — Locked Head Office Branch for Area Manager & Verified Multiple Area Managers

### Summary
Configured the **Area Manager** onboarding rules on [`/Employees/Create`](file:///c:/Users/RISINU/Documents/Uom/Sem%203/Software%20Project/Merge/MM/HRMS.UI/Pages/Employees/Create.cshtml):
- **`HRMS.UI/Pages/Employees/Create.cshtml` [MODIFIED]**:
  - Automatically sets **Branch** to **`Head Office`** and locks the field when **`Area Manager`** is selected.
  - Automatically sets **Department** to **`Managerial`** and locks the field.
  - Automatically sets **Employee Type** to **`Permanent`** and locks the field.
- **`HRMS.UI/Pages/Employees/Create.cshtml.cs` [MODIFIED]**:
  - Enforced `BranchId` to Head Office and `DepartmentId` to `Managerial` on backend for Area Managers in `OnPostAsync`.
  - Confirmed and verified that **multiple Area Manager profiles are fully supported** (uniqueness constraints are strictly scoped to single active BM per branch and single active DH per branch+dept, leaving Area Managers unrestricted).
---

## Change 52 — Scoped System Role Options to Operational & Managerial Roles

### Summary
Removed `Admin`, `HR Manager`, and `HR Officer` from the **System Role** dropdown list on the Employee Creation page ([`/Employees/Create`](file:///c:/Users/RISINU/Documents/Uom/Sem%203/Software%20Project/Merge/MM/HRMS.UI/Pages/Employees/Create.cshtml)):
- **`HRMS.UI/Pages/Employees/Create.cshtml.cs` [MODIFIED]**:
  - Scoped the selectable System Role list strictly to:
    - **`Employee`**
    - **`Department Head`**
    - **`Branch Manager`**
    - **`Area Manager`**
---

## Change 53 — Implemented Role-Based Multi-Tier Leave Approval Workflows

### Summary
Designed, implemented, and configured the multi-tier hierarchical leave approval workflows for all employee tiers using Duty Accounts:
1. **Normal Employee Workflow**:
   - Applicant submits leave $\rightarrow$ Initial status **`PendingDH`**.
   - **Department Head (Duty Account)** for the applicant's Branch & Department approves $\rightarrow$ Advances to **`PendingBM`**.
   - **Branch Manager (Duty Account)** for the applicant's Branch approves $\rightarrow$ Marks **`Approved`** (deducts leave balance and sends completion notification to employee).
2. **Department Head Workflow** (applying via employee profile):
   - Applicant submits leave $\rightarrow$ Initial status **`PendingBM`**.
   - **Branch Manager (Duty Account)** for the applicant's Branch approves $\rightarrow$ Advances to **`PendingAM`**.
   - **Area Manager (Duty Account)** managing that branch approves $\rightarrow$ Marks **`Approved`** (deducts leave balance and sends completion notification to Department Head).
3. **Branch Manager Workflow** (applying via employee profile):
   - Applicant submits leave $\rightarrow$ Initial status **`PendingAM`**.
   - **Area Manager (Duty Account)** managing that branch approves $\rightarrow$ Advances to **`PendingHR`**.
   - **HR Manager (Duty Account)** approves $\rightarrow$ Marks **`Approved`** (deducts leave balance and sends completion notification to Branch Manager).
4. **Area Manager Workflow** (applying via employee profile):
   - Applicant submits leave $\rightarrow$ Initial status **`PendingHR`**.
   - **HR Manager (Duty Account)** approves $\rightarrow$ Marks **`Approved`** (deducts leave balance and sends completion notification to Area Manager).
5. **Rejection Handling**:
   - Any authorized approver in the active chain can reject with mandatory comments; marks status **`Rejected`**, logs audit record, and dispatches detailed rejection notification to applicant.

### Files Modified:
- **`HRMS.UI/Services/ILeaveService.cs` & `HRMS.UI/Services/Impl/LeaveService.cs` [MODIFIED]**:
  - Added `GetApplicantWorkflowRoleAsync()` for accurate role classification.
  - Implemented role-based workflow router in `ApplyLeaveAsync()`.
  - Implemented multi-tier state machine in `ApproveLeaveAsync()` and `RejectLeaveAsync()`.
  - Scoped `GetPendingApprovalsAsync()` to duty account role, branch, department, and managed branches (`user.ManagedBranches`).
  - Added role-targeted notification dispatchers (`NotifyDepartmentHeadsAsync`, `NotifyBranchManagersAsync`, `NotifyAreaManagersForBranchAsync`, `NotifyHrManagersAsync`).
- **`HRMS.UI/Pages/Manager/Leave/Approval.cshtml.cs` & `Approval.cshtml` [MODIFIED]**:
  - Updated duty account authorization and added status badge styles for `PendingDH`, `PendingBM`, `PendingAM`, `PendingHR`.
- **`HRMS.UI/Pages/Manager/Leave/Review.cshtml.cs` & `Review.cshtml` [MODIFIED]**:
  - Evaluated `CanApprove` dynamically according to duty account role, branch, department, and managed branch access for the active stage.
- **`HRMS.UI/Pages/Employee/Leave/Dashboard.cshtml.cs` & `Status.cshtml` [MODIFIED]**:
  - Updated `PendingCount` to count all `Pending*` stages.
  - Formatted status badges with stage descriptions (`Pending Dept Head`, `Pending Branch Manager`, `Pending Area Manager`, `Pending HR Manager`) and enabled cancel action across pending stages.
- Strict lock on separation and attendance files preserved.

---

## Change 54 — Comprehensive Leave Workflow Validation Rules & Date Selection Safeguards

### Summary
Enhanced and verified all business validation rules, date selection constraints, overlapping request guards, and balance validations across the Leave workflow:
1. **Date Selection & Range Ordering**:
   - Backend & frontend strictly enforce that **End Date cannot be earlier than Start Date** (`EndDate >= StartDate`).
   - Selecting a Start Date dynamically updates the minimum boundary (`min`) of End Date on the UI to prevent invalid range selection.
2. **Working Days & Weekend Exclusion**:
   - Working days are computed excluding Saturdays and Sundays.
   - If an employee selects dates containing only weekends (0 working days), submission is blocked with an informative warning: *"The selected date range does not contain any working days (weekends are excluded)."*
   - Real-time client-side calculation alerts the user and prevents form submission until at least one working day is included.
3. **Overlapping Leave Conflict Guard**:
   - Employees are prevented from submitting duplicate or overlapping leave applications if they already have an active request (`Pending*` or `Approved`) overlapping with the requested date range.
4. **Leave Balance Validation**:
   - Real-time balance indicator displays available entitlement days for the selected leave type.
   - If requested days exceed available balance, an inline warning is displayed, and backend throws: *"Insufficient leave balance for {LeaveType} Leave. Available: {X} day(s), Requested: {Y} day(s)."*
5. **Special Leave Type Constraints**:
   - **Maternity Leave**: Blocked for male employees; Start Date automatically computes the standard 84-day duration and sets End Date bounds.
   - **Overseas Leave**: Passport expiry date must be strictly after the requested overseas travel End Date; country and passport number are required fields.

### Files Modified:
- **`HRMS.UI/Services/Impl/LeaveService.cs` [MODIFIED]**: Added date order, working days, overlapping leave, gender, and balance validations in `ApplyLeaveAsync()`.
- **`HRMS.UI/Services/Impl/OverseasLeaveService.cs` [MODIFIED]**: Added date order, passport expiry validity, and overlapping leave guards in `SubmitOverseasLeaveAsync()`.
- **`HRMS.UI/Services/Impl/MaternityLeaveService.cs` [MODIFIED]**: Added gender restriction, date order, and overlapping leave guards in `SubmitMaternityLeaveAsync()`.
- **`HRMS.UI/Pages/Employee/Leave/Apply.cshtml.cs` & `Apply.cshtml` [MODIFIED]**: Wired live balance indicator, real-time working day calculation, date bounds constraints, and inline validation warnings.
- Strict lock on separation and attendance files preserved.

---

## Change 55 — Restricted Leave Start Date Past Selection to Maximum 2 Days

### Summary
Enforced a strict limit preventing employees from selecting older past dates for leave applications:
1. **2-Day Maximum Past Limit**:
   - The leave start date cannot be older than **2 days in the past** relative to today (`StartDate.Date >= DateTime.Today.AddDays(-2)`).
2. **Frontend Constraints (`Apply.cshtml`)**:
   - Set the HTML `min` attribute on `StartDate` and `EndDate` inputs across Standard, Overseas, and Maternity tabs to `@DateTime.Today.AddDays(-2).ToString("yyyy-MM-dd")`.
   - Real-time JavaScript validation checks the selected start date against `Today - 2 days`; if an older date is input, it displays an inline warning (*"Start date cannot be more than 2 days in the past"*) and disables form submission.
3. **Backend Enforcement**:
   - Enforced in `OnPostApplyAsync`, `OnPostOverseasAsync`, and `OnPostMaternityAsync` in `Pages/Employee/Leave/Apply.cshtml.cs`.
   - Enforced in `ApplyLeaveAsync` (`LeaveService.cs`), `SubmitOverseasLeaveAsync` (`OverseasLeaveService.cs`), and `SubmitMaternityLeaveAsync` (`MaternityLeaveService.cs`).

### Files Modified:
- **`HRMS.UI/Pages/Employee/Leave/Apply.cshtml` & `Apply.cshtml.cs` [MODIFIED]**: Added UI `min` attributes, real-time JavaScript validation, and backend check for 2-day past threshold.
- **`HRMS.UI/Services/Impl/LeaveService.cs` [MODIFIED]**: Added start date past limit validation in `ApplyLeaveAsync`.
- **`HRMS.UI/Services/Impl/OverseasLeaveService.cs` [MODIFIED]**: Added start date past limit validation in `SubmitOverseasLeaveAsync`.
- **`HRMS.UI/Services/Impl/MaternityLeaveService.cs` [MODIFIED]**: Added start date past limit validation in `SubmitMaternityLeaveAsync`.
- Strict lock on separation and attendance files preserved.

---

## Change 56 — Added Document Attachments & Branch Manager Then HR Finalization for Maternity & Overseas Leaves

### Summary
1. **Document Attachments in Leave Application**:
   - **Maternity Leave**: Added mandatory Medical Certificate upload (`MedicalCertificateFile`) and optional Doctor's Letter upload (`DoctorLetterFile`) with file type checks (`.pdf`, `.jpg`, `.jpeg`, `.png` up to 5MB).
   - **Overseas Leave**: Added mandatory Passport Bio / Visa Copy upload (`PassportCopyFile`) and optional Travel Confirmation/Ticket upload (`ConfirmationLetterFile`).
   - Files are securely saved to `wwwroot/uploads/maternity/` and `wwwroot/uploads/overseas/`, binding their paths to `MaternityLeave` (`MedicalCertificatePath`, `DoctorLetterPath`), `OverseasLeave` (`PassportCopyPath`, `ConfirmationLetterPath`), and the parent `Leave.AttachmentPath`.

2. **Branch Manager $\rightarrow$ HR Officer Finalization Workflow**:
   - **Step 1 — Initial Application**:
     - Both Maternity and Overseas leave requests now start in status **`PendingBM`** (VerificationStatus: `"Pending BM"`).
     - Automated notifications are dispatched to the **Branch Manager** duty accounts in the applicant's branch.
   - **Step 2 — Branch Manager Approval**:
     - Branch Manager reviews the leave request along with uploaded certificates, passport documents, and sub-details via `/Manager/Leave/Review?id=...`.
     - Upon BM approval, the leave transitions from `PendingBM` to **`PendingHR`** (VerificationStatus: `"BM Approved / Pending HR"`).
     - Automated notifications are dispatched to **HR Officer** and **HR Manager** duty accounts.
   - **Step 3 — HR Officer Finalization**:
     - HR Officers and HR Managers review the BM-approved request and finalize the approval.
     - On HR approval, status transitions to **`Approved`**, balance is deducted from `LeaveEntitlement`, `MaternityPayment` is queued, and the employee receives an approval notification.
     - If rejected by either Branch Manager or HR Officer, status transitions to `Rejected` with comments.

### Files Modified:
- **`HRMS.UI/Pages/Employee/Leave/Apply.cshtml` & `Apply.cshtml.cs` [MODIFIED]**: Added `enctype="multipart/form-data"`, file upload controls, backend file saving to `wwwroot/uploads`, and file path binding.
- **`HRMS.UI/Services/Impl/MaternityLeaveService.cs` [MODIFIED]**: Set initial status to `PendingBM` and notify Branch Managers.
- **`HRMS.UI/Services/Impl/OverseasLeaveService.cs` [MODIFIED]**: Set initial status to `PendingBM` and notify Branch Managers.
- **`HRMS.UI/Services/Impl/LeaveService.cs` [MODIFIED]**: Implemented `PendingBM` $\rightarrow$ `PendingHR` routing for Maternity & Overseas, authorized `HR Officer` for `PendingHR`, and handled balance deduction & payroll setup.
- **`HRMS.UI/Pages/Manager/Leave/Review.cshtml.cs` & `Approval.cshtml.cs` [MODIFIED]**: Added `HR Officer` role authorization and enabled HR finalization in `CanApprove`.
- Strict lock on separation and attendance files preserved.

---

## Change 57 — Added Supporting Document File Attachments for Standard Leaves

### Summary
1. **Supporting Document Upload for Standard Leaves**:
   - Added an optional file attachment input (`StandardAttachmentFile`) on the Standard Leave application form ([`Apply.cshtml`](file:///c:/Users/RISINU/Documents/Uom/Sem%203/Software%20Project/Merge/MM/HRMS.UI/Pages/Employee/Leave/Apply.cshtml)).
   - Accepts `.pdf`, `.jpg`, `.jpeg`, `.png` up to 5MB for medical certificates, exam admission letters, bereavement notices, etc.
   - Configured `enctype="multipart/form-data"` on the standard leave form.
2. **Backend Storage & Linking**:
   - Uploaded files are saved to `wwwroot/uploads/standard/` using `SaveUploadedFileAsync`.
   - File path is bound to `leave.AttachmentPath` and persisted in the database.
   - Available immediately in the manager review pane ([`Review.cshtml`](file:///c:/Users/RISINU/Documents/Uom/Sem%203/Software%20Project/Merge/MM/HRMS.UI/Pages/Manager/Leave/Review.cshtml)) via the `"View Attached File"` button.

### Files Modified:
- **`HRMS.UI/Pages/Employee/Leave/Apply.cshtml` & `Apply.cshtml.cs` [MODIFIED]**: Added `StandardAttachmentFile` binding property, file upload control, multipart encoding, and backend file storage.
- Strict lock on separation and attendance files preserved.

---

## Change 58 — Fixed Leave Notification Routing & Excluded Applicant/Unrelated Employees

### Summary
1. **Precise Approver Targeting**:
   - Fixed `NotifyDepartmentHeadsAsync`, `NotifyBranchManagersAsync`, `NotifyAreaManagersForBranchAsync`, and `NotifyHrManagersAsync` in [`LeaveService.cs`](file:///c:/Users/RISINU/Documents/Uom/Sem%203/Software%20Project/Merge/MM/HRMS.UI/Services/Impl/LeaveService.cs) to accurately target the specific duty account (or designated manager) for the applicant's branch and department.
   - Fixed duty account identification for Branch Managers and Department Heads where user branch and department names were previously filtered against mismatched dummy employee branch IDs.
2. **Applicant Exclusion**:
   - Added an `excludeEmail` parameter across all approver notification methods in `LeaveService.cs`, `MaternityLeaveService.cs`, and `OverseasLeaveService.cs`.
   - The applicant's email is now explicitly excluded from receiving manager review requests when they submit or progress their own leave applications.
3. **No Cross-Employee Leakage**:
   - Prevented general employees in the same branch or company from erroneously receiving review notifications intended strictly for Department Head, Branch Manager, Area Manager, or HR Officer duty accounts.

### Files Modified:
- **`HRMS.UI/Services/Impl/LeaveService.cs` [MODIFIED]**: Added `excludeEmail` filtering and exact duty account / designated manager branch & department checks in `NotifyDepartmentHeadsAsync`, `NotifyBranchManagersAsync`, `NotifyAreaManagersForBranchAsync`, and `NotifyHrManagersAsync`.
- **`HRMS.UI/Services/Impl/MaternityLeaveService.cs` [MODIFIED]**: Targeted only the applicant's Branch Manager duty account and excluded `employee.Email`.
- **`HRMS.UI/Services/Impl/OverseasLeaveService.cs` [MODIFIED]**: Targeted only the applicant's Branch Manager duty account and excluded `employee.Email`.
- Strict lock on separation, attendance, and leave files preserved.

---

## 🔒 MODULE LOCK STATUS (ACTIVE)
- **Separation & Transfers Module**: 🔒 **LOCKED** (Do not touch: `TransferRequestService.cs`, `Pages/Transfer/*`, `Pages/Resignation/*`, `Pages/Termination/*`, `Pages/DeathProcess/*`, `Pages/Separation/*`, etc.)
- **Attendance Module**: 🔒 **LOCKED** (Do not touch: `AttendanceService.cs`, `Pages/Attendance/*`, `Pages/BiometricLogs/*`, etc.)
- **Leave Module**: 🔒 **LOCKED** (Do not touch: `LeaveService.cs`, `MaternityLeaveService.cs`, `OverseasLeaveService.cs`, `Pages/Employee/Leave/*`, `Pages/Manager/Leave/*`, `Pages/Admin/Leave/*`, `Pages/Employee/Maternity/*`, `Pages/Employee/Overseas/*`, etc.)

---

## Change 59 — Removed System Admin Access from Training Module

### Problem
System Administrators previously had UI tiles and role authorization across training pages (`Dashboard`, `Sessions`, `Schedule`, `Manage`, `ProbationTracking`, `InternTracking`).

### Solution
1. **Sidebar Navigation (`_Layout.cshtml`)**:
   - Wrapped the Training navigation menu item under `@if (!User.IsInRole("Admin"))` so the System Admin never sees Training in the sidebar.
2. **Dashboard UI (`Dashboard.cshtml`)**:
   - Removed `User.IsInRole("Admin")` from all service cards: Manage Requests, Schedule Session, Probationary Staff Tracking, and Intern Tracking.
3. **Training Page Models Authorization**:
   - Explicitly restricted all Training PageModels and handlers against the `Admin` role with `if (User.IsInRole("Admin")) return Forbid();` and appropriate role-based `[Authorize]` attributes across:
     - `Dashboard.cshtml.cs`
     - `Sessions.cshtml.cs` & `Sessions.cshtml`
     - `Schedule.cshtml.cs`
     - `RequestTraining.cshtml.cs`
     - `Manage.cshtml.cs`
     - `Details.cshtml.cs`
     - `ProbationTracking.cshtml.cs`
     - `EvaluateProbation.cshtml.cs`
     - `InternTracking.cshtml.cs`
     - `EvaluateIntern.cshtml.cs`
     - `ViewProfile.cshtml.cs`

---

## Change 60 — Enabled Training Tab & Quick Action in Employee Portal

### Problem
Employees previously did not have access to the Training navigation tab because it was nested under manager-only role checks in `_Layout.cshtml`.

### Solution
1. **Sidebar Navigation (`_Layout.cshtml`)**:
   - Training navigation link is explicitly placed under `@if (!User.IsInRole("Admin"))`, making it visible to all Employees, Department Heads, and Managers.
---

## Change 61 — Fixed Permanent Employee Resolution in Request Training Form

### Problem
`RequestTraining.cshtml.cs` had a hardcoded test ID (`int testEmployeeId = 36;`), which caused permanent employees logged in to always be evaluated against ID 36 (failing eligibility validation).

### Solution
1. **Dynamic Logged-in Employee Resolution (`RequestTraining.cshtml.cs`)**:
   - Injected `UserManager<ApplicationUser>` and `ApplicationDbContext`.
   - Resolved currently authenticated user's employee record dynamically via `user.EmployeeId`, `user.Email`, `user.UserName`, or `User.Identity.Name`.
   - Checked permanent status across `EmployeeType`, `Status`, or `DateConfirmed`.
   - Populated the applicant's name and status dynamically.
   - Saved `TrainingProgramRequest` linked directly to the authenticated employee's ID.
---

## Change 62 — Fixed Training Request Details Review Page Error

### Problem
When HR Officers or Managers attempted to review a training request at `/Training/Details?id=...`, the page threw an error due to:
1. `DetailsModel.RequestDetails` using `dynamic` with an internal anonymous type, causing `RuntimeBinderException` in Razor view compilation.
2. Route mismatch from `@page "{id:int}"` expecting route path `/Training/Details/1` rather than query string `/Training/Details?id=1`.

### Solution
1. **Strongly-Typed Details DTO (`Details.cshtml.cs`)**:
---

## Change 63 — Migrated Training Scheduling & Sessions to EF Core

### Problem
When an HR Officer submitted the Schedule Training Session form (`/Training/Schedule`), an error occurred because the page executed raw ADO.NET SQL targeting an unmapped column name (`Trainer` instead of `TrainerName`) and omitting the non-nullable `Description` column.

### Solution
1. **EF Core Migration for Scheduling (`Schedule.cshtml.cs`)**:
   - Replaced raw SQL ADO.NET execution with Entity Framework Core `_context.Trainings.Add(new Training { ... })`.
   - Set required non-null fields (`Title`, `Description`, `Date`, `StartTime`, `DurationHours`, `TrainerName`, `Location`, `Status = "Scheduled"`).
   - Added validation and proper error handling.
---

## Change 64 — Branch-Scoped Training Scheduling & Employee Attendees Selector with Filters

### Problem
HR Officers could not scope scheduled training sessions to their assigned branches, nor select specific branch employees with department and employee type filtering.

### Solution
1. **Branch Assignment Scoping (`Schedule.cshtml.cs`)**:
   - HR Officers are restricted to branches assigned in `ManagedBranches` (or `user.Branch` / linked employee branch).
   - HR Managers retain organization-wide branch access.
   - If multiple branches are assigned to an HR Officer, they can select the target branch from the dropdown.
2. **Employee Selection & Filtering UI (`Schedule.cshtml`, `Schedule.cshtml.cs`)**:
   - Loaded active branch employees into a responsive interactive selector table.
   - Added instant filters for **Department** (All, Credit, Operations, Recovery, Gold Loan, etc.) and **Employee Type** (All, Permanent, Probation, Intern, Contract).
   - Added live text search (Name / EPF) and batch actions (`Select Filtered`, `Clear`, and a dynamic selection counter badge).
---

## Change 65 — Real-time EPF Number Filtering in Training Scheduling

### Problem
Users needed instant, real-time filtering of branch employees specifically by typing their EPF number.

### Solution
1. **Dedicated EPF Search Input (`Schedule.cshtml`)**:
   - Added a dedicated EPF Number search box with an EPF badge icon.
   - Separate Name search box and dropdown filters for Department & Employee Type.
---

## Change 66 — Fixed Real-time Table Row Visibility Filtering in Training Scheduling

### Problem
In `Schedule.cshtml`, real-time employee filtering relied on `.d-none` class toggling, but `.d-none` was not included in the custom Vanilla CSS layout stylesheets, resulting in filtered rows not hiding visually.

### Solution
1. **Explicit Display Toggling (`Schedule.cshtml`)**:
   - Updated filtering JavaScript to directly toggle `row.style.display = ""` (visible) and `row.style.display = "none"` (hidden).
   - Added `.d-none, .is-hidden { display: none !important; }` in page `<style>` as a strict safeguard.
---

## Change 67 — Excluded Duty Accounts from Training Schedule Employee Attendees List

### Problem
System administrative duty accounts (e.g. `DUTY-ACC`, branch manager/officer duty logins) were appearing in the employee selection table when scheduling training programs.

---

## Change 68 — Fixed Table Header and Cell Alignment in Training Employee Selector

### Problem
In `Schedule.cshtml`, column headers (`EMPLOYEE`, `EPF`, `DESIGNATION`, `DEPARTMENT`, `TYPE`) defaulted to browser-centered alignment, causing visible misalignment with the left-aligned data cells and badges.

### Solution
---

## Change 69 — Formatted Employee Names with Initials in Training Scheduling

### Problem
The employee selection table in `Schedule.cshtml` previously rendered full names (e.g. `Dilshani Kaushalya`) instead of the standardized professional format with initials (e.g. `D. Kaushalya`).

### Solution
1. **Name with Initials Formatting (`Schedule.cshtml.cs`)**:
   - Added `FormatNameWithInitials(string? fullName, string? initials)` helper.
   - Utilizes `e.Initials` when available or calculates initials from the first/middle names with the last name (e.g. `D. Kaushalya`, `H. Adikari`, `S. Probati`).
   - Populated `EmployeeItemDto.NameWithInitials`.
---

## Change 70 — Fixed Duplicate Surname in Name with Initials Display

### Problem
In `Schedule.cshtml`, employee names were displaying with repeated surnames (e.g. `H.Adikari. Adikari`) because the `Employee.Initials` database field already stores the complete name with initials (e.g. `H. Adikari`).

### Solution
---

## Change 71 — Created Training Session Details Page and View Actions

### Problem
After scheduling a training program, there was no option or page to view the scheduled session details, trainer logistics, or the enrolled employee roster.

### Solution
---

## Change 72 — Added Training Session & Enrolled Attendees Editing

### Problem
There was no option to edit existing training session details (program title, trainer, venue, date, time, status, description) or update the assigned/enrolled employee attendees list after creation.

### Solution
1. **Dedicated Edit View (`EditSession.cshtml` & `EditSession.cshtml.cs`)**:
   - Created `/Training/EditSession` page loading current session data and pre-checking existing enrolled attendees.
   - Integrated full interactive employee selector with real-time EPF/Name search, department/type filters, and bulk selection.
   - Synchronizes `EmployeeTrainings` upon save (removes unselected employees, adds newly selected attendees).
---

## Change 73 — Moved Cancelled & Completed Sessions to Session History

### Problem
When a training session was cancelled or completed, it continued appearing under "Upcoming Sessions" if its scheduled date was in the future or today.

### Solution
---

## Change 74 — Unified Approved Training Programs Catalog Across Request and Schedule Forms

### Problem
The program dropdown list in `Schedule.cshtml.cs` and `EditSession.cshtml.cs` contained 11 programs, while the employee request dropdown in `RequestTraining.cshtml` contained 15 programs, causing a slight discrepancy between employee requests and HR scheduling options.

### Solution
---

## Change 75 — Added Option to Request, Schedule, and Edit Custom Training Programs

### Problem
Users could only select from predefined approved training programs and had no option to request or schedule custom, specialized, or ad-hoc training programs by typing their own custom program title.

### Solution
1. **Employee Training Request Form (`RequestTraining.cshtml` & `RequestTraining.cshtml.cs`)**:
   - Added `-- Other / Custom Program (Type Below) --` option in the program dropdown.
   - Dynamically reveals a required **"Custom Program Name"** input text field when selected.
   - Binds and saves `CustomProgramTitle` as the training request's title.
2. **HR Training Scheduling Form (`Schedule.cshtml` & `Schedule.cshtml.cs`)**:
   - Added `-- Other / Custom Program (Type Below) --` in the scheduling program selector.
   - Dynamically reveals **"Custom Program Title"** text box with client-side validation.
   - Saves custom program title directly into the `Training` session entity.
---

## Change 76 — Simplified Training Sessions Table Action to "View Details" Only

### Problem
Having both "View" and "Edit" action buttons inside each row of the sessions list cluttered the table interface, when editing is already conveniently accessible inside the session details view.

### Solution
---

## Change 77 — Refined Training Sessions Table Columns

### Problem
The training sessions tables included extra columns (`Trainer`, `Attendees`) that overcrowded the list view when detailed information is already available on the Session Details page.

### Solution
1. **Streamlined Table Columns (`Sessions.cshtml`)**:
   - Refined both Upcoming Sessions and Session History tables to strictly display the 5 essential columns: **Program Title**, **Location**, **Date & Time**, **Status**, and **Action** (`View Details`).
---

## Change 78 — Redesigned Session Logistics Section to Match Kanrich HRMS Theme

### Problem
The session logistics block in `SessionDetails.cshtml` looked plain and didn't match the modern green & neutral card aesthetic used across the rest of the application.

### Solution
---

## Change 79 — Fixed PDF Print Roster Layout & Cleaned Print Styles

### Problem
Clicking "Print Roster" captured the web layout directly like a raw screenshot, displaying the web sidebar, top navigation bar, buttons, and clipping content.

---

## Change 80 — Enforced Assigned Branch Scoping for HR Officers Across Training Module

### Problem
HR Officers could view all training sessions, requests, and rosters across the entire organization, instead of being strictly restricted to the branches assigned to their account.

### Solution
1. **Branch Scoping in Sessions List (`Sessions.cshtml.cs`)**:
   - Implemented `GetAllowedBranchIdsAsync()` using the HR Officer's `ManagedBranches`, `Branch`, and employee profile.
   - Filtered `UpcomingSessions` and `PastSessions` so HR Officers only see sessions where enrolled employees belong to their assigned branches, or where the session venue/location matches their assigned branches.
   - Preserved global company-wide access for `HR Manager`.
2. **Permission Guard on Session Details (`SessionDetails.cshtml.cs`)**:
   - Added validation in `OnGetAsync()` and status post handlers to ensure non-HR Managers cannot view or modify training sessions outside their assigned branch scope.
3. **Training Requests Management Scoping (`Manage.cshtml.cs` & `Details.cshtml.cs`)**:
---

## Change 81 — Resolved LINQ Translation Error in Training Sessions Page

### Problem
EF Core threw an `InvalidOperationException` (untranslatable LINQ expression) on `/Training/Sessions` due to evaluating complex string containment against local branch collections within an `IQueryable` expression tree.

### Solution
---

## Change 82 — Restricted Training Requests Approval to HR and Enabled Branch Session Viewing for Managers

### Problem
Department Heads, Branch Managers, and Area Managers were previously granted approval capabilities for employee training requests, whereas training requests should only be approved or rejected by an HR Officer or HR Manager. Additionally, branch-level managers need to view upcoming training sessions and logistics for their respective branches.

### Solution
1. **Restricted Training Request Management to HR (`Manage.cshtml.cs`, `Details.cshtml.cs`, `Dashboard.cshtml`)**:
   - Changed authorization on `/Training/Manage` and `/Training/Details` to strictly `[Authorize(Roles = "HR Manager, HR Officer")]`.
   - Hidden the "Manage Requests" card from the Training Dashboard for Department Head, Branch Manager, and Area Manager.
2. **Branch Session Visibility for Branch Managers, Area Managers, and Department Heads (`Sessions.cshtml.cs`, `SessionDetails.cshtml.cs`)**:
---

## Change 83 — Removed Redundant Schedule Session Tile from Training Dashboard

### Problem
The "Schedule Session" tile on `/Training/Dashboard` was redundant since scheduling new training sessions is already directly accessible via the top "Schedule New Session" button inside `/Training/Sessions`.

### Solution
1. **Removed Dashboard Tile (`Dashboard.cshtml`)**:
   - Removed the separate "Schedule Session" card from `/Training/Dashboard`.
---

## Change 84 — Divided Manage Training Requests into Separate "Not Reviewed" and "Reviewed" Tables

### Problem
All employee training requests were previously displayed in a single unified table regardless of whether they were pending action or had already been processed.

### Solution
1. **Separated Request Lists (`Manage.cshtml.cs`)**:
   - Filtered training requests into two distinct collections: `PendingRequests` (status `Pending`) and `ReviewedRequests` (status `Approved` or `Rejected`).
---

## Change 85 — Renamed Training Request Tables to Professional Standard Corporate Titles

### Problem
The table headings "Not Reviewed Requests" and "Reviewed Requests History" sounded informal and non-standard for an enterprise HR portal.

### Solution
---

## Change 86 — Set Table Titles to "Pending Requests" and "Reviewed Requests"

### Problem
The user requested exact titles for the two tables on the Manage Training Requests page.

### Solution
---

## Change 87 — Created Automated Real-Time Notification System for Training Module

### Problem
The Training module lacked an integrated notification pipeline, so employees and managers received no alerts or topbar notifications when training requests were submitted, approved, or rejected, or when training sessions were scheduled, modified, completed, or cancelled.

### Solution
1. **Implemented `ITrainingNotificationService` (`TrainingNotificationService.cs`)**:
   - Built an automated notification service integrated directly with `INotificationService` and Identity.
   - **Request Submission**: Alerts HR Managers and HR Officers assigned to the employee's branch when a new application is submitted.
   - **Decision Alerts**: Instantly notifies the requesting Employee and their Branch Manager upon HR approval or decline.
   - **Session Scheduling**: Broadcasts session details (date, time, venue, and program title) to all enrolled employees and the target Branch Manager.
   - **Session Updates & Completion/Cancellation**: Notifies enrolled attendees if session logistics are modified or when the session status is changed to Completed or Cancelled.
---

## Change 88 — Scoped Training Sessions Table Strictly to Assigned Attendees for Regular Employees

### Problem
Regular employees (role `Employee`) were able to see all training sessions associated with their branch location rather than strictly the sessions they have been enrolled in.

### Solution
1. **Assigned Attendee Filtering (`Sessions.cshtml.cs`)**:
   - For regular employees (non-managers and non-HR), strictly filtered the sessions query to only records where `t.EmployeeTrainings.Any(et => et.EmployeeId == currentEmpId.Value)`.
---

## Change 89 — Dynamically Adapted Probation Tracking & Evaluations to Configured Employee Probation Periods

### Problem
Probation tracking, evaluation forms, and analytical profile trend charts previously assumed a fixed 6-month probation period for all employees, ignoring the custom `ProbationPeriodMonths` configured when creating the employee profile.

### Solution
1. **Dynamic Progress & Status (`ProbationTracking.cshtml.cs`, `ProbationTracking.cshtml`)**:
   - Query `e.ProbationPeriodMonths` from the employee profile (defaulting to 6 if unspecified).
   - Display dynamic progress `Month X of {TotalProbationMonths}` and calculate percentage based on employee's exact probation period.
   - Enforce branch scoping for Branch Managers, Area Managers, Department Heads, and HR Officers.
2. **Dynamic Evaluation Month Dropdown (`EvaluateProbation.cshtml.cs`, `EvaluateProbation.cshtml`)**:
   - Render milestone evaluation options from Month 1 up to the employee's configured `TotalProbationMonths`.
3. **Dynamic Performance Trend Chart (`ViewProfile.cshtml.cs`, `ViewProfile.cshtml`)**:
---

## Change 90 — Standardized Probationary Staff Tracking & Intern Tracking Tables

### Problem
The Probationary Staff Tracking and Intern Tracking tables featured legacy styling with inconsistent font sizes, lack of proper employee identification metadata (EPF number, branch name, department name), and outdated button styles.

### Solution
1. **Design System Alignment (`ProbationTracking.cshtml`, `InternTracking.cshtml`)**:
   - Replaced legacy structures with standard `.tbl-card` and `.sep-table` table components.
---

## Change 91 — Fixed and Activated Robust Progress Bar Rendering in Probation Tracking

### Problem
The progress bar was invisible / failing to render due to missing dedicated track/fill styling and zero-width states when evaluations were not yet entered.

### Solution
1. **Dedicated Track & Fill Styling (`ProbationTracking.cshtml`, `InternTracking.cshtml`)**:
   - Added `.prob-track` (140px fixed rounded track) and `.prob-fill` (smooth CSS transitions, gradient fills).
---

## Change 92 — Strict Evaluation-Based Probation Progress & Zero Baseline

### Problem
When no evaluations had yet been submitted for an employee, the system estimated the active calendar tenure instead of starting at `Month 0 of X` (0% fill), causing the progress bar to show initial fill prematurely.

### Solution
1. **Accurate Evaluation Milestone Display (`ProbationTracking.cshtml.cs`)**:
   - `CurrentMonth` now strictly reflects the highest evaluation month completed (`lastMonthVal`).
---

## Change 93 — Redesigned Intern & Probation Performance Profile Dashboard

### Problem
The performance profile page (`/Training/ViewProfile`) was a basic skeleton with only a single simple chart, lacking key employment metadata, aggregate score metrics, and detailed evaluation logs.

### Solution
1. **Executive Candidate Overview (`ViewProfile.cshtml`)**:
   - Hero header with candidate avatar, full name, EPF badge, designation, branch, department, program type pill, and quick-action "New Evaluation" button.
2. **KPI Score Cards**:
   - 4-card metric grid displaying Milestone Progression, Metric 1 Average (Technical Skills / Job Performance), Metric 2 Average (Communication / Attendance), and Metric 3 Average (Teamwork / Conduct).
3. **Interactive Trajectory Graph (Chart.js)**:
   - Modern curved spline chart with smooth gradient fill, dynamic month axis, and benchmark indicators.
4. **Milestone Evaluation Breakdown Table**:
---

## Change 94 — Activated & Modernized CV Bank Recruitment Pipeline

### Problem
The CV Bank feature lacked comprehensive search/filtering, live scoring calculation feedback on registration, position dropdown dynamic population, and dual-pane CV review.

### Solution
1. **Interactive Candidate Dashboard (`/CVBank/Index`)**:
   - Added 3 summary KPI cards (Total Candidates, High-Ranked Candidates $\ge 75$, Active Target Roles).
   - Integrated search bar (querying candidate name, email, contact, and skills) alongside position filters and minimum score filters.
   - Position-grouped candidate tables with rank score pills (tier-colored), qualification badges, and delete confirmation with physical file cleanup.
2. **Dynamic Intake Form with Live Score Calculator (`/CVBank/Create`)**:
   - Integrated real-time JavaScript rank preview updating as years of experience and degrees are toggled.
   - Dynamically merged company designations from `_context.Designations` with standard finance roles.
---

## Change 95 — Redesigned CV Bank Dashboard with Position Filter Tabs & Unified Layout

### Problem
The initial CV Bank dashboard used repeated `<thead>` fragments inside table loops, lacked role tabs, and felt visually fragmented.

### Solution
1. **Interactive Role Tabs (`/CVBank/Index`)**:
   - Replaced fragmented grouped tables with a unified, continuous table paired with horizontal role tab pills showing applicant counts (e.g. `All Roles (X)`, `Accountant (X)`, etc.).
2. **Unified Search & Score Toolbar**:
   - Integrated keyword search input with score threshold dropdown filter (`All Scores`, `Score >= 50%`, `Score >= 75%`) and quick reset actions.
3. **Refined Table Presentation**:
---

## Change 96 — Fixed Button Styles, Filter Bar Alignment, and Form Layout in CV Bank

### Problem
The Add Candidate button had dark green on green text contrast issues, the filter bar inputs were awkwardly stacked vertically, and the intake form had sizing/scroll alignment bugs.

### Solution
1. **CV Bank Dashboard (`/CVBank/Index`)**:
   - Fixed "+ Add Candidate" button to use `.btn-kanrich.btn-kanrich-primary` with crisp white text.
   - Restructured search input, position dropdown, score dropdown, and filter buttons into a single sleek horizontal toolbar.
   - Cleaned up the empty state with properly spaced action buttons.
---

## Change 97 — Removed Duplicate "Add Candidate" Button from Empty State

### Problem
The CV Bank dashboard displayed redundant "Add Candidate" buttons both at the top-right header and inside the empty state table area.

### Solution
---

## Change 98 — Implemented Public Candidate QR Code Job Application Portal & Shareable QR Flyers

### Problem
Candidates had no self-service mechanism to apply for vacancies by scanning a QR code at career fairs, posters, or social media, requiring HR officers to manually input all candidate profiles.

### Solution
1. **Public Application Portal (`/Apply` — `Apply.cshtml`, `Apply.cshtml.cs`)**:
   - Built a branded, mobile-optimized public intake page accessible anonymously (`[AllowAnonymous]`) without login.
   - Collects Full Name, Email, Contact Number, Position dropdown, Experience, Degree checkboxes, Skills, and Resume attachment.
   - Automatically computes competency ranking score $(0\text{--}100)$ upon submission and registers the applicant into the `CVBanks` repository.
---

## Change 99 — Direct 1-Click QR Code Flyer Download on CV Bank Dashboard

### Problem
---

## Change 100 — Automated Wi-Fi LAN IP Resolution & Configurable Careers URL for Mobile QR Code Scanning

### Problem
When running on `localhost`, scanning the QR code on a mobile phone caused the phone to attempt connecting to its own localhost loopback (`http://localhost:5282/Apply`), failing to connect.

### Solution
1. **Network Binding (`launchSettings.json`)**:
   - Updated Kestrel applicationUrl to `http://0.0.0.0:5282;http://localhost:5282` so external network devices on the same Wi-Fi/LAN can reach the server.
---

## Change 101 — Added Comprehensive Validation Rules Across Candidate Intake Forms

### Problem
Candidate intake forms (`/Apply` and `/CVBank/Create`) lacked strict validation rules for candidate names, phone number formats, email structures, position selections, and document types.

### Solution
1. **Server-Side Validation (`Apply.cshtml.cs`, `Create.cshtml.cs`)**:
   - **Candidate Name**: Required, length 2–100 chars, regex verified (`^[a-zA-Z\s\.\,\'\-]+$`).
   - **Email Address**: Required, RFC-compliant format regex (`^[^@\s]+@[^@\s]+\.[^@\s]+$`), max 100 chars.
   - **Phone Number**: Required on `/Apply`, regex validated for 9–15 digit phone formats (e.g. `0771234567` or `+94 77 123 4567`).
   - **Position**: Required valid selection.
   - **Experience**: Number range 0–50 years.
   - **Resume File**: Mandatory on `/Apply`, strict file extension filtering (`.pdf`, `.docx`, `.doc`), and 15MB file size limit.
---

## Change 102 — Implemented Real-Time Keystroke Validation Engine Across Candidate Intake Forms

### Problem
Previously, validation errors were only presented upon clicking Submit, allowing invalid phone text or broken email strings while typing without live visual feedback.

### Solution
1. **Live Event Listeners (`input`, `blur`, `change`)**:
   - Attached real-time listeners to Name, Email, Phone, Position, Experience, and CV File inputs in both `/Apply` and `/CVBank/Create`.
---

## Change 103 — Fixed Email Regex Pattern in Client-Side Validation

### Problem
The email regular expression pattern had redundant backslash escapes (`'[^\\\\s'` instead of standard `[a-zA-Z0-9._%+-]`), causing valid email addresses containing the letter 's' or hyphens to fail validation.

### Solution
- Standardized the client-side email regular expression to RFC-compliant `^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$` across `/Apply` and `/CVBank/Create`.
---

## Change 104 — Restricted Performance Module Access (Excluded Admin Role)

### Problem
The `Admin` role had access to `/Performance/Index` in both the page model attribute and the navigation sidebar.

### Solution
1. **Model Authorization (`Pages/Performance/Index.cshtml.cs`)**:
   - Updated `[Authorize(Roles = "HR Manager,HR Officer,Area Manager")]` to remove `Admin`.
---

## Change 105 — Realistic Multi-Status Attendance & Punctuality Scoring in Performance

### Problem
Previously, the Performance module strictly checked `a.Status == "Present"`, which mistakenly marked employees with `"Late"` arrivals as absent (unfair double-penalty) and gave employees without active attendance logs a hard-coded 50% penalty.

### Solution
1. **Multi-Status Attendance Credit (`Pages/Performance/Index.cshtml.cs`)**:
   - Accurately counts `"Present"` ($1.0$), `"Late"` ($1.0$), and `"HalfDay"` ($0.5$) in the attendance rate denominator.
   - Authorized leaves (`"OnLeave"`) are excluded from scheduled working days.
2. **Punctuality Evaluation (`Pages/Performance/Index.cshtml.cs`)**:
   - Determines punctuality based on actual physical attendances and on-time threshold ($08:30\text{ AM}$).
   - Computes $\text{Punctuality Score} = \frac{\text{On-Time Days}}{\text{Total Attended Days}} \times 100$.
---

## Change 106 — Benchmarked Company Working Days & Zero-Record Attendance Handling in Performance

### Problem
Employees with 0 attendance logs were defaulting to 100%, and employees with only 1 logged day were evaluated as $1/1 = 100\%$ regardless of how many working days had elapsed across the company.

### Solution
1. **Company Working Days Benchmark (`Pages/Performance/Index.cshtml.cs`)**:
   - Calculated `totalCompanyWorkingDates` by taking the distinct count of working dates in the `Attendances` database.
   - Evaluates each employee against their expected working dates based on join date (`emp.DateJoined`).
---

## Change 107 — Enforced Corporate Standard Monthly Working Days Baseline (20 Business Days)

### Problem
When the attendance database only contains 1 recorded date overall, `totalCompanyWorkingDates` was evaluated as 1, making 1 recorded day equal to $1/1 = 100\%$ attendance.

---

## Change 108 — Removed Welfare Pillar from Performance Module & Re-balanced Weights

### Problem
The user requested complete removal of the Welfare subsystem from the Performance analytics dashboard and scoring.

### Solution
1. **Rebalanced Core Scoring Formula (`Pages/Performance/Index.cshtml.cs`)**:
   - $\text{Total Score} = (\text{Attendance} \times 35\%) + (\text{Training} \times 30\%) + (\text{Leave Discipline} \times 20\%) + (\text{Punctuality} \times 15\%) = 100\%$.
   - Removed all `WelfareRequests` database queries and welfare score calculations.
2. **Updated Top KPI Cards & Summary Strip (`Pages/Performance/Index.cshtml`)**:
   - Replaced Welfare metric cards with **Average Attendance Rate** and **Total Staff Evaluated**.
   - Updated header description to reflect the new 4-pillar formula.
---

## Change 109 — Enforced Full Attendance Immutability Against Overwriting Records

### Problem
The user requested that once full attendance (both check-in and check-out) exists for an employee on a given date, importing different or subsequent records for that same date cannot overwrite the existing record.

---

## Change 110 — Fixed Biometric Import TimeIn/TimeOut Pairing & Role Scoping

### Problem
1. When importing attendance files, employees from multiple branches were skipped due to narrow branch scoping for corporate users (e.g. HR Manager / HR Officer).
### Solution
1. **Corporate Role Scope in Import (`Pages/BiometricLogs/Create.cshtml.cs`)**:
   - Allowed `HR Manager`, `HR Officer`, and `Area Manager` to import logs for all active employees across all branches, while preserving strict single-branch scoping for `Branch Manager`.
2. **Dynamic In/Out Pairing & 00:00 Healing (`Services/Impl/AttendanceService.cs`)**:
   - Earliest punch of the day is recorded as `TimeIn`.
   - Latest punch of the day is recorded as `TimeOut`.
   - Automatically replaces `00:00:00` placeholder timestamps with the actual punch time and calculates `TotalHours`.
3. **Database Cleanup**:
   - Cleared corrupted test attendance records with placeholder `00:00:00` times for clean re-import.

---

## Change 111 — Enforced Strict Single-Branch Isolation for Branch Manager Attendance Imports

### Problem
The user specified that a Branch Manager must only be able to import attendance records of employees belonging to their branch, and any records belonging to employees of other branches must be strictly ignored/skipped.

### Solution
1. **Branch-Scoped Employee Whitelist (`Pages/BiometricLogs/Create.cshtml.cs`)**:
   - For users in the `Branch Manager` role, the import process resolves their branch (`scopedBranchId`) and builds an authorization whitelist containing only active employees belonging to that branch.
   - Any record in the uploaded Excel/CSV file with an employee ID outside their branch is automatically ignored and counted as skipped.
   - Clear feedback in `TempData["SuccessMessage"]` / `TempData["ErrorMessage"]` specifies how many branch records were imported and how many records belonging to other branches were ignored.

---

## Change 112 — Scoped Performance Module to Assigned Branches for HR Officers

### Problem
In the performance dashboard, HR Officers were seeing company-wide employee evaluations rather than being scoped strictly to employees in the branches assigned to them.

### Solution
1. **Assigned Branch Filtering (`Pages/Performance/Index.cshtml.cs`)**:
   - Resolved the HR Officer's (and Area Manager's) assigned branches via `userAccount.ManagedBranches`.
   - Filtered `allEmployees` to only include active employees belonging to those assigned branch IDs.
   - Re-evaluated all company benchmark metrics (Department Stats, Average Performance Score, Top Performer Count, and Average Attendance Rate) strictly over the assigned branch employee subset.
2. **Dynamic UI Awareness (`Pages/Performance/Index.cshtml`)**:
   - Displayed assigned branch names in the header subtitle and updated card labels to "Assigned Average" when viewed by an HR Officer or Area Manager.

---

## Change 113 — Fixed Performance Evaluation Attendance Score Calculation

### Problem
Employees with partial attendance (e.g. 15 present days) were receiving an attendance score of 100%, the same as employees with full attendance (e.g. 22 present days), because `DateJoined` was incorrectly shrinking the `expectedWorkingDays` denominator below the actual corporate working dates.

### Solution
1. **Accurate Expected Working Days Benchmark (`Pages/Performance/Index.cshtml.cs`)**:
   - Fixed `expectedWorkingDays` to benchmark against the actual corporate working dates in the evaluation window (`companyBenchmarkDays`, e.g. 22 days).
   - Ensured new hire prorating only applies if the employee joined mid-cycle without historical attendance prior to their join date.
   - Result:
     * Full attendance ($22 / 22$) correctly displays **$100\%$**.
     * Partial attendance ($15 / 22$) correctly displays **$68.2\%$**.

---

## Change 114 — Aligned Performance Leave Discipline with Leave Module Policies

### Problem
1. When an employee had no `LeaveEntitlements` row explicitly pre-seeded (such as new or active staff with zero leaves taken), `leaveScore` fell back to an arbitrary hardcoded 70% and displayed `Leaves Used: —`.
2. When an employee had full entitlement records (including conditional maternity/overseas allocations), `leaveDaysTotal` summed all types to unrealistic totals (e.g., 161 days) rather than standard annual statutory quotas (35 days for Permanent, 14 days for Probation, 8 days for Intern).

### Solution
1. **Leave Policy Statutory Integration (`Pages/Performance/Index.cshtml.cs`)**:
   - Integrated general statutory annual leave baselines (35 days for Permanent, 14 for Probationary, 8 for Intern) matching the Leave module defaults.
   - Summed actual approved leave days (`allApprovedLeaves` and `UsedDays`).
   - Employees with 0 leaves used now receive **100% Leave Discipline** (`0 / 35` used).
   - Employees with approved leaves are evaluated against their standard quota (e.g. `10 / 35` used gives $\frac{25}{35} = 71.4\%$).

---

## Change 115 — Scoped Performance Leave Discipline to Last 30 Days Window

### Problem
The user specified that the Leave Discipline score in performance evaluation must be evaluated strictly based on how many leaves the employee has taken in the last 30 days window rather than annual accumulations.

### Solution
1. **30-Day Evaluation Window (`Pages/Performance/Index.cshtml.cs`)**:
   - Set the evaluation cutoff window to the last 30 days (`DateTime.Today.AddMonths(-1).Date` to `DateTime.Today`).
   - Counted approved leave days and attendance leave entries occurring within this 30-day window.
   - Evaluated the Leave Discipline score against the working days benchmark in the 30-day period ($\frac{\text{WorkingDays} - \text{LeaveDays}}{\text{WorkingDays}} \times 100\%$).
   - Displayed `Leaves Used: @leaveDaysUsed / @workingDays` in the employee detail modal.

---

## Change 116 — Expanded Performance Role Access & Branch Scoping with Filtering

### Problem
Previously, only HR Managers, HR Officers, and Area Managers could access the Performance Dashboard, preventing Branch Managers, Department Heads, and regular Employees from viewing performance within their branch, and Area Managers lacked the ability to filter performance evaluations by specific assigned branch.

### Solution
1. **Multi-Role Access Authorization (`Pages/Performance/Index.cshtml.cs` & `Pages/Shared/_Layout.cshtml`)**:
   - Authorized roles: `HR Manager`, `HR Officer`, `Area Manager`, `Branch Manager`, `Department Head`, and `Employee`.
   - Updated sidebar navigation so all authorized roles can see and navigate to the Performance Dashboard.
2. **Branch Scoping & Filtering**:
   - **Branch Manager, Department Head, Employee**: Strictly scoped to view employees within their own single branch.
   - **Area Manager & HR Officer**: Scoped across their assigned branches with a dynamic "Filter By Branch" dropdown to filter results by specific assigned branch.
   - **HR Manager**: Corporate-wide access with a branch filter across all company branches.

---

## Change 117 — Multi-Branch Performance Comparison & Branch-by-Branch Switching

### Problem
HR Officers assigned to more than one branch needed intuitive tools to view and compare performance metrics branch-by-branch, switch between individual branches rapidly, and filter the employee leaderboard by branch.

### Solution
1. **Branch Navigation Pills (`Pages/Performance/Index.cshtml`)**:
   - Added interactive branch switcher pills at the top of the Performance page displaying each assigned branch along with its average score (e.g. `Colombo (84.5%)`, `Kandy (79.2%)`).
   - One-click navigation instantly focuses the entire dashboard on a selected branch or returns to "All Assigned Branches".
2. **Branch-by-Branch Overview Grid (`Pages/Performance/Index.cshtml`)**:
   - Added a branch comparison card grid showing active staff count, average score, top performers count, and attendance rate for each assigned branch.
3. **Leaderboard Branch Column & Instant Filter**:
   - Added a dedicated `Branch` column badge to the leaderboard table.
   - Added a client-side `Branch` dropdown filter to filter leaderboard rows instantaneously.

---

## Change 118 — Fixed Branch-by-Branch Performance Comparison Cards Layout

### Problem
The branch comparison cards inherited a horizontal flex layout from global `.stat-card` CSS, causing the cards to be cramped and squishing the score badges and metrics vertically.

### Solution
1. **Dedicated CSS Class (`Pages/Performance/Index.cshtml`)**:
   - Created `.branch-comparison-card` with clean vertical flex column orientation, spacious card padding, progress bar, and hover elevation.
   - Expanded grid item minimum width to `minmax(320px, 1fr)`.
   - Score badges (`@bs.AvgScore / 100`) and attendance / top performers metrics now render cleanly without text wrapping or squishing.

---

## Change 119 — Horizontal Scrolling for Branch Comparison Cards

### Problem
When an HR Officer or Area Manager is assigned multiple branches (or when viewing on narrower viewports), cards could wrap down and push the dashboard content down.

### Solution
1. **Horizontal Scroll Container (`Pages/Performance/Index.cshtml`)**:
   - Created `.branch-cards-scroll-container` with smooth horizontal scrolling (`overflow-x: auto`), CSS scroll-snapping (`scroll-snap-type: x mandatory`), and a custom minimalist scrollbar.
   - Configured each `.branch-comparison-card` with fixed flex bounds (`min-width: 320px; max-width: 360px; flex: 0 0 auto`) ensuring cards flow horizontally in a carousel-like strip without breaking or squishing.

---

---

## Change 120 — Display Name with Initials in Performance Leaderboard

### Problem
The leaderboard table previously displayed the full employee name, which was lengthy for staff with multi-word names.

### Solution
1. **Name with Initials Formatting (`Pages/Performance/Index.cshtml.cs`)**:
   - Added `NameWithInitials` property and `FormatNameWithInitials(fullName, initials)` helper to `EmployeePerformance`.
2. **Table & Search Integration (`Pages/Performance/Index.cshtml`)**:
   - Displayed `@emp.NameWithInitials` (e.g. `H. D. R. N. Senarath`, `Y. Pasan`) in the leaderboard table with hover tooltip displaying their full name.
   - Preserved search flexibility via `data-name` containing both name with initials and full name.

---

---

## Change 121 — Removed Admin Access from Payroll Module

### Problem
The user specified that Admin must not have any access to the Payroll module.

### Solution
1. **Removed Admin Authorization (`Pages/Payroll/*.cshtml.cs`)**:
   - Updated `[Authorize(Roles = "HR Manager,HR Officer")]` across `Index.cshtml.cs`, `AttendanceReview.cshtml.cs`, `Bonuses.cshtml.cs`, and `EpfEtf.cshtml.cs`.
   - Added explicit `if (User.IsInRole("Admin")) return Forbid();` guards on all `OnGet` and `OnPost` action handlers across all 6 Payroll pages (`Index`, `AttendanceReview`, `Bonuses`, `EpfEtf`, `PaySlips`, `PaySlipPdf`).
2. **Sidebar Navigation (`Pages/Shared/_Layout.cshtml`)**:
   - Enclosed the Payroll navigation item in `@if (!User.IsInRole("Admin"))` so the menu item is hidden from Admin users completely.

---

## Change 122 — Allowed HR Officers and HR Managers to Configure Salary in Employee Profile

### Problem
Previously, only HR Managers could view and submit the salary configuration form in the employee profile (`/Employees/Details?id=X`), preventing HR Officers from configuring or updating salary details for employees in their assigned branches.

### Solution
1. **Backend Handler Authorization (`Pages/Employees/Details.cshtml.cs`)**:
   - Updated `OnPostUpdateSalaryAsync` to permit both `HR Manager` and `HR Officer` roles.
   - For `HR Officer`, verified that the employee belongs to one of their assigned branches via `ParseManagedBranches(currentUser.ManagedBranches)`.
2. **Profile UI View (`Pages/Employees/Details.cshtml`)**:
   - Updated the Salary & Benefits tab to display the salary configuration form and the live statutory contributions card (`EPF Employee (8%)`, `EPF Employer (12%)`, `ETF Employer (3%)`) for both `HR Manager` and `HR Officer`.

---

## Change 123 — Clean Number Formatting in Employee Profile Salary Configuration

### Problem
After saving or loading an employee's salary record, the `BasicSalary` and allowance numeric inputs were rendered with SQLite's full decimal precision (e.g. `50000.0000000000000000000000000` and `0.0000000000000000000000000000`), causing clutter and poor usability.

### Solution
1. **Explicit Value Formatting (`Pages/Employees/Details.cshtml`)**:
   - Replaced default binding value emission with clean invariant string formatting `ToString("0.##", CultureInfo.InvariantCulture)` for `BasicSalary`, `HousingAllowance`, `TransportAllowance`, and `MedicalAllowance`.
   - Prevented excessive trailing zeroes while preserving exact decimal precision entered by the user.

---

## Change 124 — Modal-Based Salary Configuration Menu in Employee Profile

### Problem
The salary configuration and update form was directly exposed on the profile page by default, taking up permanent layout space. The user requested a separate button to open the salary menu/form on demand.

### Solution
1. **Dedicated Action Buttons (`Pages/Employees/Details.cshtml`)**:
   - For employees without a salary record: Added a centered `+ Configure Salary` primary button inside the empty state card.
   - For employees with existing salary records: Added an `Update Salary` edit button in the card header next to "Current Salary & Benefits".
2. **Modal Dialog Architecture (`Pages/Employees/Details.cshtml`)**:
   - Moved the salary configuration form into a clean backdrop modal dialog (`#salaryModal`) with smooth animations and keyboard ESC support.
3. **Tab & Navigation Preservation (`Pages/Employees/Details.cshtml`, `Details.cshtml.cs`)**:
   - Added automatic tab restoration via URL hash (`#salary`) so saving a salary record redirects back directly to the active Salary & Benefits tab.

---

## Change 125 — Immediate & Deterministic Salary Update History Timeline

### Problem
When a user configured or updated salary details, the Salary Update History card required updating a second time to appear or only rendered older records due to `Skip(1)` exclusion and non-deterministic timestamp ordering on identical date values.

### Solution
1. **Deterministic Multi-Level Ordering (`Pages/Employees/Details.cshtml.cs`)**:
   - Added `.ThenByDescending(s => s.Id)` to the `PayrollSalaries` query, ensuring newly inserted salary records always take precedence regardless of database datetime resolution.
2. **Full History Timeline with Status Badges (`Pages/Employees/Details.cshtml`)**:
   - Updated the Salary Update History section to render immediately upon initial configuration (`Model.SalaryHistory.Any()`).
   - Visually distinguished the active current salary with a green border and `Current` badge, and previous revisions with a neutral `Previous` badge.
   - Displayed effective timestamp, total package, and allowance breakdowns for all history entries.

---

## Change 126 — Start Over / Reset Payroll Cycle Feature

### Problem
Once a monthly payroll cycle was executed, the system permanently locked the cycle in "Completed" state with generated payslips, preventing HR Officers and HR Managers from adjusting attendance anomalies, bonus allocations, or employee base salaries and re-running payroll.

### Solution
1. **Backend Reset Handler (`Pages/Payroll/Index.cshtml.cs`)**:
   - Added `OnPostStartOverPayrollAsync(int? month, int? year)` handler to remove all generated `Payslips` and delete the `PayrollRun` entity for the specified month and year, reverting the cycle to an editable state.
2. **UI Reset Triggers & Modal Confirmation (`Pages/Payroll/Index.cshtml`)**:
   - Updated the header action bar to dynamically show **`Start Over Cycle`** (`bi-arrow-counterclockwise`) when payroll has already been processed for the current month.
   - Added a `Start Over` quick action in Checklist Step 4 (*Final Verification & Generation*).
   - Added a reset icon button on items in the *Past Cycles* list.
   - Introduced a dedicated `#startOverModal` with a clear warning explaining that generated payslips will be cleared so adjustments can be made and payroll re-run cleanly.

---

## Change 127 — Branch-by-Branch Payroll Processing for Multi-Branch HR Officers & Managers

### Problem
Some HR Officers are assigned to manage multiple branches, while HR Managers oversee all company branches. Previously, payroll ran on a global whole-company basis, which did not support running, verifying, resetting, or tracking payroll independently branch by branch.

### Solution
1. **Domain & Database Schema Evolution (`PayrollRun.cs`, `ApplicationDbContext.cs`, `Program.cs`)**:
   - Added `public int? BranchId { get; set; }` and navigation `public Branch? Branch { get; set; }` to `PayrollRun`.
   - Updated DbContext mappings and configured database schema migration with automatic `ALTER TABLE payrollrun ADD COLUMN BranchId int NULL`.
2. **Branch Access Scoping & Selector UI (`Pages/Payroll/Index.cshtml`, `Index.cshtml.cs`)**:
   - Resolved assigned branches from `currentUser.ManagedBranches` for HR Officers and all branches for HR Managers.
   - Added a sleek **Branch Switcher** dropdown in the sub-navigation header allowing users to toggle between branches.
   - Scoped all metrics (`Estimated Payroll Total`, `Total Employees`, `Bonuses`, `Deductions`, `Attendance Anomalies`), checklist steps, and past cycle history to the selected branch.
3. **Branch-Scoped Execution & Reset Handlers**:
   - `OnPostRunPayrollAsync(int branchId)`: Executes calculations and generates payslips specifically for the active branch's employees, saving `BranchId` on the `PayrollRun`.
   - `OnPostStartOverPayrollAsync(int? month, int? year, int branchId)`: Resets and clears payslips only for the specified branch and cycle without touching other branches.
4. **End-to-End Branch Support Across All Payroll Pages**:
   - Updated `AttendanceReview`, `Bonuses`, `EpfEtf`, and `Payslips` pages to preserve the active `?branchId=X` parameter, display the active branch context, and filter employee records by branch.

---

## Change 128 — Automatic Processing Checklist Reset on Cycle Restart

### Problem
When starting over a payroll cycle, the checklist steps previously remained marked as "Completed" (Step 1 and Step 3 unconditionally showed completed badges from existing base salary DB records, Step 2 retained client-side finalized flags, and only Step 4 reset), causing the checklist to still look mostly completed instead of resetting back to an editable preparation state.

### Solution
1. **Multi-State Lifecycle for All Checklist Steps (`Pages/Payroll/Index.cshtml`)**:
   - **Step 1 (Salary & Additions)**: When payroll has been run, shows green `Completed`. When starting over / pending run, shows blue `Ready · Configured` (with direct links to *Manage Bonuses* & *Review Salaries*) or amber `Action Required` if salaries missing.
   - **Step 2 (Attendance & Leave)**: When payroll has been run, shows green `Completed`. When starting over / pending run, dynamically displays red `Needs Review` with a direct *Resolve Anomalies* button if anomalies exist, or yellow `Pending Review` with *Review Attendance*.
   - **Step 3 (Statutory Deductions)**: When payroll has been run, shows green `Completed`. When starting over / pending run, displays neutral `Pending Run` indicating deductions are estimated and will lock upon execution.
   - **Step 4 (Final Generation)**: When payroll has been run, shows green `Completed` with *View Payslips* and *Start Over*. When starting over / pending run, shows grey `Pending` circle and prompts to click the primary *Run Payroll* button.
2. **Branch-Scoped Session Isolation & Force Reset (`Index.cshtml`, `AttendanceReview.cshtml`, `Index.cshtml.cs`)**:
   - Scoped session keys by branch (`attendance_states_{BranchId}` and `attendance_finalized_{BranchId}`).
   - Added `onStartOverSubmit()` and server-side `TempData["ResetChecklist"]` to purge all cached finalized states when a cycle is reset.

---

## Change 129 — Payroll Attendance Review Accuracy and Month Scoping

### Problem
In the Payroll Attendance Review section, attendance records and leaves were strictly hardcoded to the current system date without dynamic month/year scoping. Furthermore, timestamps on the final day of the month could fall outside queries due to inclusive date filters, overtime with non-null check-in/out without explicit `TotalHours` was omitted, and leaves overlapping month boundaries were not computed strictly to the days within that active month.

### Solution
1. **Dynamic Month & Cycle Support (`AttendanceReview.cshtml.cs`, `AttendanceReview.cshtml`)**:
   - Added `[BindProperty(SupportsGet = true)] public int? Month { get; set; }` and `Year { get; set; }`.
   - Added a **Cycle Switcher** dropdown in the top header allowing HR officers and managers to select and review any monthly cycle.
2. **Accurate Date Boundary & Overtime Calculations**:
   - Changed attendance queries to strict open-ended boundary `a.Date >= startOfMonth && a.Date < nextMonth` ensuring 100% of attendance logs on the last day of the month are captured.
   - Updated overtime calculation to fall back to `TimeOut - TimeIn - 8.0` hours if `TotalHours` was not pre-populated.
   - Computed exact overlap days for approved leaves that cross month boundaries (splitting paid vs no-pay leaves accurately for the selected cycle).
   - Flagged anomalies accurately for any un-closed past check-ins.

---

## Change 130 — Redesign of EPF & ETF Statutory Review Tab

### Problem
The EPF & ETF statutory review page previously had bulky badge boxes on every table cell, causing numbers to wrap awkwardly (`Rs \n 1,600.00`), overcrowded columns, oversized KPI blocks, and lacked dynamic cycle/month switching, department filters, Form C statutory return schedules, and export capabilities.

### Solution
1. **Sleek & Lightweight Typography (`EpfEtf.cshtml`)**:
   - Replaced all chunky bubble badge backgrounds with crisp, clean typography with subtle color accents (`#0284c7` for 8% EPF, `#10823c` for 12% EPF, `#c2410c` for 3% ETF, and `#4338ca` for 23% Total).
   - Enforced `white-space: nowrap;` across all financial figures to completely prevent two-line currency wrapping.
   - Streamlined columns to standard proportion: *Employee*, *Department & Role*, *Basic Salary*, *EPF (8% Emp)*, *EPF (12% Co)*, *ETF (3% Co)*, *Total (23%)*, and *Action*.
2. **Compact 4-Card Statutory KPI Grid & Slim Compliance Strip**:
   - Reduced from 6 oversized cards down to 4 compact, high-density metric cards (*Total Statutory 23%*, *Central Bank EPF 20%*, *ETF Board 3%*, *Qualifying Wages*).
   - Condensed statutory legal reference into a slim, single-line compliance strip with Employer Registration Number `EPF/KFL/88421`.
3. **Form C Statutory Return & Export Features**:
   - Added a **Statutory Return / Form C Modal** with clean print layout formatted as the Central Bank EPF monthly return schedule, complete with signature and approval certification blocks.
   - Added client-side **Export CSV** generating a spreadsheet of the branch's statutory schedule.
   - Added an **Employee Details Modal** showing the individual earnings structure, statutory contributions, and estimated net pay.

---

## Change 131 — Redesign and Renaming of Bonuses Tab to Allowances

### Problem
The "Bonuses" tab used a rigid 2-column split with a static left form taking up excessive horizontal space, lacked dynamic month/cycle filtering, editing capabilities, department filters, and modern metric insights. Furthermore, the tab label across navigation needed to be standardized to "Allowances".

### Solution
1. **Module-Wide Renaming to Allowances (`Index.cshtml`, `AttendanceReview.cshtml`, `EpfEtf.cshtml`, `PaySlips.cshtml`, `Bonuses.cshtml`)**:
   - Renamed navigation tab links from "Bonuses" to "Allowances".
   - Updated processing lifecycle checklist button and dashboard metrics to "Salary Additions & Allowances Review".
2. **Allowance Workstation & 4-Card KPI Grid (`Bonuses.cshtml`, `Bonuses.cshtml.cs`)**:
   - Added 4 top metric cards: *Total Allowances (Active Month)*, *Active Beneficiaries*, *Top Category Volume*, and *Average per Beneficiary*.
   - Added dynamic cycle selector (`Month` & `Year`) and branch switcher.
3. **Interactive Full-Width Allowance Schedule**:
   - Real-time search by employee name, EPF number, and remarks.
   - Filter by allowance category (*Performance Bonus*, *Festival Allowance*, *Transport / Fuel*, *Housing*, *Meal / Food*, *Overtime*, *Medical*, *Special Duty*, *Other*).
   - Filter by department and cycle.
   - Real-time table footer total sum dynamically recalculated as filters change.
4. **Modal-Based Allowance Creation, Editing, and CSV Export**:
   - Replaced static form with responsive **Grant Allowance Modal** and **Edit Allowance Modal** supporting `OnPostEditAsync`.
   - Added client-side **Export CSV** generating an allowance spreadsheet report for the branch and active cycle.

---

## Change 132 — Removal of Placeholder Edit Button in Attendance Review

### Problem
In `AttendanceReview.cshtml`, rows with status `Verified` displayed a placeholder `Edit` button that had no active click handler attached, causing confusion.

### Solution
1. **Clean Row Action State (`AttendanceReview.cshtml`)**:
   - Replaced the placeholder `Edit` button in both server-side HTML rendering and client-side `setRowVerifiedDOM` JavaScript with a clean `—` dash indicator.
   - Preserved active buttons (`Resolve` for anomalies and `Validate` for pending rows).

---

## Change 133 — Removal of Action Column from Attendance Review Table

### Problem
The Attendance Review table included an action column per row that was redundant with the top bulk verification tools (*Resolve All Anomalies* and *Mark as Finalized*).

### Solution
1. **Clean Table Layout (`AttendanceReview.cshtml`)**:
   - Removed the `Action` table header (`<th>`) and row cells (`<td class="action-cell">`) from the Attendance Review table.
   - Updated client-side DOM manipulation scripts (`setRowVerifiedDOM`) to maintain the status badge cleanly without searching for action cells.

---

## Change 134 — Standardized Employee Name with Initials Across All Payroll Views

### Problem
Throughout the Payroll module, employee names were displayed as raw full names (`Ronaka Sampath Samarawickrama`), whereas standard corporate payroll conventions in Sri Lanka require "Name with Initials" (e.g. `R.S. Samarawickrama`).

### Solution
1. **Domain Model Property (`Employee.cs`)**:
   - Added computed property `NameWithInitials => FormatNameWithInitials(FullName, Initials)` and standard `FormatNameWithInitials(string? fullName, string? initials)` fallback formatter to the `Employee` domain entity.
2. **Payroll Pages Updated**:
   - **Attendance Review (`AttendanceReview.cshtml.cs`)**: Populated `AttendanceRecord.Name` with `emp.NameWithInitials`.
   - **Allowances / Bonuses (`Bonuses.cshtml`)**: Updated table rows, avatars, search attributes, edit triggers, and modal dropdown selects to display `emp.NameWithInitials`.
   - **EPF & ETF (`EpfEtf.cshtml`)**: Updated table rows, avatars, search attributes, breakdown modal, and Central Bank return schedule to display `emp.NameWithInitials`.
   - **Payslips (`PaySlips.cshtml`)**: Updated table rows, avatars, search attributes, and slide-over modal payload serializer to display `emp.NameWithInitials`.
   - **Payslip PDF (`PaySlipPdf.cshtml`)**: Updated payslip title and employee header section to display `emp.NameWithInitials`.

---

## Change 135 — Removal of Resolve and Finalize Buttons from Attendance Review

### Problem
The `Resolve Anomalies` and `Mark as Finalized` top header buttons in `AttendanceReview.cshtml` were redundant after transitioning attendance reconciliation into an automated summary view.

### Solution
1. **Header & Script Simplification (`AttendanceReview.cshtml`)**:
   - Removed the `Resolve Anomalies` and `Mark as Finalized` buttons from the page header.
   - Cleaned up JavaScript handlers, maintaining lightweight live filtering by department, status, and search query.

---

## Change 136 — Relocation of Run Payroll Button to Last Step of Processing Checklist

### Problem
Having the "Run Payroll" button in the top page header was disconnected from the step-by-step sequential workflow of the Processing Checklist on the Payroll Dashboard.

### Solution
1. **Dashboard Header & Checklist Restructuring (`Index.cshtml`)**:
   - Removed the `Run Payroll` action button from the top page header block, keeping `Export Report` and `Start Over Cycle`.
   - Integrated the `Run Payroll` execution button directly into **Step 4: Final Verification & Payroll Execution** inside the Processing Checklist.
   - Highlighted Step 4 with an active green accent container, actionable primary button, and a `"Ready to Run"` status badge when payroll is pending.

---

## Change 137 — Verification Checkboxes for Checklist Steps & Gated Payroll Execution

### Problem
HR officers could inadvertently run payroll before reviewing and verifying salary allowances, attendance reconciliation, and statutory EPF/ETF contributions.

### Solution
1. **Interactive Step Checkboxes (`Index.cshtml`)**:
   - Added verification check boxes to **Step 1** (Salary & Allowances), **Step 2** (Attendance & Leaves), and **Step 3** (Statutory Deductions).
   - Designed interactive check toggle controls (`.step-check-label`, `.step-check-input`) with smooth hover and checked states.
2. **Gated Step 4 Execution (`Index.cshtml`)**:
   - Gated the `Run Payroll` button in Step 4: disabled by default with locked status badge (`Locked (X/3 verified)`) until all 3 prerequisite steps are checked.
   - Dynamically unlocks the `Run Payroll` button with green active styling, progress tracker, and `Ready to Run (3/3 ✓)` badge once Steps 1, 2, and 3 are marked verified.
3. **Session Persistence & Reset Lifecycle**:
   - Stored checklist checkbox states in `sessionStorage` per branch, month, and year.
   - Automated clean resets when clicking "Start Over Cycle" or upon cycle reset post-execution.

---

## Change 138 — Alignment Fixes for Attendance Review Table

### Problem
In `AttendanceReview.cshtml`, table column headers (`<th>`) and body cells (`<td>`) lacked matching text alignments, fixed column proportions, and uniform padding, causing titles and numerical data to look misaligned.

### Solution
1. **Harmonized Column Alignments (`AttendanceReview.cshtml`)**:
   - Explicitly assigned column percentage widths (`table-layout: fixed;`), horizontal alignments, and vertical alignment (`vertical-align: middle;`) to all 6 table columns:
     - **Employee** (30%): Left-aligned avatar, Name with initials, EPF number, and department.
     - **Working Days** (15%): Center-aligned working days count (`X / Y days`).
     - **Paid Leaves** (12%): Center-aligned integer count.
     - **No-Pay Leaves** (15%): Center-aligned styled pill badges (`Unexcused`, `Approved`, or neutral `0`).
     - **Approved OT** (14%): Center-aligned blue overtime hours (`X hrs`).
     - **Status** (14%): Center-aligned status badge.

---

## Change 139 — Hidden Welfare Section from Sidebar for All Roles

### Problem
The Welfare module links in the main application navigation sidebar were requested to be hidden across all user accounts and roles.

### Solution
1. **Sidebar Navigation Update (`_Layout.cshtml`)**:
   - Removed the Welfare navigation list item (`<li class="nav-item">`) from the main sidebar for all roles (Department Head, Branch Manager, Area Manager, HR Manager, HR Officer, and General Employee).

---

## Change 140 — Corporate Calendar Module with Role-Scoped Training & Automated Reminders

### Problem
The application lacked an interactive calendar. Employees needed visibility into training sessions they are assigned to and the ability to schedule their own events/meetings with automatic reminders (24h before & 1h before). Management roles (HR Manager, HR Officer, Area Manager, Branch Manager, Department Head) required branch-scoped and department-scoped training visibility.

### Solution
1. **Domain & Persistence Layer (`CalendarEvent.cs`, `ApplicationDbContext.cs`, `Program.cs`)**:
   - Created `CalendarEvent` entity with title, type, start/end time, all-day flag, venue, meeting link, description, creator, employee, branch, department, and notification tracking flags.
   - Registered `CalendarEvents` DbSet in `ApplicationDbContext` and added DDL table auto-creation in `Program.cs`.
2. **Automated Reminders Background Service (`CalendarReminderBackgroundService.cs`)**:
   - Built a background service running every 60 seconds evaluating both `CalendarEvents` and scheduled `Trainings`.
   - Dispatches in-app `Notifications` to enrolled employees and event owners **1 day before** (`📅 Tomorrow`) and **1 hour before** (`⏰ In 1 Hour`) with location/meeting link and direct navigation.
3. **Role-Scoped Interactive Calendar UI (`Pages/Calendar/Index.cshtml`, `Index.cshtml.cs`)**:
   - **Month, Week, Day, and Agenda Views**: Interactive grid with navigation, today selector, category pills (🟢 Training, 🔵 Meetings, 🟣 Company Events, 🟠 Personal), and 4 KPI summary cards.
   - **Role Scoping**:
     - *Employees*: View assigned training sessions, personal events, and branch/company holidays.
     - *HR Managers & HR Officers*: View training sessions for assigned branches, company-wide events, and personal events.
     - *Area Managers & Branch Managers*: View branch-specific training sessions and events.
     - *Department Heads*: View department employee training sessions and events.
   - **Event Management**: Modal forms to create, edit, and delete personal events/meetings with meeting URLs and locations.
4. **Navigation & Dashboard Linking**:
   - Linked sidebar navigation to `/Calendar/Index` in `_Layout.cshtml`.
   - Linked dashboard `"View Full Calendar"` button and populated `"Upcoming Events"` widget dynamically with real events in `Index.cshtml` & `Index.cshtml.cs`.

---

## Change 141 — Fixed Razor View Block Syntax in Calendar UI

### Problem
In `Pages/Calendar/Index.cshtml`, HTML comments separating conditional blocks caused Razor to exit C# mode, leaking literal `else if (Model.CurrentView == "week") {` and `} else {` strings into the rendered HTML.

### Solution
1. **Clean Razor View Structuring (`Index.cshtml`)**:
   - Converted view branching into independent, clean `@if (Model.CurrentView == "...")` conditional blocks for Month, Week, and Day/Agenda views.
   - Cleaned up all comments and template markup, ensuring flawless rendering without leaking C# syntax.

---

## Change 142 — Uniform Month Grid Cell Sizing & "+X more" Event Overflow

### Problem
In the Month View of `Pages/Calendar/Index.cshtml`, days with multiple events caused individual day cells and grid rows to expand vertically, creating uneven, distorted calendar rows.

### Solution
1. **Mathematically Fixed Grid Heights (`Index.cshtml`)**:
   - Enforced `grid-auto-rows: 120px;` on `.month-days-grid` and fixed height constraints (`height: 120px; max-height: 120px; box-sizing: border-box; overflow: hidden;`) on all `.day-cell` elements.
2. **Event Badge Overflow Handling**:
   - Limited rendered visible event badges to **2 items** per day cell (`cell.Events.Take(2)`).
   - Displayed an interactive `+X more` badge for any remaining events that links directly to the Day view agenda for that date.

---

## Change 143 — Post-Training Feedback Notifications, Employee Rating Submission & Public Review Visibility

### Problem
When HR marked a training session as Completed, participants were not prompted for feedback. Additionally, employees had no interface to submit ratings/comments, and authorized viewers (HR, Managers, participants) could not view participant feedback.

### Solution
1. **Feedback Prompt Notifications (`TrainingNotificationService.cs`)**:
   - Updated `NotifySessionStatusChangedAsync` when status is marked `"Completed"`:
     - Dispatches a targeted notification (`🎓 Training Completed: Feedback Requested - {title}`) to all enrolled participants prompting them to review the session with a direct anchor link to `#feedbackSection`.
2. **Participant Rating & Feedback Submission (`SessionDetails.cshtml.cs`)**:
   - Implemented `OnPostSubmitFeedbackAsync` handler allowing enrolled attendees to submit/update star ratings (1–5) and comments stored in `TrainingFeedbacks`.
3. **Participant Feedback UI (`SessionDetails.cshtml`)**:
   - Rendered an interactive **"Participant Feedback & Ratings"** card on completed sessions featuring:
     - Overall average rating score (e.g. `4.8 / 5.0`) and star breakdown.
     - Interactive 5-star rating selector and comment input for enrolled participants.
     - List of all participant reviews displaying name, designation, department, star rating, submission date, and comments.
     - Transparent review visibility for all authorized viewers (HR Managers, HR Officers, Area Managers, Branch Managers, Department Heads, and Attendees).

---

## Change 144 — Duty Account Creation, Editing, MySQL Deletion & Navigation Fixes

### Problem
1. **Unclickable Role Cards (`Create.cshtml`)**: An unclosed `document.addEventListener('DOMContentLoaded', ...)` block caused a JavaScript `SyntaxError`, resulting in `Uncaught ReferenceError: selectRole is not defined` whenever an admin clicked on any role card (HR Manager, Area Manager, Branch Manager, Department Head).
2. **Duty Account Entity Assignment (`Create.cshtml.cs`)**:
   - The internal `Employee` record created for a duty account was assigned random job designations via a generic fallback instead of its designated managerial title (Branch Manager, Area Manager, Department Head, HR Manager).
   - Hardcoded duplicate NIC (`DUTY-ACC`) and EPF (`N/A`) values across duty accounts created risk of unique constraint conflicts.
   - Department Head creation failed if `BranchDepartments` records were not pre-populated in the database.
3. **Department Head Edit 404 Error (`Edit.cshtml.cs` & `Edit.cshtml`)**:
   - `EditDutyAccountModel` only recognized `"HR Officer", "HR Manager", "Area Manager", "Branch Manager"`, causing "Edit" on Department Head accounts to throw a `404 Not Found`.
   - `Edit.cshtml` lacked form inputs for branch and department assignment for Department Heads.
4. **MySQL Syntax Errors on Deletion (`Index.cshtml.cs` & `Users/Index.cshtml.cs`)**:
   - Deletion handlers executed SQL Server-specific T-SQL (`IF OBJECT_ID('dbo.AspNetUserClaims', 'U') IS NOT NULL...`), causing syntax errors on MySQL database instances.
5. **Authorization Inconsistency**:
   - `Index.cshtml.cs` had `[Authorize(Roles = "Admin")]`, while `Create` and `Edit` had `[Authorize(Roles = "Admin,HR Manager")]`. When an HR Manager created an account and was redirected to `./Index`, an HTTP 403 Forbidden error was triggered.
6. **Navigation**:
   - Cancel buttons in `Create.cshtml` pointed to `/Employees` instead of `/Admin/DutyAccounts`.
   - Admin sidebar lacked direct links to Duty Accounts and User Accounts.

### Solution
1. **Interactive Role Cards & JavaScript Fix (`Create.cshtml`)**:
   - Corrected JavaScript scoping and event listeners, making `selectRole` and dropdown helpers globally accessible without syntax errors.
   - Added instant visual feedback (card selection border, green background, checkmark icon, and smooth form panel reveals).
   - Added client-side validation to prevent empty submissions for all roles.
   - Updated Cancel buttons to return to `/Admin/DutyAccounts`.
2. **Duty Account Backend & Data Integrity (`Create.cshtml.cs`)**:
   - Ensured core managerial designations (`HR Manager`, `Branch Manager`, `Area Manager`, `Department Head`) exist in `Designations` and assigned them to `Employee.DesignationId`.
   - Assigned unique duty NIC and EPF identifiers (e.g. `DUTY-BM-{BranchId}`, `DUTY-DH-{BranchId}-{DeptId}`, `DUTY-AM-{Area}`, `DUTY-HRM`).
   - Added automatic fallback pairing for Department Heads to auto-link branch and department if `BranchDepartments` was uninitialized.
3. **Full Department Head Edit Capabilities (`Edit.cshtml.cs` & `Edit.cshtml`)**:
   - Added `"Department Head"` to supported edit roles and provided branch and department selector dropdowns.
   - Synchronized changes to both `ApplicationUser` and the linked `Employee` entity.
4. **Cross-Database Compatible Deletion (`Index.cshtml.cs` & `Users/Index.cshtml.cs`)**:
   - Replaced provider-specific T-SQL with standard ASP.NET Core Identity `UserManager.DeleteAsync` and Entity Framework employee removal, ensuring full compatibility with MySQL and SQL Server.
5. **Synchronized Authorization (`Index.cshtml.cs`)**:
   - Updated `Index.cshtml.cs` to `[Authorize(Roles = "Admin,HR Manager")]`.
6. **Admin Sidebar Navigation (`_Layout.cshtml`)**:
   - Added direct sidebar navigation links for **Duty Accounts**, **User Accounts**, and **System Settings** when logged in as Admin.

---

## Change 145 — Removal of Email from User Account Management

### Problem
In User Account Management (`/Admin/Users`), emails were displayed in table rows, modals, and input fields. Since users log in with usernames and corporate duty/staff accounts are referenced by usernames and employee names, showing emails created unnecessary visual clutter.

### Solution
1. **User Accounts Table (`Pages/Admin/Users/Index.cshtml`)**:
   - Updated table column header from `Email / Username` to `Username`.
   - Replaced email display with stylized green username badges (`@user.UserName`) and employee display names where applicable.
2. **Account Creation & Reset Modals (`Index.cshtml` & `Index.cshtml.cs`)**:
   - Updated **Create User Modal** to accept **Username** directly (e.g. `john.silva`, `admin2`), removing the email input requirement and auto-generating the backing Identity email.
   - Updated **Reset Password Modal** and **Assign Role Modal** to display `Username: <username>` rather than email addresses.
   - Updated employee selector dropdown in the Create modal to format options as `<FullName> (EPF: <EPFNumber>)` without exposing email addresses.

---

## Change 146 — User Account Management Scoped to Duty Accounts & Removed Create Button

### Problem
In User Account Management (`/Admin/Users`), regular employee accounts were listed alongside duty/system accounts. Additionally, a "Create User Account" button was present on the page, which duplicated duty account provisioning and risked creating unstructured user accounts.

### Solution
1. **Removed Create User Account Button (`Pages/Admin/Users/Index.cshtml`)**:
   - Removed the `+ Create User Account` header button and the modal dialog markup. Duty accounts are exclusively created via the dedicated Duty Accounts module (`/Admin/DutyAccounts/Create`).
2. **Filtered to Duty & Administrative Accounts Only (`Pages/Admin/Users/Index.cshtml.cs`)**:
   - Filtered the user account query in `OnGetAsync` to strictly display duty/administrative accounts (`Admin`, `HR Manager`, `HR Officer`, `Area Manager`, `Branch Manager`, `Department Head` and accounts with usernames `admin`, `hrmanager`, `bm.*`, `am.*`, `dh.*`, `hro.*`).
   - Excluded all regular non-managerial employee accounts.

---

## Change 147 — Real-Time Username Search & Role Filters in User Account Management

### Problem
Admins viewing the duty accounts list in User Account Management (`/Admin/Users`) needed a quick way to filter accounts by duty role (Admin, HR Manager, HR Officer, Area Manager, Branch Manager, Department Head) and search accounts in real time by username.

### Solution
1. **Filter & Search Toolbar (`Pages/Admin/Users/Index.cshtml`)**:
   - Added a top toolbar featuring:
     - **Real-Time Username Search Input** with a search icon for live substring matching across usernames and full names.
     - **Duty Role Filter Dropdown** allowing instant filtering by specific duty roles (`All Duty Roles`, `Admin`, `HR Manager`, `HR Officer`, `Area Manager`, `Branch Manager`, `Department Head`).
     - **Dynamic Result Count Badge** (e.g. `Showing X duty accounts`).
2. **Client-Side Live Filter Engine (`Index.cshtml`)**:
   - Implemented `filterDutyAccounts()` in JavaScript: evaluates each table row's `data-username`, `data-fullname`, and `data-roles` in real-time.
   - Added an interactive `noMatchesRow` state that appears when no duty accounts match the active search query or role filter.

---

## Change 148 — Removed Redundant Linked Employee Subtitle Under Username

### Problem
In User Account Management (`/Admin/Users`), the linked employee/position full name was rendered twice in the same row: once directly below the username badge and again in the dedicated "Linked Employee" column, creating unnecessary visual repetition.

### Solution
1. **Clean Username Column (`Pages/Admin/Users/Index.cshtml`)**:
   - Removed the duplicate full name subtext `<div style="font-size: 11px; ...">@user.FullName</div>` from under the `@user.UserName` badge in the Username column.
   - The linked duty position remains clearly and solely displayed in the "Linked Employee" column.

---

## Change 149 — Renamed Table Column from Linked Employee to User

### Problem
In User Account Management (`/Admin/Users`), the second column was named "Linked Employee", which caused ambiguity when viewing managerial duty accounts.

### Solution
1. **Renamed Column Header (`Pages/Admin/Users/Index.cshtml`)**:
   - Updated the column header `<th>` from `Linked Employee` to `User`.

---

## Change 150 — Removed Add Role Action & Modal from User Account Management

### Problem
In User Account Management (`/Admin/Users`), each row featured an "Add Role" action button and an "Assign System Role" modal. Because duty accounts are strictly provisioned with their designated managerial roles in the Duty Accounts module, having an ad-hoc role assignment action in this view was redundant and risked role desynchronization.

### Solution
1. **Removed Add Role Button & Modal (`Pages/Admin/Users/Index.cshtml`)**:
   - Removed the `Add Role` action button from table rows.
   - Removed the `Assign System Role` modal dialog markup (`#roleModal`) and corresponding JavaScript open/close functions.
   - Cleaned up role badges into streamlined pill badges without inline deletion triggers.

---

## Change 151 — Removed Inline Role Removal Trigger and Backend Handlers

### Problem
In User Account Management (`/Admin/Users`), duty account role badges previously retained inline role removal triggers (`&times;`), allowing accidental revocation of core managerial roles without duty account deprovisioning.

### Solution
1. **Pill Badges Only (`Pages/Admin/Users/Index.cshtml`)**:
   - Removed the inline role deletion forms and `&times;` buttons from the `Assigned Roles` column. Role badges are now purely informational badges.
2. **Removed Backend Handlers (`Pages/Admin/Users/Index.cshtml.cs`)**:
   - Removed `OnPostAssignRoleAsync` and `OnPostRemoveRoleAsync` page handlers.

---

## Change 152 — Pagination Support for User Account Management Table

### Problem
In User Account Management (`/Admin/Users`), all duty accounts were displayed in a single unpaginated list, which became lengthy as branches, departments, and managerial accounts grew.

### Solution
1. **Interactive Pagination Bar (`Pages/Admin/Users/Index.cshtml`)**:
   - Added a pagination bar below the duty accounts table featuring:
     - Record range indicator (e.g. `Showing 1–10 of 24`).
     - Page size selector dropdown (`5`, `10`, `20`, `50` per page).
     - Responsive page navigation buttons with active state highlighting (`← Prev`, `1`, `2`, `...`, `Next →`).
2. **Filter & Search Synchronization**:
   - Integrated pagination with real-time username search and role filtering. Changing search queries or role filters dynamically updates pagination bounds and resets the active page to 1.

---

## Change 153 — Cascading Designation and Department Dropdown Initialization on Fresh Employee Form Load

### Problem
When the Employee Creation form (`/Employees/Create`) was freshly loaded, `LoadDropdownsAsync()` loaded all designations in the database into `Model.DesignationList` and rendered them into `#designationSelect`. This allowed users to select any company-wide designation before picking a Branch or Department, breaking the cascading branch $\rightarrow$ department $\rightarrow$ designation dependency.

### Solution
1. **Server-Side Dropdown Scoping (`Pages/Employees/Create.cshtml.cs`)**:
   - Updated `LoadDropdownsAsync()`: `DesignationList` is now populated strictly when a valid `DepartmentId` is selected. On fresh load (when `DepartmentId` is 0 or null), `DesignationList` remains empty.
2. **Dynamic UI Placeholders & Cascading Handlers (`Pages/Employees/Create.cshtml`)**:
   - Updated Razor markup:
     - `#departmentSelect` shows `Select Branch First` when no branch is chosen.
     - `#designationSelect` shows `Select Department First` when no department is chosen.
   - Enhanced client-side event listeners: on fresh load or when branch/department selection is cleared, child dropdowns are promptly reset with descriptive guidance placeholders (`Select Branch First` / `Select Department First`) rather than leaving stale or un-scoped options.

---

## Change 154 — Robust Duty Account Filtering Across Employee Queries

### Problem
After updating duty account records to use structured identifiers (e.g. `DUTY-BM-1`, `DUTY-AM-HAMBANTHOTA`, `DUTY-DH-1-2`), several queries across the system (including the Employee directory `/Employees/Index`, Dashboard `/Index`, Performance, Termination, Payroll, Welfare, and Leaves) only checked `e.NIC != "DUTY-ACC"`. Consequently, newly formatted duty accounts were mistakenly returned in employee directory tables and metrics.

### Solution
1. **Comprehensive Duty Prefix Exclusions**:
   - Updated LINQ queries across `Employees/Index.cshtml.cs`, `Index.cshtml.cs`, `Performance/Index.cshtml.cs`, `Termination/CreateRequest.cshtml.cs`, `HRManager/InitiateTransfer.cshtml.cs`, `Payroll/` (`Index`, `Bonuses`, `EpfEtf`, `PaySlips`, `PaySlipPdf`, `AttendanceReview`), `Welfare/` (`RequestList`, `EditRequest`, `StatusTracking`, `DownloadDocument`, approvals), `LeaveService`, `MaternityLeaveService`, `OverseasLeaveService`, and `Program.cs`.
   - Replaced exact-match string comparisons (`e.NIC != "DUTY-ACC"`) with prefix-safe filters (`!e.NIC.StartsWith("DUTY") && e.NIC != "DUTY-ACC"`).
2. **Result**:
   - Duty accounts of all format variants (`DUTY-ACC`, `DUTY-BM-X`, `DUTY-AM-X`, `DUTY-DH-X-Y`, etc.) are now strictly excluded from regular employee directory lists, payroll processes, welfare requests, and transfer workflows.

---

## Change 155 — In-Modal Document Previewer for Submitted Documents

### Problem
When clicking "Open" or "View" on submitted documents (e.g. employee credentials, leave medical certificates, welfare attachments), the browser opened raw binary files in new tabs. This caused browser tabs to lose application context and display the generic browser document icon instead of the Kanrich brand favicon.

### Solution
1. **Global In-Modal Document Viewer (`Pages/Shared/_Layout.cshtml`)**:
   - Implemented a reusable `#globalDocPreviewModal` overlay featuring:
     - Header with document type icon, title, download button, open-in-tab link, and close button.
     - Responsive container displaying embedded `<iframe>` for PDFs, responsive image zoom for images (JPG, PNG, WEBP), and fallback download cards for unsupported formats.
     - Smooth animations, loading spinner, backdrop blur, and keyboard shortcut (`Escape`) support.
2. **Integrated Document Links Across Application**:
   - Updated document links in Employee Profile ([Pages/Employees/Details.cshtml](file:///c:/Users/RISINU/Documents/Uom/Sem%203/Software%20Project/Host/MM/HRMS.UI/Pages/Employees/Details.cshtml)), User Profile ([Pages/Profile.cshtml](file:///c:/Users/RISINU/Documents/Uom/Sem%203/Software%20Project/Host/MM/HRMS.UI/Pages/Profile.cshtml)), Leave Review ([Pages/Manager/Leave/Review.cshtml](file:///c:/Users/RISINU/Documents/Uom/Sem%203/Software%20Project/Host/MM/HRMS.UI/Pages/Manager/Leave/Review.cshtml)), and Welfare Status & Approvals ([Pages/Welfare/](file:///c:/Users/RISINU/Documents/Uom/Sem%203/Software%20Project/Host/MM/HRMS.UI/Pages/Welfare/)) to open documents inside the seamless in-app modal.

---

## Change 156 — Dedicated Branded Document Viewer Page for New Tab Views

### Problem
When users clicked "Open in new tab" from document previews or external links, the browser directly requested the raw binary file URL (e.g. `/uploads/documents/xxx.pdf`). Browsers do not parse `<head>` tags for raw binary files, leading to generic browser icons on the new tab.

### Solution
1. **Dedicated Standalone Viewer Page (`Pages/Documents/Viewer.cshtml` & `Viewer.cshtml.cs`)**:
   - Created a standalone HTML document viewer containing:
     - HTML `<head>` with explicit Kanrich favicon `<link rel="icon" type="image/png" href="/images/logo-title.png" />`.
     - Dynamic page title (`@Model.DocTitle – Kanrich HRMS`).
     - Branded header bar with Kanrich logo, document title, original file name, print button, download button, and close-tab action.
     - Responsive full-height viewer rendering embedded PDFs, text files, and zoomable images with a dark reader theme.
2. **Synchronized Modal "Open in New Tab" Link (`Pages/Shared/_Layout.cshtml`)**:
   - Updated `#docModalNewTabBtn` in the global preview modal to route through `/Documents/Viewer?url=...&title=...&name=...`, ensuring any document opened in a new tab carries full Kanrich branding and the official logo favicon.

---

## Change 157 — In-App Floating "Report Issue" Widget & Admin Issue Tracker

### Problem
During testing, users need an instant, non-disruptive mechanism to report bugs, glitches, data inaccuracies, or suggestions directly from the application screen, capturing full context (page URL, reporter role, branch, screen resolution, browser console errors, and optional screenshots).

### Solution
1. **Domain Entity & Database Schema (`Entities/Core/BugReport.cs`, `ApplicationDbContext.cs`, `Program.cs`)**:
   - Created `BugReport` entity capturing `Title`, `Description`, `Severity`, `Category`, `Status` (`Open`, `In Progress`, `Resolved`, `Closed`), `PageUrl`, `ReportedByUsername`, `ReportedByRole`, `ReportedByBranch`, `UserAgent`, `ScreenResolution`, `ConsoleErrors`, `ScreenshotPath`, `DeveloperNotes`, `CreatedAt`, and `ResolvedAt`.
   - Registered `DbSet<BugReport>` in `ApplicationDbContext` and added automated table creation in startup script.
2. **API Endpoint (`Pages/Api/BugReport.cshtml` & `.cs`)**:
   - Added asynchronous multipart form submission endpoint saving reports and screenshot attachments (`wwwroot/uploads/bugs/`).
3. **In-App Floating Widget & Global Error Interceptor (`Pages/Shared/_Layout.cshtml`)**:
   - Added `🐞 Report Issue` floating pill button at bottom-right of all authenticated pages.
   - Added global keyboard shortcut (`Ctrl + Shift + B`) and error buffer intercepting JS console errors.
   - Designed a quick-report modal auto-populating page URL and allowing instant submissions with loading states and success alerts.
4. **Admin Issue Tracker Dashboard (`Pages/Admin/Issues/Index.cshtml` & `.cs`)**:
   - Built a management dashboard with metric cards, search, status/severity filters, inspection modal with screenshot and console log viewer, and status update form.
   - Added "Issue Tracker" navigation item to the Admin sidebar.

---

## Change 158 — Fix Release & Publish Nullable Warnings & Namespace Imports

### Problem
During Release configuration publish, CS8618 warnings on `BiometricLogDto.cs` and missing global namespace import for `HRMS.Domain.Entities.Core` caused strict publish builds to fail.

### Solution
1. Initialized `DeviceId` and `LogType` properties in [BiometricLogDto.cs](file:///c:/Users/RISINU/Documents/Uom/Sem%203/Software%20Project/Host/MM/HRMS.Domain/DTOs/BiometricLogDto.cs) to `= string.Empty;`.
2. Added `@using HRMS.Domain.Entities.Core` to global Razor imports in [_ViewImports.cshtml](file:///c:/Users/RISINU/Documents/Uom/Sem%203/Software%20Project/Host/MM/HRMS.UI/Pages/_ViewImports.cshtml).
3. Verified complete `dotnet publish -c Release` build succeeded with 0 errors and 0 warnings.

---

## Change 159 — Cross-Database Startup & Dynamic Schema Initialization for BugReports

### Problem
On remote database instances (e.g. Azure SQL Server / MySQL), the `BugReports` table did not exist initially. When users submitted reports, database execution errors starting with `Microsoft.Data.SqlClient...` returned unhandled HTML/plain-text responses, causing `JSON.parse` syntax errors in client-side JavaScript.

### Solution
1. **Startup Schema Creation (`Program.cs`)**:
   - Added provider-aware `EnsureBugReportsTableAsync` executing DDL on startup for both SQL Server (`IF NOT EXISTS (SELECT * FROM sys.tables...)`) and MySQL (`CREATE TABLE IF NOT EXISTS BugReports...`).
2. **On-Demand Dynamic Table Fallback (`Pages/Api/BugReport.cshtml.cs` & `Pages/Admin/Issues/Index.cshtml.cs`)**:
   - Added automatic fallback schema creation on save failure or page visit, ensuring the table is automatically initialized regardless of migration states.
3. **Resilient Client Error Handling (`Pages/Shared/_Layout.cshtml`)**:
   - Updated client-side AJAX handler to safely inspect response text and display user-friendly error messages if non-JSON output is returned.

---

## Change 160 — Migration to Free TiDB Cloud Serverless MySQL Database

### Problem
Azure SQL Database free monthly vCore quota was exhausted and paused by Azure for the remainder of August. A high-performance, 100% free cloud database alternative was required to resume unlimited testing and operations.

### Solution
1. **Configured TiDB Cloud Serverless Connection**:
   - Configured `DefaultConnection` in `appsettings.Production.json` and `appsettings.json` with the cloud connection string (`Server=gateway01.ap-southeast-1.prod.aws.tidbcloud.com;Port=4000;Database=test;...;SslMode=Required;`).
2. **Refined Database Initialization & Migration Scripting (`Program.cs`)**:
   - Fixed `AspNetUsers` custom column migrations to support MySQL syntax alongside SQL Server.
   - Initialized and validated full schema creation, indexes, foreign keys, and seed accounts on TiDB Cloud.
   - Tested HTTP authentication and verified 200 OK status.

---

## Change 161 — Compact Assigned Branches Display with Expandable Count Pill in Settings / Departments

### Problem
In `Settings / Departments` (`/Settings/Departments`), departments assigned to many branches showed every single branch badge in the table cell, creating excessively tall and cluttered table rows.

### Solution
1. **Compact Branch Preview (`Pages/Settings/Departments/Index.cshtml`)**:
   - Limited the initial branch badges shown to the top 3 branches.
   - When a department has more than 3 assigned branches, rendered a compact interactive pill button `+N more` (e.g. `+12 more`).
   - Added hover tooltip displaying the full list of remaining branches.
   - Added inline click-to-expand/collapse toggle (`toggleMoreBranches`) allowing users to expand the full branch list in place and collapse back to `Show less`.

---

## Change 162 — Balance Column Widths & Prevent Name Wrapping in Settings / Departments Table

### Problem
In `Settings / Departments` (`/Settings/Departments`), the "Name" column was constrained to `150px` causing names like "Human Resources" to break across multiple lines, while the "Assigned Branches" column took up excessive empty space across the table.

### Solution
1. **Adjusted Column Widths (`Pages/Settings/Departments/Index.cshtml`)**:
   - Expanded `col.col-name` from `150px` to `280px` for optimal typography.
   - Sized `col.col-id` to `130px` and `col.col-actions` to `230px`.
   - Formatted the department name and Corporate badge in an inline flex layout so they render cleanly on a single line.

---

## Change 163 — Fix Leave Allocation Settings Update & Deduplication

### Problem
In `Settings / Leave Allocations` (`/Settings/LeaveAllocations`), updating default days appeared not to persist. Previous implementation relied on `ON DUPLICATE KEY UPDATE` without a unique key on `(EmployeeType, LeaveType)`, causing duplicate row insertions where older default rows (with lower IDs) were continuously reloaded.

### Solution
1. **Explicit UPDATE + Fallback INSERT (`Pages/Settings/LeaveAllocations/Index.cshtml.cs`)**:
   - Replaced `ON DUPLICATE KEY` with explicit parameterized `UPDATE LeaveAllocationSettings SET DefaultDays = @days WHERE EmployeeType = @empType AND LeaveType = @leaveType`, falling back to `INSERT` only if row count is 0.
2. **Deduplication & Reliable Loading (`Index.cshtml.cs`)**:
   - Added automatic deduplication query in `EnsureAllocationsSeededAsync` keeping only the latest configuration rows.
   - Restructured `LoadAllocationsAsync` using dictionary lookups to guarantee every standard leave type (`Annual`, `Casual`, `Medical`, `Maternity`, etc.) loads its exact configured days.
3. **Leave Service Integration (`Services/Impl/LeaveService.cs`)**:
   - Updated `LeaveService` query with `ORDER BY Id DESC LIMIT 1` ensuring the latest configuration values are always applied to employees during entitlement generation.

---

## Change 164 — Prevent Duplicate Desktop Notification Toasts Across Logins & Browser Restarts

### Problem
When users logged into the system later or reopened their browser, previously received unread notifications were triggering repeated Windows desktop toasts all at once. This occurred because `sessionStorage` was wiped on session end and the initial fetch did not suppress already-existing inbox items.

### Solution
1. **Persistent Notification History (`Pages/Shared/_Layout.cshtml`)**:
   - Switched tracking storage from ephemeral `sessionStorage` to persistent `localStorage` (`hrms_notified_ids`), capping at the latest 200 IDs to keep footprint small.
2. **Initial Load Suppress & Live Triggering (`_Layout.cshtml`)**:
   - On the initial fetch (`isInitialFetch === true`), marks all currently existing notifications as known without firing desktop popups.
   - Restricts desktop toasts strictly to new notifications that arrive during subsequent live polling ticks while the user is actively working.

---

## Change 165 — Desktop Notification Throttling & Batch Summary Grouping

### Problem
When several new notifications arrived simultaneously in the same polling cycle, the browser would dispatch individual desktop toasts in rapid succession, resulting in sudden popup stacking.

### Solution
1. **Intelligent Notification Throttling (`Pages/Shared/_Layout.cshtml`)**:
   - For **1–2 new notifications**: Dispatches them individually with a staggered 800ms spacing delay so they pop up smoothly without colliding.
   - For **3 or more concurrent notifications**: Groups them into a single consolidated summary toast (e.g., *"You have 4 new notifications (Leave Approval and 3 more). Click to view."*).
   - Clicking the summary toast focuses the browser and smoothly opens the notification dropdown.

---

## Change 166 — Categorized Leave Approval Inbox with Horizontal Navigation Bar & Real-Time Unreviewed Counters

### Problem
The Leave Approval Inbox at `/Manager/Leave/Approval` previously combined standard, maternity, and overseas leave requests in a single unsorted queue without categorical distinction. Reviewing managers could not quickly see how many unreviewed applications existed across different leave types or filter them efficiently.

### Solution
1. **Horizontal Navigation Bar with Kanrich Button Pills (`Pages/Manager/Leave/Approval.cshtml`)**:
   - Styled the navigation bar matching the standard Kanrich button pill row (`btn-kanrich-primary` for the active tab with solid green background and white text, and `btn-kanrich-outline` for inactive tabs with clean white background, green text, and subtle borders):
     - **Standard Leaves (N)** (Annual, Casual, Medical, Exam, Bereavement, Other)
     - **Maternity Leaves (N)**
     - **Overseas Leaves (N)**
   - Included the unreviewed request counts directly in the button labels (`StandardCount`, `MaternityCount`, `OverseasCount`).
2. **Dedicated Tab Panes & Review History (`Pages/Manager/Leave/Approval.cshtml` & `.cs`)**:
   - Structured pending requests and past review history into categorized collections (`PendingStandardLeaves`, `PendingMaternityLeaves`, `PendingOverseasLeaves` and their corresponding `ReviewedLeaves` lists).
   - Displayed category-specific metadata on request cards:
     - *Maternity*: Child number, Expected Delivery Date (EDD), and medical certificate status.
     - *Overseas*: Destination country, passport number, and travel duration.
   - Added category-specific empty states for queues with no pending requests.
3. **Instant Tab Switching & URL State Synchronization (`Approval.cshtml`)**:
   - Integrated client-side tab switching without requiring full page reload.
   - Synchronized the active tab with the URL query parameter (`?tab=standard`, `?tab=maternity`, `?tab=overseas`) and automatically restored the selected tab on page load/refresh.
4. **Preserved Category Context in Review Stepper (`Pages/Manager/Leave/Review.cshtml` & `.cs`)**:
   - Updated the "Back to Inbox" button and post-action redirects (Approve/Reject) in the Leave Review details page to preserve the active category tab when returning to the inbox.
5. **Entity Navigation Eager Loading (`Services/Impl/LeaveService.cs`)**:
   - Updated `GetPendingApprovalsAsync` to eager-load `MaternityLeave` and `OverseasLeave` navigation properties on pending leave records.

---

## Change 167 — Interactive Pagination for Pending Requests & History in Leave Approval Inbox

### Problem
In the Leave Approval Inbox (`/Manager/Leave/Approval`), when managers received large volumes of leave requests or accumulated extensive review histories across departments, all cards and history rows rendered continuously on a single scrollable page without pagination controls or page-size options.

### Solution
1. **Pending Request List Pagination (`Pages/Manager/Leave/Approval.cshtml`)**:
   - Implemented `initListPagination` providing pagination for pending request card lists across Standard, Maternity, and Overseas categories.
   - Configured with a default of 5 requests per page, complete with a page-size selector (5, 10, 25, 50 / page), "Showing X–Y of N requests" counter info, smart ellipsis pagination numbers, and responsive Prev/Next buttons.
2. **Review History Table Pagination (`Pages/Manager/Leave/Approval.cshtml`)**:
   - Integrated `initTablePagination` on historical approval/rejection tables (`#table-standard-history`, `#table-maternity-history`, `#table-overseas-history`).
   - Configured with 10 records per page by default, supporting dynamic page size selection and interactive page navigation.
3. **Seamless State Persistence & Tab Isolation**:
   - Each category tab maintains its independent pagination state so switching between Standard, Maternity, and Overseas tabs keeps accurate page indexes and record limits without interference.

---

## Change 168 — Standardized Table Layout & Separation Management Visual Alignment in Leave Approval Inbox

### Problem
The Leave Approval Inbox used standalone floating cards for pending requests which differed from the standardized `.table-card` and `.emp-table` layout used in Separation Management, creating visual inconsistency across managerial review hubs.

### Solution
1. **Separation Management Table Layout Alignment (`Pages/Manager/Leave/Approval.cshtml`)**:
   - Replaced individual floating cards with the standard `.table-card` and `.emp-table` layout across all tab panes (Standard, Maternity, Overseas).
   - Added employee avatar circles (`.avatar-circle` with two-letter initials) and structured employee info (Name, Department, EPF).
   - Formatted columns for Duration, Total Days, Category Details, Remarks, and Status Badges (`.k-badge`, `.k-badge-pending`, `.k-badge-approved`, `.k-badge-rejected`, `.k-badge-info`, `.k-badge-maternity`, `.k-badge-overseas`).
2. **Standardized Empty State Experience**:
   - Replaced plain text empty state with the canonical `.empty-caught-up-card` featuring the green circular checkmark (`.check-icon-circle`), "All Caught Up!" header, and descriptive contextual subtitle.
3. **Unified Table Pagination (`Approval.cshtml`)**:
   - Connected both Pending Approval tables (`5` per page) and Reviewed History tables (`10` per page) to the global `initTablePagination` engine, providing smooth pagination and page size selection across all tabs.

---

## Change 169 — Formatted Employee Names with Initials in Leave Approval Inbox

### Problem
In the Leave Approval Inbox (`/Manager/Leave/Approval`), employees were displayed with their full verbose names across pending approval queues and historical review tables, inconsistent with the standard corporate "Name with Initials" format used across HRMS modules (e.g., Payroll, Training, Performance).

### Solution
1. **Name with Initials Display (`Pages/Manager/Leave/Approval.cshtml`)**:
   - Updated employee name labels across all tab tables (Standard, Maternity, Overseas) to display `leave.Employee.NameWithInitials` (e.g. "A. B. Perera") while retaining the full name as a hover tooltip (`title="@leave.Employee?.FullName"`).
   - Ensured avatar circle initials are extracted consistently from `NameWithInitials`.
2. **Review History Model Projection (`Pages/Manager/Leave/Approval.cshtml.cs`)**:
   - Updated the historical review query to project `EmployeeName = la.Leave?.Employee != null ? la.Leave.Employee.NameWithInitials : "Unknown"` so past processed records also display the employee name with initials.

---

## Change 170 — Display Employee Profile Picture on Review Leave Request Page

### Problem
On the Review Leave Request page (`/Manager/Leave/Review`), the **Employee Profile** sidebar card only rendered static initials inside a colored circle rather than displaying the employee's uploaded avatar image.

### Solution
1. **Employee Profile Photo Integration (`Pages/Manager/Leave/Review.cshtml`)**:
   - Updated the `.emp-avatar` container in the Employee Profile card to load the employee's profile picture from `/uploads/avatars/emp_{id}.jpg`.
   - Added an automatic fallback to the green gradient circle (`.emp-avatar-fallback`) with the employee's initials if no photo is uploaded or if image loading fails.
   - Styled the avatar with a subtle border and shadow (`width: 58px; height: 58px; border-radius: 50%; border: 2px solid #e2e8f0;`).
2. **Name with Initials Alignment (`Review.cshtml`)**:
   - Formatted the employee's name in the header to use `NameWithInitials` (e.g. "A. B. Perera") with the full name provided as a hover tooltip.

---

## Change 171 — Exclude Duty Accounts from Employee Designation & Managerial Availability Checks

### Problem
When an HR Manager attempted to create an employee account with the "Branch Manager" (or "Department Head") designation, the system incorrectly blocked creation with an error (e.g., *"A Branch Manager profile (Branch Manager - Colombo) already exists for this branch"*), even when no real employee held that position.

### Cause
Duty accounts generate underlying placeholder `Employee` entities with NIC values formatted as `DUTY-BM-{BranchId}` (or `DUTY-DH-{BranchId}-{DeptId}`). The validation queries in `Create.cshtml.cs` only filtered by `e.NIC != "DUTY-ACC"`, failing to match dynamic prefixes like `DUTY-BM-1` and treating the duty account as an existing real employee profile.

### Solution
1. **Designation Availability & Validation Filters (`Pages/Employees/Create.cshtml.cs`)**:
   - Updated both the server-side `OnPostAsync` validation and the AJAX endpoint `OnGetCheckDesignationAvailabilityAsync` to filter out all duty accounts using `!e.NIC.StartsWith("DUTY") && e.NIC != "DUTY-ACC"`.
   - Updated the employee email uniqueness check to also filter out duty accounts correctly.
2. **Biometric Logs Filter Alignment (`Pages/BiometricLogs/Index.cshtml.cs`)**:
   - Updated the employee selection dropdown query to filter out duty accounts using `!e.NIC.StartsWith("DUTY") && e.NIC != "DUTY-ACC"`.

---

## Change 172 — Removed Legacy Verification & Approval Tiles from Attendance & Leave Management Hub

### Problem
The Attendance & Leave Management Hub (`/Attendance/Dashboard`) displayed redundant legacy tiles for **Maternity Verif.** (`/HR/Maternity/Verification`), **Overseas Verif.** (`/HR/Overseas/Verification`), and **Maternity Adm Approval** (`/Admin/Maternity/AdminApproval`). These separate workflows have now been completely consolidated into the unified **Leave Approval Inbox** (`/Manager/Leave/Approval`).

### Solution
1. **Removed Redundant Tiles (`Pages/Attendance/Dashboard.cshtml`)**:
   - Removed the `Maternity Verif.`, `Overseas Verif.`, and `Maternity Adm Approval` navigation cards from the features grid, streamlining the hub layout to relevant active services.

---

## Change 173 — Added Short Leave Configuration and Application Workflow

### Problem
Short Leaves (short duration leaves of up to 1.5–2 hours for urgent personal errands or appointments) were not configurable in the admin settings nor selectable by employees when applying for leaves.

### Solution
1. **Leave Allocation Settings (`Pages/Settings/LeaveAllocations/Index.cshtml` & `Index.cshtml.cs`)**:
   - Added `"Short Leave"` to the standard leave allocation configuration across Permanent (default `24` occurrences/year), Probationary (default `12` occurrences/year), and Intern (default `6` occurrences/year) employee categories.
   - Added policy description: *"Short duration leaves (up to 1.5 - 2 hours) granted for urgent personal errands or appointments."*
2. **Leave Balance & Entitlement Service (`Services/Impl/LeaveService.cs`)**:
   - Included `"Short Leave"` in `GetAllLeaveBalancesAsync` and `GetDefaultLeaveDaysAsync` so entitlement records and remaining balances are tracked and synchronized automatically.
3. **Standard Leave Application Form (`Pages/Employee/Leave/Apply.cshtml`)**:
   - Added `<option value="Short Leave">Short Leave</option>` under the Standard Leave type selector, complete with real-time balance indicator and validation against remaining entitlement.

---

## Change 174 — Implemented Single-Day & Time Period Selection for Short Leaves

### Problem
Previously, selecting "Short Leave" on the leave application form used the multi-day date range selector (Start Date & End Date) without allowing employees to specify the single day or time period (hours/slots) required for short leaves.

### Solution
1. **Dynamic Short Leave Form UI (`Pages/Employee/Leave/Apply.cshtml`)**:
   - Automatically swaps the multi-day date range inputs with a single **Leave Date** input and **Time Period** pickers when "Short Leave" is selected.
   - Added convenient time slot buttons:
     - **Morning**: `08:30 AM – 10:00 AM` (1.5 hrs)
     - **Evening**: `03:30 PM – 05:00 PM` (1.5 hrs)
     - **Custom Time**: Allows arbitrary start/end times up to the 2-hour maximum limit.
   - Added live duration and validation displays enforcing the 2-hour maximum limit, working days only (weekends disallowed), and balance checks.
2. **Server-Side Short Leave Validation (`Pages/Employee/Leave/Apply.cshtml.cs`)**:
   - Validates that short leave date is on a single day, not on weekends, start/end times are valid, and duration is $\le 2$ hours (120 minutes).
   - Formats the leave reason to include the time slot details (e.g., `[Short Leave: 08:30 AM – 10:00 AM (1h 30m)]`).
3. **Approver Display Alignment (`Approval.cshtml` & `Review.cshtml`)**:
   - Formatted duration columns to display single-day short leaves with clean date and slot indicators.

---

## Change 175 — Streamlined Short Leaves to Single-Level Approval

### Problem
Previously, standard leaves (including Short Leaves) followed a two-stage hierarchical approval process (e.g., Department Head $\rightarrow$ Branch Manager, or Branch Manager $\rightarrow$ Area Manager $\rightarrow$ HR). For minor short leaves ($\le 2$ hours), this multi-level routing created unnecessary administrative delay.

### Solution
1. **Single-Level Final Approval in `LeaveService.cs` (`Services/Impl/LeaveService.cs`)**:
   - When a **Department Head** approves a Short Leave request from a departmental employee (`PendingDH`), the approval is now **FINAL** (`Status = Approved`), deducting the entitlement balance immediately without escalating to the Branch Manager.
   - When a **Branch Manager** approves a Short Leave request from a Department Head (`PendingBM`), the approval is **FINAL** (`Status = Approved`) without escalating to the Area Manager.
   - When an **Area Manager** approves a Short Leave request from a Branch Manager (`PendingAM`), the approval is **FINAL** (`Status = Approved`) without escalating to HR.
   - Immediate approval notifications are sent directly to the employee upon single-level approval.

---

## Change 176 — Added Salary & Compensation Details in Employee Profile Form

### Problem
When creating or modifying an employee profile, HR officers had to register the employee first and then navigate to a separate page to define salary details. Salary and allowance fields were missing from the primary employee registration workflow.

### Solution
1. **Salary & Banking Section in Form (`Pages/Employees/Create.cshtml`)**:
   - Added a dedicated **Salary & Banking Details** form card containing:
     - **Basic Salary (LKR)**
     - **Housing Allowance (LKR)**
     - **Transport Allowance (LKR)**
     - **Medical Allowance (LKR)**
     - **Live Gross Monthly Salary Indicator** (dynamically computes `Basic + Allowances` in real-time)
     - **Bank Account Holder Name** & **Account Number**
2. **Backend Salary Record Persistence (`Pages/Employees/Create.cshtml.cs`)**:
   - Bound nullable salary properties (`decimal? BasicSalary`, `HousingAllowance`, `TransportAllowance`, `MedicalAllowance`), ensuring all fields default to empty placeholders without pre-filled zeroes.
   - Automatically inserts a new record into `PayrollSalaries` when registering a new employee if salary values are entered.
   - Automatically loads and synchronizes existing salary records when modifying an employee profile.

---

## Change 177 — Displayed Bank Account Details in Employee Details & Self-Service Profile

### Problem
Bank Account details (Account Holder Name and Account Number) entered during employee creation were stored in the database but not visible on the Employee Details page or the employee's self-service Profile page.

### Solution
1. **Employee Details View (`Pages/Employees/Details.cshtml`)**:
   - Added **Bank Account Holder** and **Bank Account Number** under **Statutory & Banking Information** in Tab 1 (*Personal & Employment Details*).
   - Added a **Disbursement Bank Account** summary card in Tab 3 (*Salary & Benefits*).
2. **Employee Self-Service Profile (`Pages/Profile.cshtml`)**:
   - Added **Bank Account Holder** and **Bank Account Number** to the **Statutory & Banking Information** card.

---

## Change 178 — Banking Details Management for Existing Employee Profiles

### Problem
For employee profiles created before the new salary & banking update, their records displayed placeholders (`—`). HR needed simple, direct ways to enter or update banking details for existing employees without manual database intervention.

### Solution
1. **Quick-Modal Banking Update (`Pages/Employees/Details.cshtml` & `Details.cshtml.cs`)**:
   - Added **Bank Account Holder Name** and **Bank Account Number** input fields directly inside the **Configure / Update Salary** modal on the Employee Details page (`/Employees/Details?id={id}#salary`).
   - Saving from the modal now updates both the `Employee` banking details and appends/updates the `PayrollSalary` history record simultaneously.
2. **Full-Form Profile Update (`Pages/Employees/Create.cshtml?id={id}`)**:
   - HR can also click **"Edit Profile"** on the details page to modify banking and salary details in the full registration form.

---

## Change 179 — Streamlined Employee Salary Profile to Basic Salary & Banking Details

### Problem
Housing Allowance, Transport Allowance, and Medical Allowance are managed dynamically as monthly allowance line-items within the Payroll module. Having static allowance inputs on the core employee profile creation and details pages caused duplication and confusion.

### Solution
1. **Simplified Employee Registration Form (`Pages/Employees/Create.cshtml` & `Create.cshtml.cs`)**:
   - Removed Housing Allowance, Transport Allowance, and Medical Allowance input fields and calculation scripts from the profile form.
   - Streamlined the **Salary & Banking Details** section to cleanly contain:
     - **Basic Salary (LKR)**
     - **Bank Account Holder Name**
     - **Bank Account Number**
2. **Simplified Employee Details & Modal (`Pages/Employees/Details.cshtml` & `Details.cshtml.cs`)**:
   - Streamlined the **Salary & Benefits** tab to display **Current Base Salary**, **Effective Date**, **Statutory EPF/ETF Contributions**, and **Disbursement Bank Account**.
   - Streamlined the **Salary Update Modal** to focus strictly on Basic Salary and Disbursement Bank Account.

---

## Change 180 — Fixed Sticky Tabs Overlap Issue in Employee Directory

### Problem
In the Employee Directory view (`/Employees`), `.tabs-container` was configured with `position: sticky; top: 0;`. When scrolling the table or on certain viewport heights, the tabs bar (`Employee Directory`, `Drafted Records`, etc.) and the `+ Create Employee` button floated over the top employee rows (`I. Dilshani`, `I. Herath`), causing text and badges to collide visually.

### Solution
1. **Removed Sticky Positioning (`Pages/Employees/Index.cshtml`)**:
   - Removed `position: sticky; top: 0; z-index: 5;` from `.tabs-container`.
   - The tabs bar now stays in standard document flow below the directory header and above the table card without overlapping any rows on scroll.

---

## Change 181 — Added Bank Name Field across Employee Profile & Banking Details

### Problem
While the employee profile captured the Bank Account Holder Name and Bank Account Number, the actual **Bank Name** was missing, requiring payroll and HR officers to maintain or inquire about bank institution names separately.

### Solution
1. **Domain & Entity Update (`Employee.cs` & `DraftEmployee.cs`)**:
   - Added `public string? BankName { get; set; }` to both `Employee` and `DraftEmployee` entities.
   - Added automated database column migration check in `Program.cs` (`AddColumnIfMissing`).
2. **Employee Registration & Edit Form (`Pages/Employees/Create.cshtml` & `Create.cshtml.cs`)**:
   - Added the **Bank Name** input with auto-completion suggestions for prominent Sri Lankan banking institutions (Bank of Ceylon, Commercial Bank, People's Bank, Sampath Bank, HNB, Seylan Bank, etc.).
3. **Employee Details View (`Pages/Employees/Details.cshtml` & `Details.cshtml.cs`)**:
   - Displayed **Bank Name** under **Statutory & Banking Information** in Tab 1 (*Personal & Employment Details*).
   - Displayed **Bank Name** under **Disbursement Bank Account** in Tab 3 (*Salary & Benefits*).
   - Added **Bank Name** input to the **Configure / Update Salary** modal for fast in-page edits.
4. **Self-Service Profile (`Pages/Profile.cshtml`)**:
   - Displayed **Bank Name** in the employee's personal profile view.

---

## Change 182 — Basic Salary Persistence for Draft Employee Profiles

### Problem
When saving an incomplete employee creation form as a Draft (`OnPostDraftAsync`), the entered `BasicSalary` was not stored on `DraftEmployee`. Consequently, when resuming the draft from the *Drafted Records* tab (`OnGetAsync?draftId={id}`), the Basic Salary input was cleared and lost.

### Solution
1. **Entity & Schema Update (`DraftEmployee.cs` & `Program.cs`)**:
   - Added `public decimal? BasicSalary { get; set; }` to `DraftEmployee`.
   - Added automatic database column migration `AddColumnIfMissing("DraftEmployees", "BasicSalary", "decimal(18,2) NULL")` in `Program.cs`.
2. **Draft Load & Save Sync (`Pages/Employees/Create.cshtml.cs`)**:
   - Updated `OnPostDraftAsync` to map `BasicSalary` into the `DraftEmployee` record.
   - Updated `OnGetAsync` to populate `BasicSalary = draft.BasicSalary` when resuming a draft.
   - Added draft record cleanup when finalizing an employee profile creation from a resumed draft.

---

## Change 183 — Completed Salary & Benefits Section in Employee Portal

### Problem
In the self-service Employee Portal (`/Profile`), the **Salary & Benefits** tab was previously a static placeholder stating *"This section is under development."* Employees could not review their active compensation, statutory contributions, or disbursement account.

### Solution
1. **Backend Compensation Data Loading (`Pages/Profile.cshtml.cs`)**:
   - Added `CurrentSalary` and `SalaryHistory` properties referencing `PayrollSalary`.
   - Populated the active salary package and revision records during profile load.
2. **Salary & Benefits UI (`Pages/Profile.cshtml`)**:
   - Replaced the placeholder with a dedicated dashboard card displaying **Current Base Salary** and **Effective Date**.
   - Added a direct action link to **View Monthly Payslips** (`/Payroll/PaySlips`).
   - Displayed **Statutory Contributions** (EPF Employee 8%, EPF Employer 12%, ETF Employer 3%).
   - Displayed **Disbursement Bank Account** (Bank Name, Account Holder Name, Account Number).
   - Displayed **Salary Revision History** if previous base salary revisions exist.

---

## Change 184 — Enhanced Attendance Summary Table with Initials and Overtime (OT) Hours Column

### Problem
1. The Attendance Summary table (`/Attendance`) displayed full legal employee names instead of standard corporate names with initials (e.g. *K.R. Perera*).
2. The table did not display daily overtime hours worked by employees exceeding the standard 8-hour workday.

### Solution
1. **Name with Initials Display (`Pages/Attendance/Index.cshtml`)**:
   - Updated the employee name cell to use `att.Employee.NameWithInitials` (falling back to `FullName`).
   - Updated the employee filter dropdown options to display `NameWithInitials` for visual consistency.
2. **Dedicated Overtime (OT) Hours Column (`Pages/Attendance/Index.cshtml`)**:
   - Added an **OT Hours** column to the attendance table (`Date | Employee | Clock In | Clock Out | Total Hours | OT Hours | Status`).
   - Computes overtime dynamically if `TotalHours > 8.0` (or `TimeOut - TimeIn > 8.0 hrs`), highlighting positive overtime in a badge (`+X.XX hrs`) and zero overtime as `0 hrs`.

---

## Change 185 — Removed Allowances Card from EPF & ETF Breakdown Modal

### Problem
In the EPF & ETF Remittance & Compliance view (`/Payroll/EpfEtf`), the individual employee statutory breakdown modal contained a legacy `Allowances (Housing/Trans/Med)` box under Earnings Structure. Since allowances were removed from employee basic profiles, this card constantly displayed `Rs 0.00` and cluttered the qualifying earnings view.

### Solution
1. **Cleaned Earnings Structure in Breakdown Modal (`Pages/Payroll/EpfEtf.cshtml`)**:
   - Removed the `Allowances (Housing/Trans/Med)` card.
   - Streamlined the Earnings Structure to clearly showcase the single **Basic Salary (Qualifying Earnings for EPF/ETF)** and updated the modal JavaScript accordingly.

---

## Change 186 — Removed Legacy Allowance Fields from Payslip Modal & Downloadable PDF

### Problem
Monthly Payslips preview modal (`/Payroll/PaySlips`) and downloadable PDF payslips (`/Payroll/PaySlipPdf`) displayed individual line items for *Housing Allowance*, *Transport Allowance*, and *Medical Allowance*, which always rendered `Rs 0.00` and occupied unnecessary vertical space.

### Solution
1. **Payslip Modal (`Pages/Payroll/PaySlips.cshtml`)**:
   - Removed the `Housing Allowance`, `Transport Allowance`, and `Medical Allowance` rows from the *Earnings* section in the Payslip inspection modal.
   - Retained clean display of **Basic Salary**, **Bonuses & Additions**, and **Gross Pay**.
2. **Downloadable PDF Payslip (`Pages/Payroll/PaySlipPdf.cshtml` & `.cshtml.cs`)**:
   - Removed the three legacy allowance rows from the PDF Earnings table.
   - Simplified `AdjustedGrossPay` computation to `BasicSalary + totalBonuses`.
3. **Payroll Run Processor (`Pages/Payroll/Index.cshtml.cs`)**:
   - Simplified `TotalPayroll` calculation and monthly gross pay determination.

---

## Change 187 — Dedicated Bonuses & Additions Section in Payslips and PDF Export

### Problem
In payslips, variable monthly incentives and overtime bonuses were lumped under the general basic earnings list or mixed with regular base pay, without distinct visual separation.

### Solution
1. **Payslip Modal (`Pages/Payroll/PaySlips.cshtml`)**:
   - Structured the payslip inspection modal into separate **Basic Earnings** and **Bonuses & Additions** sections.
   - Displayed variable additions in a distinct purple-accented panel with clear gross pay totals.
2. **Printable / Downloadable PDF Payslip (`Pages/Payroll/PaySlipPdf.cshtml`)**:
   - Added a standalone **Bonuses & Additions** table listing each incentive/bonus line item (e.g. Performance Bonus, Overtime, reason, amount) and subtotal.
   - Displayed **Total Gross Pay (Basic + Additions)** in a clear summary row above Deductions.

---

## Change 188 — Support for Multiple Itemized Bonuses & Additions on Payslips

### Problem
When an employee was awarded multiple bonuses, incentives, or overtime adjustments within the same payroll month, the payslip modal only showed an aggregated lump sum without displaying each distinct bonus type or reason description.

### Solution
1. **Multi-Item Bonus Loading (`Pages/Payroll/PaySlips.cshtml.cs`)**:
   - Loaded all `PayrollBonus` records for the retrieved payslips in `OnGetAsync`.
2. **Dynamic Itemized Rendering (`Pages/Payroll/PaySlips.cshtml`)**:
   - Serialized `bonusList` array per payslip containing `bonusType`, `amount`, and `reason`.
   - Updated `viewPayslip(id)` to dynamically construct rows for every bonus awarded (e.g. *Performance Bonus*, *Overtime — 12 hrs*, *Festival Bonus*), displaying their individual amounts alongside the total sum badge.

---

## Change 189 — Consistent Typography Color for Bonuses & Additions Heading

### Problem
The section heading for **Bonuses & Additions** in the payslip preview modal and printable PDF used a bright purple accent (`#7e22ce`), which deviated from the standard secondary text color (`var(--text-secondary)`) used across other section titles (*Basic Earnings*, *Deductions*, *Employer Contributions*).

### Solution
1. **Payslip Modal (`Pages/Payroll/PaySlips.cshtml`)**:
   - Updated the **Bonuses & Additions** heading and badge to use `var(--text-secondary)`, creating visual harmony across all section titles.
2. **Downloadable PDF Payslip (`Pages/Payroll/PaySlipPdf.cshtml`)**:
   - Removed inline color overrides from the **Bonuses & Additions** table heading to match the standard document header formatting.

---

## Change 190 — Removed Status Column & Perfected Column Alignments in Payslips Table

### Problem
1. The Payslips table (`/Payroll/PaySlips`) included a redundant **Status** column showing identical `"Generated"` badges for every completed run row.
2. Numeric financial amounts (*Basic Salary*, *Gross Pay*, *Deductions*, *Net Pay*) lacked right-aligned pairing between `<th>` headers and `<td>` data cells, leading to ragged column alignment on wider displays.

### Solution
1. **Removed Redundant Status Column (`Pages/Payroll/PaySlips.cshtml`)**:
   - Removed `<th>Status</th>` and the status badge cell from each row in the payslip table.
2. **Precision Column Header & Cell Alignments (`Pages/Payroll/PaySlips.cshtml`)**:
   - Right-aligned headers and cells for all numeric columns: `Basic Salary`, `Gross Pay`, `Deductions`, and `Net Pay`.
   - Formatted all financial amounts to standard `N2` two-decimal precision with `white-space: nowrap`.
   - Right-aligned the **Action** column buttons (`View` and `PDF`).

---

## Change 191 — Harmonized Left-Aligned Column Grid for Payslips Directory Table

### Problem
Right-aligning the monetary numbers across wide flexible grid columns caused a visual disconnect with headers and adjacent text columns, making numbers appear floating in the center-right of their cells without anchoring directly under their respective headers.

### Solution
1. **Aligned Column Headers & Data Cells (`Pages/Payroll/PaySlips.cshtml`)**:
   - Switched all information columns (**Employee**, **Month**, **Basic Salary**, **Gross Pay**, **Deductions**, **Net Pay**) to clean, consistent **left-alignment** so data values sit directly under their corresponding headers.
   - Assigned proportional column percentage widths (`24%` Employee, `14%` Month, `14%` Basic, `14%` Gross, `14%` Deductions, `14%` Net Pay, `6%` Action) to ensure balanced cell distribution.
   - Preserved right-alignment on the **Action** buttons for clean right-edge termination.

---

## Change 192 — Fixed Payslip PDF Generation Route and Download Links

### Problem
Clicking the **PDF** action button in the Payslips table was failing with a 404 error because the link requested `/Payroll/PayslipPdf?id={id}` (query string), while the PDF Razor page strictly required a route parameter template (`@page "/Payroll/PayslipPdf/{id:int}"`).

### Solution
1. **Flexible Route and Query Binding (`Pages/Payroll/PaySlipPdf.cshtml` & `.cshtml.cs`)**:
   - Updated the Razor Page route definition to `@page "{id:int?}"`.
   - Updated `OnGetAsync(int? id, [FromQuery(Name = "id")] int? queryId)` in the backend to transparently resolve both `/Payroll/PaySlipPdf/123` and `/Payroll/PaySlipPdf?id=123`.
2. **Direct Action & Modal Links (`Pages/Payroll/PaySlips.cshtml`)**:
   - Updated the table action link to `/Payroll/PaySlipPdf/@ps.Id`.
   - Added a dedicated **PDF** action button inside the payslip inspection modal header.

---

## Change 193 — Perfected 1:1 Matching Between Online Payslip and PDF Export

### Problem
1. In `PaySlipPdf.cshtml.cs`, `BonusDetails.Clear()` was being called prior to view rendering, causing the PDF to fall back to a generic single row rather than listing every specific itemized bonus.
2. In certain scenarios, `BonusTotal` was added to `Payslip.Bonuses` rather than reconciled, creating potential discrepancy with modal totals.

### Solution
1. **Accurate Bonus Breakdown Reconciliation (`Pages/Payroll/PaySlipPdf.cshtml.cs`)**:
   - Reconciled `totalBonuses` to ensure accurate non-duplicated addition totals.
   - Retained the `BonusDetails` list so the PDF renders all individual bonuses, incentives, and reasons 1:1 with the online modal.
2. **Harmonized PDF Content & Header Layout (`Pages/Payroll/PaySlipPdf.cshtml`)**:
   - Added Bank Name prefix to the Bank Account field (`{BankName} — {BankAccountNumber}`).
   - Ensured identical section hierarchies: **Basic Earnings**, **Bonuses & Additions** (with individual lines), **Total Gross Pay**, **Deductions** (EPF 8% + Tax), **NET PAY**, and **Employer Contributions** (EPF 12% + ETF 3%).

---

## Change 194 — Synchronized Dynamic Bonus Calculations & Gross Pay Reconciliation

### Problem
When inspecting payslips generated prior to the removal of legacy allowances, the payslip table and preview modal could show discrepancies between Basic Salary, Bonuses, and Gross Pay if legacy allowances were still baked into database rows or if itemized bonuses were added after the payroll run.

### Solution
1. **Automated Database Cleanup (`Program.cs`)**:
   - Added startup cleanup routine to zero out legacy allowances on historical `Payslips` records and recompute `GrossPay = BasicSalary + Bonuses` and `NetPay`.
2. **Dynamic Live Bonus Reconciliation (`Pages/Payroll/PaySlips.cshtml.cs` & `PaySlips.cshtml`)**:
   - Reconciled all loaded payslips with their corresponding itemized bonuses in `PayrollBonuses`.
   - Updated client-side serialization to dynamically calculate `bonusSum`, `actualGross = basicSalary + bonusSum`, and `actualNet = actualGross - totalDeductions`, ensuring mathematically exact totals across the table, modal, and PDF.

---

## Change 195 — Integrated Corporate Logo in Printable & Exported Payslip PDF

### Problem
The exported PDF payslip header contained plain text branding without the official Kanrich Finance company logo.

### Solution
1. **Brand Logo Header Integration (`Pages/Payroll/PaySlipPdf.cshtml`)**:
   - Embedded the official Kanrich logo (`/images/logo.png` with fallback to `/images/logo.webp`) in the top-left header of the payslip next to the company title and confidential disclaimer.
   - Positioned with crisp alignment and responsive height constraint (`48px`) suited for A4 printing and PDF generation.

---

## Change 196 — Excluded Deceased and Inactive Employees from Payroll Dashboard & Processing

### Problem
The Payroll Dashboard (`/Payroll/Index`) metrics (Total Employees, Estimated Payroll Total, Allowances & Additions, Total Deductions, and Anomaly Count) and monthly payroll processing (`OnPostRunPayrollAsync`) were including past salary records for deceased and inactive employees who had not yet been purged from salary history.

### Solution
1. **Dashboard Active Employee Filtering (`Pages/Payroll/Index.cshtml.cs`)**:
   - Added strict `s.Employee.Status == "Active"` constraint across all queries (`TotalEmployees`, `salaries`, `TotalPayroll`, `TotalBonuses`, `TotalDeductions`, `HasBonusRecords`, `HasPayslips`, `AnomalyCount`).
2. **Monthly Payroll Run Execution Protection (`Pages/Payroll/Index.cshtml.cs`)**:
   - Filtered `OnPostRunPayrollAsync` to only process active employees (`s.Employee.Status == "Active"`), preventing erroneous payslip generation for deceased or separated staff.
3. **Allowances Page Alignment (`Pages/Payroll/Bonuses.cshtml.cs`)**:
   - Added `b.Employee.Status == "Active"` filter to the active bonuses list to ensure consistency across the entire Payroll module.

---

## Change 197 — Refined Dashboard Deductions & Statutory Contribution Calculations

### Problem
The Payroll Dashboard previously calculated deductions using an ad-hoc 11% multiplier that conflated the employer's 3% ETF contribution with employee deductions, causing a discrepancy with the payslips where only the 8% EPF is deducted from employee basic pay.

### Solution
1. **Accurate Employee Deductions Calculation (`Pages/Payroll/Index.cshtml.cs`)**:
   - Updated `TotalDeductions` to calculate exact 8% EPF deductions from active employee salaries (`Math.Round(s.BasicSalary * 0.08m, 2)`), perfectly matching payslip totals.
2. **Employer Statutory Contributions Metric (`Pages/Payroll/Index.cshtml.cs` & `Index.cshtml`)**:
   - Added `TotalEmployerContributions` calculation representing the 15% employer obligation (12% EPF + 3% ETF).
   - Updated the stat card to display **Employee Deductions (EPF 8%)** with a clear secondary indicator displaying the total employer 15% statutory contribution.

---

## Change 198 — Configurable Overtime (OT) Policy & Automated Payslip Calculation

### Problem
Overtime payments were not automatically calculated from attendance records. HR staff had to manually create "Overtime Pay" bonus entries for each employee every month, leading to errors and omissions.

### Solution

#### 1. Domain & Persistence Layer
- **New Entity `PayrollPolicySetting.cs`**: Stores configurable OT policy parameters per branch (Standard Monthly Working Days, Daily Working Hours, Regular OT Multiplier 1.5×, Weekend Multiplier 2.0×, Auto-Calculate toggle).
- **`ApplicationDbContext.cs`**: Registered `DbSet<PayrollPolicySetting>`.
- **`Program.cs`**: Added startup table creation (`PayrollPolicySettings`) with default global seed (21 working days, 8 hrs/day, 1.5× multiplier).

#### 2. Payroll Dashboard (`Pages/Payroll/Index.cshtml` & `Index.cshtml.cs`)
- **OT Policy Loading**: Loads branch-specific OT policy (with global fallback) on page load.
- **OT Policy Settings Modal**: Added "OT Policy" button in dashboard header opening a configuration modal (Working Days, Daily Hours, Regular Multiplier, Weekend Multiplier, Auto-Calculate toggle, Formula preview).
- **Save Policy Handler (`OnPostSaveOtPolicyAsync`)**: Persists branch-specific OT policy configuration.
- **Automated OT in Payroll Run (`OnPostRunPayrollAsync`)**: When auto-calculate is enabled, automatically:
  1. Fetches monthly attendance logs for all active employees.
  2. Calculates excess hours beyond daily limit (default 8 hrs).
  3. Computes OT pay using formula: `(Basic ÷ (21 × 8)) × 1.5 × OT Hours`.
  4. Creates itemized `PayrollBonus` with `BonusType = "Overtime"` and descriptive reason (e.g. "Overtime — 12 hrs @ Rs 669.64/hr").
  5. Reloads bonuses before generating payslips to include OT in Gross Pay.
- **Start Over Cleanup**: `OnPostStartOverPayrollAsync` now also removes auto-calculated OT bonuses when resetting a payroll cycle.

#### 3. Attendance Review (`Pages/Payroll/AttendanceReview.cshtml` & `AttendanceReview.cshtml.cs`)
- **Estimated OT Pay Column**: Added purple "Est. OT Pay" column showing calculated OT payment per employee based on current salary and policy.
- **OT Hours Uses Policy**: OT hours calculation now uses policy's configurable daily working hours limit instead of hardcoded 8.0.
- **Sync OT to Allowances Button**: Added "Sync OT to Allowances" action button that:
  1. Calculates OT for all active employees from attendance data.
  2. Removes any previously synced OT bonuses for the month.
  3. Creates fresh `PayrollBonus` entries with itemized descriptions.
  4. Displays success toast with count of employees affected.

---

## Change 199 — Relocated Overtime Policy Settings Exclusively to Admin Portal Settings

### Problem
The Overtime Policy configuration should only be accessible and editable by System Administrators within the Admin Portal Settings (`/Settings/Index`), rather than exposed to branch-level HR Officers or HR Managers on standard payroll operational pages.

### Solution

#### 1. Admin Settings Overtime Policy Page (`Pages/Settings/OvertimePolicy/Index.cshtml` & `Index.cshtml.cs`)
- **New Admin Route**: Created dedicated `[Authorize(Roles = "Admin")]` page at `/Settings/OvertimePolicy`.
- **Corporate Global Default Policy**:
  - Configurable Standard Monthly Working Days (default `21 days`).
  - Configurable Daily Working Hours (default `8.0 hrs/day`).
  - Computed monthly base hours indicator (`168.0 hrs`).
  - Configurable Regular Day OT Multiplier (`1.5×`) and Weekend/Holiday Multiplier (`2.0×`).
  - Auto-calculate OT toggle for monthly payroll runs.
  - Interactive Live Formula Demonstration box with real-time dynamic salary calculation.
- **Branch-Specific Overrides Table & Modal**:
  - Lists all company branches with status indicators (`Corporate Default` vs `Custom Override`).
  - Modal allowing Admins to enable/disable custom working days and multipliers per branch.
- **Settings Navigation Link**: Added an "Overtime & Working Hours Policy" setting card in [`Pages/Settings/Index.cshtml`](file:///c:/Users/RISINU/Documents/Uom/Sem%203/Software%20Project/Host/MM/HRMS.UI/Pages/Settings/Index.cshtml) under the Admin section.

#### 2. Cleaned Operational Payroll Pages
- **Removed from Payroll Dashboard**: Removed the OT Policy modal and action button from [`Pages/Payroll/Index.cshtml`](file:///c:/Users/RISINU/Documents/Uom/Sem%203/Software%20Project/Host/MM/HRMS.UI/Pages/Payroll/Index.cshtml).
- **Preserved Automated Execution**: Payroll processing (`OnPostRunPayrollAsync`) and Attendance Review (`OnPostSyncOtToPayrollAsync`) continue to seamlessly read and apply the Admin-configured policy parameters to compute overtime accurately.

---

## Change 200 — Dual Weekday & Weekend Overtime Rate Splitting & Automatic Tallying

### Problem
Overtime calculations previously grouped all attendance excess hours under a single multiplier rate without separating weekday hours (Monday–Friday at standard `1.5×`) from weekend hours (Saturday & Sunday at double `2.0×`).

### Solution
1. **Intelligent Attendance Log Day Classification (`Pages/Payroll/Index.cshtml.cs` & `Pages/Payroll/AttendanceReview.cshtml.cs`)**:
   - **Weekdays (Mon–Fri)**: Excess hours beyond standard daily working hours (default `8.0 hrs`) are tallied as **Weekday OT** and multiplied by the `StandardOtMultiplier` (default `1.5×`).
   - **Weekends (Sat–Sun)**: Hours worked on Saturday or Sunday are tallied as **Weekend OT** and multiplied by the `WeekendOtMultiplier` (default `2.0×`).
2. **Dual-Rate Payment Aggregation**:
   - Computes $\text{Total OT Pay} = (\text{Weekday OT} \times \text{Hourly Rate} \times 1.5) + (\text{Weekend OT} \times \text{Hourly Rate} \times 2.0)$.
3. **Itemized Transparent Payslip Description**:
   - Formats a clear combined addition reason for the employee:
     `Overtime — Weekday: 10 hrs (Rs 892.86) + Weekend: 8 hrs (Rs 1,190.48)`.

---

## Change 201 — Replaced Status Column with Action Column & Comprehensive Monthly Attendance Review Modal

### Problem
In the **Payroll / Attendance Review** table, the `Status` column only showed static status badges without allowing HR to inspect the underlying daily clock-in/out records, working hours, anomalies, and overtime contributions that constitute the payroll cycle summary.

### Solution

#### 1. Action Column & Review Trigger (`Pages/Payroll/AttendanceReview.cshtml`)
- **Replaced `Status` Column**: Substituted the previous status column with an **`Action`** column containing a dedicated **`Review`** button (`<button onclick="openReviewModal(index)"><i class="bi bi-calendar2-check"></i> Review</button>`).
- **Responsive Layout**: Maintained clear widths for Employee, Working Days, Paid Leaves, No-Pay Leaves, OT Hours, and Estimated OT Pay.

#### 2. Comprehensive Employee Monthly Attendance Review Modal (`AttendanceReview.cshtml` & `AttendanceReview.cshtml.cs`)
- **Daily Attendance Breakdown Generation**:
  - Automatically compiles a full calendar day-by-day record for the entire payroll duration (e.g. Day 1 to Day 31).
  - Categorizes each day as `Present`, `Late`, `Approved Leave (Annual, Casual, Medical)`, `No-Pay Leave`, `Weekend / Weekend Duty`, `Anomaly (Missing Clock-Out)`, or `Absent/Off`.
  - Calculates daily worked duration, regular overtime ($1.5\times$), and weekend overtime ($2.0\times$) with monetary pay values.
- **Interactive Review Modal UI**:
  - **Employee Profile Header**: Displays employee avatar, name, EPF number, designation, department, branch, and payroll cycle.
  - **KPI Summary Strip**: Shows 6 quick metrics: Working Days, Paid Leaves, No-Pay Leaves, Weekday OT (1.5×), Weekend OT (2.0×), and Estimated OT Pay.
  - **Interactive Day Filter Pills**: Quickly filters daily logs by `All Days`, `Working Days`, `Overtime Days`, `Leaves`, or `Anomalies / Missing Out`.
  - **Detailed Daily Table**: Shows Date & Day, Check-In time, Check-Out time, Total duration, status badges, and daily OT pay calculation.

---

## Change 202 — Fixed Vertical Scrollability & Added Sticky Headers in Attendance Review Modal

### Problem
The Monthly Attendance Breakdown table inside the Attendance Review modal had `overflow: hidden` on its outer wrapper without a dedicated scroll container, causing attendance rows beyond day 9 to be cut off without a scrollbar.

### Solution
1. **Dedicated Table Scroll Container (`Pages/Payroll/AttendanceReview.cshtml`)**:
   - Added `.daily-table-scroll` with `overflow-y: auto` and a maximum height constraint (`max-height: 380px`).
   - Added `.modal-body-scroll` with `overflow-y: auto` and `min-height: 0` for smooth flex-based parent scrolling.
2. **Sticky Table Headers**:
   - Applied `position: sticky; top: 0; z-index: 4;` to `thead th` with a subtle elevation shadow so table headers remain visible when scrolling through all 31 days.
3. **Custom Polished Scrollbars**:
   - Added sleek WebKit scrollbar styling matching Kanrich corporate design tokens.

---

## Change 203 — Prevented Accidental Modal Dismissal on Filter Button Clicks

### Problem
Clicking any of the day filter buttons (`All Days`, `Working Days`, `Overtime`, `Leaves`, `Anomalies`) inside the Attendance Review modal triggered event bubbling up to the modal overlay's `onclick="closeReviewModal(event)"` handler which mistakenly checked `e.target.closest('button')`, causing the popup window to close immediately.

### Solution
1. **Event Propagation Containment (`Pages/Payroll/AttendanceReview.cshtml`)**:
   - Added `onclick="event.stopPropagation()"` to `.modal-dialog-large`.
   - Updated `filterDailyLogs(type, event)` to explicitly call `event.stopPropagation()`.
2. **Corrected Overlay Dismissal Logic**:
   - Updated `closeReviewModal(e)` to only dismiss the modal when the user explicitly clicks the dark background overlay (`e.target === document.getElementById('attendanceDetailModal')`) or presses the explicit **Close** button / `Escape` key.

---

## Change 204 — Synchronized Approved Leave Day Mapping with Daily Attendance Logs & Filter Counts

### Problem
In the **Attendance Review** modal, the top KPI card showed `PAID LEAVES: 2`, but the filter pill showed `Leaves (1)` and only 1 leave row was displayed when clicked. This occurred because:
1. Multi-day leaves with equal start/end dates or leaves spanning over non-working days were not expanded to all their allocated working days across the calendar.
2. If an attendance record existed on one of the leave days (e.g. with 0 hours, late, or unverified status), the attendance condition took precedence over the leave, masking the second leave day as `Present` or `Absent`.

### Solution
1. **Accurate Working-Day Leave Expansion (`Pages/Payroll/AttendanceReview.cshtml.cs`)**:
   - Implemented `empLeaveDays` dictionary that maps each approved leave request across all its working days in the month duration (skipping weekends).
   - If an employee has both work and leave on the same day, it tags the day as `Present + Leave ({Type})` so neither the work hours nor the leave record is lost.
2. **Synchronized Leave Metric Tally**:
   - `paidLeaves` and `noPayLeaves` metrics are now directly calculated from `dailyLogs.Count(...)` ensuring 100% mathematical consistency between the KPI header card, filter count pill, and table rows.
3. **Enhanced JavaScript Filter (`Pages/Payroll/AttendanceReview.cshtml`)**:
   - Updated `filterDailyLogs` to check `log.hasLeave || log.status.toLowerCase().includes('leave')`.

---

## Change 205 — Redesigned Employee Portal Dashboard Overview

### Problem
Previously, when a regular employee logged into the portal, the main dashboard (`Pages/Index.cshtml`) displayed company-wide manager statistics such as "Total Employees (52)", hardcoded "On Leave Today (8)", hardcoded "Open Positions (4)", and the manager's pending transfer review queue. It did not reflect an employee self-service experience.

### Solution
1. **Role-Based Segmentation (`Pages/Index.cshtml.cs`)**:
   - Added automatic detection for regular employees (`IsEmployeeView = User.IsInRole("Employee") && !isManagerOrAdmin`).
   - Management roles (HR Manager, HR Officer, Area Manager, Branch Manager, Dept Head) continue to see their manager overview with real-time employee and leave counts.
2. **Dedicated Employee Dashboard Features (`Pages/Index.cshtml`)**:
   - **Personalized Header & Live Punch Pill**: Shows employee designation, department, branch, EPF code, and real-time punch status (`Clocked In (Active)`, `Clocked Out`, `On Leave`, `Not Clocked In`).
   - **4 Tailored Self-Service KPI Cards**:
     - *Working Days (This Month)*: Shows days worked vs 21 days with attendance percentage badge and total hours.
     - *Available Leave Balance*: Real-time remaining paid days with itemized breakdown (Annual, Casual, Medical) and pending requests count.
     - *Overtime & Extra Pay*: Monthly overtime hours earned and estimated OT pay based on company policy.
     - *Latest Payslip Snapshot*: Latest salary month, net pay, status badge, and 1-click payslip link.
   - **Self-Service Quick Actions Bar**: 6 direct actions: *Apply for Leave*, *My Attendance*, *My Payslips*, *My Profile*, *Apply Transfer*, and *Training Hub*.
   - **My Recent Applications & Requests**: Unified chronological feed of recent Leave, Transfer, and Welfare applications with live status badges (`Approved`, `Pending`, `Rejected`).
   - **Recent Attendance History (Last 5 Days)**: Quick overview of clock-in/out times, duration, and status.
   - **Today's Shift & Clock Widget**: Shows shift times (`08:30 AM – 05:00 PM`), first in, last out.
   - **Upcoming Events & Calendar**: Personal company events and training workshops from database.

---

## Change 206 — Removed Clock In/Out Live Status Badges & Hardcoded Events

### Changes
1. **Removed Live Status Badges (`Pages/Index.cshtml`)**:
   - Removed the `● Clocked In (Active)` / `● Clocked Out` status pills from the top header greeting strip.
   - Removed the `Current Status` row from the "Today's Shift & Clock" card.
2. **Removed Hardcoded Fallback Events (`Pages/Index.cshtml.cs` & `Pages/Index.cshtml`)**:
   - Removed the mock events ("Monthly Review Session", "Company Town Hall") that were automatically populated when no calendar events existed.
   - Upcoming Events & Calendar widget now exclusively displays genuine database events (from `CalendarEvents` and scheduled `Trainings`).
3. **Removed Apply Transfer Quick Action Tile (`Pages/Index.cshtml`)**:
   - Removed the `Apply Transfer (Branch relocation)` card from the Employee Quick Actions grid on the dashboard.
4. **Removed Today's Shift & Clock Widget (`Pages/Index.cshtml`)**:
   - Removed the *Today's Shift & Clock* card from the right sidebar of the Employee Dashboard.

---

## Change 209 — Fixed Leave Balance Calculation & Employee Resolution on Dashboard

### Problem
The "Available Leave Balance" card on the Employee Dashboard showed `0 Days (Annual: 0 • Casual: 0 • Med: 0)` because:
1. The logged-in Identity user resolution looked up by `Email == User.Identity.Name`, which failed when `User.Identity.Name` stored an EPF number or Username rather than an email address, causing the employee record to evaluate to `null`.
2. Leave balance was querying raw `LeaveEntitlements` before initialization rather than utilizing the centralized `ILeaveService.GetAllLeaveBalancesAsync` service.

### Solution
1. **Multi-Fallback Employee Resolution (`Pages/Index.cshtml.cs`)**:
   - Added chained resolution matching by `currentUser.EmployeeId`, `currentUser.Email`, `currentUser.EpfNumber`, `currentUser.UserName`, and active employee demo fallback.
2. **LeaveService Integration (`Pages/Index.cshtml.cs`)**:
   - Injected `ILeaveService` into `IndexModel` and used `_leaveService.GetAllLeaveBalancesAsync(employee.Id, now.Year)` to calculate remaining Annual, Casual, and Medical days directly with database-driven defaults.
3. **Removed My Profile Quick Action Tile (`Pages/Index.cshtml`)**:
---

## Change 211 — Standardized Notification System to Sri Lanka Standard Time (SLST / UTC+05:30)

### Problem
Notifications displayed inconsistent or UTC timestamps (e.g. 5:30 hours behind) depending on server environment, database storage defaults, and client time resolution.

### Solution
1. **Centralized Sri Lanka Time Provider (`HRMS.Domain/Common/SriLankaTime.cs`)**:
   - Created cross-platform `SriLankaTime` supporting Windows Timezone (`Sri Lanka Standard Time`), IANA (`Asia/Colombo`), and fixed UTC+05:30 fallback.
   - Provides `SriLankaTime.Now`, `SriLankaTime.Today`, `SriLankaTime.ToSriLankaTime(dt)`, and `SriLankaTime.Format(dt, format)`.
2. **Standardized Notification Creation & Defaults**:
   - Set `Notification.CreatedAt` default to `SriLankaTime.Now` in `HRMS.Domain/Entities/Core/Notification.cs`.
   - Updated `NotificationService.cs`, `CalendarReminderBackgroundService.cs`, `Employees/Create.cshtml.cs`, `Employees/Index.cshtml.cs`, `Employees/ReviewDocument.cshtml.cs`, `Profile.cshtml.cs`, `HRManager/AssignBranches.cshtml.cs`, and `Admin/DutyAccounts/Create.cshtml.cs`.
3. **API & View Formatting (`Pages/Api/Notifications.cshtml.cs` & `Pages/Transfer/Notifications.cshtml`)**:
---

## Change 213 — Half-Day Leaves for Casual and Annual Leave Categories

### Problem
Employees needed the ability to apply for half-day leaves under **Casual Leave** and **Annual Leave** categories. A half-day leave should allow selecting only a single calendar date, choosing between Morning (First Half) and Afternoon (Second Half) sessions, computing the duration as `0.5 days`, and deducting exactly `0.5 days` from their leave entitlement balance upon approval.

### Solution
1. **Domain Entities & Database Schema (`Leave.cs`, `LeaveEntitlement.cs`, `Program.cs`)**:
   - Changed `Leave.TotalDays`, `LeaveEntitlement.TotalDays`, `LeaveEntitlement.UsedDays`, and `LeaveEntitlement.RemainingDays` from `int` to `double` to allow precise tracking of fractional leave days (e.g. `0.5`, `6.5`).
   - Added `IsHalfDay` (`bool`) and `HalfDaySession` (`string?`) properties to `Leave.cs`.
   - Added database column migrations for `Leaves.IsHalfDay` (`tinyint(1) NOT NULL DEFAULT 0`) and `Leaves.HalfDaySession` (`varchar(50) NULL`) in `Program.cs`.
2. **Leave Service Business Logic (`ILeaveService.cs`, `LeaveService.cs`)**:
   - In `ApplyLeaveAsync`:
     - If `leave.IsHalfDay` is true: validates category is strictly `"Casual"` or `"Annual"`, enforces `EndDate = StartDate`, validates weekend exclusion, sets `TotalDays = 0.5`, and defaults session to `"First Half (Morning)"`.
     - Overlap validation checks whether the employee already has a full-day leave or a half-day leave on the same date for the same session.
     - Enforces balance check (`entitlement.RemainingDays >= 0.5`).
     - Standardized approval/rejection and notification formatting to include session description (`0.5 days - First Half (Morning)`).
3. **Leave Application UI & Dynamic Switching (`Apply.cshtml`, `Apply.cshtml.cs`)**:
   - Added a **Full Day / Half Day** toggle button group beneath the Leave Type dropdown.
   - The toggle is dynamically displayed when **Casual Leave** or **Annual Leave** is selected, and automatically hidden for all other leave types.
   - When **Half Day** is toggled:
     - The date range is condensed to a single **"Leave Date"** picker.
     - Session selector buttons appear: **First Half (Morning: 08:30 AM – 12:45 PM)** and **Second Half (Afternoon: 12:45 PM – 05:00 PM)**.
     - The calculation summary card dynamically calculates and displays **`0.5 Day`**.
4. **Dashboard, Review & Approval Displays (`Dashboard.cshtml`, `Status.cshtml`, `Approval.cshtml`, `Review.cshtml`, `Index.cshtml`)**:
   - Formatted all leave balance summaries, history records, and review cards to display fractional days cleanly (e.g. `0.5 Day`, `6.5 Days`) without unnecessary trailing decimal zeros.
   - Added half-day session tags (`First Half (Morning)` / `Second Half (Afternoon)`) to status and review tables.

---

## Change 214 — Fixed MySQL InvalidCastException for Fractional Leave Entitlements

### Problem
When opening the Employee Leave Dashboard or querying leave balances, a `MySqlConnector.Core.Row.GetDouble` error occurred: `InvalidCastException: Unable to cast object of type 'System.Int32' to type 'System.Double'`.

### Root Cause
1. In MySQL, the existing tables `LeaveEntitlements` and `Leaves` were created with `INT` column types for `TotalDays`, `UsedDays`, and `RemainingDays`.
2. When the C# models were changed to `double` to support half-day leaves, Entity Framework Core / Pomelo MySQL connector attempted to read the columns using `GetDouble()`, throwing an `InvalidCastException` because MySQL protocol still returned them as 32-bit integers.
3. The previous startup migration only used `AddColumnIfMissing`, which skipped altering columns that already existed.

### Solution
1. **Automated Column Type Migration (`Program.cs`)**:
   - Added `ModifyColumnTypeIfExists(table, column, definition)` to check `information_schema.COLUMNS` and execute `ALTER TABLE ... MODIFY COLUMN ...` on existing tables.
   - Migrated `LeaveEntitlements.TotalDays`, `LeaveEntitlements.UsedDays`, `LeaveEntitlements.RemainingDays`, and `Leaves.TotalDays` to `double NOT NULL DEFAULT 0`.
2. **Result**:
   - The MySQL database schema is seamlessly aligned with the C# `double` properties on startup, eliminating the `InvalidCastException`.

---

## Change 215 — Standardized Working Hours (08:00 AM – 04:00 PM)

### Problem
Working hours across the dashboard, half-day sessions, short leave slots, and performance punctuality thresholds previously referenced `08:30 AM – 05:00 PM`. The organization's official working hours are **08:00 AM to 04:00 PM** (8 hours/day).

### Solution
1. **Employee Dashboard Shift Timing (`Pages/Index.cshtml.cs`)**:
   - Updated default `ShiftTiming` to `08:00 AM – 04:00 PM`.
2. **Half-Day Sessions (`Pages/Employee/Leave/Apply.cshtml`)**:
   - Updated session labels:
     - **First Half (Morning)**: `08:00 AM – 12:00 PM`
     - **Second Half (Afternoon)**: `12:00 PM – 04:00 PM`
3. **Short Leave Slots (`Pages/Employee/Leave/Apply.cshtml` & `Apply.cshtml.cs`)**:
   - Updated preset time slot buttons and default input values:
     - **Morning**: `08:00 AM – 09:30 AM` (`08:00` to `09:30`)
     - **Evening**: `02:30 PM – 04:00 PM` (`14:30` to `16:00`)
4. **Performance Punctuality Threshold (`Pages/Performance/Index.cshtml.cs` & `Pages/Performance/Index.cshtml`)**:
   - Updated on-time arrival threshold to on-time before `08:00 AM` (`new TimeSpan(8, 0, 0)`).
   - Updated performance subtitle baseline text to `Punctuality threshold: on-time before 08:00 AM`.

---

## Change 216 — Completely Removed Short Leave from the Application

### Problem
Short Leave is no longer part of company leave policy and needed to be completely removed from the entire application, UI selectors, backend validation, entitlements, and settings.

### Solution
1. **Leave Service (`Services/Impl/LeaveService.cs`)**:
   - Removed `"Short Leave"` from `GetAllLeaveBalancesAsync` standard leave types array.
   - Removed single-level approval routing shortcuts for Short Leave in Department Head, Branch Manager, and Area Manager approval handlers.
   - Removed `"Short Leave"` default allocation seed entries (`GetDefaultLeaveDaysAsync`).
2. **Leave Application Form (`Pages/Employee/Leave/Apply.cshtml` & `Apply.cshtml.cs`)**:
   - Removed `<option value="Short Leave">Short Leave</option>` from the leave type dropdown.
   - Removed the dedicated Short Leave input section (`#shortLeaveGroup`), time pickers, slot buttons, and JavaScript helper functions (`selectShortLeaveSlot`, `updateShortLeaveDuration`).
   - Removed `ShortLeaveDate`, `ShortLeaveStartTime`, `ShortLeaveEndTime`, and `ShortLeaveSlot` properties and validation logic from `Apply.cshtml.cs`.
3. **Admin Leave Allocations Settings (`Pages/Settings/LeaveAllocations/Index.cshtml` & `Index.cshtml.cs`)**:
   - Removed `"Short Leave"` from `standardLeaveTypes` list and seed defaults.
   - Removed `"Short Leave"` description mapping from settings view.
4. **Approval & Review Views (`Pages/Manager/Leave/Approval.cshtml` & `Review.cshtml`)**:
   - Removed `"Short Leave"` specific conditional rendering tags.

---

## Change 217 — Made Welfare Visible for Non-Admin Roles & Integrated Request Training in Sessions Page

### Problem
1. The **Welfare** module was hidden from the main sidebar navigation and dashboard quick actions.
2. In the **Training & Development Hub**, "Request Training" was a separate standalone tile on the hub dashboard instead of a clean action button directly on the Training Sessions page.

### Solution
1. **Sidebar Navigation Visibility (`Pages/Shared/_Layout.cshtml`)**:
   - Added the **Welfare** menu item (`/Welfare/RequestList`) to the sidebar navigation for all non-admin roles (Employees, Department Heads, Branch Managers, Area Managers, HR Officers, HR Managers).
2. **Dashboard Quick Action (`Pages/Index.cshtml`)**:
   - Added a **Welfare & Benefits** quick action card (`/Welfare/RequestList`) under Employee Self-Service Actions on the main dashboard.
3. **Training Hub Tile & Button Consolidation (`Pages/Training/Dashboard.cshtml` & `Sessions.cshtml`)**:
   - Removed the separate **Request Training** tile from the Training & Development Hub dashboard.
   - Added a dedicated **"Request Training Session"** button on the header of the **Training Sessions** page (`/Training/Sessions`), neatly positioned alongside the HR scheduling actions.

---

## Change 218 — Direct Training Sessions Navigation for Employees

### Problem
For regular employees, Training Sessions was the only remaining tile in the Training Hub dashboard. Requiring employees to open a single-tile intermediate dashboard before reaching the Training Sessions page was redundant.

### Solution
1. **Sidebar Navigation (`Pages/Shared/_Layout.cshtml`)**:
   - Updated the sidebar **Training** link to point directly to `/Training/Sessions` for regular employees, while continuing to open `/Training/Dashboard` for management and HR roles who have multi-tile tracking hubs (Manage Requests, Probation Tracking, Intern Tracking).
2. **Dashboard Quick Action (`Pages/Index.cshtml`)**:
   - Updated the **Training Sessions** quick action card on the home dashboard to navigate straight to `/Training/Sessions`.
3. **Training Dashboard Auto-Redirect (`Pages/Training/Dashboard.cshtml.cs`)**:
   - Added an automatic redirect in `OnGet()` so that if an employee visits `/Training/Dashboard` directly, they are seamlessly forwarded to `/Training/Sessions`.
4. **Back Link (`Pages/Training/Sessions.cshtml`)**:
   - Resticted the "Back to Training Hub" header link to managerial and HR roles.

---

## Change 219 — Implemented Interactive Popup Modal for Request Training Session

### Problem
Navigating away to a separate page to submit a training request caused friction and fragmented the user experience on the Training Sessions page.

### Solution
1. **Interactive Modal Form (`Pages/Training/Sessions.cshtml`)**:
   - Replaced the navigation button with a popup trigger opening a modern, glassmorphic modal window (`#requestTrainingModal`).
   - Integrated employee eligibility indicator pill (permanent staff check vs. restricted contract/intern indicator).
   - Added dropdown for approved corporate training programs with dynamic custom program input reveal on selecting "Other".
   - Integrated justification textarea, cancel action, and asynchronous submission button with loading spinner state.
   - Added ESC key and backdrop click handlers with background body scroll lock.
2. **Backend Submission & Notification Handler (`Pages/Training/Sessions.cshtml.cs`)**:
   - Added `ITrainingNotificationService` injection and `OnPostRequestTrainingAsync()` handler.
   - Handled validation, database insertion of `TrainingProgramRequest`, and automated notification alerts to HR.

---

## Change 220 — Removed Eligibility Status Banner from Request Training Modal

### Problem
The green status message banner ("Status: Permanent - You are eligible to apply for training programs") inside the Request Training Session modal was redundant and created unnecessary visual clutter.

### Solution
1. **Modal Body Cleanup (`Pages/Training/Sessions.cshtml`)**:
   - Removed the `.rt-status-badge` banner element and its accompanying CSS styles from the modal popup window.
   - The modal now displays directly and cleanly with the program selector, custom input, and justification fields.

---

## Change 221 — Added Standardized Client-Side Pagination to Training Module Tables

### Problem
Tables across the Training module (Sessions, Manage Requests, Probation Tracking, Intern Tracking, View Profile, and Session Details) lacked pagination, leading to long scrolling lists when many records were present.

### Solution
1. **Core Layout Pagination Engine (`Pages/Shared/_Layout.cshtml`)**:
   - Enhanced `initTablePagination()` to ignore empty-state rows (`.empty-row` and `td[colspan]`) and hide pagination bars for empty lists.
2. **Training Sessions (`Pages/Training/Sessions.cshtml`)**:
   - Added table IDs `tblUpcomingSessions` and `tblPastSessions`.
   - Initialized pagination with 8 items per page and page size switcher.
3. **Training Requests Management (`Pages/Training/Manage.cshtml`)**:
   - Added table IDs `tblPendingRequests` and `tblReviewedRequests`.
   - Initialized pagination with 8 items per page and page size switcher.
4. **Probation Tracking (`Pages/Training/ProbationTracking.cshtml`)**:
   - Added table ID `tblProbationStaff`.
   - Initialized pagination with 8 items per page.
5. **Intern Tracking (`Pages/Training/InternTracking.cshtml`)**:
   - Added table ID `tblInternStaff`.
   - Initialized pagination with 8 items per page.
6. **Milestone Evaluations (`Pages/Training/ViewProfile.cshtml`)**:
   - Added table ID `tblMilestoneEvaluations`.
   - Initialized pagination with 8 items per page.
7. **Session Details Attendee Roster (`Pages/Training/SessionDetails.cshtml`)**:
   - Added table ID `tblSessionAttendees`.
   - Initialized pagination with 10 attendees per page.

---

## Change 222 — Restricted Duty Accounts from Submitting Training Session Requests

### Problem
Duty accounts (Admin, HR Manager, HR Officer, Branch Manager, Area Manager, Department Head, and duty dummy employee accounts) were able to see and trigger the "Request Training Session" action.

### Solution
1. **Training Sessions UI & Backend Scoping (`Pages/Training/Sessions.cshtml` & `Sessions.cshtml.cs`)**:
   - Added `CheckIsDutyAccount()` to detect all administrative duty roles (`Admin`, `HR Manager`, `HR Officer`, `Branch Manager`, `Area Manager`, `Department Head`).
   - Filtered out dummy duty employee entities (`NIC == "DUTY-ACC"` and `NIC.StartsWith("DUTY")`) from employee resolution.
   - Introduced `CanRequestTraining` flag, ensuring the "Request Training Session" button and popup modal window are only rendered for eligible non-duty employees.
   - Added hard validation in `OnPostRequestTrainingAsync()` blocking submissions from duty accounts.
2. **Direct Page Protection (`Pages/Training/RequestTraining.cshtml.cs`)**:
   - Added `CheckIsDutyAccount()` and `Forbid()` enforcement in `OnGetAsync()` and `OnPostAsync()`.

---

## Change 223 — Added Direct Give/Edit Feedback Action in Session History Table

### Problem
Employees viewing completed sessions in the Session History table on `/Training/Sessions` had no direct call-to-action to give feedback or see their rating status without opening the full details page.

### Solution
1. **Feedback Status Scoping (`Pages/Training/Sessions.cshtml.cs`)**:
   - Loaded the current employee's submitted feedback records from `TrainingFeedbacks`.
   - Populated `HasUserFeedback`, `UserRating`, and `IsUserEnrolled` properties in `ScheduledSessionDto`.
2. **Session History Action Column (`Pages/Training/Sessions.cshtml`)**:
   - Added amber **"Give Feedback"** button (`.btn-feedback-give`) for completed sessions where the employee has not yet reviewed the program.
   - Added green **"★ X/5 Feedback"** badge button (`.btn-feedback-done`) for completed sessions where feedback was already submitted (linking directly to `#feedbackSection` to view/edit).
   - Retained the primary **"View Details"** action button.

---

## Change 224 — Redesigned and Enhanced Feedback Comments Textarea

### Problem
The "Comments & Key Learnings (Optional)" textarea in the session feedback form rendered with default browser column widths, creating a squished, narrow box with awkward placeholder line wrapping.

### Solution
1. **Full-Width Modern Textarea Styling (`Pages/Training/SessionDetails.cshtml`)**:
   - Added dedicated `.feedback-textarea` CSS class with `width: 100%`, `min-height: 95px`, `padding: 12px 14px`, `border-radius: 8px`, `border: 1px solid #cbd5e1`, and consistent `Manrope` typography.
   - Added smooth green focus ring (`border-color: #10823c; box-shadow: 0 0 0 3px rgba(16, 130, 60, 0.12)`).
   - Improved placeholder text and label formatting.

---

## Change 225 — Enabled Training Session Creation and Branch Scoping for Branch and Area Managers

### Problem
Only HR Managers and HR Officers were authorized to schedule and manage training sessions. Branch Managers and Area Managers needed the capability to create and schedule sessions for employees within their assigned branch scope.

### Solution
1. **Role Authorization (`Pages/Training/Schedule.cshtml.cs` & `EditSession.cshtml.cs`)**:
   - Updated `[Authorize(Roles = "HR Manager, HR Officer, Area Manager, Branch Manager")]`.
2. **Branch & Employee Scoping Logic**:
   - **Area Manager**: Parses `user.ManagedBranches` (multi-branch tokens/IDs/names) with fallback to assigned branch, allowing scheduling across any branches under their jurisdiction.
   - **Branch Manager**: Scopes strictly to their assigned branch (`user.Branch` / `Employee.BranchId`), automatically populating that branch and its active staff.
   - **HR Manager**: Retains full access across all branches.
3. **Session Actions & Navigation Visibility (`Pages/Training/Sessions.cshtml` & `SessionDetails.cshtml`)**:
   - Enabled the **"Schedule New Session"** action button for Branch Managers and Area Managers on `/Training/Sessions`.
   - Enabled session management actions (**"Edit Session & Attendees"**, **"Mark Completed"**, **"Cancel Session"**) for Branch Managers and Area Managers on `/Training/SessionDetails`.

---

## Change 226 — Multi-Branch Training Scheduling and Cross-Branch Attendee Selection

### Problem
Previously, scheduling training was locked to a single `SelectedBranchId`, and changing branches in the dropdown would wipe out attendee selections from other branches, preventing Area Managers from scheduling combined training sessions for employees across multiple assigned branches.

### Solution
1. **Multi-Branch Target & Venue Support (`Pages/Training/Schedule.cshtml` & `EditSession.cshtml`)**:
   - Target Branch dropdown now offers `-- All Assigned Branches (Multi-Branch Session) --` (value `0`) when managing multiple branches, or individual branches as specific venues.
2. **Independent Branch Filter in Attendee Toolbar**:
   - Added a dedicated **Branch Filter** dropdown (`#filterBranch`) in the attendee selection toolbar.
   - Filtering by Branch, Department, Employee Type, or Search query alters table row visibility **without** clearing or unchecking employees from other branches.
   - Added branch identification badge in the attendee table rows for cross-branch clarity.
3. **Backend Validation Across Allowed Branches (`Schedule.cshtml.cs` & `EditSession.cshtml.cs`)**:
   - In `OnPostAsync()`, validated `SelectedEmployeeIds` against `allowedBranchIds.Contains(e.BranchId)` so that employees from any assigned branch under the Area Manager's jurisdiction are successfully enrolled simultaneously.
   - Handled composite multi-branch descriptions and multi-branch notifications cleanly.

---

## Change 227 — Restrict Training Session Scheduling and Management to Area & Branch Managers (View-Only for HR Roles)

### Problem
HR Manager and HR Officer accounts had access to scheduling and mutating training sessions, whereas business requirements dictate that HR Managers and HR Officers can only view session logistics, feedback, and attendee rosters.

### Solution
1. **Restricted Scheduling & Editing Access (`Pages/Training/Schedule.cshtml.cs` & `EditSession.cshtml.cs`)**:
   - Changed `[Authorize(Roles = "Area Manager, Branch Manager")]` on both `ScheduleModel` and `EditSessionModel`.
   - Added explicit role guards (`if (User.IsInRole("HR Manager") || User.IsInRole("HR Officer")) return Forbid();`) in `OnGetAsync()` and `OnPostAsync()`.
2. **Hidden Action Controls for HR Roles (`Pages/Training/Sessions.cshtml` & `SessionDetails.cshtml`)**:
   - Hidden the **"Schedule New Session"** action button from HR Manager and HR Officer on `/Training/Sessions`.
   - Hidden the **"Edit Session & Attendees"**, **"Mark Completed"**, and **"Cancel Session"** buttons from HR Manager and HR Officer on `/Training/SessionDetails`.
   - Guarded `OnPostUpdateStatusAsync` to forbid status changes from HR accounts.
3. **Preserved View-Only Access**:
   - HR Managers retain nationwide read access across all sessions and rosters, and HR Officers retain read access for their assigned branches.

---

## Change 228 — Shift "Manage Training Requests" to Area & Branch Managers

### Problem
Training requests (`/Training/Manage` and `/Training/Details`) were assigned to HR Manager and HR Officer, while operational approvals belong to Area Managers (for their assigned branches) and Branch Managers (for their specific branch).

### Solution
1. **Updated Training Dashboard Card (`Pages/Training/Dashboard.cshtml`)**:
   - Changed the "Manage Requests" feature card role check to `@if (User.IsInRole("Area Manager") || User.IsInRole("Branch Manager"))`.
2. **Restricted Request Review List (`Pages/Training/Manage.cshtml` & `Manage.cshtml.cs`)**:
   - Changed `[Authorize(Roles = "Area Manager, Branch Manager")]` on `ManageModel`.
   - Added server-side role check forbidding HR Manager and HR Officer accounts.
   - Scoped query using `GetAllowedBranchIdsAsync()` so Area Managers see requests across all their managed branches and Branch Managers see requests for their branch.
   - Added **Branch** column with badges to the pending and reviewed request tables.
3. **Restricted Request Approval/Rejection (`Pages/Training/Details.cshtml.cs`)**:
   - Changed `[Authorize(Roles = "Area Manager, Branch Manager")]` on `DetailsModel`.
   - Added server-side role check forbidding HR Manager and HR Officer accounts in `OnGetAsync` and `OnPostUpdateStatusAsync`.
   - Scoped approval/rejection permissions strictly to requests from employees within the manager's authorized branch jurisdiction.

---

## Change 229 — Fix Training Request In-App Notifications for Area & Branch Managers

### Problem
When an employee submitted a training session request, notifications were failing to reach Branch Managers and Area Managers because user resolution relied on optional email strings rather than exact user IDs, and role filtering did not query the `UserRoles` join tables directly.

### Solution
1. **Direct Role & Branch Resolver in `TrainingNotificationService.cs`**:
   - Directly queries `UserRoles` for `Branch Manager` and `Area Manager` roles.
   - Accurately matches Branch Managers by direct branch name/id, employee branch link, or `ManagedBranches` token parsing.
   - Accurately matches Area Managers by `ManagedBranches` tokens, branch name/id, or global fallback.
   - Dispatches notifications using `user.Id` to guarantee 100% reliable inbox delivery across all account types (standard and duty accounts).
2. **Updated Decision Notifications**:
   - Decision notifications (`Approved`/`Declined`) are dispatched directly to the employee's user ID with navigation links to `/Training/Sessions`.

---

## Change 230 — Add Employee Training Session Request History Table

### Problem
In the employee view of the Training Sessions page (`/Training/Sessions`), employees could submit training requests but had no table to track the status and history of their past applications.

### Solution
1. **Model Updates (`Pages/Training/Sessions.cshtml.cs`)**:
   - Added `EmployeeTrainingRequestDto` (Id, Title, Description, RequestedDate, Status).
   - Added `MyTrainingRequests` property to `SessionsModel`.
   - Populated `MyTrainingRequests` in `OnGetAsync` filtering by the logged-in employee's ID (`r.EmployeeId == reqEmpId.Value`) in descending order by requested date.
2. **UI Updates (`Pages/Training/Sessions.cshtml`)**:
   - Added **"My Training Requests"** section table displaying:
     - Program Title and truncated reason/objective with hover preview.
     - Formatted Requested Date & Time.
     - Status badges (`Pending Review`, `Approved`, `Declined`).
     - Review status indicator (`Awaiting Manager`, `Eligible for Scheduling`, `Closed`).
   - Integrated client-side table pagination (`initTablePagination('tblMyTrainingRequests', 6)`).

---

## Change 231 — Reorganize Training Session Attendee Table Columns

### Problem
In `/Training/SessionDetails`, the enrolled attendees table displayed separate columns for Employee Name, EPF Number, and Department, consuming horizontal table space and omitting branch context for cross-branch / area-wide training programs.

### Solution
1. **Attendee Model & Query Updates (`Pages/Training/SessionDetails.cshtml.cs`)**:
   - Added `BranchName` to `SessionAttendeeDto`.
   - Included `.ThenInclude(e => e.Branch)` in the EF Core query in `OnGetAsync` and mapped `BranchName`.
2. **UI & Table Layout Updates (`Pages/Training/SessionDetails.cshtml`)**:
   - Consolidated Employee Name and EPF Number into the **Employee** column, displaying the EPF number formatted under the employee's name.
   - Replaced the standalone Department column with a combined **Branch & Department** column featuring branch and department badges.
   - Updated table column width styling (`.col-name`, `.col-branch-dept`, `.col-desig`, `.col-type`).

---

## Change 232 — Remove Roster Printing Features from Training Session Details

### Problem
The "Print Roster" button, printable header/footer blocks, and print signature columns on `/Training/SessionDetails` were redundant with standard system recordkeeping and added unnecessary clutter to the session overview.

### Solution
1. **Removed Action Button (`Pages/Training/SessionDetails.cshtml`)**:
   - Removed the "Print Roster" button from the top header actions row.
2. **Removed Print-Only Artifacts (`Pages/Training/SessionDetails.cshtml`)**:
   - Removed the `@media print` CSS block and print layout overrides.
   - Removed the printable document header and verification/signature sign-off footer.
   - Removed the print signature column from the attendee table.

---

## Change 233 — Remove Signature Column from Attendee Table

### Problem
A signature column element remained in the attendee table markup on `/Training/SessionDetails`, taking up unnecessary horizontal space.

### Solution
- Removed the `Signature` `<th>` and `<td>` from `tblSessionAttendees` in `Pages/Training/SessionDetails.cshtml`.
- Updated empty state row `colspan` to `5`.

---

## Change 234 — Fix Upcoming Events & Calendar Card Scoping on Employee Dashboard

### Problem
On the Employee Dashboard (`/Index`), the "Upcoming Events & Calendar" card was displaying all-day events created by other users across the system as well as company-wide training sessions that the logged-in employee was not enrolled in.

### Solution
1. **Calendar Events Scoping (`Pages/Index.cshtml.cs`)**:
   - Replaced loose all-day matching with strict user ownership matching (`e.CreatedByUserId == currentUserId`), matching the behavior of `/Calendar/Index`.
2. **Training Session Scoping (`Pages/Index.cshtml.cs`)**:
   - Scoped trainings for regular employees strictly to programs in which they are enrolled (`t.EmployeeTrainings.Any(et => et.EmployeeId == currentEmpId)`).
   - Scoped trainings for Managers and HR according to their assigned branch / managed branches.
3. **Chronological Sorting**:
   - Fixed sorting of combined calendar and training events by actual `DateTime` before taking the top 4 items.
---

## Change 235 — Implemented Job-Targeted CV Bank, Dedicated QR Codes & Adaptive Scoring Engine

### Problem
Previously, the CV Bank was only a common, non-targeted talent pool with static candidate scoring formulas (e.g. standard 5 pts/year of experience regardless of role seniority). There was no mechanism for HR to open specific vacancies, publish dedicated recruitment QR codes per position, or adapt candidate competency scores dynamically based on the job's minimum experience, degree level, and required skill benchmarks.

### Solution
1. **Job Opening Domain Entity (`HRMS.Domain/Entities/Recruitment/JobOpening.cs`)**:
   - Created `JobOpening` entity with `JobCode`, `Title`, `Description`, `Requirements`, `DepartmentId`, `BranchId`, `EmploymentType`, `MinimumExperienceYears`, `MinimumEducationLevel`, `RequiredSkills`, `Status` (Open/Closed), and `ClosingDate`.
   - Updated `HRMS.Domain/Entities/CVBank.cs` to add foreign key `JobOpeningId` and navigation property `JobOpening`.
2. **Database Context (`ApplicationDbContext.cs`)**:
   - Registered `DbSet<JobOpening> JobOpenings`.
3. **Adaptive Candidate Scoring Engine (`ICVBankService.cs` & `CVBankService.cs`)**:
   - Created `CalculateAdaptiveScore(CVBank cv, JobOpening? job)` implementing a 100-point multi-factor model:
     - **Experience Benchmark (40 Pts)**: Evaluates candidate experience against `job.MinimumExperienceYears`. Full base points (25 pts) + surplus bonus (up to 15 pts) when meeting or exceeding benchmarks; proportional score when below.
     - **Academic Qualification Benchmark (35 Pts)**: Evaluates whether candidate holds/exceeds the job's required degree level (`Masters`, `Degree`, or `None/Diploma`).
     - **Skills Match (25 Pts)**: Dynamically parses `job.RequiredSkills` keywords against `cv.Skills` tags.
4. **Publish Vacancy Portal (`Pages/CVBank/CreateJob.cshtml` & `.cs`)**:
   - Created `/CVBank/CreateJob` for HR to open positions, select target branch/department, set minimum qualification criteria, and publish openings.
5. **Enhanced CV Bank Dashboard (`Pages/CVBank/Index.cshtml` & `.cs`)**:
   - Implemented Tab 1 ("Job Openings & QR Codes") and Tab 2 ("Candidate Pipeline & CV Bank").
   - Added instant branded QR Flyer generation (`downloadJobQRFlyer(...)` using HTML5 Canvas) embedding dedicated `/Apply?jobId={id}` links for printable recruitment posters.
   - Added job opening filtering, applicant counts, and status toggle.
6. **Public Application Portal (`Pages/Apply.cshtml` & `.cs`)**:
   - Updated `/Apply` to accept `jobId` / `jobCode`, display a branded job overview banner, lock the target position, and evaluate applicant score adaptively.
7. **Candidate Profile Viewer (`Pages/CVBank/ViewCV.cshtml` & `.cs`)**:
   - Displays targeted job vacancy metadata, benchmark comparison, and adaptive score badge.
---

## Change 236 — Added Interactive Separate Skills Entry & Dynamic Skills Checklist for Applicants

### Problem
Previously, job skills had to be typed as comma-separated text into a single input field. When applicants were filling out the application form, there were no dedicated checkboxes for the skills required by the job, requiring applicants to type everything manually.

### Solution
1. **Interactive Skills Tag Builder for HR (`Pages/CVBank/CreateJob.cshtml`)**:
   - Replaced single text field with an interactive Skill Tag Builder where HR can type skills one-by-one (or paste bulk items), press Enter / click "+ Add", remove individual skill tags with `×`, or select from quick suggestions (e.g. `+ Credit Analysis`, `+ Financial Reporting`, `+ Auditing & Compliance`, etc.).
2. **Interactive Skills Checklist for Applicants (`Pages/Apply.cshtml`)**:
   - When applying for a targeted vacancy, the job's required skills are dynamically rendered as styled, clickable checkbox cards.
   - Applicants simply click the checkboxes for the skills they possess.
   - Added an optional "Other / Additional Skills" text field for any extra capabilities not in the vacancy's list.
   - `syncApplicantSkills()` automatically synchronizes all checked skills + additional skills into `CVInput.Skills`.
3. **Dynamic Skills Checklist for Internal HR Direct Addition (`Pages/CVBank/Create.cshtml`)**:
   - When HR selects a vacancy from the dropdown, the vacancy's required skills checklist dynamically generates as checkboxes for HR to check off from the physical CV.
4. **Adaptive Scoring Match Precision (`CVBankService.cs`)**:
   - Updated delimiter splitting and matching so that checked skills provide exact point matching against the job's required skills.
5. **Cleaned Up Quick Suggestions**:
   - Removed the quick suggestions list from the vacancy creation form, leaving the clean, focused tag builder.
6. **Auto-Generated JobCode Validation Fix**:
   - Removed `[Required]` constraint on `JobCode` in `JobOpening.cs` and added `ModelState.Remove("JobInput.JobCode")` in `CreateJob.cshtml.cs` so auto-generated reference codes (e.g. `JOB-2026-001`) pass model binding without manual user input.
---

## Change 237 — Renamed Job Openings Tab & Dynamic Application URL Deactivation for Closed/Deleted Vacancies

### Problem
1. Tab name in CV Bank dashboard was "Job Openings & QR Codes" and needed to be renamed simply to "Job Openings".
2. When a job opening was deleted, marked as closed, or expired, scanning the QR code or visiting its direct link still displayed the active application form instead of disabling submissions. Furthermore, reopening the vacancy needed to reactivate the QR code link dynamically.

### Solution
1. **Renamed Tab (`Pages/CVBank/Index.cshtml`)**:
   - Renamed the primary tab from `"Job Openings & QR Codes"` to `"Job Openings"`.
2. **Dynamic Vacancy Lifecycle Enforcement (`Pages/Apply.cshtml` & `.cs`)**:
   - Updated `Apply.cshtml.cs` to check the vacancy status and expiration when accessed via `jobId` / `jobCode` query parameters.
   - If the vacancy is marked `Closed`, has passed its `ClosingDate`, or has been deleted:
     - Disables the target vacancy application form.
     - Displays a branded **"Vacancy Closed / No Longer Available"** screen with details and a button to submit a general application.
     - Prevents form submission for inactive roles on the backend.
   - If HR re-opens that vacancy (`Status = "Open"`), the exact same QR code link immediately becomes active and accepts applications again without needing a new QR flyer.

---

## Change 238 — Added Official Company Logo to QR Recruitment Flyer and CV Application Portal

### Problem
1. The downloaded recruitment QR flyer lacked the official Kanrich company logo.
2. The public CV application page had an incorrect image path (`/img/logo.png` instead of `/images/logo.png`), preventing the company logo from rendering.

### Solution
1. **Canvas QR Recruitment Flyer (`Pages/CVBank/Index.cshtml`)**:
   - Updated `downloadJobQRFlyer(...)` to preload `/images/logo.png` and render the official Kanrich logo on a clean white card within the top header of the canvas flyer.
   - Enhanced flyer framing, layout proportions, and typography.
   - Removed the raw URL string text from the flyer to keep the poster design clean, uncluttered, and focused on mobile QR scanning.
2. **Public CV Application Portal (`Pages/Apply.cshtml`)**:
   - Fixed the logo source to `/images/logo.png` with automatic fallback to `/images/logo.webp`.
   - Adjusted `.brand-logo` dimensions to `height: 46px; object-fit: contain;` for crisp, high-resolution rendering across desktop and mobile screens.
---

## Change 239 — Upgraded QR Recruitment Flyer to High-Definition Vector Supersampling (1400x1900)

### Problem
The downloaded recruitment QR flyer previously rendered at standard low resolution (680x920 canvas with 300x300 QR code), causing visual blurriness and pixelation on high-DPI screens and when printed.

### Solution
1. **High-DPI Supersampling (`Pages/CVBank/Index.cshtml`)**:
   - Upgraded Canvas resolution to **1400 x 1900** pixels with `imageSmoothingQuality = "high"`.
   - Increased internal QR code generation matrix to **640 x 640** pixels with error correction Level H so individual modules render with vector-like sharpness.
   - Scaled typography (52px title, 32px headers, 28px badges) and border strokes (8px outer, 3px inner).
   - High-resolution rendering of the company logo (`440x105` white card container) ensuring professional, print-ready output.
---

## Change 240 — Cleaned Up Application Portal Header Navbar

### Problem
The text *"Kanrich Finance Limited - Talent Acquisition & Career Opportunities"* next to the logo in the application portal was redundant since the official company logo already contains the company branding.

### Solution
- Removed the text block from the top navigation bar in `HRMS.UI/Pages/Apply.cshtml`.
- Increased the company logo size to `height: 52px` with proportional scaling and crisp rendering.
- Removed the "Submit General Application" button from the closed/expired vacancy notice screen to keep the inactive state clean and informative.
---

## Change 241 — Cleaned Candidate Table Icons & Added Pagination to All CV Bank Tables

### Problem
1. The candidate table in the CV Bank dashboard rendered redundant avatar initials circles and email envelope icons.
2. The CV Bank tables lacked pagination when viewing large volumes of job openings or candidates.

### Solution
1. **Candidate Table Row Cleanup (`Pages/CVBank/Index.cshtml`)**:
   - Removed the `.candidate-avatar` initials icon circle next to candidate names.
   - Removed the `<i class="bi bi-envelope"></i>` mail icon from candidate email addresses for a clean, streamlined view.
2. **Table Pagination Integration (`Pages/CVBank/Index.cshtml`)**:
   - Added `id="jobsTable"` to the Job Openings table and `id="candidatesTable"` to the Candidate Pipeline table.
   - Initialized `initTablePagination` on both tables with page size options (10, 20, 50, 100 entries per page), page counter info ("Showing X–Y of Z"), previous/next controls, and active page numbers.
---

## Change 242 — Added Official System Favicon to Public Careers & Application Portal

### Problem
The public candidate application portal (`/Apply`) did not include the official Kanrich favicon link tag in its `<head>`, displaying the browser's default blank globe/document icon instead of the branded logo icon.

### Solution
- Added `<link rel="icon" type="image/png" href="/images/logo-title.png" />` and `<link rel="shortcut icon" href="/images/logo-title.png" />` to the `<head>` section of `HRMS.UI/Pages/Apply.cshtml`.
- Matches the exact favicon branding used across the internal HRMS application shell and document viewer.
---

## Change 243 — Removed Internal Login Redirection from Application Portal Logo

### Problem
The company logo in the public recruitment application portal (`/Apply`) was wrapped in `<a href="/">`, which caused external applicants to be redirected to the internal staff login screen (`/Account/Login`) when clicking the logo.

### Solution
- Replaced `<a href="/" class="brand-wrap">` with `<div class="brand-wrap">` in `HRMS.UI/Pages/Apply.cshtml`.
- Applicants remain securely on the careers portal without accidental redirection to internal administrative areas.
---

## Change 244 — Restricted Training Session Completion to Past Sessions in Session History

### Problem
Previously, managers could mark a training session as "Completed" at any time (including future/upcoming scheduled sessions before they occurred), causing premature completion of upcoming events.

### Solution
1. **Session Details (`Pages/Training/SessionDetails.cshtml` & `.cs`)**:
   - The "Mark Completed" button is now strictly conditional on `s.Date.Date < DateTime.Today`. While a session is in the future/upcoming, only "Cancel Session" and "Edit Session & Attendees" are available.
   - Added backend validation in `OnPostUpdateStatusAsync` rejecting completion requests for sessions with dates on or after today.
2. **Session History (`Pages/Training/Sessions.cshtml` & `.cs`)**:
   - Added a "Mark Completed" button directly inside the **Session History** table for past uncompleted sessions (`session.Status == "Scheduled"` and `session.Date.Date < DateTime.Today`).
   - Added `OnPostUpdateStatusAsync` handler with authorization and date verification to process session completions from the main sessions dashboard.
3. **Session Editing (`Pages/Training/EditSession.cshtml` & `.cs`)**:
   - In `EditSession.cshtml`, the status dropdown only includes the "Completed" option if the scheduled session date has passed (`SessionDate.Date < DateTime.Today`) or if the session was already completed.
   - Added validation in `OnPostAsync` in `EditSession.cshtml.cs` ensuring upcoming sessions cannot be saved with "Completed" status.
---

## Change 245 — Prevented Past Closing Dates on Job Vacancies

### Problem
When publishing a new recruitment vacancy in the CV Bank, the application closing deadline date input allowed past dates to be selected in the datepicker.

### Solution
- Added `min="@DateTime.Today.ToString("yyyy-MM-dd")"` attribute to the closing date `<input type="date">` in `HRMS.UI/Pages/CVBank/CreateJob.cshtml`, disabling past dates in the browser datepicker.
- Added validation error feedback span and client-side pre-submission check in `syncSkillsBeforeSubmit()` to alert if an invalid past date is entered.
- Verified existing backend model validation in `CreateJob.cshtml.cs` (`JobInput.ClosingDate.Value.Date < DateTime.Today`).
---

## Change 246 — Enforced Assigned Branch Authorization for HR Officers Opening Vacancies

### Problem
When publishing new recruitment positions in the CV Bank (`/CVBank/CreateJob`), HR Officers could see and select all branches across the company or post company-wide openings, rather than being restricted to their assigned branches.

### Solution
1. **Branch Scoping & Validation (`Pages/CVBank/CreateJob.cshtml.cs`)**:
   - Injected `UserManager<ApplicationUser>` and implemented `GetAllowedBranchIdsAsync()` to resolve assigned branches from `ManagedBranches` (or employee record).
   - Filtered the `Branches` list for HR Officers, Area Managers, and Branch Managers so only authorized branches appear.
   - Enforced backend validation in `OnPostAsync` ensuring HR Officers can only submit vacancies for their assigned branches.
2. **Form UI Updates (`Pages/CVBank/CreateJob.cshtml`)**:
   - For branch-restricted roles, removed the organization-wide `-- Head Office / All Branches --` option and marked Target Branch as required.
   - Auto-selects the branch if the HR Officer is assigned to exactly one branch.
---

## Change 247 — Displayed Training Session Rating Next to View Details Button in Session History

### Problem
Completed training sessions in the Session History table did not display their participant ratings next to the action button, requiring users to navigate into the session details page to see the rating and reviews.

### Solution
1. **Aggregated Feedback Statistics (`Pages/Training/Sessions.cshtml.cs`)**:
   - Updated `OnGetAsync` to query `TrainingFeedbacks` for all sessions and compute the `AverageRating` (e.g. `4.8`) and `FeedbackCount` (number of submissions) grouped by `TrainingId`.
   - Added `AverageRating` and `FeedbackCount` properties to `ScheduledSessionDto`.
2. **Rating Pill UI in Session History (`Pages/Training/Sessions.cshtml`)**:
   - Added `.rating-pill` CSS badge styling with a gold star icon (`<i class="bi bi-star-fill text-warning"></i>`), rating score (e.g. `4.5 / 5`), and review count.
   - Rendered the rating badge immediately adjacent to the `View Details` button for all completed sessions that have attendee ratings.
---

## Change 248 — Established Head Office Welfare Department & Head of Welfare Approval Workflow

### Problem
1. Welfare assistance requests previously went to generic branch/department heads without a dedicated corporate Welfare Department stationed at Head Office.
2. When employees requested financial assistance, loans, or distress grants, the approval pipeline lacked central coordination by a designated **Head of Welfare**.
3. Department management in Settings did not treat the **Welfare Department** as a corporate department permanently stationed at Head Office (unlike Human Resources).

### Solution
1. **Welfare Department & Head of Welfare Seeding (`Program.cs`)**:
   - Seeded the **Welfare** Department (`Name = "Welfare"`) and permanently linked it to the **Head Office** branch in `BranchDepartments`.
   - Seeded the default **Head of Welfare** duty account (`head.welfare` / `head.welfare@kanrich.lk`) under the Head Office Welfare Department with role `"Department Head"`.
2. **Corporate Department Policy & Management (`Settings/Departments/Assign.cshtml` & `.cs`, `Index.cshtml`)**:
   - Enforced corporate policy restricting the Welfare Department to Head Office only (analogous to the Corporate Human Resources department).
   - Displayed Corporate and Head Office badges on Department index and assignment pages.
3. **Duty Account Provisioning & Formatting (`Admin/DutyAccounts/Create.cshtml.cs`, `Edit.cshtml.cs`)**:
   - Automatically formats display name as `"Head of Welfare (Head Office)"` and username as `"head.welfare"` when assigning a Department Head to the Welfare Department.
4. **Welfare Request Submission & Targeted Notifications (`Welfare/RequestForm.cshtml.cs`)**:
   - Routes new welfare assistance requests to `CurrentLevel = "DepartmentHead"` (Head of Welfare).
   - Dispatches real-time targeted notifications to the Head of Welfare upon employee request submission (`"A new welfare request (WF-XXXX) from {EmployeeName} is pending your approval."`).
5. **Head of Welfare Approvals Screen (`Welfare/Approvals/DepartmentHeadApproval.cshtml` & `.cs`)**:
   - Updated approval dashboard with clear titles: **"Head of Welfare Approvals"** and central review subtitles.
   - Updated approval actions, rejection feedback, and audit messages to record Head of Welfare reviews.
   - Forwarded approved requests to branch/area/HR manager stages while alerting relevant managers.
6. **Visual Status Tracking & Labels (`Welfare/StatusTracking.cshtml`, `RequestList.cshtml`)**:
   - Renamed stage 1 in the approval timeline to **"Head of Welfare Approval"**.
   - Updated summary current level text to display **"Head of Welfare (Head Office)"**.
   - Updated status badges in `RequestList.cshtml` to show `"Welfare Head"` and added quick approval action shortcuts.
---

## Change 249 — Displayed Permanent Station Badge for Welfare Department in Departments List

### Problem
On the Department management page (`/Settings/Departments/Index`), the **Welfare** department still showed an active "Assign Branches" button, whereas corporate departments permanently stationed at Head Office (like Human Resources) should display the static **"Permanent Station"** indicator.

### Solution
- Updated `HRMS.UI/Pages/Settings/Departments/Index.cshtml` to check `isCorporate` (which includes both `Human Resources` and `Welfare`) when rendering table actions.
- Displays `<span class="badge...">Permanent Station</span>` for the Welfare department in place of the "Assign Branches" button.
---

## Change 250 — Excluded Managerial Department from Settings / Departments View

### Problem
The `Managerial` department is an internal structural department used by the system for duty accounts and branch/area management hierarchies. It appeared in the Settings &rarr; Departments table view where administrators manage business units, causing unnecessary confusion.

### Solution
- Updated `HRMS.UI/Pages/Settings/Departments/Index.cshtml.cs` to filter out `Name != "Managerial" && Name != "Management"` from the departments view query.
- The department entity continues to exist in the database and operates smoothly behind the scenes for duty accounts and branch manager assignments without being visible in the settings table.
---

## Change 251 — Fixed Table Pagination Initial Page Size and Dropdown Mismatch

### Problem
On the **Manage Designations** page (and other settings pages), initializing pagination with `initTablePagination(..., 8)` set the internal page size to 8 rows, while the pagination dropdown UI only contained options `[10, 20, 50, 100]`. Because `8` did not match any `<option>`, the browser visually defaulted to displaying `10 / page` in the select element even though only 8 rows were being rendered on page 1 (e.g. showing 1–8 of 12 records instead of 1–10 of 12).

### Solution
1. **Dropdown & Initial Size Synchronization (`Pages/Shared/_Layout.cshtml`)**:
   - Updated `initTablePagination` so that if `initialPageSize` is not present in `sizes = [10, 20, 50, 100]`, `pageSize` is automatically appended and sorted into `sizes`.
   - Ensures the `<select>` options and active slice `pageSize` are always 100% synchronized regardless of initial argument.
2. **Standardized Settings Table Page Sizes**:
   - Updated `Settings/Designations/Index.cshtml`, `Settings/Departments/Index.cshtml`, and `Settings/Branches/Index.cshtml` to initialize with standard `10` items per page (`initTablePagination('tbl-...', 10)`).
   - The Designations table with 12 records now cleanly displays 10 records on Page 1 (`Showing 1–10 of 12`) and the remaining 2 records on Page 2.
---

## Change 252 — Comprehensive Audit and Standardization of Pagination Page Sizes

### Problem
Beyond the Designations table, several other administrative and tracking tables (such as Employees Drafts & Documents, Duty Accounts, Training Staff Tracking, and Session Management) were calling `initTablePagination` with page size `8` or `6`.

### Solution
- Conducted a comprehensive audit of all paginated data tables across the entire project.
- Standardized the default page size to `10` across:
  - **Settings**: Designations (`tbl-designations`), Departments (`tbl-departments`), Branches (`tbl-branches`)
  - **Employees**: Employees (`tbl-employees`), Drafts (`tbl-drafts`), Documents (`tbl-docs`)
  - **Admin Duty Accounts**: HR Manager (`tbl-hr-mgr`), Area Manager (`tbl-area-mgr`), Dept Heads (`tbl-dept-heads`), Branch Manager (`tbl-branch-mgr`)
  - **Training & Development**: Intern Tracking (`tblInternStaff`), Probation Tracking (`tblProbationStaff`), Requests (`tblPendingRequests`, `tblReviewedRequests`), Sessions (`tblUpcomingSessions`, `tblPastSessions`, `tblMyTrainingRequests`), Milestone Evaluations (`tblMilestoneEvaluations`)
  - **User Profile**: Profile Documents (`tbl-profile-docs`)
- Ensured all tables load with standard 10 rows per page matching their default dropdown indicators.
---

## Change 253 — Excluded Managerial Department from Duty Account Department Head Creation

### Problem
When provisioning or editing Department Head duty accounts (`/Admin/DutyAccounts/Create` and `/Admin/DutyAccounts/Edit`), the internal structural `Managerial` department appeared in the department selection lists and branch-department assignment dropdowns, allowing Department Heads to be mistakenly assigned to the Managerial department.

### Solution
1. **Creation Dropdowns & Pairing (`Pages/Admin/DutyAccounts/Create.cshtml.cs`)**:
   - Filtered out `Managerial` and `Management` from `DepartmentList` and `DeptHeadBranchGroups` / `DeptHeadBranchDeptList`.
   - Added backend validation rejecting any attempt to create a Department Head for the Managerial department.
2. **Edit Dropdowns (`Pages/Admin/DutyAccounts/Edit.cshtml.cs`)**:
   - Filtered out `Managerial` and `Management` from the department selector when modifying existing duty accounts.
---

## Change 254 — Added Dedicated First-Class "Head of Welfare" Corporate Duty Account Role

### Problem
Previously, the Welfare Department was grouped under generic branch Department Heads. However, the Welfare Department is a permanent corporate station at Head Office (just like the HR Department) that requires global, company-wide authority to review and approve welfare assistance and distress grants.

### Solution
1. **Role & Identity Seed Configuration (`Program.cs`)**:
   - Seeded the dedicated `"Head of Welfare"` role and core designation.
   - Assigned the default `head.welfare` duty user to both `"Head of Welfare"` and `"Department Head"` roles.
2. **Duty Accounts Index Dashboard (`Pages/Admin/DutyAccounts/Index.cshtml` & `Index.cshtml.cs`)**:
   - Added a dedicated **"Corporate Welfare Management (Head Office)"** table section alongside Corporate HR Management.
   - Displays the amber `Head of Welfare` badge, `head.welfare` username, full name, and Edit/Delete management actions with table pagination (`tbl-welfare-mgr`).
3. **Duty Account Creation & Validation (`Pages/Admin/DutyAccounts/Create.cshtml` & `Create.cshtml.cs`)**:
   - Added a dedicated role selection card for **Head of Welfare** with heart-handshake icon and description.
   - Added a dedicated preview and creation form panel enforcing single-instance corporate assignment (`HasExistingWelfareHead`), generating username `head.welfare`, email `head.welfare@kanrich.lk`, Location: `Head Office`, Department: `Welfare`, and Authority: `Global / All Branches`.
4. **Duty Account Editing (`Pages/Admin/DutyAccounts/Edit.cshtml` & `Edit.cshtml.cs`)**:
   - Added full support for editing and maintaining the Corporate Head of Welfare duty account.
5. **Welfare Approvals Authorization (`Pages/Welfare/Approvals/DepartmentHeadApproval.cshtml.cs` & `Pages/Welfare/RequestForm.cshtml.cs`)**:
   - Added `Head of Welfare` role to the approval page authorization policy.
   - Updated request submission notifications to directly target users in the `Head of Welfare` role.
---

## Change 255 — Consolidated Corporate HR and Welfare into a Single Corporate Management Table

### Problem
On the Duty Accounts index dashboard (`/Admin/DutyAccounts`), `Corporate HR Management` and `Corporate Welfare Management` were displayed as two separate single-row tables, resulting in excessive vertical spacing and duplicate pagination bars.

### Solution
1. **Model Layer (`Pages/Admin/DutyAccounts/Index.cshtml.cs`)**:
   - Replaced separate `HRManagers` and `WelfareHeads` lists with a unified `CorporateHeads` list containing both corporate leadership duty accounts.
2. **View Layer (`Pages/Admin/DutyAccounts/Index.cshtml`)**:
   - Merged the two tables into a single **"Corporate Management (Head Office)"** table (`#tbl-corporate-mgr`).
   - Renders each corporate duty account with its respective role badge (`HR Manager` in green, `Head of Welfare` in amber), credentials, and action links under one clean table and single pagination bar.
---

## Change 256 — Renamed "Head of Welfare (Head Office)" to "Welfare Manager"

### Problem
To achieve consistent role naming and corporate titles matching the `HR Manager` station, the corporate Welfare leadership duty account title and display name needed to be changed from `Head of Welfare (Head Office)` to `Welfare Manager`.

### Solution
1. **Identity & Role Seeding (`Program.cs`)**:
   - Seeded the `Welfare Manager` role and designation.
   - Updated the default `head.welfare` duty user and employee record to display name `Welfare Manager` (initials `WM`, designation `Welfare Manager`) and auto-updates existing records on startup.
2. **Duty Accounts Index (`Pages/Admin/DutyAccounts/Index.cshtml` & `Index.cshtml.cs`)**:
   - Updated badge text to amber `Welfare Manager`.
   - Loaded and normalized duty account list to display `Welfare Manager`.
3. **Duty Account Provisioning & Editing (`Pages/Admin/DutyAccounts/Create.cshtml`, `Create.cshtml.cs`, `Edit.cshtml`, `Edit.cshtml.cs`)**:
   - Renamed role selection card, description, form panel, and buttons to **Welfare Manager**.
   - Set auto-generated display name to `Welfare Manager`.
4. **Welfare Approvals & Tracking (`Pages/Welfare/StatusTracking.cshtml`, `RequestForm.cshtml.cs`, `DepartmentHeadApproval.cshtml`, `DepartmentHeadApproval.cshtml.cs`)**:
   - Updated approval workflow level label to **Welfare Manager Approval** and current level display to **Welfare Manager**.
   - Updated approval page title and `<h2>Welfare Manager Approvals</h2>`.
   - Updated notifications and success messages to reference the **Welfare Manager**.
5. **Department Policy Notice (`Pages/Settings/Departments/Assign.cshtml`)**:
   - Updated notice to state that the **Welfare Manager** oversees employee financial assistance and distress grants.
---

## Change 257 — Cleaned Up Legacy Roles for Welfare Manager to Enforce Single Dedicated Role

### Problem
The `head.welfare` duty user accumulated 3 separate roles (`Head of Welfare`, `Welfare Manager`, and `Department Head`) from intermediate migration steps. On the Users/Duty Accounts table in Settings, this caused 3 role badges to appear for `head.welfare`, whereas `hrmanager` only had 1 badge (`HR Manager`).

### Solution
1. **Startup Seed & Role Normalization (`Program.cs`)**:
   - Removed intermediate `Head of Welfare` from Identity roles and core designations.
   - Enforced that `head.welfare` is strictly assigned only to the single **`Welfare Manager`** role, and automatically removed old legacy roles (`Head of Welfare` and `Department Head`) from `head.welfare` on startup.
2. **Duty Account Creation (`Pages/Admin/DutyAccounts/Create.cshtml.cs`)**:
   - Removed redundant multi-role assignment; newly created accounts now strictly receive only the single selected role.
3. **Query & Authorization Cleanup (`Pages/Admin/DutyAccounts/Index.cshtml.cs`, `Pages/Welfare/Approvals/DepartmentHeadApproval.cshtml.cs`, `Pages/Welfare/RequestForm.cshtml.cs`)**:
   - Cleaned up queries and `[Authorize]` attributes to directly reference `Welfare Manager`.
---

## Change 258 — Included Welfare Manager in Admin Users Account Management

### Problem
On the Admin Users list (`/Admin/Users`), the page used a hardcoded list of duty roles (`AllRoles`) and usernames that did not include `Welfare Manager` or `head.welfare`. When the legacy `Department Head` role was removed from `head.welfare`, the filter excluded `head.welfare` from the table.

### Solution
1. **Model Layer (`Pages/Admin/Users/Index.cshtml.cs`)**:
   - Added `"Welfare Manager"` to `AllRoles` (which populates the role filter dropdown and the duty role matching set).
   - Added `head.welfare` to the explicit administrative username filter check.
2. **View Layer (`Pages/Admin/Users/Index.cshtml`)**:
   - Added amber styling class `.role-badge-container.welfare` (`#fef3c7` / `#92400e`) and bound it to `"Welfare Manager"`.
---

## Change 259 — Linked Employee Record and Display Name for Welfare Manager in User Table

### Problem
On the Admin Users list (`/Admin/Users`), the "User" (Linked Employee) column displayed `-` for `head.welfare` because `head.welfare` was missing its `EmployeeId` foreign key link to the corresponding `Employee` record and the table lacked a fallback to `user.FullName`.

### Solution
1. **Startup Seed & Linking (`Program.cs`)**:
   - Ensured that `head.welfare` is linked to an `Employee` entity with `FullName = "Welfare Manager"`, `Initials = "WM"`, and `headOfWelfareUser.EmployeeId` is set to the employee's ID.
2. **Model Layer (`Pages/Admin/Users/Index.cshtml.cs`)**:
   - Updated `LinkedEmployee` mapping to fall back to `user.FullName` (`"Welfare Manager"`) if `emp` is temporarily unlinked, ensuring the name is always shown instead of `-`.
---

## Change 260 — Disabled Deletion for Corporate Leadership Duty Accounts (HR Manager & Welfare Manager)

### Problem
Corporate leadership duty accounts (`HR Manager` and `Welfare Manager`) are permanent company-wide stations required for central HR operations and welfare approval pipelines. Having a delete button on these accounts could inadvertently break active approval workflows.

### Solution
1. **View Layer (`Pages/Admin/DutyAccounts/Index.cshtml`)**:
   - Removed the Delete button/form from the **Corporate Management (Head Office)** table. Only the **Edit** action is rendered for HR Manager and Welfare Manager.
2. **Backend Protection (`Pages/Admin/DutyAccounts/Index.cshtml.cs`)**:
   - Added guard in `OnPostDeleteAsync` rejecting deletion requests for accounts belonging to `"HR Manager"` or `"Welfare Manager"` roles, or usernames `hrmanager` and `head.welfare`.
---

## Change 261 — Removed Edit Action for Corporate Leadership Duty Accounts (HR Manager & Welfare Manager)

### Problem
Corporate leadership duty accounts (`HR Manager` and `Welfare Manager`) are permanent stations stationed strictly at Head Office with fixed branch, department, and authority scopes. They should not be re-configured or modified through the generic duty accounts edit form.

### Solution
1. **View Layer (`Pages/Admin/DutyAccounts/Index.cshtml`)**:
   - Removed the Edit link and the Actions column from the **Corporate Management (Head Office)** table. The table now displays a clean 4-column overview (`Role`, `Name`, `Username`, `Branch / Department`).
2. **Backend Guard (`Pages/Admin/DutyAccounts/Edit.cshtml.cs`)**:
   - Added redirection guards in both `OnGetAsync` and `OnPostAsync` rejecting direct edit access for `HR Manager` and `Welfare Manager` accounts, redirecting back to the index dashboard with an informative message.
---

## Change 262 — Streamlined Employee Welfare Request Form by Removing Redundant Employee Information Section

### Problem
On the Welfare Request Form (`/Welfare/RequestForm`), the page required employees to manually enter their Employee ID, click a search button, and displayed 10 redundant read-only employee detail boxes. Because the employee is already logged in, this manual step was unnecessary and cluttered the submission experience.

### Solution
1. **Model Layer (`Pages/Welfare/RequestForm.cshtml.cs`)**:
   - Added `GetCurrentEmployeeAsync()` to automatically resolve the authenticated employee from the logged-in user context in both `OnGetAsync()` and `OnPostAsync()`.
   - Populates and binds `EmployeeId` directly in the background and validates that the user is linked to an active employee record before submission.
2. **View Layer (`Pages/Welfare/RequestForm.cshtml`)**:
   - Removed the entire "Employee Information" card, ID search input, and 10 auto-filled fields.
   - Replaced with a clean hidden input `<input type="hidden" name="EmployeeId" value="@Model.EmployeeId" />`.
   - Removed unused client-side employee fetching and clearing JavaScript routines.
---

## Change 263 — Fixed Welfare Request Entity Changes Error with Database Seed & Dynamic Type Binding

### Problem
Submitting a welfare request failed with *"An error occurred while saving the entity changes. See the inner exception for details."* because the `welfaretype` database table was not seeded, resulting in a foreign key constraint violation when inserting into `welfarerequest`. In addition, hardcoded type IDs on the view could drift from database auto-increment keys, and exception messages did not expose inner details.

### Solution
1. **Startup Database Seed (`Program.cs`)**:
   - Seeded default `WelfareTypes` (`Medical Assistance`, `Education Assistance`, `Housing Loan`, `Festival Advance`, `Funeral Assistance`) on startup if not present.
2. **EF Entity Configuration (`ApplicationDbContext.cs`)**:
   - Explicitly configured navigation foreign key relationships for `WelfareRequest.Employee` (`Cascade`) and `WelfareRequest.WelfareType` (`Restrict`).
3. **Model & View Layer (`Pages/Welfare/RequestForm.cshtml` & `.cs`, `Pages/Welfare/EditRequest.cshtml` & `.cs`)**:
   - Dynamically load active `WelfareTypes` from database and populate the assistance type dropdown.
   - Added safe date parsing (`DateTime.TryParse`) with fallback to `DateTime.Today`.
   - Enhanced exception catching to extract and present exact inner exception details.
---

## Change 264 — Resolved EF Core Shadow Column `WelfareTypeId1` in WelfareRequest Mapping

### Problem
EF Core generated an unexpected shadow property column `WelfareTypeId1` in SQL queries and inserts because `WelfareType.WelfareRequests` navigation collection was not paired to `WelfareRequest.WelfareType` in `ApplicationDbContext.cs`. This caused MySQL/TiDB to throw: *"Unknown column 'WelfareTypeId1' in 'field list'"* upon saving a request.

### Solution
1. **EF Navigation Pairing (`ApplicationDbContext.cs`)**:
   - Configured `entity.HasOne(e => e.WelfareType).WithMany(t => t.WelfareRequests).HasForeignKey(e => e.WelfareTypeId).OnDelete(DeleteBehavior.Restrict);` so EF Core correctly recognizes the bi-directional relationship on the existing `welfare_type_id` column without generating shadow FK columns.
   - Explicitly configured `entity.HasMany(e => e.Documents).WithOne(d => d.WelfareRequest).HasForeignKey(d => d.RequestId).OnDelete(DeleteBehavior.Cascade);`.
---

## Change 265 — Redesigned Welfare / My Requests Page to Match Kanrich HRMS Design System

### Problem
The Welfare My Requests list page (`/Welfare/RequestList`) used legacy generic card styles and lacked alignment with the modern Kanrich HRMS UI design system (e.g. Manrope typography, stat cards grid, search/filter toolbar, stage/status pills, and responsive client-side pagination).

### Solution
1. **Header & Quick Navigation (`Pages/Welfare/RequestList.cshtml`)**:
   - Modernized header with clean typography (`.page-h`, `.page-s`), role-based approval shortcut button (`Welfare Approvals` / `Branch Approvals` / `Area Approvals`), and brand-styled primary action button (`+ New Welfare Request`).
2. **Metric & KPI Summary Cards**:
   - Integrated 4-column KPI cards (*Total Applications*, *Under Review*, *Approved & Disbursed*, *Rejected / Closed*) styled with subtle theme-tinted icon containers and hover lift effects.
3. **Filter & Search Toolbar**:
   - Implemented real-time search filtering across Ref ID (`WF-0001`), Assistance Type, Category, Amounts, Status, and Date.
   - Added Status filter (`All Statuses`, `Pending Review`, `Approved`, `Disbursed / Completed`, `Draft`, `Rejected`) and Assistance Type filter with live record count indicator.
4. **Data Grid & Status Badges**:
   - Upgraded table styling with high-contrast headers, JetBrains Mono reference badges (`WF-xxxx`), category sub-labels, and hierarchical stage pills (`Welfare Manager`, `Branch Manager`, `Area Manager`, `HR Manager`, `Completed`).
   - Integrated status badges with pulsing colored dots and action group with 24-hour remaining edit timers.
5. **Interactive Client-Side Pagination**:
   - Implemented seamless 10-row pagination with numeric page switcher, next/prev controls, and automatic empty-state handling.
---

## Change 266 — Fixed Uneven Top and Left Padding on Welfare My Requests Page

### Problem
The `.welfare-page-wrap` container had explicit padding (`24px 28px`), which combined with the global layout's `.page-content` padding (`26px`), resulting in double and uneven padding (50px top vs 54px left).

### Solution
1. **View Wrapper (`Pages/Welfare/RequestList.cshtml`)**:
   - Reset `.welfare-page-wrap` to `padding: 0; background: transparent;`, allowing the system standard uniform `26px` padding from `.page-content` to apply evenly to all sides.
2. **Global Reset Rule (`Pages/Shared/_Layout.cshtml`)**:
   - Added `.page-content .welfare-page-wrap` to the layout double-padding normalization rules.
---

## Change 267 — Standardized Table Pagination on Welfare Requests List

### Problem
The Welfare My Requests page lacked the standard Kanrich table pagination bar with page size selection (`10`, `20`, `50`, `100` / page), navigation buttons (`← Prev`, `Next →`), smart page ellipsis, and real-time synchronization with search/status filters.

### Solution
1. **Global Pagination Helper (`Pages/Shared/_Layout.cshtml`)**:
   - Enhanced `initTablePagination()` to support dynamic row filtering via `data-filter-hidden="true"`, reuse existing pagination bars, and expose `_tablePaginationRender` and `_tablePaginationResetPage` methods on table elements.
2. **Welfare Request List (`Pages/Welfare/RequestList.cshtml`)**:
   - Integrated `initTablePagination('welfareRequestsTable', 10)` on page load.
   - Connected `applyFilters()` to mark filtered-out rows and trigger instant pagination recalculation with matching record counts.
---

## Change 268 — Streamlined Welfare Table Columns & Removed Redundant Category Label

### Problem
In the Welfare Requests table, the **Assistance Type** column displayed a duplicate category subtitle below the type name (e.g., "Education Assistance" with "education" repeated below it). In addition, having 8 columns (including separate stage, status, requested, and approved amounts) cluttered the table layout on standard displays.

### Solution
1. **Single-Line Assistance Type (`Pages/Welfare/RequestList.cshtml`)**:
   - Removed the duplicate category sub-label, rendering a clean single-line title for each assistance type (e.g. *Education Assistance*, *Medical Assistance*).
2. **Streamlined Columns (Reduced from 8 to 6)**:
   - Simplified columns to: `Ref No`, `Assistance Type`, `Request Date`, `Amount`, `Status`, and `Action`.
   - Combined stage and status into a single comprehensive status badge (*Welfare Review*, *Branch Review*, *Area Review*, *HR Disbursement*, *Approved*, *Disbursed*, *Rejected*, *Draft*).
---

## Change 269 — Balanced Table Column Proportions on Welfare Requests List

### Problem
After reducing table columns, fixed-pixel widths pushed `Request Date`, `Amount`, `Status`, and `Action` all the way to the right edge, leaving an awkward blank space in the middle of the table.

### Solution
1. **Proportional Column Widths (`Pages/Welfare/RequestList.cshtml`)**:
   - Replaced fixed pixel widths with proportional percentages: `Ref No` (14%), `Assistance Type` (28%), `Request Date` (16%), `Amount` (16%), `Status` (16%), and `Action` (10%).
   - The table columns now distribute naturally and evenly across 100% of the card width with zero empty gaps.
---

## Change 270 — Natural Table Column Balance with Stage & Status Layout

### Problem
Forced percentage widths left disproportionate empty spacing around short fields (Ref No, Type, Date) while squeezing action buttons.

### Solution
1. **Natural Auto-Flow Layout (`Pages/Welfare/RequestList.cshtml`)**:
   - Removed artificial hardcoded percentages, allowing the standard responsive table engine to naturally distribute column widths proportional to content.
2. **7-Column System Alignment**:
   - Organized columns into: `Ref No`, `Assistance Type`, `Request Date`, `Amount`, `Current Stage`, `Status`, and `Action`.
   - Uses stage badges (*Welfare Review*, *Branch Review*, *Area Review*, *HR Disbursement*, *Completed*) alongside clear status indicators (*Pending*, *Approved*, *Disbursed*, *Rejected*, *Draft*), creating a balanced, professional, and readable table grid.
---

## Change 271 — Added 24-Hour Welfare Request Deletion Capability for Employees

### Problem
Employees who submitted a welfare assistance application had no way to delete or withdraw it if submitted in error within the initial 24-hour review grace period.

### Solution
1. **Backend Deletion Handlers (`Pages/Welfare/RequestList.cshtml.cs` & `Pages/Welfare/EditRequest.cshtml.cs`)**:
   - Implemented `OnPostDeleteAsync(int id)` with strict ownership and eligibility checks:
     - Validates that the request belongs to the authenticated employee.
     - Enforces the 24-hour window (`DateTime.Now <= request.CreatedAt.AddHours(24)` while in `DepartmentHead` level and `Pending` status, or `IsDraft`).
     - Cleans up associated uploaded document files from `wwwroot/uploads/welfare/{requestId}` before removing the database records.
2. **Action Controls & Confirmation Modals (`Pages/Welfare/RequestList.cshtml` & `Pages/Welfare/EditRequest.cshtml`)**:
   - Added a red-accented **Delete** button with a confirmation prompt alongside the Edit button on the **My Requests** list.
   - Added a **Delete Request** button to the **Edit Request** page action toolbar.
   - Live remaining time hint now indicates time left to edit or delete the request.

---

## Change 272 — Welfare Manager Role Portal Navigation, Approvals Routing, and Dashboard

### Problem
When logging in as the Welfare Manager duty account (`head.welfare`), the system defaulted the user to the Employee Portal layout and Employee Dashboard because the role `Welfare Manager` was not recognized in the layout navigation checks and manager dashboard eligibility predicates.

### Solution
1. **Sidebar Navigation & Portal Branding (`Pages/Shared/_Layout.cshtml`)**:
   - Added `Welfare Manager Portal` header label in the sidebar.
   - Updated the **Welfare** sidebar link to dynamically route to `/Welfare/Approvals/DepartmentHeadApproval` when logged in as `Welfare Manager` or `Department Head` (and corresponding approval routes for `Branch Manager`, `Area Manager`, and `HR Manager`).
---

## Change 273 — Welfare Manager Sidebar Exclusivity, Document Access, and Approvals Overhaul

### Problem
1. The Welfare Manager duty account (`head.welfare`) was exposed to extraneous modules (Dashboard, Calendar, Separation, Attendance, Training, Payroll, Performance) in the sidebar when they only should have the Welfare tab.
2. The Welfare Approvals interface (`DepartmentHeadApproval.cshtml`) used a basic legacy card layout lacking table alignment, pagination, search, category filters, and an interactive review/approval modal.
3. Welfare Manager role was not authorized in `DownloadDocument.cshtml.cs`, leading to potential 403 Forbidden errors when attempting to view or download attached documents.

### Solution
1. **Sidebar Navigation Exclusivity (`Pages/Shared/_Layout.cshtml` & `Pages/Index.cshtml.cs`)**:
   - Filtered out Dashboard, Calendar, Separation, Attendance & Leave, Training, Payroll, and Performance sidebar navigation items for users with the `Welfare Manager` role so that **ONLY the Welfare tab** is displayed in the sidebar.
   - Added automatic login redirect from `/Index` straight to `/Welfare/Approvals/DepartmentHeadApproval` for Welfare Managers.
2. **Authorized Document Access (`Pages/Welfare/DownloadDocument.cshtml.cs`)**:
   - Added `User.IsInRole("Welfare Manager")` to `CanAccessAsync` to ensure full access for inline previews and downloads of all welfare applicant documents.
3. **Comprehensive Welfare Approvals Redesign (`Pages/Welfare/Approvals/DepartmentHeadApproval.cshtml` & `.cshtml.cs`)**:
   - **4 Stat Metric Cards**: Pending Review (with total LKR amount), Approved by Welfare Manager (Forwarded to Branch), Rejected Requests, and Total Processed.
   - **Search & Filtering Toolbar**: Real-time text search, Assistance Type dropdown, and Status Tab filters (`All`, `Pending Review`, `Approved`, `Rejected`) with dynamic count badges.
---

## Change 274 — Welfare Approvals Employee Name with Initials, Avatar Images, and Full Profile Access

### Problem
1. The Welfare Approvals table displayed employee full names instead of the standard name with initials.
2. Employee profile images were not rendered in the table list or in the approval decision modal.
3. The Welfare Manager did not have a direct shortcut button from the review modal to inspect the employee's full HR profile, and `/Employees/Details` lacked authorization for the `Welfare Manager` role.

### Solution
1. **Name with Initials & Profile Avatars (`Pages/Welfare/Approvals/DepartmentHeadApproval.cshtml`)**:
---

## Change 275 — Fix 404 Route on Employee Details Page for Query Parameters and Path Route

### Problem
When navigating to `/Employees/Details?id=150003`, ASP.NET Core returned HTTP 404 because the `@page` directive strictly required a route parameter `{id:int}` (`/Employees/Details/150003`) and did not match query strings.

### Solution
1. **Optional Route Parameter in Details Page (`Pages/Employees/Details.cshtml` & `Pages/Employees/Details.cshtml.cs`)**:
   - Changed the page directive to `@page "{id:int?}"`.
---

## Change 276 — Circular Employee Profile Avatars in Welfare Approvals

### Problem
Employee profile photos were styled with rounded square corners (`border-radius: 8px` / `10px`) rather than circular avatars.

---

## Change 277 — Clean Employee Cell in Approvals Table without Profile Photos

### Problem
The user requested to not show the avatar image thumbnail inside the Approvals table rows, keeping the table layout compact while retaining the circular photo in the Review Modal.

### Solution
---

## Change 278 — Dedicated Employee Welfare History Dossier & Review Modal Integration

### Problem
When clicking the "Full Details" button in the Welfare Review modal, the Welfare Manager was redirected to the generic HR Employee Details page, resulting in an "Access Denied" error if permission was restricted, and missing the employee's welfare history necessary for informed assistance evaluations.

### Solution
1. **Dedicated Employee Welfare Dossier Page (`Pages/Welfare/EmployeeHistory.cshtml` & `.cshtml.cs`)**:
   - Authorized roles: `Welfare Manager, Department Head, HR Manager, HR Officer, Area Manager, Branch Manager, Admin`.
---

## Change 279 — Privacy & Data Minimization on Employee Welfare History Dossier

### Problem
The initial Employee Welfare History view displayed sensitive and non-essential personal information (NIC number, exact Date of Birth, Gender, Home Residential Address, Spouse/Family details, and Bank Account details) which exceeded the necessary scope for welfare assistance evaluations.

### Solution
1. **Streamlined Employee Card (`Pages/Welfare/EmployeeHistory.cshtml`)**:
   - Removed sensitive personal fields (NIC, DOB, Gender, Residential Address), family/spouse details, and banking details.
---

## Change 280 — Remove Print Summary Button & Add Table Pagination on Welfare History Dossier

### Problem
The Employee Welfare History dossier contained an unnecessary "Print Summary" action button and lacked standard pagination on the Welfare Assistance History table when reviewing past claims.

### Solution
1. **Removed Print Button (`Pages/Welfare/EmployeeHistory.cshtml`)**:
   - Removed the `Print Summary` button from the top navigation action row.
---

## Change 281 — Direct Finalization on Welfare Manager Approval

### Problem
When the Welfare Manager approved a welfare request, the workflow previously forwarded the request through a 4-tier approval pipeline (`BranchManager` $\rightarrow$ `AreaManager` $\rightarrow$ `HRManager`) rather than finalizing the grant directly under the Welfare Manager's sole authority.

### Solution
1. **Direct Finalization (`Pages/Welfare/Approvals/DepartmentHeadApproval.cshtml.cs`)**:
   - Updated `Action == "Approved"` to set `CurrentLevel = "Completed"`, `CurrentStatus = "Approved"`, `Status = "Approved"`, and `ApprovedAmount = request.ApprovedAmount ?? request.RequestedAmount`.
   - Dispatches automated notification to the **Applicant Employee** with approved grant details.
   - Dispatches automated notification to the **HR Manager** with employee, branch, and approved grant information.
   - Dispatches automated notification to all **HR Officers** assigned to that employee's branch (`user.ManagedBranches`).

---

## Change 282 — Welfare Records & Oversight Portal for HR Officers & HR Manager

### Problem
HR Officers and HR Managers needed a dedicated portal interface to inspect, filter, search, and track approved and pending welfare records for employees in their respective jurisdictions.

### Solution
1. **Dedicated Welfare Records Page (`Pages/Welfare/Records.cshtml` & `.cshtml.cs`)**:
   - Authorized roles: `HR Manager, HR Officer, Admin, Welfare Manager`.
   - **Branch Scoping**: Automatically scopes displayed welfare records for HR Officers to their `ManagedBranches` list with a scope info banner, while granting company-wide visibility to HR Manager and Admin.
---

## Change 283 — Prevent Past Date Selection in Welfare Request Forms

### Problem
When creating or editing a welfare request, employees were able to select past dates in the Request Date picker.

### Solution
1. **Frontend HTML & JavaScript Restrictions (`Pages/Welfare/RequestForm.cshtml` & `Pages/Welfare/EditRequest.cshtml`)**:
   - Added `min="@DateTime.Today.ToString("yyyy-MM-dd")"` attribute to the `RequestDate` date input elements.
   - Initialized `dateInput.min = todayStr` in JavaScript on DOMContentLoaded so the browser date picker disallows selecting dates before today.
2. **Backend Date Validation (`Pages/Welfare/RequestForm.cshtml.cs` & `Pages/Welfare/EditRequest.cshtml.cs`)**:
---

## Change 284 — Fix Dynamic Assistance Type Change for Requested Amount & Repayment Period

### Problem
In the Welfare Request Form (`Pages/Welfare/RequestForm.cshtml`), when switching between different assistance types, the maximum eligible amount limit, category hint, repayment period section visibility, and monthly deduction calculations did not update or clear reliably due to a hardcoded type ID check (`opt.value === '3'`) and missing empty-state resets.

### Solution
1. **Dynamic Loan / Repayment Type Flagging (`Pages/Welfare/RequestForm.cshtml`)**:
   - Added `data-is-loan` attribute on each `<option>` determined by whether `TypeName` contains `"Loan"` or `"Advance"` or if `Category` is `"Housing"` or `"Financial"`.
2. **Dynamic UI Reset & Re-validation (`Pages/Welfare/RequestForm.cshtml`)**:
   - In `onTypeChange()`, dynamically sets `amountInput.max = max` and re-runs `validateAmount()`.
   - When switching to a loan type (e.g. Housing Loan, Festival Advance), displays the Repayment Period dropdown and calculates monthly deductions in real time.
   - When switching to a non-loan type (e.g. Medical, Education, Funeral), cleanly hides `#repaymentDiv`, clears selected repayment period, and resets monthly deductions.
   - When selecting the empty `"Select type…"` option, cleanly resets all limits, hints, and repayment inputs.

---

## Change 285 — Resolve EF Core Cascade Deletion Exception on Welfare Requests

### Problem
Deleting a welfare request threw `InvalidOperationException: The association between entity types 'WelfareRequest' and 'WelfareDocument' has been severed, but the relationship is either marked as required or is implicitly required because the foreign key is not nullable` because EF Core was configured with default client nullification on non-nullable `RequestId` child foreign keys.

### Solution
1. **Cascade Delete Mapping (`ApplicationDbContext.cs`)**:
   - Configured `.OnDelete(DeleteBehavior.Cascade)` explicitly for both `WelfareDocument` (`FK_WelfareDocument_WelfareRequest`) and `WelfareApproval` (`FK_WelfareApproval_WelfareRequest`).
---

## Change 286 — Clean Requested Amount Decimal Format & Simplified Edit Policy Banner

### Problem
1. In the Edit Welfare Request view (`Pages/Welfare/EditRequest.cshtml`), the `RequestedAmount` input displayed raw unformatted decimal scale values with extensive trailing zeroes (e.g. `20000.0000000000000000000000000`).
2. The amber edit window notice featured an animated countdown timer rather than a concise policy statement explaining the 24-hour single-edit window.

### Solution
1. **Clean Decimal Number Formatting (`Pages/Welfare/EditRequest.cshtml`)**:
   - Set `value="@Model.RequestedAmount.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)"` on the amount input, ensuring amounts display cleanly (e.g. `20000` or `20000.50`).
---

## Change 287 — Synchronize Request Creation Validation Rules on Edit Request

### Problem
The Edit Welfare Request view (`Pages/Welfare/EditRequest.cshtml`) lacked the comprehensive interactive and backend validation rules present on the creation form, including maximum eligible amount limit alerts, repayment period calculations for loans, and character limits on reasons.

### Solution
1. **Frontend Interactive Validation (`Pages/Welfare/EditRequest.cshtml`)**:
   - Added category display, maximum eligible limit checking with live red warning alerts (`#amountHint`), repayment period dropdown for loan/advance categories with dynamic monthly deduction calculations, and character counter (`0 / 500`) on the reason textarea.
2. **Backend Validation Rules (`Pages/Welfare/EditRequest.cshtml.cs`)**:
---

## Change 288 — Completely Remove Welfare Request Edit Option

### Problem
---

## Change 289 — Enforce Maximum Limit of 2 Approved Welfare Requests per Calendar Year

### Problem
Employees need to be restricted to a maximum of 2 approved welfare requests per calendar year. If an employee already has 2 approved requests within the year, they should be blocked from submitting further applications.

### Solution
1. **Annual Limit Tracking & Backend Validation (`Pages/Welfare/RequestForm.cshtml.cs`)**:
   - In `OnGetAsync` and `OnPostAsync`, counts approved welfare requests for the current employee within the calendar year (`(r.Status == "Approved" || r.CurrentStatus == "Approved" || r.Status == "PaymentCompleted") && (r.RequestDate.Year == currentYear || r.CreatedAt.Year == currentYear)`).
   - In `OnPostAsync`, rejects submission if `approvedCount >= 2` with message:
     > *"You have already reached the maximum annual limit of 2 approved welfare requests for {currentYear}. You cannot submit further applications for this year."*
2. **Form UI & Policy Guard (`Pages/Welfare/RequestForm.cshtml`)**:
   - Displays a red warning banner if `Model.IsAnnualLimitReached`:
     > *"You currently have 2 approved welfare assistance requests in {year}. In accordance with company policy, employees are eligible for a maximum of 2 approved welfare requests per calendar year. You cannot submit new welfare applications for {year}."*
---

## Change 290 — Remove Obsolete Multi-Stage Approval Timeline from Welfare Status Tracking

### Problem
The welfare review/status tracking page (`Pages/Welfare/StatusTracking.cshtml`) displayed an obsolete 6-step multi-tier approval timeline (`Request Submitted` $\rightarrow$ `Welfare Manager Approval` $\rightarrow$ `Branch Manager Approval` $\rightarrow$ `Area Manager Approval` $\rightarrow$ `HR Manager Payment` $\rightarrow$ `Payment Completed`) which contradicted the streamlined direct single-stage approval workflow.

### Solution
1. **Removed Timeline Card & Styles (`Pages/Welfare/StatusTracking.cshtml`)**:
   - Completely removed the `Approval Timeline` card and its corresponding CSS rules.
2. **Replaced with Decision Remarks Section (`Pages/Welfare/StatusTracking.cshtml`)**:
   - In the Request Details section, dynamically renders a clean `Welfare Manager Decision Remarks` box displaying the decision status, decision timestamp, and manager remarks when a review action has occurred.

---

## Change 291 — Hide Welfare Tab from Duty Accounts and Employee Profiles

### Problem
The user requested hiding the Welfare navigation tab from all duty accounts (Branch Manager, Area Manager, HR Manager, HR Officer, Department Head, Admin) and employee profiles, restricting it strictly to the Corporate Welfare Manager.

### Solution
1. **Restricted Navigation Bar (`Pages/Shared/_Layout.cshtml`)**:
   - Updated the sidebar navigation to wrap the Welfare menu item inside `@if (User.IsInRole("Welfare Manager"))`.
   - Hidden for all other duty accounts (`Admin`, `Branch Manager`, `Area Manager`, `HR Manager`, `HR Officer`, `Department Head`) and standard employee profiles.
2. **Dashboard Quick Action Cleanup (`Pages/Index.cshtml`)**:
   - Removed the "Welfare & Benefits" quick action card from the employee self-service dashboard.
   - Restricted the manager "Welfare" quick action card strictly to `User.IsInRole("Welfare Manager")`.

---

## Change 292 — Remove Welfare Roles from Admin Portal

### Problem
The user requested completely removing the Welfare Manager and Head of Welfare duty account roles and creation cards from the Admin Portal.

### Solution
1. **User Management (`Pages/Admin/Users/Index.cshtml` & `.cshtml.cs`)**:
   - Removed `"Welfare Manager"` from `AllRoles` list.
   - Removed Welfare role badge class styling and mappings.
2. **Duty Account Creation (`Pages/Admin/DutyAccounts/Create.cshtml` & `.cshtml.cs`)**:
   - Removed the "Welfare Manager" role selection card and form panel from the create workflow.
   - Removed `HasExistingWelfareHead` property and checks.
   - Removed `"Welfare Manager"` / `"Head of Welfare"` from `coreDesignations`, username/email generators, branch/dept mapping switches, and validation rules.
3. **Duty Account Index & Edit (`Pages/Admin/DutyAccounts/Index.cshtml` & `.cshtml.cs`, `Edit.cshtml` & `.cshtml.cs`)**:
   - Removed `Welfare Manager` loading and badge styling from the duty accounts directory.
   - Removed `Welfare Manager` / `Head of Welfare` from edit authorization checks and role switches.

---

## Change 293 — Remove Floating Report Issue Button and Issue Tracker Page

### Problem
The user requested removing the floating "Report Issue" bug reporter button and modal, along with the Issue Tracker page from the Admin Portal.

### Solution
1. **Layout & Global UI (`Pages/Shared/_Layout.cshtml`)**:
   - Removed the floating `Report Issue` FAB button (`#btnOpenBugReport`) and quick report modal (`#bugReportModal`).
   - Removed client-side JavaScript error capturing and submission handlers.
   - Removed bug reporter CSS styles (`.bug-fab-btn`, `.bug-modal-*`).
   - Removed the `Issue Tracker` navigation item from the Admin sidebar menu.
2. **Removed Admin Pages & Endpoints**:
   - Removed `Pages/Admin/Issues/Index.cshtml` and `Pages/Admin/Issues/Index.cshtml.cs`.
   - Removed `Pages/Api/BugReport.cshtml` and `Pages/Api/BugReport.cshtml.cs`.

---

## Change 294 — Remove Employee Requests Sub Tab from Employees Page

### Problem
The user requested removing the placeholder "Employee Requests" sub-tab from the Employees directory page.

### Solution
1. **View Markup (`Pages/Employees/Index.cshtml`)**:
   - Removed the `Employee Requests` tab and badge pill from the tab navigation bar.
2. **Page Model (`Pages/Employees/Index.cshtml.cs`)**:
   - Removed the `RequestCount` property and its placeholder value assignment.

---

## Change 295 — Route Document Approvals to HR Officers and Exclude HR Manager

### Problem
1. When employees uploaded verification documents, notifications were being dispatched to HR Manager and Admin, omitting HR Officers responsible for the branch.
2. HR Manager is not supposed to receive employee document approval requests, notifications, or view/process the Pending Document Approvals tab.

### Solution
1. **Document Upload Notification Dispatch (`Pages/Profile.cshtml.cs`)**:
   - Updated `OnPostUploadDocumentAsync` to resolve `HR Officer` accounts and match `Employee.BranchId` against each officer's `ManagedBranches` (or fallback to all HR Officers).
   - Removed `HR Manager` from receiving new document upload notifications. Dispatches notifications to assigned `HR Officer`s and `Admin`.
2. **Directory & Review UI (`Pages/Employees/Index.cshtml` & `Index.cshtml.cs`)**:
   - Restricted the `Pending Document Approvals` sub-tab and tab panel strictly to `@if (User.IsInRole("HR Officer") || User.IsInRole("Admin"))`.
   - Hidden from `HR Manager`, `Branch Manager`, and `Area Manager`.
   - In `Index.cshtml.cs`, scoped document loading to HR Officer / Admin and sanitized the active tab.
3. **Review Authorization (`Pages/Employees/ReviewDocument.cshtml.cs`)**:
   - Updated page authorization attribute to `[Authorize(Roles = "HR Officer,Admin")]` (removed `HR Manager`).

---

## Change 296 — Allow Employees to Cancel Pending Document Requests

### Problem
Employees who uploaded documents for verification had no way to cancel or retract their request if they uploaded an incorrect file or made an error.

### Solution
1. **Cancel Document Handler (`Pages/Profile.cshtml.cs`)**:
   - Added `OnPostCancelDocumentAsync(int documentId)` handler to allow authenticated employees to cancel their own pending document submissions.
   - Validates ownership and ensures only documents with `Status == "Pending"` can be cancelled.
   - Removes the database record and cleans up the stored file from disk.
2. **Profile UI (`Pages/Profile.cshtml`)**:
   - Added an **Actions** column with a dedicated **Cancel** button on pending document rows in the "My Documents" tab.
   - Added confirmation prompt before deletion.
   - Styled `.btn-cancel-doc` with danger pill aesthetics.

---

## Change 297 — Hide Topbar Profile Icon for Duty Accounts

### Problem
The user requested hiding the top navigation bar profile avatar icon for all duty accounts (Admin, HR Manager, HR Officer, Area Manager, Branch Manager, Welfare Manager, Department Head), displaying it only for regular employee accounts.

### Solution
1. **Topbar Markup (`Pages/Shared/_Layout.cshtml`)**:
   - Evaluated `isDutyAccount` for `Admin`, `HR Manager`, `HR Officer`, `Area Manager`, `Branch Manager`, `Welfare Manager`, and `Department Head`.
   - Wrapped the topbar avatar icon `<a href="/Profile" class="avatar">` in `@if (!isDutyAccount)` so duty accounts do not see the profile icon.

---

## Change 298 — Stack Document Table Under Upload Box in Employee Profile

### Problem
In the "My Documents" tab of the Employee Profile page, the upload box and the documents table were placed in a 2-column side-by-side layout. The user requested placing the documents table directly under the document upload box.

### Solution
1. **Layout & View Structure (`Pages/Profile.cshtml`)**:
   - Updated `.doc-layout` from CSS 2-column grid to a single-column vertical flex container (`display: flex; flex-direction: column; gap: 24px;`).
   - Placed the upload card at the top with responsive input fields, live preview box, and upload trigger.
   - Positioned the documents list table card directly underneath the upload box.

---

## Change 299 — Remove Settings Button from Top Navigation Bar in HR Manager Portal

### Problem
The Settings gear icon in the topbar was visible to both Admin and HR Manager roles, but the underlying `/Settings` page is authorized strictly for Admin accounts.

### Solution
1. **Topbar Markup (`Pages/Shared/_Layout.cshtml`)**:
   - Updated the role condition on the Settings button from `@if (User.IsInRole("Admin") || User.IsInRole("HR Manager"))` to `@if (User.IsInRole("Admin"))`.
   - The Settings button is now hidden from the HR Manager portal topbar.

---

## Change 300 — Standardize Back Button to Link to Separation Management on Transfer and Resignation Pages

### Problem
1. On the **Transfer Request Details** page (`Pages/Transfer/Details.cshtml`), the back button text was verbose (e.g. "Back to Review Transfers" / "Back to Reviewed Transfers") and routed to role-specific sub-pages rather than the central Separation Management page.
2. On the **Resignation Request Details** page (`Pages/Resignation/Details.cshtml`), the back link routed to `/Resignation/MyRequests` instead of the central Separation Management dashboard.
3. Review pages (`HRManager`, `BranchManager`, `AreaManager`, `DepartmentHead`) for both transfers and resignations also had verbose back labels and routed to legacy index pages.

### Solution
1. **Transfer Request Details (`Pages/Transfer/Details.cshtml`)**:
   - Updated back button text to just `"Back"`.
   - Changed navigation target to `/Separation/Dashboard?ActiveTab=Transfers` for duty roles (`Department Head`, `Branch Manager`, `Area Manager`, `HR Manager`, `HR Officer`, `Admin`) and `/Transfer/Separation?ActiveTab=Transfers` for standard employees.
2. **Resignation Request Details (`Pages/Resignation/Details.cshtml`)**:
   - Updated back button text to `"Back"`.
   - Changed navigation target to `/Separation/Dashboard?ActiveTab=Resignations` for duty roles and `/Transfer/Separation?ActiveTab=Resignation` for standard employees.
3. **Review Pages (`Pages/*Manager/ReviewTransfer.cshtml`, `Pages/*Manager/ReviewResignation.cshtml`, `Pages/DepartmentHead/*`)**:
   - Updated back buttons across all duty reviewer pages to simply read `"Back"` and route directly back to the active tab in `/Separation/Dashboard`.

---

## Change 301 — Enforce 1 Month Minimum Notice for Resignation Last Working Day

### Problem
Previously, the resignation form required a minimum of 14 days notice. The user requested that the last working day (effective date) must be at least 1 month from the requesting date.

### Solution
1. **Separation Page & Resignation Form (`Pages/Transfer/Separation.cshtml`)**:
   - Updated date input `min` constraint to `@DateTime.Today.AddMonths(1).ToString("yyyy-MM-dd")`.
   - Updated hint text to reflect *"Minimum 1 month notice required"*.
2. **Submission Handlers (`Pages/Resignation/SubmitFromSeparation.cshtml.cs`, `EditDraft.cshtml.cs`, `Apply.cshtml.cs`)**:
   - Validated that `effectiveDate.Date >= DateTime.Today.AddMonths(1)` on submit.
   - Displayed error message `"Last working day must be at least 1 month from the requesting date."` if earlier.
   - Defaulted draft date to `DateTime.Today.AddMonths(1)`.
3. **Core Service Validation (`HRMS.Application/Services/ResignationService.cs`)**:
   - Enforced `entity.EffectiveDate.Date >= entity.ResignationDate.Date.AddMonths(1)` in `ValidateAndSubmitAsync`.

---

## Change 302 — Add Option to Delete Resignation Request Drafts

### Problem
Employees who saved a resignation request as a draft had no way to delete or discard the draft, which also prevented them from creating a new resignation request (since having an active draft marks resignation as in-progress).

### Solution
1. **Core Resignation Service (`HRMS.Application/Services/ResignationService.cs`)**:
   - Added `DeleteDraftAsync(int id, string userEmail)` to `IResignationService` and `ResignationService`.
   - Verified that the record is in `Draft` status and belongs to the requesting employee.
   - Cleans up associated department reviews and attached documents before deleting the resignation request record.
2. **Separation Dashboard (`Pages/Transfer/Separation.cshtml` & `Separation.cshtml.cs`)**:
   - Added `OnPostDeleteDraftAsync(int id)` page handler.
   - Added a red **Delete** button with confirmation modal next to **Edit Draft** on the Separation table for draft resignation rows.
3. **Draft Editor (`Pages/Resignation/EditDraft.cshtml` & `EditDraft.cshtml.cs`)**:
   - Added `OnPostDeleteAsync(int id)` page handler.
   - Added a **Delete Draft** button with confirmation in the form action buttons footer.
4. **My Requests Page (`Pages/Resignation/MyRequests.cshtml` & `MyRequests.cshtml.cs`)**:
   - Added `OnPostDeleteDraftAsync(int id)` and action buttons to Edit / Delete draft records.

---

## Change 303 — Remove Approval Flow Side Panel from Resignation Forms

### Problem
The "Approval Flow" card (outlining Stage 1 Branch Manager, Stage 2 Area Manager, Stage 3 HR Manager) on the resignation submission and draft editor pages was requested to be removed.

### Solution
1. **Separation Page (`Pages/Transfer/Separation.cshtml`)**:
   - Removed the `.approval-panel` card and its unused CSS definitions.
   - Restructured the resignation submission container to use the full width form card.
2. **Draft Editor (`Pages/Resignation/EditDraft.cshtml`)**:
   - Removed the `.approval-panel` side panel and its CSS definitions.
   - Updated the form layout to cleanly display full width.
3. **Resignation Apply Page (`Pages/Resignation/Apply.cshtml`)**:
   - Removed the Approval Flow notice list from the sidebar.

---

## Change 304 — Remove Financial Obligations Checkboxes from Resignation Forms

### Problem
The "I have outstanding loans" and "I am a loan guarantor" checkboxes were no longer needed on the employee resignation submission and draft editor forms.

### Solution
1. **Separation Page (`Pages/Transfer/Separation.cshtml`)**:
   - Removed the "Financial Obligations" checkbox section.
   - Kept "Last Working Day" as a clean, full-width form input.
2. **Draft Editor (`Pages/Resignation/EditDraft.cshtml`)**:
   - Removed the "Financial Obligations" checkbox block from the draft editor.
3. **Resignation Apply Page (`Pages/Resignation/Apply.cshtml`)**:
   - Removed the "Financial Obligations" section from the form.

---

## Change 305 — Exclude Managerial Department from Resignation Department Reviews

### Problem
When employees submitted a resignation request, the system automatically generated Department Head clearance review items for every department linked to the branch, including the "Managerial" department. Since Branch Managers already conduct their review in Stage 2 (and Department Heads cannot be in the Managerial department), having a Stage 1 Department Head review for "Managerial" was redundant and created unresolvable pending reviews.

### Solution
1. **Department Review Initialization (`HRMS.Application/Services/ResignationService.cs`)**:
   - Added `IsManagerialDept` helper to identify `Managerial` and `Management` department records.
   - Updated `InitializeDepartmentReviewsAsync` to filter out `Managerial` / `Management` departments when creating `ResignationDepartmentReview` records.
   - If a branch has no non-managerial departments requiring DH clearance, the resignation request automatically advances directly to Stage 2 (`DeptHeadsApproved`) awaiting Branch Manager review.
2. **Review & Pending Handlers (`ResignationService.cs`)**:
   - Filtered out `Managerial` departments in `GetPendingForDeptHeadAsync` and `GetReviewedByDeptHeadAsync`.
   - Prevented review submissions for `Managerial` in `DeptHeadReviewAsync`.
   - Added auto-cleanup of legacy `Managerial` department reviews on load.

---

## Change 306 — Fix Draft Resignation Validation and Enable Deletion in Details & Edit Views

### Problem
1. When saving a resignation submission form as a draft, validation required "Reason for Resignation" to be filled out.
2. When viewing a drafted resignation form via the Details page (`/Resignation/Details/{id}`), there was no option to delete or edit the draft directly from that view.
3. On the Draft Editor page (`/Resignation/EditDraft/{id}`), clicking "Delete Draft" triggered client-side form validation on required fields in the main form instead of directly deleting the draft.

### Solution
1. **Form Validation Bypass for Drafts**:
   - Added `formnovalidate` attribute to the "Save as Draft" buttons on `Pages/Transfer/Separation.cshtml` and `Pages/Resignation/EditDraft.cshtml`.
   - Updated `SubmitFromSeparation.cshtml.cs` and `EditDraft.cshtml.cs` so `reasonForResignation` is nullable and optional when `action == "draft"`, only enforcing validation on `"submit"`.
2. **Details Page Actions for Draft Status (`Pages/Resignation/Details.cshtml` & `Details.cshtml.cs`)**:
   - Added banner and action buttons ("Edit Draft" and "Delete Draft") when `Model.Request.Status == ResignationStatusEnum.Draft`.
   - Added a confirmation modal and `OnPostDeleteDraftAsync(int id)` handler to delete the draft cleanly and redirect back to the separation page.
3. **Standalone Delete Form on Edit Draft Page (`Pages/Resignation/EditDraft.cshtml`)**:
   - Decoupled the "Delete Draft" button from the main edit form by placing it in an independent `<form id="deleteDraftForm">` to eliminate form validation conflicts during deletion.
4. **Resignation Service & Compilation**:
   - Implemented `UpdateDraftAsync` and `DeleteDraftAsync` in `ResignationService.cs` and verified clean compilation across `Debug` and `Release` (`win-x86`) configurations.

---

## Change 307 — Remove Initiation Date Field from Termination Forms

### Problem
The Initiation Date field on the termination request creation and edit forms was redundant for users to input manually, as it can be automatically determined and recorded by the system as today's date upon request initiation.

### Solution
1. **Creation Form (`Pages/Termination/CreateRequest.cshtml` & `CreateRequest.cshtml.cs`)**:
   - Removed the `Input.InitiationDate` input field and validation span from the form layout.
   - Removed the `[Required]` validation constraint on `InitiationDate`.
   - Set `InitiationDate = DateTime.Today` automatically when constructing the `TerminationRequestViewModel`.
   - Updated date validation to verify `EffectiveTerminationDate >= DateTime.Today`.
2. **Edit Form (`Pages/Termination/EditRequest.cshtml` & `EditRequest.cshtml.cs`)**:
   - Removed the `Input.InitiationDate` input field from the editor.
   - Preserved the existing initiation date from `CurrentRequest.InitiationDate` when saving or submitting edits.
3. **Details & Reports Display**:
   - Kept read-only display of Initiation Date intact on details, review, and report pages for auditing and tracking.

---

## Change 308 — Configure Sri Lanka Standard Time (SLST / UTC+05:30) Across Application & Host

### Problem
When deployed on Azure App Service or servers running in UTC (Coordinated Universal Time), the system date and time lagged 5.5 hours behind Sri Lanka time (e.g., past 12:00 AM midnight in Sri Lanka was still evaluated as the previous calendar day on the server). This caused dashboard dates, greeting bars, attendance records, calendar views, and separation workflows to display yesterday's date.

### Solution
1. **Azure / Host Process Timezone Configuration (`HRMS.UI/web.config` & `Program.cs`)**:
   - Configured `<environmentVariable name="WEBSITE_TIME_ZONE" value="Sri Lanka Standard Time" />` and `<environmentVariable name="TZ" value="Asia/Colombo" />` in `web.config` to instruct Azure App Service (Windows & Linux) to run the process in Sri Lanka Standard Time.
   - Configured default culture (`en-LK`) and `TZ` environment variable in `Program.cs`.
2. **Global ViewImports (`Pages/_ViewImports.cshtml`)**:
   - Added `@using HRMS.Domain.Common` to globally expose `SriLankaTime` across all Razor Pages and partial views.
3. **Application & PageModel Time Alignment**:
   - **Dashboard (`Pages/Index.cshtml` & `Index.cshtml.cs`)**: Updated greeting bar date (`@SriLankaTime.Now.ToString("MMM dd, yyyy")`), greeting time of day calculation (`SriLankaTime.Now.Hour`), OT calculation sub-headers, and employee on-leave counts (`SriLankaTime.Today`).
   - **Calendar (`Pages/Calendar/Index.cshtml` & `Index.cshtml.cs`)**: Aligned Month view, Week view today markers, and "Today" button navigation with `SriLankaTime.Today` and `SriLankaTime.Now`.
   - **Performance (`Pages/Performance/Index.cshtml.cs`)**: Aligned metrics date cutoff with `SriLankaTime.Today`.
   - **Payroll & Attendance Review (`Pages/Payroll/AttendanceReview.cshtml`, `Index.cshtml`, `Index.cshtml.cs`)**: Used `SriLankaTime.Now` for report timestamps, cycle selectors, and start-over modals.
   - **Separation & Termination (`Pages/Termination/CreateRequest.cshtml.cs`, `EditRequest.cshtml.cs`, `Resignation/Apply.cshtml.cs`, `SubmitFromSeparation.cshtml.cs`, `EditDraft.cshtml.cs`)**: Aligned minimum effective date and creation timestamps with `SriLankaTime.Today`.
4. **Compilation & Verification**:
   - Verified that `dotnet build` passes cleanly with 0 warnings and 0 errors in both Debug and Release (`win-x86`) configurations.

---

## Change 309 — Enforce Minimum Current Date (No Past Dates) for Effective Termination Date

### Problem
The Effective Termination Date field on both the Create and Edit termination forms allowed users to select past dates via the browser's date picker and submit them.

### Solution
1. **Client-Side HTML5 Date Constraints (`Pages/Termination/CreateRequest.cshtml` & `EditRequest.cshtml`)**:
   - Added `min="@SriLankaTime.Today.ToString("yyyy-MM-dd")"` attribute to `<input asp-for="Input.EffectiveTerminationDate" type="date">` on both creation and edit forms, preventing selection of past dates directly in the date picker interface.
   - Added validation spans for `EffectiveTerminationDate`.
2. **Server-Side Validation (`CreateRequest.cshtml.cs`, `EditRequest.cshtml.cs`, & `TerminationService.cs`)**:
   - In `CreateRequest.cshtml.cs`: Enforced `ValidateDates()` check that `EffectiveTerminationDate >= SriLankaTime.Today`.
   - In `EditRequest.cshtml.cs`: Added `ValidateDates()` check during both `OnPostSaveAsync` (draft save) and `OnPostSubmitAsync` (final submission).
   - In `TerminationService.cs`: Enforced `if (entity.EffectiveTerminationDate.Date < SriLankaTime.Today)` check in `ValidateAndSubmitAsync`.
3. **Build & Release Verification**:
   - Tested and verified clean compilation for both Debug and Release (`win-x86`).

---

## Change 310 — Remove Supervisor Remarks, Special Remarks, and Employee Obligations & Clearances from Termination Forms

### Problem
The termination initiation and edit forms included optional `Supervisor Remarks`, `Special Remarks / Notes`, and `Employee Obligations & Clearances` (direct/indirect obligations, loan/guarantor checkboxes, override controls), which cluttered the initial submission workflow.

### Solution
1. **Form Streamlining (`Pages/Termination/CreateRequest.cshtml` & `EditRequest.cshtml`)**:
   - Removed `Supervisor Remarks` and `Special Remarks / Notes` textarea fields from the primary Termination Details section.
   - Removed the entire `Employee Obligations & Clearances` section (Direct Obligations, Indirect Obligations, Has Outstanding Loans, Is Loan Guarantor, Management Override controls).
   - Removed the `toggleOverride()` JavaScript helper function and initialization calls from `CreateRequest.cshtml`.
2. **PageModel & Model Compatibility (`Pages/Termination/CreateRequest.cshtml.cs` & `EditRequest.cshtml.cs`)**:
   - In `CreateRequest.cshtml.cs`: Safely initialized view model with default values for removed fields.
   - In `EditRequest.cshtml.cs`: Preserved any existing remarks/obligations from `CurrentRequest` to prevent unintended data loss when updating previously recorded terminations.
3. **Build Verification**:
   - Verified clean compilation in Debug and Release (`win-x86`) configurations with 0 errors and 0 warnings.

---

## Change 311 — Remove Loans, Guarantor, Override, and Obligations Display from Resignation & Termination Review / Approval Pages

### Problem
Resignation and Termination review, approval, details, and report views displayed legacy financial obligations, outstanding loans, guarantor indicators, and management override status that are no longer part of the streamlined separation process.

### Solution
1. **Resignation Review & Approval Views**:
   - `Pages/Resignation/Details.cshtml`: Removed `Has Loans`, `Loan Guarantor`, and `Override` property rows from the Resignation Details card.
   - `Pages/DepartmentHead/ReviewResignation.cshtml`: Removed the `Financial & Guarantee Obligations` card and details.
   - `Pages/BranchManager/ReviewResignation.cshtml`: Removed the `Financial Obligations Declared` card and the eligibility checklist item for financial obligations.
   - `Pages/BranchManager/ReviewResignations.cshtml`: Removed the `Obligations` table header and column.
   - `Pages/AreaManager/ReviewResignation.cshtml`: Removed the `Financial Obligations Declared` card and the eligibility checklist item.
   - `Pages/AreaManager/ReviewResignations.cshtml`: Removed the `Obligations` table header and column.
   - `Pages/HRManager/ReviewResignation.cshtml`: Removed the `Financial Obligations Declared` card and the eligibility checklist item.
   - `Pages/HRManager/ReviewResignations.cshtml`: Removed the `Obligations` table header and column.
2. **Termination Review & Approval Views (Full UI Parity)**:
   - `Pages/Termination/Details.cshtml`: Removed `Supervisor Remarks` and `Employee Obligations` card.
   - `Pages/Termination/ReviewTermination.cshtml`: Removed `Financial Obligations` card.
   - `Pages/BranchManager/ReviewTermination.cshtml`: Removed `Obligations Check` card.
   - `Pages/AreaManager/ReviewTermination.cshtml`: Removed `Obligations Check` card.
   - `Pages/DepartmentHead/ReviewTermination.cshtml`: Removed `Obligations Check` card.
   - `Pages/Termination/ApprovalQueue.cshtml`: Removed `Obligations` column header and badge cells.
   - `Pages/Termination/TerminationReport.cshtml`: Removed `Supervisor Remarks`, `Special Remarks`, and `Obligations` card.
3. **Build & Release Verification**:
   - Verified that `dotnet build` passes cleanly with 0 errors and 0 warnings in both Debug and Release (`win-x86`) configurations.

---

## Change 312 — Exclude Managerial Department from Termination Clearances & Enforce Mandatory Supporting Documents on Termination Creation

### Problem
1. When a termination was initiated, clearance review tasks were created for all branch departments including the structural "Managerial" department. Since Branch Managers already conduct the overarching Stage 2 review, sending Stage 1 clearance requests to the Managerial department created redundant and unresolvable pending reviews.
2. In the termination creation and editing workflows, uploading supporting documentation was optional, allowing users to submit termination requests without attaching mandatory evidence (e.g., inquiry reports, medical reports, termination letters), without instant client-side verification before submitting.

### Solution
1. **Exclude Managerial Department from Termination Workflow (`HRMS.Application/Services/TerminationService.cs`)**:
   - Added `IsManagerialDept` helper to identify `Managerial` and `Management` department records.
   - Updated `InitializeBranchDepartmentReviewsAsync` to exclude managerial departments when creating `TerminationDepartmentReview` records.
   - In `ValidateAndSubmitAsync`: If a branch has no non-managerial departments needing clearance, the request auto-advances directly to Stage 2 (`DeptHeadsApproved`) for Branch Manager review and notifies the Branch Manager.
   - Updated `GetPendingForDeptHeadAsync`, `GetReviewedByDeptHeadAsync`, and `DeptHeadReviewAsync` to filter out `Managerial` departments.
   - Updated `GetDepartmentHeadUserIdentifiersAsync` to exclude managerial department heads from Stage 1 clearance notifications.
2. **Mandatory Supporting Document Upload & Pre-Submit Validation (`Pages/Termination/CreateRequest.cshtml` & `EditRequest.cshtml`)**:
   - In `CreateRequest.cshtml`: Marked Supporting Documents as mandatory (`<span class="req">*</span>`), bound `Input.DocumentType`, configured `accept=".pdf,.doc,.docx,.jpg,.jpeg,.png"`, and added instant client-side JavaScript validation that intercepts the "Submit for Approval" button to verify file selection before form submission, scrolling smoothly and highlighting the file input on error.
   - In `CreateRequest.cshtml.cs`: Enforced server-side validation in `OnPostSubmitAsync` adding a `ModelState` error if `Input.Documents` is empty or missing.
   - In `EditRequest.cshtml` & `EditRequest.cshtml.cs`: Enforced that at least one existing document or newly uploaded document is present prior to submitting draft requests for approval, with matching pre-submit client validation.
3. **Build & Release Verification**:
   - Verified clean compilation in Debug and Release (`win-x86`) configurations with 0 errors and 0 warnings.

---

## Change 313 — Performance Dashboard: Remove Training Scoring, Rebalance Core Weights (45% Attendance, 30% Leave, 25% Punctuality), & Restrict Leave Evaluation to Annual and Casual Leaves Only

### Problem
1. The Performance Dashboard included a 30% Training score pillar, which is no longer part of the statutory evaluation model.
2. The remaining score needed to be redistributed properly among Attendance, Punctuality, and Leave Discipline.
3. The Leave Discipline score was previously counting all leave categories (such as Medical, Maternity, Overseas, etc.) instead of strictly focusing on statutory Annual and Casual leaves.

### Solution
1. **Remove Training Pillar & Rebalance Weights (`HRMS.UI/Pages/Performance/Index.cshtml.cs` & `Index.cshtml`)**:
   - Removed database queries and calculations for Employee Training, Training Feedback, Intern Feedback, and Probation Feedback.
   - Redistributed the 100% total score across the 3 core pillars:
     - **Attendance**: **45%** (Primary attendance baseline)
     - **Leave Discipline**: **30%** (Annual & Casual leave usage)
     - **Punctuality**: **25%** (On-time clock-ins before 08:00 AM)
   - Updated formula: `(attendanceScore * 0.45) + (leaveScore * 0.30) + (punctualityScore * 0.25)`.
2. **Restrict Leave Discipline to Annual & Casual Leaves Only**:
   - Filtered database queries and in-memory leave lookups in `Index.cshtml.cs` so that only `Annual` and `Casual` leaves (`LeaveType == "Annual" || LeaveType == "Casual"`) are evaluated against the monthly working day baseline.
3. **UI Updates (`HRMS.UI/Pages/Performance/Index.cshtml`)**:
   - Updated header subtitle: `Attendance 45% · Leave (Annual & Casual) 30% · Punctuality 25%`.
   - Removed the `Training (30%)` table column from the Leaderboard.
   - Updated table headers to `Attend. (45%)`, `Punct. (25%)`, and `Leave (30%)`.
   - Updated Employee Details modal to display the 3-pillar breakdown and metric cards (`Attendance Log`, `Punctuality Record`, `Annual & Casual Leaves`).
   - Cleaned modal script functions and bar reset animations.
4. **Build & Release Verification**:
   - Verified clean compilation in Debug and Release (`win-x86`) configurations with 0 errors and 0 warnings.

---

## Change 314 — Performance Dashboard: Fix Punctuality Evaluation and Align with Corporate 08:30 AM Shift Start Threshold

### Problem
1. In the Performance dashboard evaluation, punctuality was using a hardcoded 08:00 AM clock-in threshold (`TimeSpan(8, 0, 0)`).
2. The company shift policy (enforced in `AttendanceService.cs` and across attendance reviews) treats clock-ins up to 08:30 AM as on-time (`Status = "Present"`). Consequently, employees who arrived on time between 08:00 AM and 08:30 AM were erroneously flagged as "Late", producing artificially deflated punctuality scores and inaccurate on-time counts.
3. The upper date query bound was excluding clock-ins logged today that had non-zero timestamps.

### Solution
1. **Align Shift Start Threshold to 08:30 AM (`HRMS.UI/Pages/Performance/Index.cshtml.cs`)**:
   - Changed late arrival clock-in threshold to `new TimeSpan(8, 30, 0)`.
   - Updated attendance date query upper bound to `today.AddDays(1)` to fully include all records from the current day.
   - Refined `onTimeDays` and `lateDays` counting so that `onTimeDays + lateDays == totalAttended` (`presentDays + halfDays`), and `punctualityScore = (onTimeDays / totalAttended) * 100`.
2. **UI & Modal Display Updates (`HRMS.UI/Pages/Performance/Index.cshtml`)**:
   - Updated header subtitle: `Punctuality threshold: on-time before 08:30 AM`.
   - In the Employee Details modal, updated `mPunctDays` script formatting to reliably display `${onTimeDays} On-Time · ${lateDays} Late` (or `0 Days Logged` when no attendance is logged).
3. **Build & Release Verification**:
   - Verified clean compilation in Debug and Release (`win-x86`) configurations with 0 errors and 0 warnings.

---

## Change 315 — Performance Dashboard: Switch Evaluation Window to Current Calendar Month (Month-to-Date)

### Problem
1. The Performance Dashboard previously used a rolling 30-day window (`Today.AddMonths(-1)` to `Today`). On early days of a new month (such as September 2), it was pulling attendance records and leaves from the previous month (August), showing 20+ present days and previous month history instead of the current month's records.
2. In addition, if no records were found in the query, a fallback was querying the entire historical `Attendances` table, contaminating current month performance with all past records.

### Solution
1. **Scope Evaluation Strictly to Current Calendar Month (`HRMS.UI/Pages/Performance/Index.cshtml.cs`)**:
   - Evaluated period is now strictly `startOfMonth = new DateTime(today.Year, today.Month, 1)` to `nextMonth = startOfMonth.AddMonths(1)`.
   - Removed fallback loading of historical records across all months.
   - Calculated `elapsedBusinessDays` in the current month up to today.
   - Set expected working days benchmark dynamically: `companyBenchmarkDays = Math.Max(1, Math.Max(totalCompanyWorkingDates, elapsedBusinessDays))` so that on early days in the month (e.g., day 2 with 2 days attended), employees receive 100% attendance ($2/2$) rather than being penalized against 20 business days.
   - Restricted approved leave evaluations strictly to the current calendar month.
2. **UI & Header Updates (`HRMS.UI/Pages/Performance/Index.cshtml`)**:
   - Header subtitle dynamically displays `Evaluation Period: @Model.CurrentMonthName (Month-to-Date) · Punctuality threshold: on-time before 08:30 AM`.
3. **Build & Release Verification**:
   - Verified clean compilation in Debug and Release (`win-x86`) configurations with 0 errors and 0 warnings.

---

## Change 316 — Performance Dashboard: Establish Rolling Last 30 Days Evaluation Window with Explicit Date Range Display

### Problem
1. When evaluating performance on early calendar dates, a calendar-month snapshot lacks sufficient data (e.g., on day 2, 1 late arrival swings punctuality to 0%).
2. The rolling 30-day evaluation model ensures a stable sample size (~20–22 business days) at all times, but requires an explicit date range display to clearly inform users why recent days from the previous month are included.

### Solution
1. **Rolling 30-Day Window Configuration (`HRMS.UI/Pages/Performance/Index.cshtml.cs`)**:
   - Evaluates the rolling 30-day window: `cutoff = today.AddDays(-30).Date` to `todayEnd = today.AddDays(1)`.
   - Generates explicit display label: `EvaluationPeriodText = $"Last 30 Days ({cutoff:MMM dd, yyyy} – {today:MMM dd, yyyy})"`.
   - Benchmarks expected working days dynamically against business days elapsed in the 30-day window (`elapsedBusinessDays`).
   - Restricted leaves strictly to approved Annual and Casual leaves overlapping the 30-day window (`EndDate >= cutoff && StartDate <= today`).
2. **UI & Header Updates (`HRMS.UI/Pages/Performance/Index.cshtml`)**:
   - Header subtitle displays: `Evaluation Period: @Model.EvaluationPeriodText · Punctuality threshold: on-time before 08:30 AM`.
3. **Build & Release Verification**:
   - Verified clean compilation in Debug and Release (`win-x86`) configurations with 0 errors and 0 warnings.

---

## Change 317 — Performance Dashboard: Exclude Managerial Department from Evaluation, Leaderboard, and Departmental Stats

### Problem
1. Members of the Managerial / Management department were appearing in the Performance leaderboard and departmental comparison charts. Managerial roles are executive positions not evaluated under the standard branch staff performance metrics.

### Solution
1. **Exclude Managerial Employees & Departments (`HRMS.UI/Pages/Performance/Index.cshtml.cs`)**:
   - In `employeesQuery`: Added filtering to exclude employees belonging to `Managerial` or `Management` departments (`e.Department == null || (!e.Department.Name.Equals("Managerial") && !e.Department.Name.Equals("Management") && !e.Department.Name.Contains("Managerial") && !e.Department.Name.Contains("Management"))`).
   - In `DepartmentStats` & `deptGroups`: Filtered out `Managerial` and `Management` departments from departmental bar chart comparisons.
   - Result: Managerial department members no longer appear in the company/branch leaderboard table, branch metric cards, or department filter dropdowns.
2. **Build & Release Verification**:
   - Verified clean compilation in Debug and Release (`win-x86`) configurations with 0 errors and 0 warnings.

---

## Change 318 — Performance Dashboard: Standardize Attendance and Leave Evaluation to Fixed 20 Business Days Benchmark

### Problem
1. Previously, an employee's `DateJoined` within the last 30 days dynamically modified their expected working days denominator (e.g., dividing 1 day by 13 elapsed calendar days = 7.7%, 1 day by 12 days = 8.3%, 1 day by 16 days = 6.2%, and 5 days by 13 days = 38.5%).
2. This created irregular and confusing percentages for employees with the same or similar attendance counts.

### Solution
1. **Uniform Fixed 20 Business Days Baseline (`HRMS.UI/Pages/Performance/Index.cshtml.cs`)**:
   - Standardized the Attendance benchmark to a uniform 20 business days across all employees (`standardBenchmarkWorkingDays = 20`):
     - $1 \text{ day present} \rightarrow 1 / 20 = \mathbf{5.0\%}$
     - $2 \text{ days present} \rightarrow 2 / 20 = \mathbf{10.0\%}$
     - $5 \text{ days present} \rightarrow 5 / 20 = \mathbf{25.0\%}$
     - $10 \text{ days present} \rightarrow 10 / 20 = \mathbf{50.0\%}$
     - $20 \text{ days present} \rightarrow 20 / 20 = \mathbf{100.0\%}$
   - Standardized Leave Discipline evaluation to the same 20 business days target.
   - Punctuality remains evaluated proportionally on the days actually attended ($onTimeDays / totalAttended$).
2. **Build & Release Verification**:
   - Verified clean compilation in Debug and Release (`win-x86`) configurations with 0 errors and 0 warnings.

---

## Change 319 — Employee Creation: Add Non-Negative Constraints and Validations for Previous Experience and Period Fields

### Problem
1. In the Employee Creation form (`/Employees/Create`), the `PreviousExperienceYears` field allowed negative numerical inputs without HTML `min` boundary constraints or client-side/server-side error handling.
2. Similar duration fields (`ProbationPeriodMonths`, `InternPeriodMonths`) lacked explicit non-negative constraints.

### Solution
1. **HTML & Client-Side Validation (`HRMS.UI/Pages/Employees/Create.cshtml`)**:
   - Added `min="0" max="60"` to `NewEmployee.PreviousExperienceYears` and `min="0" max="36"` to `ProbationPeriodMonths` and `InternPeriodMonths`.
   - Added real-time JavaScript validation (`validateExperience`) that automatically clamps negative inputs to 0 on `input` and displays clear error messages (`expError`) if an invalid negative value is submitted.
   - Connected `validateExperience()` into the form submission blocking pipeline.
2. **Server-Side Validation (`HRMS.UI/Pages/Employees/Create.cshtml.cs`)**:
   - In `OnPostAsync()`: Added server validation rejecting negative values and values exceeding 60 years for `PreviousExperienceYears`, as well as negative `ProbationPeriodMonths` and `InternPeriodMonths`.
   - In `OnPostDraftAsync()`: Added safety sanitization to clamp any negative values to `0` / `null` when saving drafts.
3. **Build & Release Verification**:
   - Verified clean compilation in Debug and Release (`win-x86`) configurations with 0 errors and 0 warnings.

---

## Change 320 — Welfare Navigation: Enable Role-Adaptive Visibility Across All User Roles

### Problem
1. In `_Layout.cshtml`, the Welfare sidebar navigation item was restricted strictly to `User.IsInRole("Welfare Manager")`.
2. As a result, all other roles (HR Manager, HR Officer, Area Manager, Branch Manager, Department Head, Employee, Admin) could not see or access their respective Welfare modules, approval queues, or request submission portals from the main navigation menu.

### Solution
1. **Role-Adaptive Welfare Navigation (`HRMS.UI/Pages/Shared/_Layout.cshtml`)**:
   - Made the Welfare menu item visible to all roles with intelligent destination routing:
     - **Welfare Manager & Department Head**: &rarr; `/Welfare/Approvals/DepartmentHeadApproval` (`Welfare Approvals`)
     - **Branch Manager**: &rarr; `/Welfare/Approvals/BranchManagerApproval` (`Welfare Approvals`)
     - **Area Manager**: &rarr; `/Welfare/Approvals/AreaManagerApproval` (`Welfare Approvals`)
     - **HR Manager & HR Officer**: &rarr; `/Welfare/Approvals/HRManagerApproval` (`Welfare Approvals`)
     - **Admin**: &rarr; `/Welfare/Records` (`Welfare Records`)
     - **Standard Employee & Other Users**: &rarr; `/Welfare/RequestList` (`Welfare`)
2. **Access Authorization (`HRMS.UI/Pages/Welfare/RequestList.cshtml.cs`)**:
   - Changed `[Authorize(Roles = "Employee")]` to `[Authorize]` so any authenticated employee profile can view their personal welfare requests.
3. **Build & Release Verification**:
   - Verified clean compilation in Debug and Release (`win-x86`) configurations with 0 errors and 0 warnings.

---

## Change 321 — Welfare Module: Implement End-to-End Workflow with Welfare Department Head Approvals & Dedicated HR Welfare Payments Dashboard

### Problem
1. When the Welfare Department Head approved an employee's welfare request, the system set `CurrentLevel = "Completed"` and closed the request immediately.
2. As a result, approved welfare requests never reached HR for payment disbursement, and HR had no dedicated portal to view approved requests, check employee bank details, record bank transfer transaction references, or finalize payments.

### Solution
1. **Welfare Department Head Approval Stage (`HRMS.UI/Pages/Welfare/Approvals/DepartmentHeadApproval.cshtml.cs`)**:
   - Updated `OnPostAsync()` so approving a request sets:
     - `CurrentLevel = "HRManager"`
     - `CurrentStatus = "PendingPayment"`
     - `Status = "Approved"`
     - `ApprovedAmount = ApprovedAmount ?? RequestedAmount`
   - Logs approval event in `WelfareApprovals` table (`ApproverLevel = "DepartmentHead"`).
   - Dispatches real-time notification to HR Managers and assigned HR Officers (`/Welfare/Payments`): *"Welfare request WF-XXXX for [Employee] has been approved by the Welfare Department Head and is awaiting payment."*
   - Dispatches notification to the employee: *"Your welfare request has been approved and forwarded to HR for payment."*
2. **Dedicated HR Welfare Payments Dashboard (`HRMS.UI/Pages/Welfare/Payments.cshtml`, `Payments.cshtml.cs`)**:
   - Created a comprehensive payment processing hub for `HR Manager`, `HR Officer`, and `Admin`:
     - **Stat Metric Cards**: Pending Disbursements, Pending Amount (LKR), Disbursed This Month (LKR), Total Disbursed Count.
     - **Pending Payments Queue**:
       - Lists all approved requests awaiting disbursement.
       - Highlights **Employee Banking Details** (Bank Name, Account Holder Name, Account Number with 1-click copy).
       - Shows welfare assistance type, requested vs approved amount, welfare head remarks, and supporting proof documents.
       - **Payment Confirmation Form**: Approved Amount, Payment Method (Direct Bank Transfer, SLIPS, Cheque, Cash), Payment Date, Transaction Ref, and HR Notes.
       - **Actions**: `Confirm & Mark as Paid` (updates `CurrentStatus = "PaymentCompleted"`, `Status = "Paid"`) or `Decline Payment`.
     - **Disbursement History Tab**: Paginated archive table of all disbursed welfare payments.
   - On payment confirmation, sends notification to the employee with payment method, date, and reference.
3. **Sidebar Navigation & Routing (`HRMS.UI/Pages/Shared/_Layout.cshtml`, `HRManagerApproval.cshtml.cs`)**:
   - Linked `HR Manager`, `HR Officer`, and `Admin` directly to `/Welfare/Payments` with label **"Welfare Payments"**.
   - Redirected legacy `/Welfare/Approvals/HRManagerApproval` route seamlessly to `/Welfare/Payments`.
4. **Status Tracking 3-Step Lifecycle Visual Progress (`HRMS.UI/Pages/Welfare/StatusTracking.cshtml`)**:
   - Updated status tracking page with a visual 3-stage progress indicator:
     - **Step 1**: Request Submitted
     - **Step 2**: Welfare Department Head Approved
     - **Step 3**: HR Payment Disbursed
   - Enhanced approval history to display both Welfare Head decision remarks and HR payment disbursement details (Method, Ref, Date).
5. **Build & Release Verification**:
   - Verified clean compilation in Debug and Release (`win-x86`) configurations with 0 errors and 0 warnings.

---

## Change 322 — Payroll & Payslips: Category-Based Welfare Integration (Loan Installment Deductions & Grant Additions)

### Problem
1. Welfare payments (such as repayable Housing Loans, Festival Advances, and non-repayable Medical/Education/Funeral grants) were not reflected in employee monthly payslips.
2. When an employee received a welfare loan or advance, the monthly installment was not deducted from salary.
3. When an employee received an assistance grant/allowance, it was not added as a welfare earning to Gross Pay.

### Solution
1. **Welfare Payroll Service (`HRMS.Application/Services/WelfarePayrollHelper.cs`)**:
   - Implemented `WelfarePayrollHelper` to categorize and calculate:
     - **Welfare Loan / Advance Deductions**: Repayable items (`Housing Loan`, `Festival Advance`, `Distress Loan`, etc.) are amortized monthly across their repayment period (e.g. 12, 10, or 6 months). Monthly installments are computed and deducted from gross pay.
     - **Welfare Grant / Allowance Additions**: Non-repayable welfare benefits (`Medical Assistance`, `Education Assistance`, `Funeral Assistance`, `Marriage Grant`, etc.) disbursed in that payroll month are calculated as additions to Gross Pay.
2. **Monthly Payroll Generation Engine (`HRMS.UI/Pages/Payroll/Index.cshtml.cs`)**:
   - In `OnPostProcessPayrollAsync()`, actively loads active welfare requests for branch employees.
   - Dynamically adds welfare allowances to Gross Pay: `grossPay = basicSalary + empBonus + empWelfareAdditions`.
   - Dynamically adds welfare loan installments to Total Deductions: `totalDed = epfEmployee + tax + empWelfareDeductions`.
   - Computes net pay: `netPay = grossPay - totalDed`.
3. **Interactive Web Payslip Portal & Modal (`HRMS.UI/Pages/Payroll/PaySlips.cshtml.cs`, `PaySlips.cshtml`)**:
   - In `PaySlips.cshtml.cs`, recalculates active welfare additions and loan deductions per payslip.
   - In `PaySlips.cshtml`, popup modal displays:
     - Itemized **Welfare Additions & Grants** with green `+` indicators under Gross Pay.
     - Itemized **Welfare Loan Deductions** with red `-` indicators under Deductions.
4. **Official Printable PDF Payslip (`HRMS.UI/Pages/Payroll/PaySlipPdf.cshtml.cs`, `PaySlipPdf.cshtml`)**:
   - Renders a dedicated **Bonuses, Allowances & Welfare Additions** section for disbursed grants.
   - Renders a dedicated **Deductions & Loan Repayments** section for active loan installments.
5. **Request Form Repayment Term Capture (`HRMS.UI/Pages/Welfare/RequestForm.cshtml`, `RequestForm.cshtml.cs`)**:
   - Bound `RepaymentMonths` on form submission and formatted repayment terms into the request description.
6. **Build & Release Verification**:
   - Verified clean compilation in Debug and Release (`win-x86`) configurations with 0 errors and 0 warnings.

---

## Change 323 — Welfare Department Head Duty Account Integration & Admin Provisioning

### Overview & Account Details
1. **Existing Default Welfare Head Duty Account**:
   - **Username**: `head.welfare`
   - **Email**: `head.welfare@kanrich.lk`
   - **Default Password**: `Welfare@123`
   - **Assigned Roles**: `Welfare Manager`, `Department Head`
   - **Department / Location**: Welfare Department (Head Office)
   - **Access Rights**:
     - Direct access to `/Welfare/Approvals/DepartmentHeadApproval` (Review, approve, or reject employee welfare assistance & loan requests).
     - Full access to `/Welfare/Records` (Company-wide welfare request archive).
     - Direct notifications whenever an employee submits a new welfare request.

2. **Admin Duty Accounts Category Integration (`HRMS.UI/Pages/Admin/DutyAccounts/Create.cshtml`, `Create.cshtml.cs`, `Index.cshtml`, `Index.cshtml.cs`)**:
   - Added **Welfare Department Head / Welfare Manager** as a distinct first-class duty account option in the Admin Duty Accounts creation portal (`/Admin/DutyAccounts/Create`).
   - Includes full role validation (`1 Max` corporate limit, duplicate protection, automatic `DUTY-WLF` NIC / `DUTY-WLF-01` EPF provisioning, and automatic dual-role assignment `Welfare Manager` + `Department Head`).
   - Displays the **Welfare Head** badge and account details under Corporate Management in the Duty Accounts overview (`/Admin/DutyAccounts`).

3. **Build & Release Verification**:
   - Verified clean compilation in Debug and Release (`win-x86`) configurations with 0 errors and 0 warnings.

---

## Change 324 — User Accounts Portal: Welfare Manager Account Visibility & Direct Password Management

### Problem
1. In Admin User Accounts Management (`/Admin/Users`), the `Welfare Manager` (`head.welfare`) account was filtered out because `Welfare Manager` was not included in the duty role whitelist.
2. Admins were unable to view the Welfare Manager account or set/reset its password directly from the User Account Management table.

### Solution
1. **User Accounts Model Whitelist (`HRMS.UI/Pages/Admin/Users/Index.cshtml.cs`)**:
   - Added `"Welfare Manager"` to `AllRoles`.
   - Updated the duty account filter to explicitly include `head.welfare`, `welfare*`, and users with role `Welfare Manager`.
2. **User Accounts View & Reset Password UI (`HRMS.UI/Pages/Admin/Users/Index.cshtml`)**:
   - Added `Welfare Manager` role badge styling (`.role-badge-container.welfare`).
   - Enabled standard 1-click **Reset Password** modal dialog for `head.welfare` so Admins can set any desired custom password with confirmation.
3. **Build & Release Verification**:
   - Verified clean compilation in Debug and Release (`win-x86`) configurations with 0 errors and 0 warnings.

---

## Change 325 — Sri Lanka IRD Progressive Income Tax (APIT) Payroll & Payslip Integration

### Problem & Requirement
1. Income tax was previously hardcoded to `0.00` during payroll runs and payslip generation.
2. In Sri Lanka, employers are required by the Inland Revenue Department (IRD) to deduct Advance Personal Income Tax (APIT) from employment income on a progressive monthly basis and remit it to IRD.
3. The user provided the official progressive tax brackets:
   - Up to Rs. 1,800,000 / year (Rs. 150,000 / month): **0%** (Tax-Free Allowance)
   - Rs. 1,800,000 – 2,800,000 / year: **6%** (First Rs. 1,000,000 taxable slab)
   - Rs. 2,800,000 – 3,800,000 / year: **18%** (Next Rs. 1,000,000 taxable slab)
   - Rs. 3,800,000 – 4,800,000 / year: **24%** (Next Rs. 1,000,000 taxable slab)
   - Rs. 4,800,000 – 5,800,000 / year: **30%** (Next Rs. 1,000,000 taxable slab)
   - Above Rs. 5,800,000 / year: **36%** (Remaining balance)

### Solution
1. **Tax Calculation Service (`HRMS.Application/Services/TaxCalculationService.cs`)**:
   - Implemented `CalculateMonthlyApitTax(decimal monthlyGrossPay)`, `CalculateAnnualTax(decimal annualGrossIncome)`, and `GetTaxBreakdown(decimal monthlyGrossPay)`.
   - Uses progressive slab logic: annualizes monthly gross, applies tiered tax rates across brackets, and converts to the exact monthly APIT deduction.
   - Example validations:
     - Gross Rs. 100,000 / mo $\rightarrow$ Tax = Rs. 0.00 (Tax-free)
     - Gross Rs. 150,000 / mo $\rightarrow$ Tax = Rs. 0.00 (Tax-free)
     - Gross Rs. 200,000 / mo $\rightarrow$ Tax = Rs. 3,000.00 / mo
     - Gross Rs. 250,000 / mo $\rightarrow$ Tax = Rs. 8,000.00 / mo
     - Gross Rs. 300,000 / mo $\rightarrow$ Tax = Rs. 17,000.00 / mo
     - Gross Rs. 500,000 / mo $\rightarrow$ Tax = Rs. 71,000.00 / mo
2. **Monthly Payroll Run Processing (`HRMS.UI/Pages/Payroll/Index.cshtml.cs`)**:
   - Updated `OnPostProcessPayrollAsync()` to calculate `tax = TaxCalculationService.CalculateMonthlyApitTax(grossPay)`.
   - Included APIT tax deduction in `totalDed = epfEmployee + tax + empWelfareDeductions` and `netPay = grossPay - totalDed`.
3. **Interactive Payslip Reconciliation & Modal View (`HRMS.UI/Pages/Payroll/PaySlips.cshtml.cs`, `PaySlips.cshtml`)**:
   - Dynamic reconciliation in `OnGetAsync()` applies `TaxCalculationService.CalculateMonthlyApitTax(gross)` to ensure all payslips reflect accurate APIT.
   - Updated modal and JavaScript model: displays `Income Tax (APIT)` (`Rs 0.00 (Tax-Free)` or itemized monthly amount).
4. **Official PDF Payslip (`HRMS.UI/Pages/Payroll/PaySlipPdf.cshtml.cs`, `PaySlipPdf.cshtml`)**:
   - Binds computed APIT tax into `TaxDeduction` and renders **Advance Personal Income Tax (APIT)** under Deductions.
5. **Build & Release Verification**:
   - Verified clean compilation in Debug and Release (`win-x86`) configurations with 0 errors and 0 warnings.

---

## Change 326 — Maternity Leave Salary Adjustment Integration in Monthly Payroll Runs

### Problem & Requirement
- Needed to ensure that when a female employee is on an approved Maternity Leave, her monthly salary calculation in the Payroll Run (`/Payroll/Index`) seamlessly honors the Finance department's `MaternityPayment` configuration (`Full Pay 100%`, `Half Pay 50%`, or `No Pay 0%`).

### Solution
1. **Payroll Run Maternity Sync (`HRMS.UI/Pages/Payroll/Index.cshtml.cs`)**:
   - In `OnPostProcessPayrollAsync()`, actively queries active approved `Maternity` leaves for the current payroll month.
   - If a processed `MaternityPayment` exists, applies `salaryMultiplier = SalaryPercentage / 100m` to calculate the `effectiveBasic` salary.
   - For standard 100% full-pay maternity leaves (or normal working employees), `salaryMultiplier = 1.0` (100% full basic salary).
   - For half-pay (50%) or no-pay (0%) maternity extensions, the effective basic is adjusted accordingly.
   - Statutory EPF (8% employee, 12% employer), ETF (3%), APIT progressive tax, and Net Pay are derived from the resulting effective earnings.
2. **Build & Release Verification**:
   - Verified clean compilation in Debug and Release (`win-x86`) configurations with 0 errors and 0 warnings.

---

## Change 327 — Enforce Tomorrow-Onwards Date Constraint for Training Session Scheduling

### Problem & Requirement
- When scheduling a training session, the date picker and backend permitted scheduling on past dates and the current date (today).
- Requirement: Training sessions must strictly be scheduled for future dates starting from tomorrow onwards.

### Solution
1. **Schedule Page Model (`HRMS.UI/Pages/Training/Schedule.cshtml.cs`)**:
   - Initialized `SessionDate` to `SriLankaTime.Now.Date.AddDays(1)`.
   - Updated `OnPostAsync()` validation to verify `SessionDate.Date >= SriLankaTime.Now.Date.AddDays(1)`. Rejects today and any past dates with an explicit ModelState error: `"Training session must be scheduled for a future date (tomorrow onwards). Today or past dates are not permitted."`
2. **Schedule Razor View & Client Script (`HRMS.UI/Pages/Training/Schedule.cshtml`)**:
   - Added HTML attribute `min="@HRMS.Domain.Common.SriLankaTime.Now.AddDays(1).ToString("yyyy-MM-dd")"`.
   - Added helper hint text and validation span.
   - Added JavaScript dynamic constraint and `change` event listener enforcing tomorrow onwards.
3. **Edit Session Page (`HRMS.UI/Pages/Training/EditSession.cshtml.cs`, `EditSession.cshtml`)**:
   - Added backend validation ensuring any session in `"Scheduled"` status is dated $\ge$ tomorrow.
   - Added dynamic `min` date attribute and JS validation for `"Scheduled"` sessions.
4. **Build & Verification**:
   - Verified clean build (`dotnet build -c Release -r win-x86`) with 0 errors and 0 warnings.

---

## Change 328 — Dynamic Intern Evaluation Duration Based on Employee InternPeriodMonths

### Problem & Requirement
- The Intern Skills Assessment evaluation page (`/Training/EvaluateIntern`) had a hardcoded 6-month loop (`for (int i = 1; i <= 6; i++)`).
- As a result, interns whose internship duration was longer than 6 months (e.g. 9 months, 12 months, etc.) could not be evaluated beyond Month 6.

### Solution
1. **Evaluate Intern Page Model (`HRMS.UI/Pages/Training/EvaluateIntern.cshtml.cs`)**:
   - Added `InternPeriodMonths` property to `EmployeeDetailsDto` (defaulting to 6 if unspecified/null).
   - Updated `OnGetAsync()` SQL query to fetch `e.InternPeriodMonths` from the `Employees` table.
   - Parsed and populated `Intern.InternPeriodMonths` dynamically for the evaluated intern.
2. **Evaluate Intern Razor View (`HRMS.UI/Pages/Training/EvaluateIntern.cshtml`)**:
   - Replaced the hardcoded `@for (int i = 1; i <= 6; i++)` with `@for (int i = 1; i <= Model.Intern.InternPeriodMonths; i++)`.
   - Updated label to explicitly show the intern's duration: `Select Evaluation Month (1 to @Model.Intern.InternPeriodMonths Months)`.
3. **Build & Verification**:
   - Verified clean compilation with `dotnet build -c Release -r win-x86` (0 errors, 0 warnings).

---

## Change 329 — Display Employee Profile Pictures in Intern & Probation Tracking Modules

### Problem & Requirement
- The Intern and Probation tracking hero cards and tracking tables displayed generic initial placeholder circles instead of the employee's uploaded profile picture.
- Requirement: Display the employee's uploaded profile photo (`/uploads/avatars/emp_{id}.jpg`) with seamless fallback to styled initials when no photo is uploaded.

### Solution
1. **Candidate Profile Hero Overview (`HRMS.UI/Pages/Training/ViewProfile.cshtml`)**:
   - Updated `.hero-avatar` CSS with `position: relative; overflow: hidden;` and `img` styling (`width: 100%; height: 100%; object-fit: cover; border-radius: 50%`).
   - Replaced static text initial with an `<img src="/uploads/avatars/emp_@(Model.Profile.Id).jpg" ... />` element with dynamic `onerror` fallback to the initials container.
2. **Intern Tracking Table (`HRMS.UI/Pages/Training/InternTracking.cshtml`)**:
   - Added `.track-avatar` circular avatar styling.
   - Updated table row to render the intern's avatar image alongside full name and EPF number.
3. **Probation Tracking Table (`HRMS.UI/Pages/Training/ProbationTracking.cshtml`)**:
   - Added `.track-avatar` circular avatar styling.
   - Updated table row to render the probationary employee's avatar image alongside full name and EPF number.
4. **Evaluation Forms (`EvaluateIntern.cshtml`, `EvaluateProbation.cshtml`)**:
   - Added matching 48px profile picture avatar in the evaluation form headers.
5. **Build & Verification**:
   - Verified clean compilation (`dotnet build -c Release -r win-x86`) with 0 errors and 0 warnings.

---

## Change 330 — Keep Clean Text Format in Tracking Tables & Limit Profile Picture to Candidate Profile

### Problem & Requirement
- User requested to keep the tracking list tables (`InternTracking.cshtml` & `ProbationTracking.cshtml`) clean and text-only (without circular avatars), and to only display the employee's profile picture inside the candidate's Profile hero card (`ViewProfile.cshtml`).

### Solution
1. **Intern Tracking Table (`HRMS.UI/Pages/Training/InternTracking.cshtml`)**:
   - Removed `.track-avatar` styling and reverted the `Intern Name` cell back to the original clean text-only layout (`<strong>@intern.FullName</strong>` and EPF number).
2. **Probation Tracking Table (`HRMS.UI/Pages/Training/ProbationTracking.cshtml`)**:
   - Removed `.track-avatar` styling and reverted the `Employee` cell back to the original clean text-only layout (`<strong>@item.FullName</strong>` and EPF number).
3. **Candidate Profile (`HRMS.UI/Pages/Training/ViewProfile.cshtml`)**:
   - Maintained the profile picture display inside the hero candidate header card with smooth fallback to initials.
4. **Build & Verification**:
   - Successfully built release with `dotnet build -c Release -r win-x86` (0 errors, 0 warnings).

---

## Change 331 — Direct HR Manager Notification & Review Routing for Managerial Transfers and Resignations

### Problem & Requirement
- When a managerial employee (e.g. Department Head, Branch Manager, Area Manager) submits a Transfer or Resignation request, it was previously subjected to normal employee multi-tier clearance steps (Department Heads in Branch -> Branch Manager -> Target Branch Manager -> Area Manager).
- Requirement: Managerial requests are purely administrative notices. They must **bypass all intermediate branch and department clearances** and route **directly to the HR Manager as a Notification**.
- When the HR Manager reviews the request, it should not require approval/rejection voting; instead, the HR Manager provides remarks and clicks **"Mark as Reviewed"**, which acknowledges the notice and marks the request as reviewed.

### Solution
1. **Domain Enums & Statuses (`TransferRequest.cs`, `ResignationRequest.cs`)**:
   - Added `PendingHRReview` (value 8) and `ManagerReviewed` (value 9) to `TransferStatus` enum.
   - Added `PendingHRReview` (value 9) and `ManagerReviewed` (value 10) to `ResignationStatus` enum and `ResignationStatusEnum`.
2. **Transfer Workflow Service (`TransferRequestService.cs`)**:
   - Added `IsManagerialEmployeeAsync(...)` and `IsManagerialTitle(...)` detection helper checking role claims, designation title, department name, and user roles (`Department Head`, `Branch Manager`, `Area Manager`).
   - Updated `CreateRequestAsync()`: If the requester is managerial, automatically sets `Status = TransferStatus.PendingHRReview`, bypasses all intermediate stage reviews, and sends notifications directly to HR Managers and the requester.
   - Added `HRManagerMarkAsReviewedAsync(id, comments, reviewerEmail)`: Transitions status to `TransferStatus.ManagerReviewed` and notifies the requester.
   - Updated `GetPendingForHRManagerAsync()` to retrieve both standard `AreaManagerApproved` requests and managerial `PendingHRReview` requests.
3. **Resignation Workflow Service (`ResignationService.cs`)**:
   - Added `IsManagerialEmployeeAsync(...)` and `IsManagerialTitle(...)` detection helpers.
   - Updated `ValidateAndSubmitAsync()`: If the requester is managerial, automatically sets `Status = ResignationStatus.PendingHRReview`, skips branch department reviews, and sends notifications directly to HR Managers and the employee.
   - Added `HRManagerMarkAsReviewedAsync(id, comments, reviewerEmail)`: Sets `HRReview = "Reviewed"`, sets `Status = ResignationStatus.ManagerReviewed`, generates acceptance letter date, and notifies the employee.
   - Updated `GetPendingForHRManagerAsync()` to query both `AMApproved` and `PendingHRReview` requests.
4. **UI & Review Pages**:
   - **Transfer Review (`ReviewTransfer.cshtml`, `ReviewTransfer.cshtml.cs`)**: Renders a dedicated Managerial Transfer Notice panel with `Mark as Reviewed` action button (no Approve/Reject buttons).
   - **Resignation Review (`ReviewResignation.cshtml`, `ReviewResignation.cshtml.cs`)**: Renders a dedicated Managerial Resignation Notice panel with `Mark as Reviewed` button (no Approve/Reject buttons).
   - **Details & Timelines (`Transfer/Details.cshtml`, `Resignation/Details.cshtml`)**: Displays streamlined 2-step (Transfer) and 3-step (Resignation) timelines for managerial notices.
5. **Build & Verification**:
   - Successfully compiled solution (`dotnet build -c Release -r win-x86`) with 0 errors and 0 warnings.

---

## Change 332 — Clarify Managerial Transfer/Resignation as "Seen by HR Manager" (Notice Only, External Processing)

### Problem & Requirement
- The user clarified that for managerial transfers and resignations (such as for a Branch Manager or Department Head), HR does not approve/reject or execute relocation/clearance actions inside the system.
- The submission to HR is solely an **administrative notification** to inform HR.
- HR only needs to **"See" (Acknowledge)** the notice. Marking as reviewed confirms HR has seen the request.
- Any actual transfer procedures, branch handovers, or resignation exit clearance processes happen **outside of this system**.

### Solution
1. **Transfer & Resignation Review Panels (`ReviewTransfer.cshtml`, `ReviewResignation.cshtml`)**:
   - Updated the Managerial Notice card and eligibility panel:
     - Clarified that marking as reviewed confirms HR has seen the request.
     - Highlighted that in-system approval is not required, and all subsequent processes occur externally.
     - Changed the primary action button to **"Mark as Reviewed (Seen)"**.
2. **Transfer & Resignation Details & Timelines (`Transfer/Details.cshtml`, `Resignation/Details.cshtml`)**:
   - Added an **Administrative Notice Banner** explicitly clarifying that the request is an informational notice seen by HR, and further proceedings take place outside the system.
   - Updated the timeline step from "HR Manager Review / Completed" to **"Seen by HR Manager"** with status **"Seen & Acknowledged"**.
   - Updated badges for `ManagerReviewed` to display **"Seen by HR Manager (Notice)"**.
3. **Notification Messages (`TransferRequestService.cs`, `ResignationService.cs`)**:
   - Updated notification copy to explicitly state: *"Your request/notice has been seen and acknowledged by the HR Manager. Further transfer / resignation proceedings will take place outside of this system."*
4. **Build & Verification**:
   - Built cleanly with `dotnet build HRMS.UI/HRMS.UI.csproj -c Release -r win-x86 --no-self-contained` (`0 errors, 0 warnings`).

---

## Change 333 — Welfare Department Head Account Seeding & Admin Duty Account Category Integration

### Problem & Requirement
1. The user requested to know whether the system currently has an account created for the **Welfare Department Head**.
2. If not, or to support creating one:
   - Add a distinct `Welfare Department Head` category to the Admin Duty Account creation portal (`/Admin/DutyAccounts/Create` or `/Admin/AccountCreation`).
   - Ensure the newly created or existing Welfare Department Head account has all required access to the Welfare workflow (reviewing welfare requests, viewing documents, approving requests, and forwarding to HR Welfare Payments).

### Investigation Findings
- Checked `HRMS.UI/Program.cs` and Identity database seeders:
  - System had `Admin`, `HR Manager`, `Department Head` (for IT, HR, Finance, Operations), `Branch Manager`, `Area Manager`, and `Employee` accounts.
  - A dedicated seeded Welfare Department Head account (`welfare.head@kanrich.lk`) was missing.
  - In Admin Duty Account creation (`DutyAccounts.cshtml`), the category dropdown was populated from the `Departments` table, but `"Welfare"` or `"Welfare Department Head"` wasn't available as a standard option or tied to the `Department Head` / `Welfare Manager` role.

### Solution
1. **Seeded Welfare Department Head Account (`HRMS.UI/Program.cs`)**:
   - Seeded a default Welfare Department Head user:
     - **Email / Username**: `welfare.head@kanrich.lk`
     - **Password**: `Password123!`
     - **Full Name**: `Welfare Department Head`
     - **Roles Assigned**: `Department Head`, `Welfare Manager`
     - **Claims**: Assigned Department claim `Welfare` and Department Head authorization claims.
2. **Admin Duty Account Category (`HRMS.UI/Pages/Admin/DutyAccounts.cshtml`, `DutyAccounts.cshtml.cs`)**:
   - Added `"Welfare Department Head"` as a dedicated category choice in the Admin Duty Account creation modal.
   - When selected:
     - Automatically provisions the duty account with the `Department Head` and `Welfare Manager` roles.
     - Sets the department association to `"Welfare"`.
3. **Welfare Approvals & Navigation Access (`_Layout.cshtml`, `DepartmentHeadApproval.cshtml`)**:
   - Verified that `_Layout.cshtml` renders the **Welfare Approvals** navigation item (`/Welfare/Approvals/DepartmentHeadApproval`) for users with role `Department Head` or `Welfare Manager`.
   - Verified that the Welfare Head has full access to:
     - View all pending welfare requests across categories (Loans, Advances, Grants, Medical/Education Assistance).
     - Inspect uploaded employee proofs and documents.
     - Approve requests with remarks, automatically advancing them to HR Welfare Payments (`PendingPayment`).
4. **Build & Release Verification**:
   - Successfully compiled solution (`dotnet build HRMS.UI/HRMS.UI.csproj -c Release -r win-x86 --no-self-contained`) with 0 errors and 0 warnings.

---

## Change 334 — Remove Welfare Tab for System Admin

### Problem & Requirement
- The System Admin portal had a "Welfare Payments" navigation item displayed in the sidebar menu.
- Requirement: Remove the Welfare tab from the System Admin navigation sidebar.

### Solution
1. **Sidebar Navigation Update (`HRMS.UI/Pages/Shared/_Layout.cshtml`)**:
   - Removed the `else if (User.IsInRole("Admin"))` Welfare Payments block from `_Layout.cshtml`.
   - Updated the default employee welfare fallback check to `else if (!User.IsInRole("Admin"))` so no welfare tab is rendered for System Admin accounts.
2. **Build & Verification**:
   - Verified clean build (`dotnet build HRMS.UI/HRMS.UI.csproj -c Release -r win-x86 --no-self-contained`) with 0 errors and 0 warnings.

---

## Change 335 — Welfare Manager Portal: Display Approved Requests in Approved Tab & Rename Dossier to English

### Problem & Requirement
1. In the Welfare Manager Portal (`/Welfare/Approvals/DepartmentHeadApproval`), when a welfare request was approved by the Welfare Department Head and forwarded to HR for payment disbursement (`CurrentStatus = "PendingPayment"`), it did not appear in the **Approved** tab because the filter only checked for exact strings `approved` or `disbursed`. Even though payment was pending with HR, the approved request should be visible in the Approved tab.
2. In the Welfare Records & Oversight portal (`/Welfare/Records`), there was an action button labeled `"Dossier"`. The user requested to rename this button to English.

### Solution
1. **Welfare Manager Approvals Model & Logic (`DepartmentHeadApproval.cshtml.cs`, `DepartmentHeadApproval.cshtml`)**:
   - Updated `ApprovedCount` calculation to include all requests where the Welfare Manager has approved the request (`Status == "Approved"`, `CurrentStatus == "PendingPayment"`, `PaymentCompleted`, `Paid`, or `Disbursed`).
   - Attached `data-is-approved="true"` and `data-is-rejected="true"` to table rows.
   - Updated client-side JavaScript tab filtering in `applyFilters()` so selecting the **Approved** tab checks `data-is-approved === 'true'`.
   - Updated modal submit button label to `"Approve & Forward to HR"`.
   - Updated the stat cards to accurately display the updated count and description (`"Awaiting HR Payment / Disbursed"`).
2. **Welfare Records Table & Tab Filter (`Records.cshtml`, `Records.cshtml.cs`)**:
   - Renamed the `"Dossier"` button to **`"History"`** with tooltip `"View Employee History"`.
   - In `EmployeeHistory.cshtml`, updated page header label to `"Employee Welfare History"`.
   - Updated `TotalApprovedCount` and client-side JavaScript filtering on the Approved tab in `Records.cshtml` to include `PendingPayment` requests.
3. **Build & Release Verification**:
   - Successfully compiled solution (`dotnet build HRMS.UI/HRMS.UI.csproj -c Release -r win-x86 --no-self-contained`) with 0 errors and 0 warnings.

---

## Change 336 — Welfare Records Sidebar Active Navigation Highlight Synchronization

### Problem & Requirement
- In the Welfare Manager Portal, navigating to the **Welfare Records** page (`/Welfare/Records`) failed to highlight the "Welfare Records" sidebar item with the green active pill styling, and instead incorrectly highlighted "Welfare Approvals".

### Solution
1. **Active Navigation Key (`HRMS.UI/Pages/Welfare/Records.cshtml`)**:
   - Updated `ViewData["ActiveNav"] = "WelfareRecords"`.
2. **Welfare Approvals & Employee History Pages (`DepartmentHeadApproval.cshtml`, `EmployeeHistory.cshtml`)**:
   - Set `ViewData["ActiveNav"] = "WelfareApprovals"` in `DepartmentHeadApproval.cshtml`.
   - Set `ViewData["ActiveNav"] = "WelfareRecords"` in `EmployeeHistory.cshtml`.
3. **Sidebar Markup Synchronization (`HRMS.UI/Pages/Shared/_Layout.cshtml`)**:
   - Refined the active condition for Welfare Approvals to `@(activeNav == "WelfareApprovals" || (activeNav == "Welfare" && !User.IsInRole("Welfare Manager")) ? "active" : "")`.
   - Ensured the Welfare Records sidebar item cleanly activates on `activeNav == "WelfareRecords"`.
4. **Build & Verification**:
   - Verified clean build (`dotnet build HRMS.UI/HRMS.UI.csproj -c Release -r win-x86 --no-self-contained`) with 0 errors and 0 warnings.

---

## Change 337 — Death Process Document Viewer & Download Endpoint Fix (404 Resolution)

### Problem & Requirement
- When trying to view or download supporting documents / death certificates attached to an employee's death process (e.g., from `HRManager/ReviewDeath`, `BranchManager/ReviewDeath`, `AreaManager/ReviewDeath`), a `404 Not Found` error occurred (`/api/documents/download/{id}`).
- Requirement: Resolve the 404 error and provide reliable viewing and downloading of death process supporting documents.

### Solution
1. **Dedicated Download & Preview Razor Page (`HRMS.UI/Pages/DeathProcess/DownloadDocument.cshtml`, `DownloadDocument.cshtml.cs`)**:
   - Created `DownloadDocumentModel` calling `IDeathService.DownloadDocumentAsync(id)`.
   - Supports both `mode=view` (inline preview in the global modal document viewer) and direct file downloads with proper content-type and filename headers.
2. **Minimal API Fallback Endpoint (`HRMS.UI/Program.cs`)**:
   - Mapped `app.MapGet("/api/documents/download/{id:int}", ...)` to seamlessly resolve any direct `/api/documents/download/{id}` links using `IDeathService.DownloadDocumentAsync`.
3. **Review Page UI Enhancement (`HRManager/ReviewDeath.cshtml`, `BranchManager/ReviewDeath.cshtml`, `AreaManager/ReviewDeath.cshtml`)**:
   - Updated the document attachments section to provide both a **"View"** button (integrating with the modal preview system `.js-doc-preview`) and a **"Download"** button pointing to `/DeathProcess/DownloadDocument?id=@doc.Id`.
4. **Build & Verification**:
   - Verified clean build (`dotnet build HRMS.UI/HRMS.UI.csproj -c Release -r win-x86 --no-self-contained`) with 0 errors and 0 warnings.

---

## Change 338 — Employee Transfer Initiated by HR: Routing to Normal Multi-Stage Approval Process

### Problem & Requirement
- When an HR Manager or HR Officer initiated a transfer for a non-managerial employee (e.g. an Accountant, Cashier, Executive), the system mistakenly treated the request as a "Managerial Transfer Notice" because the initiator's role was "HR Manager".
- This caused the transfer to bypass the normal multi-stage clearance workflow (Department Head $\rightarrow$ Branch Managers $\rightarrow$ Area Manager $\rightarrow$ HR Finalization) and went straight back to HR Manager as a `PendingHRReview` notice ("Mark as Reviewed").
- Requirement: Fix the workflow so that transfers initiated by HR for non-managerial employees enter the normal approval process (starting at Stage 2 Department Head review), while preserving managerial employee transfers and self-submitted employee/manager transfer requests.

### Solution
1. **Managerial Classification Logic (`TransferRequestService.cs`, `ResignationService.cs`)**:
   - In `IsManagerialEmployeeAsync`, updated the requester role check so that administrative initiator roles (`"HR Manager"`, `"HR Officer"`, `"Admin"`) do NOT cause the target employee to be classified as managerial.
   - Evaluates the employee's own position, designation, department, EPF, and linked user/employee record to accurately determine whether the employee is managerial.
   - In `TransferRequestService.CreateTransferRequestAsync`, passed `null` for `requestedByRole` when checking `isManager` so that transfers initiated by HR for regular staff default to `Status = Pending` (Stage 1) and immediately notify the Department Head.
2. **Self-Healing of Existing Mismatched Records (`TransferRequestService.cs`)**:
   - In `GetRequestsForDeptHeadAsync` and `GetRequestsForHRFinalizationAsync`, added checks so that any non-managerial transfer request mistakenly set to `PendingHRReview` is automatically healed back to `Pending` and appears in the appropriate Department Head's pending queue.
3. **Build & Verification**:
   - Verified clean build (`dotnet build HRMS.UI/HRMS.UI.csproj -c Release -r win-x86 --no-self-contained`) with 0 errors and 0 warnings.

---

## Change 339 — Managerial Review UI Modernization and Reviewed Status Fix

### Problem & Requirement
1. In the HR Manager portal, when viewing a managerial transfer or resignation notice, an overly large dark banner box was displayed. The user requested to replace that box with a clean card featuring a green button to mark the request as reviewed.
2. When a managerial request was marked as reviewed, in the reviewed requests history table (`/Separation/Dashboard`), its status/decision incorrectly rendered as "Rejected" because `r.HRManagerReview == "Approved"` was evaluated and defaulted to rejected for `"Reviewed"`.
3. Comments validation required 10+ characters on transfers and 5+ characters on resignations even when clicking "Mark as Reviewed", preventing quick acknowledgement.

### Solution
1. **Clean Card & Green Action Button (`HRManager/ReviewTransfer.cshtml`, `HRManager/ReviewResignation.cshtml`)**:
   - Replaced the dark `eligibility-panel` with a clean white card matching the standard portal aesthetic (`#ffffff`, subtle border, clean typography).
   - Added a prominent green button (`background:#10823c; color:#fff; font-weight:700`) with `<i class="bi bi-check-circle-fill"></i> Mark as Reviewed`.
   - Made acknowledgement remarks optional so HR managers can mark notices as reviewed with a single click.
2. **Review Handler Optimization (`ReviewTransfer.cshtml.cs`, `ReviewResignation.cshtml.cs`)**:
   - Updated `OnPostAsync` in both code-behind files to evaluate `action == "mark_reviewed"` before any required comment length validations, defaulting remark text to `"Seen and acknowledged by HR Manager"` if omitted.
3. **Reviewed History Status & Decision Accuracy (`Separation/Dashboard.cshtml`)**:
   - In both the **Transfers** and **Resignations** tabs under the "Recently Reviewed" tables:
     - Under "Your Decision", checked for `TransferStatus.ManagerReviewed` / `ResignationStatusEnum.ManagerReviewed` or `HRManagerReview == "Reviewed"` / `HRReview == "Reviewed"` and rendered a green `✓ Reviewed` badge (`k-badge-approved`).
     - Fixed "Overall Status" badge and text display to use `@r.StatusBadgeClass` and `@r.StatusDisplay` ("Reviewed by HR Manager (Notice)" with green badge).
4. **Details Pages Consistency (`Resignation/Details.cshtml`, `Transfer/Details.cshtml`)**:
   - Ensured timeline and decision panels for reviewed managerial requests render with green approved status (`review-approved` and `k-badge-approved`).
5. **Build & Verification**:
   - Verified clean build (`dotnet build HRMS.UI/HRMS.UI.csproj -c Release -r win-x86 --no-self-contained`) with 0 errors and 0 warnings.

---

## Change 340 — Removal of Auto-Generated Resignation Acceptance Letter

### Problem & Requirement
- The user requested to remove the auto-generated Resignation Acceptance Letter feature from the resignation module.

### Solution
1. **Service Layer (`ResignationService.cs`)**:
   - In `HRManagerApproveAsync`, set `AcceptanceLetterGenerated = false` and `AcceptanceLetterDate = null`.
   - Updated the employee notification to inform them of the final HR approval and their last working day without mentioning or linking to an acceptance letter.
2. **User Interface Cleanup**:
   - **Separation Tab (`Transfer/Separation.cshtml`)**: Removed the Acceptance Letter button from the resignation requests list.
   - **My Requests (`Resignation/MyRequests.cshtml`)**: Removed the Acceptance Letter action link.
   - **Request Details (`Resignation/Details.cshtml`)**: Removed the Acceptance Letter banner and "View Letter" action button.
   - **HR Review Pages (`HRManager/ReviewResignation.cshtml`, `ReviewResignation.cshtml.cs`, `ReviewResignations.cshtml.cs`)**: Removed the Acceptance Letter section, updated the review button text to "Final Approve Resignation", and updated flash messages.
   - **Route Cleanup (`Resignation/AcceptanceLetter.cshtml.cs`)**: Redirects any direct requests to the resignation details page (`/Resignation/Details/{id}`).
3. **Build & Verification**:
   - Verified clean build (`dotnet build HRMS.UI/HRMS.UI.csproj -c Release -r win-x86 --no-self-contained`) with 0 errors and 0 warnings.

---

## Change 341 — Fix Manual Attendance Log Creation

### Problem & Requirement
- When adding a manual attendance log via `/BiometricLogs/Create`:
  1. The navigation property `BiometricLog.Employee` was non-nullable, causing ASP.NET Core model validation to silently fail `ModelState.IsValid` upon POST.
  2. `OnPostAsync` strictly enforced `emp.BranchId != scopedBranchId` where `scopedBranchId` was resolved only from `currentUser.Branch`, causing manual log additions by **HR Managers**, **HR Officers**, **Area Managers**, or Branch Managers with branch string mismatches to fail with an error.
  3. Action buttons for adding manual attendance logs on `/Attendance/Index` and `/BiometricLogs/Index` were restricted to Branch Managers only instead of including all authorized roles (HR Manager, HR Officer, Area Manager, Branch Manager).

### Solution
1. **Entity Navigation Property (`BiometricLog.cs`)**:
   - Changed `public Employee? Employee { get; set; }` to nullable so that model binding does not fail validation when submitting a new log.
2. **Controller/Page Handler Authorization & Scoping (`BiometricLogs/Create.cshtml.cs`)**:
   - Added explicit `ModelState.Remove("BiometricLog.Employee")` and `ModelState.Remove("CsvFile")`.
   - Updated `OnPostAsync` to respect role-based scoping matching `LoadEmployeesAsync`:
     - **HR Manager & HR Officer**: Full access to record attendance for all active employees.
     - **Area Manager**: Validated against assigned regional branch IDs (`currentUser.ManagedBranches`).
     - **Branch Manager**: Validated against their assigned branch.
   - Handled default Device ID (`"MANUAL-01"`) if not provided.
   - Added user feedback via `TempData["SuccessMessage"]` on successful manual attendance creation.
3. **Form Enhancements (`BiometricLogs/Create.cshtml`)**:
   - Added Punch Type selector (`Auto Detect`, `Check-In (Arrival)`, `Check-Out (Departure)`).
   - Pre-populated default Device ID (`MANUAL-01`) and current timestamp (`SriLankaTime.Now`).
   - Changed validation summary to `asp-validation-summary="All"`.
4. **Navigation & Action Buttons (`Attendance/Index.cshtml`, `BiometricLogs/Index.cshtml`)**:
   - Allowed **HR Manager**, **HR Officer**, **Area Manager**, and **Branch Manager** to see and access the **"Add Attendance Log"** / **"Add New Log"** buttons.
5. **Build & Verification**:
   - Verified clean build (`dotnet build HRMS.UI/HRMS.UI.csproj -c Release -r win-x86 --no-self-contained`) with 0 errors.

---

## Change 342 — Fix Dashboard Greeting Partial Name & Employee Name Display

### Problem & Requirement
1. In dashboard portals for administrative and managerial roles (e.g., HR Officer, HR Manager, Branch Manager), the greeting header partially truncated the user name (e.g. displaying "Good Morning, HR 👋" instead of "Good Morning, HR Officer 👋" or "Good Morning, HR Manager 👋") because of an arbitrary `GreetingName.Split(' ')[0]` call.
2. In employee portals, the dashboard greeting should display the employee's **Name with Initials** (e.g., "K. P. Perera" or "A. B. Silva") instead of their long full name.

### Solution
1. **Dashboard Greeting Name Resolution (`Index.cshtml.cs`)**:
   - In **Employee Portal** (`IsEmployeeView`):
     - Displays `employee.NameWithInitials` (falling back gracefully to `employee.FullName` or `currentUser.FullName` if initials are not recorded).
   - In **Management & Administration Portals** (HR Manager, HR Officer, Branch Manager, Area Manager, Department Head, Admin, Welfare Manager):
     - Displays the complete, untruncated display name (`currentUser.FullName` or `employee.FullName`), eliminating the partial `Split(' ')[0]` truncation.
2. **Build & Verification**:
   - Verified clean build (`dotnet build HRMS.UI/HRMS.UI.csproj -c Release -r win-x86 --no-self-contained`) with 0 errors.

---

## Change 343 — Manual Biometric Log Branch-Scoping and Duty Account Exclusion

### Problem & Requirement
- When adding biometric logs manually (`/BiometricLogs/Create`):
  1. The employee selector listed all employees without branch scoping or filtering when accessed by managers, and duty accounts (e.g. `DUTY-*`, `DUTY-ACC`, duty roles) were visible in the list.
  2. The employee list needed to be strictly restricted to employees of the specific branch only, with all duty accounts completely excluded from selection and CSV import.

### Solution
1. **Duty Account Exclusions (`BiometricLogs/Create.cshtml.cs`)**:
   - Implemented `GetDutyAccountExclusionsAsync()` querying all duty roles (`Admin`, `HR Manager`, `HR Officer`, `Branch Manager`, `Area Manager`, `Department Head`, `Welfare Manager`) and extracting duty employee IDs and duty identifiers.
   - Filtered out all duty accounts across `LoadEmployeesAsync`, `OnPostAsync`, and `OnPostImportCsvAsync` (`!e.NIC.StartsWith("DUTY")`, `e.NIC != "DUTY-ACC"`, `!e.EPFNumber.StartsWith("DUTY")`, `!dutyEmployeeIds.Contains(e.Id)`, `!dutyIdentifiers.Contains(e.Email/EPF)`).
2. **Branch-Scoped Employee Selection (`BiometricLogs/Create.cshtml`, `Create.cshtml.cs`)**:
   - **Branch Managers**: Automatically fixed to their branch with a readonly branch display, listing only non-duty employees from that branch.
   - **HR Managers / HR Officers / Area Managers**: Added a **Select Branch** dropdown (`#branchFilterSelect`). Selecting a branch dynamically filters the **Select Employee** dropdown to only show active non-duty employees assigned to that branch.
3. **Build & Verification**:
   - Verified clean build (`dotnet build HRMS.UI/HRMS.UI.csproj -c Release -r win-x86 --no-self-contained`) with 0 errors.

---

## Change 344 — Display Name with Initials in Manual Biometric Log Employee Selector

### Problem & Requirement
- In the manual biometric log creation form (`/BiometricLogs/Create`), the employee selector listed employees with their full names.
- The requirement was to display employees with their **Name with Initials** (e.g., `A. B. Silva (EPF123)` or `K. P. Perera (EPF456)`) instead of their long full name.

### Solution
1. **Employee Dropdown Display (`BiometricLogs/Create.cshtml`)**:
   - Updated the `<option>` label rendering to prioritize `emp.NameWithInitials` (falling back to `emp.FullName` if initials are not recorded) alongside their EPF number for both Branch Manager and multi-branch views.
2. **Success Flash Message (`BiometricLogs/Create.cshtml.cs`)**:
   - Updated the confirmation message upon saving a manual punch to display the employee's Name with Initials.
3. **Build & Verification**:
   - Verified clean build (`dotnet build HRMS.UI/HRMS.UI.csproj -c Release -r win-x86 --no-self-contained`) with 0 errors.

---

## Change 345 — Restrict Biometric Punch Timestamps to Past and Current Times

### Problem & Requirement
- When recording biometric scan logs manually (`/BiometricLogs/Create`) or uploading files, future timestamps that have not yet arrived were permitted.
- The requirement was to enforce that biometric logs cannot be added for any future date/time.

### Solution
1. **Server-Side Validation (`BiometricLogs/Create.cshtml.cs`)**:
   - In `OnPostAsync()`: Added validation rejecting `BiometricLog.LogDateTime > SriLankaTime.Now.AddMinutes(1)` with an explicit model error: *"Cannot record attendance log for a future date and time that has not arrived yet."*
   - In `OnPostImportCsvAsync()`: Skipped any punch entries in the uploaded file that have future timestamps (`logTime > SriLankaTime.Now.AddMinutes(1)`).
2. **Client-Side Validation & UI Restriction (`BiometricLogs/Create.cshtml`)**:
   - Added `max="@SriLankaTime.Now.ToString("yyyy-MM-ddTHH:mm")"` attribute on the `datetime-local` input to prevent browser date-time pickers from selecting future timestamps.
   - Initialized `dateTimeInput.max = formattedNow` via JavaScript on page load.
3. **Build & Verification**:
   - Verified clean build (`dotnet build HRMS.UI/HRMS.UI.csproj -c Release -r win-x86 --no-self-contained`) with 0 errors.

---

## Change 346 — Mandatory Resume / CV Attachment on Add Candidate Form

### Problem & Requirement
- On the **Add Candidate to CV Bank** page (`/CVBank/Create`), the resume attachment input was previously optional.
- The requirement was to make attaching a resume/CV a mandatory field.

### Solution
1. **Server-Side Validation (`CVBank/Create.cshtml.cs`)**:
   - Updated validation in `OnPostAsync()` to enforce `UploadedCV != null && UploadedCV.Length > 0`. If missing, returns the model error: *"Please attach candidate Resume / CV document (PDF or Word)."*
2. **Client-Side Validation & UI Feedback (`CVBank/Create.cshtml`)**:
   - Added required asterisk `<span class="req">*</span>` and the `required` attribute to the file input.
   - Updated the `validateHrFile()` JavaScript validation routine to reject submissions where no resume file has been attached with real-time visual feedback.
3. **Build & Verification**:
   - Verified clean build (`dotnet build HRMS.UI/HRMS.UI.csproj -c Release -r win-x86 --no-self-contained`) with 0 errors.

---

## Change 347 — Remove Target Job Vacancy Field from Add Candidate Form

### Problem & Requirement
- On the **Add Candidate to CV Bank** page (`/CVBank/Create`), the form contained both `Applied Position` and `Target Job Vacancy (Optional)`.
- The requirement was to remove the `Target Job Vacancy` field from the registration form so candidates are registered directly with their Applied Position.

### Solution
1. **Markup & View (`CVBank/Create.cshtml`)**:
   - Removed the `Target Job Vacancy (Optional)` dropdown input group.
   - Cleaned up vacancy event handlers and updated client-side initialization to populate standard skills evaluation checklist.
2. **Page Model (`CVBank/Create.cshtml.cs`)**:
   - Explicitly assigned `CVInput.JobOpeningId = null;` upon direct candidate registration.
3. **Build & Verification**:
   - Verified clean build (`dotnet build HRMS.UI/HRMS.UI.csproj -c Release -r win-x86 --no-self-contained`) with 0 errors.

---

## Change 348 — Remove HR Officers Branch Allocation Quick Action Tile from Dashboard

### Problem & Requirement
- In the HR Manager dashboard view (`/Index`), the Quick Actions section displayed a tile linking to `/HRManager/AssignBranches` ("HR Officers - Branch allocations").
- The requirement was to remove this quick action tile from the HR Manager portal dashboard.

### Solution
1. **Dashboard Layout (`Index.cshtml`)**:
   - Removed the quick action card linking to `/HRManager/AssignBranches` from the Quick Actions grid.
2. **Build & Verification**:
   - Verified clean build (`dotnet build HRMS.UI/HRMS.UI.csproj -c Release -r win-x86 --no-self-contained`) with 0 errors.

---

## Change 349 — Fix Stacked Layout and Badge Spacing on Official Application Portal

### Problem & Requirement
- On the public application portal header (`/Apply`), the "Official Application Portal" badge and Kanrich logo were crowded together and breaking awkwardly onto two lines on narrow screens.
- The requirement was to fix the header layout so the logo and badge are properly spaced, do not squish, and scale responsively across all devices.

### Solution
1. **Header Layout & Badge Styling (`Apply.cshtml`)**:
   - Added `white-space: nowrap;` and `flex-shrink: 0;` to `.portal-badge` so the badge text never wraps into stacked lines.
   - Enhanced `.public-navbar` with generous horizontal padding, explicit element separation gap (`gap: 20px`), and subtle shadow.
   - Added mobile responsive media queries scaling down the logo and badge proportionally while retaining clean separation.
2. **Build & Verification**:
   - Verified clean build (`dotnet build HRMS.UI/HRMS.UI.csproj -c Release -r win-x86 --no-self-contained`) with 0 errors.

---

## Change 350 — Remove Dashboard Tab for Admin and Set System Settings as Default Page

### Problem & Requirement
- The Admin portal sidebar included a "Dashboard" nav tab linking to `/Index`.
- The requirement was to remove the Dashboard tab from the Admin portal and make the System Settings page (`/Settings`) the default selected landing page for Administrator accounts.

### Solution
1. **Sidebar Navigation (`_Layout.cshtml`)**:
   - Excluded `Admin` from the top "Dashboard" navigation item (`!User.IsInRole("Welfare Manager") && !User.IsInRole("Admin")`).
   - Reordered Admin navigation items to place **System Settings** (`/Settings`) first, followed by **Duty Accounts** and **User Accounts**.
2. **Default Redirects (`Index.cshtml.cs`, `Login.cshtml.cs`, `FirstLoginChangePassword.cshtml.cs`)**:
   - In `Index.cshtml.cs`: Redirected Admin users navigating to `/Index` directly to `/Settings/Index`.
   - In `Login.cshtml.cs` & `FirstLoginChangePassword.cshtml.cs`: Redirected Admin users directly to `/Settings/Index` upon login and password reset.
3. **Build & Verification**:
   - Verified clean build (`dotnet build HRMS.UI/HRMS.UI.csproj -c Release -r win-x86 --no-self-contained`) with 0 errors.

---

## Change 351 — Remove Duty Accounts from Admin Sidebar and Fix User Accounts Active Tab Highlight

### Problem & Requirement
- The Admin sidebar contained a direct link to Duty Accounts, which is already integrated and accessible within the System Settings page (`/Settings`).
- When navigating to the **User Accounts** section (`/Admin/Users`), the sidebar tab did not activate (green highlight) due to an `ActiveNav` key mismatch.
- The requirements were to remove the standalone Duty Accounts sidebar link for Admin, and fix the User Accounts active tab highlight.

### Solution
1. **Sidebar Navigation (`_Layout.cshtml`)**:
   - Removed the `Duty Accounts` `<li>` element from the Admin navigation section.
   - Updated the active check for User Accounts to match `activeNav == "Users" || activeNav == "AdminUsers"`.
2. **Page View (`Admin/Users/Index.cshtml`)**:
   - Set `ViewData["ActiveNav"] = "Users"` for consistency.
3. **Build & Verification**:
   - Verified clean build (`dotnet build HRMS.UI/HRMS.UI.csproj -c Release -r win-x86 --no-self-contained`) with 0 errors.

---

## Change 352 — Enforce Automatic OT Calculation and Remove Redundant Checkbox from Overtime Policy

### Problem & Requirement
- The Overtime Policy page (`/Settings/OvertimePolicy`) featured a toggle checkbox for "Automatically Calculate OT on Monthly Payroll".
- The requirement was to remove this toggle from the UI so that OT is always and consistently calculated automatically from attendance logs during monthly payroll processing.

### Solution
1. **Corporate Policy UI (`Settings/OvertimePolicy/Index.cshtml`)**:
   - Removed the "Automatically Calculate OT on Monthly Payroll" checkbox container.
   - Cleanly positioned the "Save Corporate Policy" button at the end of the form.
   - Removed the "Auto-Calculate" column from the Branch Policies & Overrides table and removed the toggle from the Branch modal.
2. **Backend Handlers (`Settings/OvertimePolicy/Index.cshtml.cs`)**:
   - Updated `OnPostSaveGlobalPolicyAsync` and `OnPostSaveBranchOverrideAsync` to remove the parameter and permanently enforce `AutoCalculateOtOnPayroll = true`.
3. **Build & Verification**:
   - Verified clean build (`dotnet build HRMS.UI/HRMS.UI.csproj -c Release -r win-x86 --no-self-contained`) with 0 errors.

---

## Change 353 — Display Employee Name with Initials on Biometric Logs Pages

### Problem & Requirement
- On the Biometric Raw Logs page (`/BiometricLogs`) and Biometric Logs History page (`/BiometricLogs/History`), employee names in the filter dropdown and table rows were shown as full names.
- The requirement was to display the employee's name with initials instead of their full name across the Biometric Logs pages.

### Solution
1. **Biometric Raw Logs View (`BiometricLogs/Index.cshtml`)**:
   - Updated the employee filter dropdown to display `NameWithInitials` (falling back to `FullName`).
   - Updated the table employee column to display `log.Employee.NameWithInitials`.
2. **Biometric Logs History View (`BiometricLogs/History.cshtml`)**:
   - Updated the employee filter dropdown to display `NameWithInitials`.
   - Updated the history table employee column to display `log.Employee.NameWithInitials`.
3. **Build & Verification**:
   - Verified clean build (`dotnet build HRMS.UI/HRMS.UI.csproj -c Release -r win-x86 --no-self-contained`) with 0 errors.

---

## Change 354 — Provide Granular Skip Reasons and Future Date Feedback on Biometric Import

### Problem & Requirement
- When importing a biometric log file where rows were skipped because punch timestamps are in the future, the system returned a generic error message: *"No valid punch records were imported (X rows skipped). Ensure employee IDs in the file correspond to employees in your branch."*
- The requirement was to provide exact, granular feedback identifying when rows are skipped specifically due to future timestamps.

### Solution
1. **Granular Skip Counters (`BiometricLogs/Create.cshtml.cs`)**:
   - Split generic skipped counter into separate tracked categories: `skippedFuture`, `skippedBranchMismatch`, `skippedInvalidFormat`, and `skippedErrors`.
   - If all skipped rows are due to future timestamps, returned the specific validation message: *"No valid punch records were imported (X rows skipped). Biometric punches cannot be in the future — all timestamps in the file are beyond current date/time."*
   - For mixed skip reasons, compiled detailed parenthetical summaries (e.g. `X future date/time punch(es), Y non-branch or duty employee(s)`).
2. **Build & Verification**:
   - Verified clean build (`dotnet build HRMS.UI/HRMS.UI.csproj -c Release -r win-x86 --no-self-contained`) with 0 errors.

---

## Change 355 — Fix Branch Clearances Badge for Managerial Resignations on Separation Dashboard

### Problem & Requirement
- When a managerial employee (e.g. Area Manager, Branch Manager, Department Head) submits a resignation notification, it is routed directly to HR for administrative review without passing through branch department head reviews.
- On the Separation Management Dashboard (`/Separation/Dashboard`), the "Branch Clearances" column displayed `0/0 Dept Heads` in a pending (yellow) badge for managerial resignations.
- The requirement was to display `Direct HR Review` instead of `0/0 Dept Heads` for managerial resignations.

### Solution
1. **Separation Dashboard View (`Separation/Dashboard.cshtml`)**:
   - Updated the Resignations "Awaiting Your Review" table: when `r.IsManagerialNotification || r.TotalDeptHeadsCount == 0`, display a blue badge `Direct HR Review` (`k-badge-info` with `bi-shield-check` icon) instead of `0/0 Dept Heads`.
2. **Department Head Resignations View (`DepartmentHead/ReviewResignations.cshtml`)**:
   - Added the same check for consistency across pending resignation review tables.
3. **Build & Verification**:
   - Verified clean build (`dotnet build HRMS.UI/HRMS.UI.csproj -c Release -r win-x86 --no-self-contained`) with 0 errors.

---

## Change 356 — Fix Access Denied on Termination Supporting Document Downloads

### Problem & Requirement
- During the termination review and clearance workflow, when Department Heads (and other authorized reviewers) clicked on supporting documents attached to a termination request, the system returned an "Access Denied" (403) error because the `Department Head` role was missing from the `DownloadDocument` endpoint authorization.
- The requirement was to fix access permissions so reviewers can view and download attached supporting documents seamlessly.

### Solution
1. **Document Download Endpoint (`Termination/DownloadDocument.cshtml.cs`)**:
   - Updated authorization from a restricted role list to `[Authorize]`, matching the Resignation and Transfer document download controllers and allowing all authenticated reviewers participating in the workflow (including Department Heads and Admins) to view documents.
2. **Termination Report View (`Termination/TerminationReport.cshtml`, `TerminationReport.cshtml.cs`)**:
   - Added `Department Head` and `Admin` to the authorized roles for consistency across termination artifacts.
3. **Build & Verification**:
   - Verified clean build (`dotnet build HRMS.UI/HRMS.UI.csproj -c Release -r win-x86 --no-self-contained`) with 0 errors.

---

## Change 357 — Remove Welfare Navigation Tab from Branch Manager, Department Head, and Area Manager Portals

### Problem & Requirement
- The sidebar navigation included Welfare / Welfare Approvals menu items for Branch Managers, Department Heads, and Area Managers.
- The requirement was to remove the Welfare tab from the Branch Manager, Department Head, and Area Manager portals.

### Solution
1. **Sidebar Navigation (`Shared/_Layout.cshtml`)**:
   - Removed the Welfare / Welfare Approvals navigation links for `Branch Manager`, `Department Head`, and `Area Manager`.
   - Maintained Welfare navigation specifically for `Welfare Manager` (Approvals & Records), `HR Manager`/`HR Officer` (Payments), and regular `Employee` users (My Requests).
2. **Build & Verification**:
   - Verified clean build (`dotnet build HRMS.UI/HRMS.UI.csproj -c Release -r win-x86 --no-self-contained`) with 0 errors.

---

## Change 358 — Enable Branch Managers to View Branch Employee Payslips in Payroll Tab

### Problem & Requirement
- In the Branch Manager portal, clicking the Payroll navigation tab previously treated Branch Managers as regular single employees, only displaying the Branch Manager's own individual payslip and denying access to other branch employee payslips / PDFs.
- The requirement was to grant Branch Managers the ability to see all employee payslips within their branch.

### Solution
1. **Payslips Page Backend (`Payroll/PaySlips.cshtml.cs`)**:
   - Updated manager role checks to include `Branch Manager` and `Area Manager`.
   - For Branch Managers, scoped `ManagedBranchesList` and the employee payslip query to their assigned branch.
   - Populated completed `PayrollRuns` and all employee payslips for the branch, supporting search, month filtering, and detailed breakdown modal.
2. **Payslips View (`Payroll/PaySlips.cshtml`)**:
   - Scoped corporate-only sub-navigation tabs (Dashboard, Attendance Review, Allowances, EPF & ETF) to `HR Manager` and `HR Officer`, while displaying the active Payslips view and branch header for Branch Managers.
3. **Payslip PDF View (`Payroll/PaySlipPdf.cshtml.cs`)**:
   - Extended authorization to allow Branch Managers to view and print PDF payslips for employees in their branch.
4. **Build & Verification**:
   - Verified clean build (`dotnet build HRMS.UI/HRMS.UI.csproj -c Release -r win-x86 --no-self-contained`) with 0 errors.












































































































