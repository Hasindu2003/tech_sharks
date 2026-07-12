using System;
using System.Collections.Generic;
using System.Data;
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
        public List<LeaveAllocationItem> Allocations { get; set; } = new();

        [TempData]
        public string? SuccessMessage { get; set; }

        [TempData]
        public string? ErrorMessage { get; set; }

        public async Task OnGetAsync()
        {
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

                foreach (var item in Allocations)
                {
                    // 1. Update the allocation in settings table
                    using (var cmd = connection.CreateCommand())
                    {
                        cmd.Transaction = transaction;
                        cmd.CommandText = "UPDATE LeaveAllocationSettings SET DefaultDays = @days WHERE LeaveType = @leaveType";
                        
                        var paramDays = cmd.CreateParameter();
                        paramDays.ParameterName = "@days";
                        paramDays.Value = item.DefaultDays;
                        cmd.Parameters.Add(paramDays);

                        var paramType = cmd.CreateParameter();
                        paramType.ParameterName = "@leaveType";
                        paramType.Value = item.LeaveType;
                        cmd.Parameters.Add(paramType);

                        await cmd.ExecuteNonQueryAsync();
                    }

                    // 2. Sync allocations of existing employees for the current year
                    using (var cmd = connection.CreateCommand())
                    {
                        cmd.Transaction = transaction;
                        cmd.CommandText = @"
                            UPDATE LeaveEntitlements 
                            SET TotalDays = @days, 
                                RemainingDays = @days - UsedDays 
                            WHERE LeaveType = @leaveType AND Year = @year";
                        
                        var paramDays = cmd.CreateParameter();
                        paramDays.ParameterName = "@days";
                        paramDays.Value = item.DefaultDays;
                        cmd.Parameters.Add(paramDays);

                        var paramType = cmd.CreateParameter();
                        paramType.ParameterName = "@leaveType";
                        paramType.Value = item.LeaveType;
                        cmd.Parameters.Add(paramType);

                        var paramYear = cmd.CreateParameter();
                        paramYear.ParameterName = "@year";
                        paramYear.Value = currentYear;
                        cmd.Parameters.Add(paramYear);

                        await cmd.ExecuteNonQueryAsync();
                    }
                }

                await transaction.CommitAsync();
                SuccessMessage = "Leave allocations updated and synchronized successfully for " + currentYear + "!";
                return RedirectToPage();
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
            Allocations.Clear();
            var connection = _context.Database.GetDbConnection();
            bool openedLocally = false;
            if (connection.State != ConnectionState.Open)
            {
                await connection.OpenAsync();
                openedLocally = true;
            }

            try
            {
                using (var cmd = connection.CreateCommand())
                {
                    cmd.CommandText = "SELECT LeaveType, DefaultDays FROM LeaveAllocationSettings ORDER BY Id";
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            Allocations.Add(new LeaveAllocationItem
                            {
                                LeaveType = reader.GetString(0),
                                DefaultDays = reader.GetInt32(1)
                            });
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
                int count = 0;
                using (var cmd = connection.CreateCommand())
                {
                    cmd.CommandText = "SELECT COUNT(*) FROM LeaveAllocationSettings";
                    count = Convert.ToInt32(await cmd.ExecuteScalarAsync());
                }

                if (count == 0)
                {
                    var defaults = new Dictionary<string, int>
                    {
                        { "Annual", 14 },
                        { "Casual", 7 },
                        { "Medical", 14 },
                        { "Maternity", 84 },
                        { "Overseas", 30 },
                        { "Exam", 7 },
                        { "Bereavement", 5 },
                        { "Other", 0 }
                    };

                    foreach (var pair in defaults)
                    {
                        using (var cmd = connection.CreateCommand())
                        {
                            cmd.CommandText = "INSERT INTO LeaveAllocationSettings (LeaveType, DefaultDays) VALUES (@leaveType, @defaultDays)";
                            
                            var paramType = cmd.CreateParameter();
                            paramType.ParameterName = "@leaveType";
                            paramType.Value = pair.Key;
                            cmd.Parameters.Add(paramType);

                            var paramDays = cmd.CreateParameter();
                            paramDays.ParameterName = "@defaultDays";
                            paramDays.Value = pair.Value;
                            cmd.Parameters.Add(paramDays);

                            await cmd.ExecuteNonQueryAsync();
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
    }
}
