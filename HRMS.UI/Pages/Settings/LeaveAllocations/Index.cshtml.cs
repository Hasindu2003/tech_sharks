using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using HRMS.Infrastructure.Persistence;

namespace HRMS.UI.Pages.Settings.LeaveAllocations
{
    [Authorize(Roles = "Admin")]
    public class IndexModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public IndexModel(ApplicationDbContext context)
        {
            _context = context;
        }

        public class LeaveAllocationItem
        {
            public string LeaveType { get; set; } = null!;
            public int DefaultDays { get; set; }
        }

        [BindProperty]
        public List<LeaveAllocationItem> PermanentAllocations { get; set; } = new();

        [BindProperty]
        public List<LeaveAllocationItem> ProbationaryAllocations { get; set; } = new();

        [BindProperty]
        public List<LeaveAllocationItem> InternAllocations { get; set; } = new();

        [BindProperty]
        public string ActiveTab { get; set; } = "Permanent";

        [TempData]
        public string? SuccessMessage { get; set; }

        [TempData]
        public string? ErrorMessage { get; set; }

        public async Task OnGetAsync(string? tab)
        {
            if (!string.IsNullOrWhiteSpace(tab))
            {
                ActiveTab = tab;
            }

            await EnsureAllocationsSeededAsync();
            await LoadAllocationsAsync();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                await LoadAllocationsAsync();
                return Page();
            }

            var connection = _context.Database.GetDbConnection();
            bool openedLocally = false;
            if (connection.State != ConnectionState.Open)
            {
                await connection.OpenAsync();
                openedLocally = true;
            }

            var transaction = await connection.BeginTransactionAsync();
            try
            {
                int currentYear = DateTime.Now.Year;

                var categories = new[]
                {
                    (Type: "Permanent", Items: PermanentAllocations),
                    (Type: "Probationary", Items: ProbationaryAllocations),
                    (Type: "Intern", Items: InternAllocations)
                };

                foreach (var cat in categories)
                {
                    foreach (var item in cat.Items)
                    {
                        if (string.IsNullOrWhiteSpace(item.LeaveType)) continue;

                        // 1. Clean update or insert setting
                        using (var updateCmd = connection.CreateCommand())
                        {
                            updateCmd.Transaction = transaction;
                            updateCmd.CommandText = @"
                                UPDATE LeaveAllocationSettings 
                                SET DefaultDays = @days 
                                WHERE EmployeeType = @empType AND LeaveType = @leaveType;";
                            
                            var pEmp = updateCmd.CreateParameter();
                            pEmp.ParameterName = "@empType";
                            pEmp.Value = cat.Type;
                            updateCmd.Parameters.Add(pEmp);

                            var pType = updateCmd.CreateParameter();
                            pType.ParameterName = "@leaveType";
                            pType.Value = item.LeaveType;
                            updateCmd.Parameters.Add(pType);

                            var pDays = updateCmd.CreateParameter();
                            pDays.ParameterName = "@days";
                            pDays.Value = item.DefaultDays;
                            updateCmd.Parameters.Add(pDays);

                            int rowsAffected = await updateCmd.ExecuteNonQueryAsync();

                            if (rowsAffected == 0)
                            {
                                using (var insertCmd = connection.CreateCommand())
                                {
                                    insertCmd.Transaction = transaction;
                                    insertCmd.CommandText = @"
                                        INSERT INTO LeaveAllocationSettings (EmployeeType, LeaveType, DefaultDays)
                                        VALUES (@empType, @leaveType, @days);";
                                    
                                    var ipEmp = insertCmd.CreateParameter();
                                    ipEmp.ParameterName = "@empType";
                                    ipEmp.Value = cat.Type;
                                    insertCmd.Parameters.Add(ipEmp);

                                    var ipType = insertCmd.CreateParameter();
                                    ipType.ParameterName = "@leaveType";
                                    ipType.Value = item.LeaveType;
                                    insertCmd.Parameters.Add(ipType);

                                    var ipDays = insertCmd.CreateParameter();
                                    ipDays.ParameterName = "@days";
                                    ipDays.Value = item.DefaultDays;
                                    insertCmd.Parameters.Add(ipDays);

                                    await insertCmd.ExecuteNonQueryAsync();
                                }
                            }
                        }

                        // 2. Synchronize existing LeaveEntitlements for matching active employees
                        using (var syncCmd = connection.CreateCommand())
                        {
                            syncCmd.Transaction = transaction;
                            
                            string empTypeCondition = cat.Type switch
                            {
                                "Permanent" => "(e.EmployeeType = 'Permanent' OR e.EmployeeType IS NULL OR e.EmployeeType = '')",
                                "Probationary" => "(e.EmployeeType = 'Probationary' OR e.EmployeeType = 'Probation')",
                                "Intern" => "e.EmployeeType = 'Intern'",
                                _ => "1=1"
                            };

                            syncCmd.CommandText = $@"
                                UPDATE LeaveEntitlements le
                                JOIN Employees e ON le.EmployeeId = e.Id
                                SET le.TotalDays = @days, 
                                    le.RemainingDays = GREATEST(0, @days - le.UsedDays) 
                                WHERE le.LeaveType = @leaveType 
                                  AND le.Year = @year
                                  AND {empTypeCondition};";
                            
                            var pDays = syncCmd.CreateParameter();
                            pDays.ParameterName = "@days";
                            pDays.Value = item.DefaultDays;
                            syncCmd.Parameters.Add(pDays);

                            var pType = syncCmd.CreateParameter();
                            pType.ParameterName = "@leaveType";
                            pType.Value = item.LeaveType;
                            syncCmd.Parameters.Add(pType);

                            var pYear = syncCmd.CreateParameter();
                            pYear.ParameterName = "@year";
                            pYear.Value = currentYear;
                            syncCmd.Parameters.Add(pYear);

                            await syncCmd.ExecuteNonQueryAsync();
                        }
                    }
                }

                await transaction.CommitAsync();
                SuccessMessage = $"Leave allocations updated and synchronized successfully for Permanent, Probationary, and Intern employees ({currentYear})!";
                return RedirectToPage(new { tab = ActiveTab });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                ErrorMessage = "Failed to update allocations: " + ex.Message;
                await LoadAllocationsAsync();
                return Page();
            }
            finally
            {
                if (openedLocally)
                {
                    await connection.CloseAsync();
                }
            }
        }

        private async Task LoadAllocationsAsync()
        {
            PermanentAllocations.Clear();
            ProbationaryAllocations.Clear();
            InternAllocations.Clear();

            var connection = _context.Database.GetDbConnection();
            bool openedLocally = false;
            if (connection.State != ConnectionState.Open)
            {
                await connection.OpenAsync();
                openedLocally = true;
            }

            try
            {
                var settingsDict = new Dictionary<string, Dictionary<string, int>>(StringComparer.OrdinalIgnoreCase)
                {
                    ["Permanent"] = new(StringComparer.OrdinalIgnoreCase),
                    ["Probationary"] = new(StringComparer.OrdinalIgnoreCase),
                    ["Intern"] = new(StringComparer.OrdinalIgnoreCase)
                };

                using (var cmd = connection.CreateCommand())
                {
                    cmd.CommandText = "SELECT EmployeeType, LeaveType, DefaultDays FROM LeaveAllocationSettings ORDER BY Id ASC";
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            var empType = reader.IsDBNull(0) ? "Permanent" : reader.GetString(0);
                            var leaveType = reader.GetString(1);
                            var defaultDays = reader.GetInt32(2);

                            string normalizedCat = "Permanent";
                            if (empType.Equals("Intern", StringComparison.OrdinalIgnoreCase))
                                normalizedCat = "Intern";
                            else if (empType.StartsWith("Probation", StringComparison.OrdinalIgnoreCase))
                                normalizedCat = "Probationary";

                            settingsDict[normalizedCat][leaveType] = defaultDays;
                        }
                    }
                }

                var standardLeaveTypes = new[] { "Annual", "Casual", "Medical", "Maternity", "Overseas", "Exam", "Bereavement", "Other" };

                foreach (var lType in standardLeaveTypes)
                {
                    PermanentAllocations.Add(new LeaveAllocationItem
                    {
                        LeaveType = lType,
                        DefaultDays = settingsDict["Permanent"].TryGetValue(lType, out var d1) ? d1 : GetDefaultSeedDays("Permanent", lType)
                    });

                    ProbationaryAllocations.Add(new LeaveAllocationItem
                    {
                        LeaveType = lType,
                        DefaultDays = settingsDict["Probationary"].TryGetValue(lType, out var d2) ? d2 : GetDefaultSeedDays("Probationary", lType)
                    });

                    InternAllocations.Add(new LeaveAllocationItem
                    {
                        LeaveType = lType,
                        DefaultDays = settingsDict["Intern"].TryGetValue(lType, out var d3) ? d3 : GetDefaultSeedDays("Intern", lType)
                    });
                }
            }
            finally
            {
                if (openedLocally)
                {
                    await connection.CloseAsync();
                }
            }
        }

        private async Task EnsureAllocationsSeededAsync()
        {
            var connection = _context.Database.GetDbConnection();
            bool openedLocally = false;
            if (connection.State != ConnectionState.Open)
            {
                await connection.OpenAsync();
                openedLocally = true;
            }

            try
            {
                // 1. Ensure table exists
                using (var cmd = connection.CreateCommand())
                {
                    cmd.CommandText = @"
                        CREATE TABLE IF NOT EXISTS `LeaveAllocationSettings` (
                            `Id` int AUTO_INCREMENT PRIMARY KEY,
                            `EmployeeType` varchar(50) NOT NULL DEFAULT 'Permanent',
                            `LeaveType` varchar(50) NOT NULL,
                            `DefaultDays` int NOT NULL
                        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;";
                    try { await cmd.ExecuteNonQueryAsync(); } catch { }
                }

                // 2. Clean up any duplicate records (keep the highest Id which is latest update)
                using (var cmd = connection.CreateCommand())
                {
                    cmd.CommandText = @"
                        DELETE t1 FROM LeaveAllocationSettings t1
                        INNER JOIN LeaveAllocationSettings t2 
                        WHERE t1.Id < t2.Id 
                          AND t1.EmployeeType = t2.EmployeeType 
                          AND t1.LeaveType = t2.LeaveType;";
                    try { await cmd.ExecuteNonQueryAsync(); } catch { }
                }

                // 3. Seed missing standard combinations
                var types = new[] { "Permanent", "Probationary", "Intern" };
                var standardLeaveTypes = new[] { "Annual", "Casual", "Medical", "Maternity", "Overseas", "Exam", "Bereavement", "Other" };

                foreach (var empType in types)
                {
                    foreach (var lType in standardLeaveTypes)
                    {
                        int count = 0;
                        using (var cmd = connection.CreateCommand())
                        {
                            cmd.CommandText = "SELECT COUNT(*) FROM LeaveAllocationSettings WHERE EmployeeType = @empType AND LeaveType = @leaveType";
                            
                            var pEmp = cmd.CreateParameter();
                            pEmp.ParameterName = "@empType";
                            pEmp.Value = empType;
                            cmd.Parameters.Add(pEmp);

                            var pType = cmd.CreateParameter();
                            pType.ParameterName = "@leaveType";
                            pType.Value = lType;
                            cmd.Parameters.Add(pType);

                            count = Convert.ToInt32(await cmd.ExecuteScalarAsync());
                        }

                        if (count == 0)
                        {
                            int defaultDays = GetDefaultSeedDays(empType, lType);

                            using (var cmd = connection.CreateCommand())
                            {
                                cmd.CommandText = "INSERT INTO LeaveAllocationSettings (EmployeeType, LeaveType, DefaultDays) VALUES (@empType, @leaveType, @defaultDays)";
                                
                                var pEmp = cmd.CreateParameter();
                                pEmp.ParameterName = "@empType";
                                pEmp.Value = empType;
                                cmd.Parameters.Add(pEmp);

                                var pType = cmd.CreateParameter();
                                pType.ParameterName = "@leaveType";
                                pType.Value = lType;
                                cmd.Parameters.Add(pType);

                                var pDays = cmd.CreateParameter();
                                pDays.ParameterName = "@defaultDays";
                                pDays.Value = defaultDays;
                                cmd.Parameters.Add(pDays);

                                await cmd.ExecuteNonQueryAsync();
                            }
                        }
                    }
                }
            }
            finally
            {
                if (openedLocally)
                {
                    await connection.CloseAsync();
                }
            }
        }

        private static int GetDefaultSeedDays(string empType, string lType)
        {
            return (empType, lType) switch
            {
                ("Intern", "Annual")      => 0,
                ("Intern", "Casual")      => 3,
                ("Intern", "Medical")     => 5,
                ("Intern", "Maternity")   => 0,
                ("Intern", "Overseas")    => 0,
                ("Intern", "Exam")        => 5,
                ("Intern", "Bereavement") => 3,
                ("Intern", "Other")       => 0,

                ("Probationary", "Annual")      => 0,
                ("Probationary", "Casual")      => 7,
                ("Probationary", "Medical")     => 7,
                ("Probationary", "Maternity")   => 84,
                ("Probationary", "Overseas")    => 0,
                ("Probationary", "Exam")        => 3,
                ("Probationary", "Bereavement") => 3,
                ("Probationary", "Other")       => 0,

                // Permanent
                (_, "Annual")      => 14,
                (_, "Casual")      => 7,
                (_, "Medical")     => 14,
                (_, "Maternity")   => 84,
                (_, "Overseas")    => 30,
                (_, "Exam")        => 7,
                (_, "Bereavement") => 5,
                (_, "Other")       => 0,
                _                  => 0
            };
        }
    }
}
