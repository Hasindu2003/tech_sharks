using HRMS.Domain.Entities.Welfare;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace HRMS.Application.Services
{
    public class WelfarePayrollItem
    {
        public int RequestId { get; set; }
        public string WelfareTypeName { get; set; } = "";
        public string Category { get; set; } = "";
        public bool IsLoan { get; set; }
        public decimal Amount { get; set; }
        public decimal TotalLoanAmount { get; set; }
        public int CurrentInstallment { get; set; }
        public int TotalInstallments { get; set; }
        public string Description { get; set; } = "";
    }

    public static class WelfarePayrollHelper
    {
        public static bool IsLoanOrAdvance(WelfareType? type, string? remark = null)
        {
            if (type == null) return false;
            var name = type.TypeName ?? "";
            var cat = type.Category ?? "";
            return name.Contains("Loan", StringComparison.OrdinalIgnoreCase)
                || name.Contains("Advance", StringComparison.OrdinalIgnoreCase)
                || cat.Equals("Housing", StringComparison.OrdinalIgnoreCase)
                || cat.Equals("Financial", StringComparison.OrdinalIgnoreCase)
                || cat.Contains("Loan", StringComparison.OrdinalIgnoreCase);
        }

        public static int GetRepaymentMonths(WelfareRequest req)
        {
            if (!string.IsNullOrEmpty(req.Remark))
            {
                var match = Regex.Match(req.Remark, @"(?:repayment|period|term)[\s:]*(\d+)", RegexOptions.IgnoreCase);
                if (match.Success && int.TryParse(match.Groups[1].Value, out var m) && m > 0)
                {
                    return m;
                }
            }

            var typeName = req.WelfareType?.TypeName ?? "";
            if (typeName.Contains("Housing", StringComparison.OrdinalIgnoreCase)) return 12;
            if (typeName.Contains("Festival", StringComparison.OrdinalIgnoreCase)) return 10;
            return 6;
        }

        public static List<WelfarePayrollItem> GetWelfareAdditions(IEnumerable<WelfareRequest> requests, int employeeId, int month, int year)
        {
            var additions = new List<WelfarePayrollItem>();
            foreach (var req in requests)
            {
                if (req.EmployeeId != employeeId) continue;
                if (req.Status != "Paid" && req.CurrentStatus != "PaymentCompleted" && req.Status != "Approved") continue;

                if (IsLoanOrAdvance(req.WelfareType, req.Remark)) continue;

                var disburseDate = req.RequestDate != default ? req.RequestDate : req.CreatedAt;
                if (disburseDate.Month == month && disburseDate.Year == year)
                {
                    var amt = req.ApprovedAmount ?? req.RequestedAmount;
                    if (amt > 0)
                    {
                        additions.Add(new WelfarePayrollItem
                        {
                            RequestId = req.RequestId,
                            WelfareTypeName = req.WelfareType?.TypeName ?? "Welfare Allowance",
                            Category = req.WelfareType?.Category ?? "Allowance",
                            IsLoan = false,
                            Amount = amt,
                            Description = $"Welfare Grant — {req.WelfareType?.TypeName ?? "Assistance"}"
                        });
                    }
                }
            }
            return additions;
        }

        public static List<WelfarePayrollItem> GetWelfareDeductions(IEnumerable<WelfareRequest> requests, int employeeId, int month, int year)
        {
            var deductions = new List<WelfarePayrollItem>();
            foreach (var req in requests)
            {
                if (req.EmployeeId != employeeId) continue;
                if (req.Status != "Paid" && req.CurrentStatus != "PaymentCompleted" && req.Status != "Approved") continue;

                if (!IsLoanOrAdvance(req.WelfareType, req.Remark)) continue;

                var startDate = req.RequestDate != default ? req.RequestDate : req.CreatedAt;
                var totalMonths = GetRepaymentMonths(req);
                var totalAmount = req.ApprovedAmount ?? req.RequestedAmount;
                if (totalAmount <= 0 || totalMonths <= 0) continue;

                var monthlyInstallment = Math.Round(totalAmount / totalMonths, 2);

                var monthsElapsed = ((year - startDate.Year) * 12) + (month - startDate.Month);
                if (monthsElapsed >= 0 && monthsElapsed < totalMonths)
                {
                    int installmentNum = monthsElapsed + 1;
                    deductions.Add(new WelfarePayrollItem
                    {
                        RequestId = req.RequestId,
                        WelfareTypeName = req.WelfareType?.TypeName ?? "Welfare Loan",
                        Category = req.WelfareType?.Category ?? "Loan",
                        IsLoan = true,
                        Amount = monthlyInstallment,
                        TotalLoanAmount = totalAmount,
                        CurrentInstallment = installmentNum,
                        TotalInstallments = totalMonths,
                        Description = $"Welfare Loan Installment — {req.WelfareType?.TypeName ?? "Loan"} ({installmentNum}/{totalMonths})"
                    });
                }
            }
            return deductions;
        }

        public static string GetUrgency(string? remark)
        {
            if (string.IsNullOrEmpty(remark)) return "Normal";
            var match = Regex.Match(remark, @"\[Urgency:\s*(High|Medium|Normal)\]", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                var val = match.Groups[1].Value;
                if (val.Equals("High", StringComparison.OrdinalIgnoreCase)) return "High";
                if (val.Equals("Medium", StringComparison.OrdinalIgnoreCase)) return "Medium";
                return "Normal";
            }

            if (remark.Contains("emergency", StringComparison.OrdinalIgnoreCase) ||
                remark.Contains("critical", StringComparison.OrdinalIgnoreCase) ||
                remark.Contains("urgent", StringComparison.OrdinalIgnoreCase) ||
                remark.Contains("death", StringComparison.OrdinalIgnoreCase) ||
                remark.Contains("hospital", StringComparison.OrdinalIgnoreCase) ||
                remark.Contains("surgery", StringComparison.OrdinalIgnoreCase) ||
                remark.Contains("accident", StringComparison.OrdinalIgnoreCase))
            {
                return "High";
            }

            if (remark.Contains("medium", StringComparison.OrdinalIgnoreCase) ||
                remark.Contains("moderate", StringComparison.OrdinalIgnoreCase) ||
                remark.Contains("medical", StringComparison.OrdinalIgnoreCase))
            {
                return "Medium";
            }

            return "Normal";
        }

        public static int GetUrgencyScore(string? remark)
        {
            var u = GetUrgency(remark);
            return u switch
            {
                "High" => 1,
                "Medium" => 2,
                _ => 3
            };
        }

        public static string CleanRemark(string? remark)
        {
            if (string.IsNullOrEmpty(remark)) return "";
            var cleaned = Regex.Replace(remark, @"\[Urgency:\s*(High|Medium|Normal)\]", "", RegexOptions.IgnoreCase);
            return cleaned.Trim();
        }
    }
}

