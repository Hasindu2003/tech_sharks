# HRMS System – Change Log

> **Last updated:** 2026-06-07  
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

## What Still Uses Placeholder / Sample Data

The following properties on `Pages/Index.cshtml.cs` (dashboard) are hardcoded and not yet connected to real data:

- `OnLeaveToday` — hardcoded `8`
- `PendingRequests` — hardcoded `12`
- `OpenPositions` — hardcoded `4`
- `PendingApprovals` list — hardcoded sample names
- `UpcomingEvents` list — hardcoded sample events

These need to be replaced with real queries in a future session.

---

## What Was NOT Changed

- Leave, Attendance, Training, Payroll modules — untouched
- Area Manager review pages (`AreaManager/ReviewResignations`, `ReviewTransfers`, etc.) — untouched
- Branch Manager review pages — untouched
- HR Manager review pages — untouched
- `Separation/Dashboard.cshtml.cs` — untouched
- All Admin pages — untouched
- Database schema / Migrations — no new migrations added during this session
