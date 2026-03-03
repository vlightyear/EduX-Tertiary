using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;

namespace SIS.Models.ViewModels
{
    public class SchoolBillingMonthly
    {
        // ── Filters (bound from query string / form) ──────────────────────────
        public string? SelectedSchool { get; set; }
        public string? MonthFrom { get; set; }   // "yyyy-MM"  e.g. "2024-01"
        public string? MonthTo { get; set; }   // "yyyy-MM"  e.g. "2024-12"

        // ── Available filter options ──────────────────────────────────────────
        public List<string> Schools { get; set; } = new();
        public List<string> AllMonths { get; set; } = new();   // sorted "yyyy-MM" strings

        // ── Raw data rows (after filter applied) ─────────────────────────────
        public List<SchoolBillingRow> Rows { get; set; } = new();

        // ── Computed summaries (populated in controller) ──────────────────────
        public decimal TotalInvoices { get; set; }
        public decimal TotalPayments { get; set; }
        public decimal TotalBalance { get; set; }
        public decimal CollectionRate => TotalInvoices == 0 ? 0
                                           : Math.Round(TotalPayments / TotalInvoices * 100, 1);
        public int ActiveSchools { get; set; }
        public int PeriodMonths { get; set; }

        // ── Month-on-month trend (for line chart) ────────────────────────────
        // Key = "yyyy-MM", Value = (invoices, payments)
        public Dictionary<string, (decimal Invoices, decimal Payments)> MonthlyTrend { get; set; } = new();

        // ── Per-school totals (for bar chart) ────────────────────────────────
        public Dictionary<string, (decimal Invoices, decimal Payments)> SchoolTotals { get; set; } = new();
    }

    public class SchoolBillingRow
    {
        public string School { get; set; } = string.Empty;
        public string MonthKey { get; set; } = string.Empty;   // "yyyy-MM"
        public decimal MonthlyInvoices { get; set; }
        public decimal MonthlyPayments { get; set; }
        public decimal MonthlyBalance { get; set; }

        // Derived
        public decimal CollectionRate => MonthlyInvoices == 0 ? 0
                                         : Math.Round(MonthlyPayments / MonthlyInvoices * 100, 1);

        /// <summary>Friendly display label, e.g. "Jan 2024"</summary>
        public string MonthLabel
        {
            get
            {
                if (DateTime.TryParseExact(MonthKey + "-01", "yyyy-MM-dd",
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None, out var dt))
                    return dt.ToString("MMM yyyy");
                return MonthKey;
            }
        }
    }

    [Keyless]
    [Table("VW_BI_SchoolBilling_Monthly")]
    public class VwBiSchoolBillingMonthly
    {
        [Column("School")]
        public string School { get; set; } = string.Empty;

        [Column("MonthKey")]
        public string MonthKey { get; set; } = string.Empty;

        [Column("MonthlyInvoices")]
        public decimal MonthlyInvoices { get; set; }

        [Column("MonthlyPayments")]
        public decimal MonthlyPayments { get; set; }

        [Column("MonthlyBalance")]
        public decimal MonthlyBalance { get; set; }
    }
}