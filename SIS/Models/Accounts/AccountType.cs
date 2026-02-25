using System.ComponentModel.DataAnnotations;

namespace SIS.Models.Accounts
{
    // The AccountType class represents different types of financial accounts in the eCampus system.
    // It inherits from AuditClass, which tracks who created and modified the record, and when those actions occurred.
    public class AccountType : AuditClass
    {
        // Unique identifier for the account type, usually auto-generated.
        [Key]
        public int Id { get; set; }

        // The name of the account type (e.g., "Tuition Fees", "Library Fees", "Research Fund").
        // This field is used to easily identify and categorize the account.
        public string Name { get; set; }

        // A description of the account type, providing more details about its purpose or how it is used.
        // For example, "Account for all tuition fee payments made by students."
        public string Description { get; set; }

        // The type of account, such as "Revenue", "Expense", "Asset", "Liability", or "Equity".
        // This categorizes the account in the broader financial system, helping with reporting and financial analysis.
        public string Type { get; set; }

        // A unique code or abbreviation used to identify the account type within the system.
        // This is often a short and concise representation, such as "TF" for Tuition Fees, or "LF" for Library Fees.
        public string Code { get; set; }
    }
}

