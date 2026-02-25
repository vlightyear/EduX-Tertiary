using SIS.Services;
using System.Threading.Tasks;

namespace SIS.Interfaces
{
    public class AccommodationFeedback
    {
        public bool Status { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    /// <summary>
    /// Interface for accommodation allocation service operations
    /// Handles bed space assignment with payment verification and invoice creation
    /// </summary>
    /// 

    public interface IAccommodationAllocationService
    {

        Task<decimal> GetAccommodationFee();


        /// <summary>
        /// Assigns bed space with specific payment amount provided
        /// </summary>
        /// <param name="studentId">Student ID to process allocation for</param>
        /// <param name="amountPaid">Amount paid for accommodation</param>
        /// <returns>AccommodationFeedback indicating success or failure with detailed message</returns>
        /// <remarks>
        /// This method will:
        /// 1. Verify student has a pending accommodation application
        /// 2. Get the bed space student selected during application
        /// 3. Validate payment amount meets or exceeds accommodation fee
        /// 4. Verify the bed space is still available
        /// 5. Create accommodation invoice for the accommodation fee amount
        /// 6. Allocate bed space and update all related records
        /// 7. Assign AccommodatedStudent role to the student
        /// Note: Invoice is created for the accommodation fee amount, not the payment amount.
        /// Any overpayment is not handled - as long as payment covers the fee, allocation proceeds.
        /// </remarks>
        Task<AccommodationFeedback> AssignBedSpaceStudentHasAppliedForAndCreateInvoice(int studentId, decimal amountPaid);





        /// <summary>
        /// Verifies student balance and assigns bed space student has selected if student has applied and has sufficient funds
        /// </summary>
        /// <param name="studentId">Student ID to process allocation for</param>
        /// <returns>AccommodationFeedback indicating success or failure with detailed message</returns>
        /// <remarks>
        /// This method will:
        /// 1. Verify student has a pending accommodation application
        /// 2. Get the bed space student selected during application
        /// 3. Check if student balance is sufficient to cover accommodation fee
        /// 4. Verify the bed space is still available
        /// 5. Create accommodation invoice using fee configuration
        /// 6. Allocate bed space and update all related records
        /// 7. Assign AccommodatedStudent role to the student
        /// </remarks>
        Task<AccommodationFeedback> VerifyBalanceAndAssignBedSpaceStudentHasAppliedForAndCreateInvoice(int studentId);

        

        /// <summary>
        /// Assigns bed space without payment verification but creates invoice (admin override)
        /// </summary>
        /// <param name="studentId">Student ID to process allocation for</param>
        /// <param name="bedId">Bed ID to assign</param>
        /// <param name="allocatedBy">User ID of person making the allocation</param>
        /// <returns>AccommodationFeedback indicating success or failure with detailed message</returns>
        /// <remarks>
        /// This method will:
        /// 1. Verify student has a pending accommodation application
        /// 2. Verify the selected bed space is available
        /// 3. Create accommodation invoice (no payment verification)
        /// 4. Allocate bed space and update all related records
        /// 5. Assign AccommodatedStudent role to the student
        /// Note: This is for admin override - no balance or payment checks are performed,
        /// but an invoice is still created for proper accounting.
        /// </remarks>
        Task<AccommodationFeedback> AssignBedSpaceWithoutPayment(int studentId, int bedId, string allocatedBy);

        /// <summary>
        /// Allocate bed space directly without requiring prior application
        /// </summary>
        Task<AccommodationFeedback> DirectBedAllocation(int studentId, int bedId, string allocatedBy, string notes = null);

        /// <summary>
        /// Removes student from accommodation and removes AccommodatedStudent role
        /// </summary>
        /// <param name="studentId">Student ID</param>
        /// <param name="removedBy">User ID of person removing the allocation</param>
        /// <param name="reason">Reason for removal</param>
        /// <returns>AccommodationFeedback indicating success or failure with detailed message</returns>
        /// <remarks>
        /// This method will:
        /// 1. Find and cancel the student's active allocation
        /// 2. Free up the bed space
        /// 3. Update student record to clear accommodation details
        /// 4. Remove AccommodatedStudent role from the student
        /// 5. Log the reason for removal
        /// </remarks>
        Task<AccommodationFeedback> RemoveStudentFromAccommodation(int studentId, string removedBy, string reason);

        /// <summary>
        /// Reallocates student to a new bed space
        /// </summary>
        /// <param name="studentId">Student ID</param>
        /// <param name="newBedId">New bed ID to assign</param>
        /// <param name="reallocatedBy">User ID of person making the reallocation</param>
        /// <param name="reason">Reason for reallocation</param>
        /// <returns>AccommodationFeedback indicating success or failure with detailed message</returns>
        /// <remarks>
        /// This method will:
        /// 1. Find the student's current active allocation
        /// 2. Verify the new bed space is available
        /// 3. Free up the old bed space
        /// 4. Allocate the new bed space
        /// 5. Update student record with new bed details
        /// 6. Log the reallocation reason
        /// 7. Student retains AccommodatedStudent role
        /// </remarks>
        Task<AccommodationFeedback> ReallocateStudentToBedSpace(int studentId, int newBedId, string reallocatedBy, string reason);

        /// <summary>
        /// Gets the student's outstanding balance (total invoices - total payments)
        /// </summary>
        /// <param name="studentId">Student ID to check balance for</param>
        /// <returns>Outstanding balance amount. Positive means student owes, negative means credit</returns>
        decimal GetStudentOutstandingBalance(int studentId);
    }
}