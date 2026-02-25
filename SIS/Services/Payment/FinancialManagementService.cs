using SIS.Repository;
using System.Text;

namespace SIS.Services.Payment
{
    public class FinancialManagementService : IFinancialManagementService
    {
        private readonly IStudentRepository _studentRepository;

        public FinancialManagementService(IStudentRepository studentRepository)
        {
            _studentRepository = studentRepository;
        }

        public async Task<string> GetFinancialStatusAsync(int studentId)
        {
            // Now the GetByIdAsync method returns a Student object
            var student = await _studentRepository.GetByIdAsync(studentId);
            if (student == null)
            {
                return "Student not found.";
            }

            var financialStatus = new StringBuilder();

            // Display outstanding fees
            financialStatus.AppendLine($"Outstanding Fees: {student.OutstandingFees:C}");

            // Display payment history
            var payments = student.FinancialStatements.OrderByDescending(f => f.PaymentDate);
            if (!payments.Any())
            {
                financialStatus.AppendLine("No payments have been made.");
            }
            else
            {
                financialStatus.AppendLine("Payment History:");
                foreach (var payment in payments)
                {
                    financialStatus.AppendLine($"{payment.PaymentDate:MM/dd/yyyy}: {payment.AmountPaid:C} ({payment.PaymentMethod})");
                }
            }

            return financialStatus.ToString();
        }
    }


}
