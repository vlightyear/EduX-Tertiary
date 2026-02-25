using SIS.Models.StudentApplication;

namespace SIS.Models.Payments
{
    public class StudentFinanceViewModel
    {
        public Student Student { get; set; }
        public decimal TotalFees { get; set; }
        public decimal CurrentTotalFees { get; set; }
        public decimal AmountPaid { get; set; }
        public decimal OutstandingBalance { get; set; }
        public decimal MinRegistrationPayment { get; set; }
        public decimal MinExamPayment { get; set; }
        public List<FeeBreakdownItem> FeeBreakdown { get; set; }
        public string AcademicYear { get; set; }
        public string YearOfStudy { get; set; }
    }


}
