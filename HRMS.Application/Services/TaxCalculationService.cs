using System;
using System.Collections.Generic;

namespace HRMS.Application.Services
{
    public class TaxSlabBreakdown
    {
        public string BracketName { get; set; } = string.Empty;
        public decimal ApplicableRate { get; set; }
        public decimal TaxableAmountInSlab { get; set; }
        public decimal AnnualTaxAmount { get; set; }
        public decimal MonthlyTaxAmount { get; set; }
    }

    /// <summary>
    /// Sri Lanka Inland Revenue Department (IRD) Progressive Advance Personal Income Tax (APIT) Calculator.
    /// 
    /// Tax Brackets (Annual Employment Income):
    /// - Up to Rs. 1,800,000      : 0% (Tax-Free Allowance / Rs. 150,000 per month)
    /// - Rs. 1,800,000 - 2,800,000: 6%  (Next Rs. 1,000,000 / year)
    /// - Rs. 2,800,000 - 3,800,000: 18% (Next Rs. 1,000,000 / year)
    /// - Rs. 3,800,000 - 4,800,000: 24% (Next Rs. 1,000,000 / year)
    /// - Rs. 4,800,000 - 5,800,000: 30% (Next Rs. 1,000,000 / year)
    /// - Above Rs. 5,800,000      : 36% (Excess over Rs. 5,800,000)
    /// </summary>
    public static class TaxCalculationService
    {
        public const decimal TaxFreeThresholdAnnual = 1800000m;
        public const decimal TaxFreeThresholdMonthly = 150000m;

        /// <summary>
        /// Calculates monthly APIT tax deduction based on monthly gross income.
        /// </summary>
        public static decimal CalculateMonthlyApitTax(decimal monthlyGrossPay)
        {
            if (monthlyGrossPay <= TaxFreeThresholdMonthly)
                return 0m;

            decimal annualIncome = monthlyGrossPay * 12m;
            decimal annualTax = CalculateAnnualTax(annualIncome);

            return Math.Round(annualTax / 12m, 2, MidpointRounding.AwayFromZero);
        }

        /// <summary>
        /// Calculates annual progressive APIT income tax based on total annual employment income.
        /// </summary>
        public static decimal CalculateAnnualTax(decimal annualGrossIncome)
        {
            if (annualGrossIncome <= TaxFreeThresholdAnnual)
                return 0m;

            decimal taxable = annualGrossIncome - TaxFreeThresholdAnnual;
            decimal totalAnnualTax = 0m;

            // Slab 1: 1,800,000 - 2,800,000 (First 1,000,000 @ 6%)
            decimal slab1 = Math.Min(taxable, 1000000m);
            totalAnnualTax += slab1 * 0.06m;
            taxable -= slab1;

            if (taxable <= 0) return totalAnnualTax;

            // Slab 2: 2,800,000 - 3,800,000 (Next 1,000,000 @ 18%)
            decimal slab2 = Math.Min(taxable, 1000000m);
            totalAnnualTax += slab2 * 0.18m;
            taxable -= slab2;

            if (taxable <= 0) return totalAnnualTax;

            // Slab 3: 3,800,000 - 4,800,000 (Next 1,000,000 @ 24%)
            decimal slab3 = Math.Min(taxable, 1000000m);
            totalAnnualTax += slab3 * 0.24m;
            taxable -= slab3;

            if (taxable <= 0) return totalAnnualTax;

            // Slab 4: 4,800,000 - 5,800,000 (Next 1,000,000 @ 30%)
            decimal slab4 = Math.Min(taxable, 1000000m);
            totalAnnualTax += slab4 * 0.30m;
            taxable -= slab4;

            if (taxable <= 0) return totalAnnualTax;

            // Slab 5: Above 5,800,000 (Balance @ 36%)
            totalAnnualTax += taxable * 0.36m;

            return totalAnnualTax;
        }

        /// <summary>
        /// Returns detailed slab-by-slab tax breakdown for audit/display.
        /// </summary>
        public static List<TaxSlabBreakdown> GetTaxBreakdown(decimal monthlyGrossPay)
        {
            var list = new List<TaxSlabBreakdown>();
            decimal annualIncome = Math.Max(0m, monthlyGrossPay * 12m);

            // 0% Tax-Free
            decimal taxFreeAmount = Math.Min(annualIncome, TaxFreeThresholdAnnual);
            list.Add(new TaxSlabBreakdown
            {
                BracketName = "First Rs. 1,800,000 (Tax-Free)",
                ApplicableRate = 0m,
                TaxableAmountInSlab = taxFreeAmount,
                AnnualTaxAmount = 0m,
                MonthlyTaxAmount = 0m
            });

            if (annualIncome <= TaxFreeThresholdAnnual)
                return list;

            decimal taxable = annualIncome - TaxFreeThresholdAnnual;

            // Slab 1: 6%
            decimal slab1 = Math.Min(taxable, 1000000m);
            decimal slab1Tax = slab1 * 0.06m;
            list.Add(new TaxSlabBreakdown
            {
                BracketName = "Rs. 1,800,000 – 2,800,000 (6%)",
                ApplicableRate = 6m,
                TaxableAmountInSlab = slab1,
                AnnualTaxAmount = slab1Tax,
                MonthlyTaxAmount = Math.Round(slab1Tax / 12m, 2, MidpointRounding.AwayFromZero)
            });
            taxable -= slab1;

            if (taxable <= 0) return list;

            // Slab 2: 18%
            decimal slab2 = Math.Min(taxable, 1000000m);
            decimal slab2Tax = slab2 * 0.18m;
            list.Add(new TaxSlabBreakdown
            {
                BracketName = "Rs. 2,800,000 – 3,800,000 (18%)",
                ApplicableRate = 18m,
                TaxableAmountInSlab = slab2,
                AnnualTaxAmount = slab2Tax,
                MonthlyTaxAmount = Math.Round(slab2Tax / 12m, 2, MidpointRounding.AwayFromZero)
            });
            taxable -= slab2;

            if (taxable <= 0) return list;

            // Slab 3: 24%
            decimal slab3 = Math.Min(taxable, 1000000m);
            decimal slab3Tax = slab3 * 0.24m;
            list.Add(new TaxSlabBreakdown
            {
                BracketName = "Rs. 3,800,000 – 4,800,000 (24%)",
                ApplicableRate = 24m,
                TaxableAmountInSlab = slab3,
                AnnualTaxAmount = slab3Tax,
                MonthlyTaxAmount = Math.Round(slab3Tax / 12m, 2, MidpointRounding.AwayFromZero)
            });
            taxable -= slab3;

            if (taxable <= 0) return list;

            // Slab 4: 30%
            decimal slab4 = Math.Min(taxable, 1000000m);
            decimal slab4Tax = slab4 * 0.30m;
            list.Add(new TaxSlabBreakdown
            {
                BracketName = "Rs. 4,800,000 – 5,800,000 (30%)",
                ApplicableRate = 30m,
                TaxableAmountInSlab = slab4,
                AnnualTaxAmount = slab4Tax,
                MonthlyTaxAmount = Math.Round(slab4Tax / 12m, 2, MidpointRounding.AwayFromZero)
            });
            taxable -= slab4;

            if (taxable <= 0) return list;

            // Slab 5: 36%
            decimal slab5Tax = taxable * 0.36m;
            list.Add(new TaxSlabBreakdown
            {
                BracketName = "Above Rs. 5,800,000 (36%)",
                ApplicableRate = 36m,
                TaxableAmountInSlab = taxable,
                AnnualTaxAmount = slab5Tax,
                MonthlyTaxAmount = Math.Round(slab5Tax / 12m, 2, MidpointRounding.AwayFromZero)
            });

            return list;
        }
    }
}
