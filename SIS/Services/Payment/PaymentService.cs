using SIS.Data;
using SIS.Enums;
using SIS.Models;
using SIS.Models.Payments;
using SIS.Models.StudentApplication;

namespace SIS.Services.Payment
{
    public class PaymentService : IPaymentService
    {
        private readonly ApplicationDbContext _dbContext;

        public PaymentService(ApplicationDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public void AddApplicationPayment(Applicant applicant)
        {
            try
            {
                var paymentDescription = "Payment to be made for Application Fee";
                // Get programme level from applicant
                var programmeLevel = applicant.ProgrammeLevelId; // TODO: Get programme level from applicant
                var academicYear = applicant.AcademicYear;

                // get payment fee by academic year from applicant and programme level
                var paymentFee = _dbContext.FeeConfigurations
                    .FirstOrDefault(pf => pf.AcademicYear == academicYear && pf.ProgramLevelId == programmeLevel);

                if (paymentFee == null)
                {
                    var message = string.Format("Application fee not found for the {0} academic year and programme {1}.", academicYear, programmeLevel);
                    throw new Exception(message);
                }

                string? referenceNumber = applicant.ReferenceNumber;
                if (referenceNumber == null)
                {
                    var message = string.Format("Application reference number can not be null.");
                }

                //var account = _dbContext.AccountTypes.FirstOrDefault(at => at.Name == "Application Fee");
                //if (account == null)
                //{ 
                //    var message = string.Format("Application fee not found for the {0} academic year and programme {1}.", academicYear, programmeLevel);
                //}

                // create application payment
                var applicationPayment = new PaymentsDetails
                {
                    ReferenceNumber = referenceNumber, // link to application by reference number
                    FeeTypeId = paymentFee.FeeTypeId,
                    PaymentTypeName = "Application Fee",
                    Amount = paymentFee.Amount,
                    Status = Status.Pending,
                    Description = paymentDescription,
                    OutStandingAmount = paymentFee.Amount,
                    PaidAmount = 0,
                    CreatedAt = DateTime.Now,
                    CreatedBy = "System"
                };


                _dbContext.PaymentsDetails.Add(applicationPayment);
                _dbContext.SaveChanges();
            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message);
            }
        }

        public void UpdateApplicationPayment(Applicant applicant)
        {
            try
            {
                decimal amountPaid = decimal.Zero;

                var payment = _dbContext.PaymentsDetails
                    .FirstOrDefault(p => p.ReferenceNumber == applicant.ReferenceNumber && p.Status == Status.Pending);

                if (payment == null)
                {
                    throw new Exception("Payment record not found or already completed.");
                }

                // Update payment details
                amountPaid = payment.Amount;    // TODO: Get the real amount from payment gateway to be implemented
                
                payment.PaidAmount = amountPaid; // Assuming full payment is made
                payment.OutStandingAmount = payment.Amount - payment.PaidAmount;


                if (payment.OutStandingAmount <= 0)
                {
                    // update applicant details for payment
                    applicant.PaymentStatus = Status.Paid;
                    
                    payment.Status = Status.Paid;
                }
                else
                {
                    applicant.PaymentStatus = Status.Pending;
                    payment.Status = Status.Pending;
                }
                
                applicant.UpdatedAt = DateTime.Now;
                applicant.UpdatedBy = "System";

                payment.UpdatedAt = DateTime.Now;
                payment.UpdatedBy = "System";

                _dbContext.SaveChanges();
            }
            catch (Exception ex)
            {
                throw new Exception("Error updating payment: " + ex.Message);
            }
        }
    }
}
