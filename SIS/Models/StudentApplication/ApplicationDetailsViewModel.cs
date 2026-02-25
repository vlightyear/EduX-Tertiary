using SIS.Models.Payments;

namespace SIS.Models.StudentApplication
{
    public class ApplicationDetailsViewModel
    {
        public Applicant Application { get; set; }
        public List<ApplicationPayment> PaymentHistory { get; set; } = new List<ApplicationPayment>();
        public bool CanMakePayment { get; set; }

        // Additional computed properties for easier access in views
        public string StatusBadgeClass => Application?.Status switch
        {
            SIS.Enums.Status.Pending => "bg-amber-100 text-amber-800",
            SIS.Enums.Status.Admitted => "bg-green-100 text-green-800",
            SIS.Enums.Status.Rejected => "bg-red-100 text-red-800",
            SIS.Enums.Status.Paid => "bg-blue-100 text-blue-800",
            _ => "bg-gray-100 text-secondary-800"
        };

        public string PaymentStatusBadgeClass => Application?.PaymentStatus switch
        {
            SIS.Enums.Status.Pending => "bg-amber-100 text-amber-800",
            SIS.Enums.Status.Paid => "bg-green-100 text-green-800",
            _ => "bg-gray-100 text-secondary-800"
        };
    }
}
