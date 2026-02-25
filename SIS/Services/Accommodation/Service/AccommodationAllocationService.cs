using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SIS.Data;
using SIS.Enums;
using SIS.Interfaces;
using SIS.Models.StudentAccommodation;
using SIS.Models.StudentApplication;
using SIS.Models.Fees;
using SIS.Models.Payments;
using System.Security.Claims;

namespace SIS.Services
{
    public class AccommodationAllocationService : IAccommodationAllocationService
    {
        private readonly ApplicationDbContext _context;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public AccommodationAllocationService(ApplicationDbContext context, IServiceScopeFactory scopeFactory, UserManager<ApplicationUser> userManager, IHttpContextAccessor httpContextAccessor)
        {
            _context = context;
            _scopeFactory = scopeFactory;
            _userManager = userManager;
            _httpContextAccessor = httpContextAccessor;
        }

        public async Task<decimal> GetAccommodationFee()
        {
            try
            {
                var currentDate = DateTime.Now;
                var activePeriod = await _context.AccommodationPeriods
                    .Where(p => p.ApplicationStartDate <= currentDate && p.ApplicationEndDate >= currentDate && p.Status == Status.Active)
                    .FirstOrDefaultAsync();
                if (activePeriod != null) return activePeriod.TypeOfPaymentAmount;
                return (await _context.AccomodationConfigurations.FirstOrDefaultAsync())?.AccommodationFee ?? 0;
            }
            catch { return 0; }
        }

        private decimal CalculateAccommodationFee(AccommodationPeriod period, int? numberOfDays)
        {
            if (period == null) return 0;
            return period.TypeOfPayment == "PerDay" ? period.TypeOfPaymentAmount * (numberOfDays ?? 1) : period.TypeOfPaymentAmount;
        }

        public async Task<AccommodationFeedback> VerifyBalanceAndAssignBedSpaceStudentHasAppliedForAndCreateInvoice(int studentId)
        {
            try
            {
                var student = await _context.Students.Include(s => s.Programme).ThenInclude(p => p.Department).ThenInclude(d => d.School)
                    .Include(s => s.AcademicYear).Include(s => s.ProgrammeLevel).Include(s => s.ModeOfStudy).FirstOrDefaultAsync(s => s.Id == studentId);
                if (student == null) return new AccommodationFeedback { Status = false, Message = "Student not found." };

                var application = await _context.AccommodationApplications.Include(a => a.Period).Include(a => a.Allocation)
                    .Where(a => a.StudentId == studentId && a.Status == Status.Pending).OrderByDescending(a => a.ApplicationDate).FirstOrDefaultAsync();
                if (application == null) return new AccommodationFeedback { Status = false, Message = "No pending accommodation application found." };
                if (!application.SelectedBedId.HasValue) return new AccommodationFeedback { Status = false, Message = "No bed space was selected during the application." };
                if (application.Allocation != null) return new AccommodationFeedback { Status = false, Message = "Student already has a bed space allocated." };

                var config = await _context.AccomodationConfigurations.FirstOrDefaultAsync();
                if (config == null) return new AccommodationFeedback { Status = false, Message = "Accommodation configuration not found." };

                decimal accommodationFee = CalculateAccommodationFee(application.Period, application.NumberOfDays);
                decimal studentBalance = GetStudentAvailableBalance(studentId);
                if (studentBalance < accommodationFee) return new AccommodationFeedback { Status = false, Message = $"Insufficient balance. Required: K{accommodationFee:N2}, Available: K{studentBalance:N2}." };

                var selectedBed = await _context.BedSpaces.Include(b => b.Room).ThenInclude(r => r.Hostel).ThenInclude(h => h.Campus).FirstOrDefaultAsync(b => b.BedId == application.SelectedBedId.Value);
                if (selectedBed == null) return new AccommodationFeedback { Status = false, Message = "Selected bed space not found." };
                if (selectedBed.Status != Status.Available && selectedBed.Status != Status.Reserved) return new AccommodationFeedback { Status = false, Message = $"Selected bed space is no longer available. Status: {selectedBed.Status}" };
                if (selectedBed.Room.Status != Status.Available || selectedBed.Room.Hostel.Status != Status.Active) return new AccommodationFeedback { Status = false, Message = "Room or hostel is not available." };
                if (selectedBed.Room.Gender != student.Gender && selectedBed.Room.Gender != "Mixed") return new AccommodationFeedback { Status = false, Message = $"Bed is for {selectedBed.Room.Gender} students." };

                var invoice = await CreateAccommodationInvoice(student, application.Period, application, accommodationFee, config);

                DateTime? allocationEndDate = application.Period.EndDate;
                if (application.Period.TypeOfPayment == "PerDay" && application.NumberOfDays.HasValue)
                    allocationEndDate = application.Period.StartDate.AddDays(application.NumberOfDays.Value);

                var allocation = new Allocation
                {
                    ApplicationId = application.ApplicationId,
                    BedId = selectedBed.BedId,
                    AllocationType = "automatic",
                    AllocatedById = null,
                    AllocationDate = DateTime.Now,
                    StartDate = application.Period.StartDate,
                    EndDate = allocationEndDate,
                    IsGraduationBased = !application.Period.EndDate.HasValue && application.Period.TypeOfPayment != "PerDay",
                    Status = Status.Active,
                    CreatedAt = DateTime.Now,
                    CreatedBy = "SYSTEM_AUTO_ALLOCATION"
                };
                _context.Allocations.Add(allocation);

                selectedBed.Status = Status.Occupied; selectedBed.UpdatedAt = DateTime.Now; selectedBed.UpdatedBy = "SYSTEM_AUTO_ALLOCATION";
                student.BedId = selectedBed.BedId; student.BedAllocationEndDate = allocationEndDate; student.HasAccommodationClearance = true;
                student.UpdatedAt = DateTime.Now; student.UpdatedBy = "SYSTEM_AUTO_ALLOCATION";

                string feeDetails = application.Period.TypeOfPayment == "PerDay" ? $"({application.NumberOfDays} days × K{application.Period.TypeOfPaymentAmount:N2}/day)" : $"({application.Period.TypeOfPayment})";
                application.Status = Status.Approved;
                application.Notes += $" [AUTO-ALLOCATED: Bed {selectedBed.BedIdentifier}, {selectedBed.Room.Hostel.HostelName} {DateTime.Now:yyyy-MM-dd HH:mm}. Fee: {feeDetails}]";
                application.UpdatedAt = DateTime.Now; application.UpdatedBy = "SYSTEM_AUTO_ALLOCATION";

                var user = await _userManager.FindByEmailAsync(student.Email);
                if (user != null && !await _userManager.IsInRoleAsync(user, "AccommodatedStudent"))
                    await _userManager.AddToRoleAsync(user, "AccommodatedStudent");

                await _context.SaveChangesAsync();

                string endDateInfo = allocationEndDate.HasValue ? allocationEndDate.Value.ToString("MMM dd, yyyy") : "Graduation";
                return new AccommodationFeedback { Status = true, Message = $"Success! Bed allocated: {selectedBed.Room.Hostel.HostelName}, Room {selectedBed.Room.RoomNumber}, Bed {selectedBed.BedIdentifier}. Invoice #{invoice.InvoiceReference} for K{accommodationFee:N2}. Valid until: {endDateInfo}" };
            }
            catch (Exception ex) 
            { 
                return new AccommodationFeedback { Status = false, Message = $"Error: {ex.Message}" }; 
            }
        }

        public async Task<AccommodationFeedback> AssignBedSpaceStudentHasAppliedForAndCreateInvoice(int studentId, decimal amountPaid)
        {
            try
            {
                var student = await _context.Students.Include(s => s.Programme).ThenInclude(p => p.Department).ThenInclude(d => d.School)
                    .Include(s => s.AcademicYear).Include(s => s.ProgrammeLevel).Include(s => s.ModeOfStudy).FirstOrDefaultAsync(s => s.Id == studentId);
                if (student == null) return new AccommodationFeedback { Status = false, Message = "Student not found." };

                var application = await _context.AccommodationApplications.Include(a => a.Period).Include(a => a.Allocation)
                    .Where(a => a.StudentId == studentId && a.Status == Status.Pending).OrderByDescending(a => a.ApplicationDate).FirstOrDefaultAsync();
                if (application == null) return new AccommodationFeedback { Status = false, Message = "No pending application found." };
                if (!application.SelectedBedId.HasValue) return new AccommodationFeedback { Status = false, Message = "No bed space was selected." };
                if (application.Allocation != null) return new AccommodationFeedback { Status = false, Message = "Already allocated." };

                var config = await _context.AccomodationConfigurations.FirstOrDefaultAsync();
                if (config == null) return new AccommodationFeedback { Status = false, Message = "Config not found." };

                decimal accommodationFee = CalculateAccommodationFee(application.Period, application.NumberOfDays);
                if (amountPaid < accommodationFee) return new AccommodationFeedback { Status = false, Message = $"Payment (K{amountPaid:N2}) less than fee (K{accommodationFee:N2})." };

                var selectedBed = await _context.BedSpaces.Include(b => b.Room).ThenInclude(r => r.Hostel).ThenInclude(h => h.Campus).FirstOrDefaultAsync(b => b.BedId == application.SelectedBedId.Value);
                if (selectedBed == null) return new AccommodationFeedback { Status = false, Message = "Bed not found." };
                if (selectedBed.Status != Status.Available && selectedBed.Status != Status.Reserved) return new AccommodationFeedback { Status = false, Message = $"Bed not available. Status: {selectedBed.Status}" };
                if (selectedBed.Room.Gender != student.Gender && selectedBed.Room.Gender != "Mixed") return new AccommodationFeedback { Status = false, Message = $"Bed is for {selectedBed.Room.Gender}." };

                var invoice = await CreateAccommodationInvoice(student, application.Period, application, accommodationFee, config);

                DateTime? allocationEndDate = application.Period.EndDate;
                if (application.Period.TypeOfPayment == "PerDay" && application.NumberOfDays.HasValue)
                    allocationEndDate = application.Period.StartDate.AddDays(application.NumberOfDays.Value);
                var loggedInUserId = GetLoggedInUserId();

                var allocation = new Allocation
                {
                    ApplicationId = application.ApplicationId,
                    BedId = selectedBed.BedId,
                    AllocationType = "payment_verified",
                    AllocatedById = loggedInUserId??null,
                    AllocationDate = DateTime.Now,
                    StartDate = application.Period.StartDate,
                    EndDate = allocationEndDate,
                    IsGraduationBased = !application.Period.EndDate.HasValue && application.Period.TypeOfPayment != "PerDay",
                    Status = Status.Active,
                    CreatedAt = DateTime.Now,
                    CreatedBy = "SYSTEM_PAYMENT"
                };
                _context.Allocations.Add(allocation);

                selectedBed.Status = Status.Occupied; selectedBed.UpdatedAt = DateTime.Now; selectedBed.UpdatedBy = "SYSTEM_PAYMENT";
                student.BedId = selectedBed.BedId; student.BedAllocationEndDate = allocationEndDate; student.HasAccommodationClearance = true;
                student.UpdatedAt = DateTime.Now; student.UpdatedBy = "SYSTEM_PAYMENT";
                application.Status = Status.Approved; application.Notes += $" [PAYMENT-ALLOCATED: K{amountPaid:N2}]";
                application.UpdatedAt = DateTime.Now; application.UpdatedBy = "SYSTEM_PAYMENT";

                var user = await _userManager.FindByEmailAsync(student.Email);
                if (user != null && !await _userManager.IsInRoleAsync(user, "AccommodatedStudent"))
                    await _userManager.AddToRoleAsync(user, "AccommodatedStudent");

                await _context.SaveChangesAsync();
                return new AccommodationFeedback { Status = true, Message = $"Success! Payment K{amountPaid:N2} verified. Bed: {selectedBed.Room.Hostel.HostelName}, Room {selectedBed.Room.RoomNumber}, Bed {selectedBed.BedIdentifier}. Invoice #{invoice.InvoiceReference}" };
            }
            catch (Exception ex) { return new AccommodationFeedback { Status = false, Message = $"Error: {ex.Message}" }; }
        }

        public async Task<AccommodationFeedback> AssignBedSpaceWithoutPayment(int studentId, int bedId, string allocatedBy)
        {
            try
            {
                var student = await _context.Students.Include(s => s.Programme).ThenInclude(p => p.Department).ThenInclude(d => d.School)
                    .Include(s => s.AcademicYear).Include(s => s.ProgrammeLevel).Include(s => s.ModeOfStudy).FirstOrDefaultAsync(s => s.Id == studentId);
                if (student == null) return new AccommodationFeedback { Status = false, Message = "Student not found." };

                var application = await _context.AccommodationApplications.Include(a => a.Period).Include(a => a.Allocation)
                    .Where(a => a.StudentId == studentId && a.Status == Status.Pending).OrderByDescending(a => a.ApplicationDate).FirstOrDefaultAsync();
                if (application == null) return new AccommodationFeedback { Status = false, Message = "No pending application." };
                if (application.Allocation != null) return new AccommodationFeedback { Status = false, Message = "Already allocated." };

                var config = await _context.AccomodationConfigurations.FirstOrDefaultAsync();
                if (config == null) return new AccommodationFeedback { Status = false, Message = "Config not found." };

                decimal accommodationFee = CalculateAccommodationFee(application.Period, application.NumberOfDays);

                var selectedBed = await _context.BedSpaces.Include(b => b.Room).ThenInclude(r => r.Hostel).ThenInclude(h => h.Campus).FirstOrDefaultAsync(b => b.BedId == bedId);
                if (selectedBed == null) return new AccommodationFeedback { Status = false, Message = "Bed not found." };
                if (selectedBed.Status != Status.Available && selectedBed.Status != Status.Reserved) return new AccommodationFeedback { Status = false, Message = $"Bed not available. Status: {selectedBed.Status}" };
                if (selectedBed.Room.Gender != student.Gender && selectedBed.Room.Gender != "Mixed") return new AccommodationFeedback { Status = false, Message = $"Bed is for {selectedBed.Room.Gender}." };

                var invoice = await CreateAccommodationInvoice(student, application.Period, application, accommodationFee, config);

                DateTime? allocationEndDate = application.Period.EndDate;
                if (application.Period.TypeOfPayment == "PerDay" && application.NumberOfDays.HasValue)
                    allocationEndDate = application.Period.StartDate.AddDays(application.NumberOfDays.Value);

                var allocation = new Allocation
                {
                    ApplicationId = application.ApplicationId,
                    BedId = selectedBed.BedId,
                    AllocationType = "pay_later",
                    AllocatedById = allocatedBy,
                    AllocationDate = DateTime.Now,
                    StartDate = application.Period.StartDate,
                    EndDate = allocationEndDate,
                    IsGraduationBased = !application.Period.EndDate.HasValue && application.Period.TypeOfPayment != "PerDay",
                    Status = Status.Active,
                    CreatedAt = DateTime.Now,
                    CreatedBy = allocatedBy
                };
                _context.Allocations.Add(allocation);

                selectedBed.Status = Status.Occupied; selectedBed.UpdatedAt = DateTime.Now; selectedBed.UpdatedBy = allocatedBy;
                student.BedId = selectedBed.BedId; student.BedAllocationEndDate = allocationEndDate; student.HasAccommodationClearance = true;
                student.UpdatedAt = DateTime.Now; student.UpdatedBy = allocatedBy;
                application.Status = Status.Approved; application.Notes += $" [PAY-LATER-ALLOCATED: {DateTime.Now:yyyy-MM-dd}]";
                application.UpdatedAt = DateTime.Now; application.UpdatedBy = allocatedBy;

                var user = await _userManager.FindByEmailAsync(student.Email);
                if (user != null && !await _userManager.IsInRoleAsync(user, "AccommodatedStudent"))
                    await _userManager.AddToRoleAsync(user, "AccommodatedStudent");

                await _context.SaveChangesAsync();
                return new AccommodationFeedback { Status = true, Message = $"Success! Bed allocated (pay later): {selectedBed.Room.Hostel.HostelName}, Room {selectedBed.Room.RoomNumber}. Invoice #{invoice.InvoiceReference} for K{accommodationFee:N2}" };
            }
            catch (Exception ex) { return new AccommodationFeedback { Status = false, Message = $"Error: {ex.Message}" }; }
        }

        public async Task<AccommodationFeedback> DirectBedAllocation(int studentId, int bedId, string allocatedBy, string notes = null)
        {
            try
            {
                var student = await _context.Students.Include(s => s.Programme).ThenInclude(p => p.Department).ThenInclude(d => d.School)
                    .Include(s => s.AcademicYear).Include(s => s.ProgrammeLevel).Include(s => s.ModeOfStudy).FirstOrDefaultAsync(s => s.Id == studentId);
                if (student == null) return new AccommodationFeedback { Status = false, Message = "Student not found." };

                var existingAllocation = await _context.Allocations.Include(a => a.Bed).Where(a => a.Application.StudentId == studentId && a.Status == Status.Active).FirstOrDefaultAsync();
                if (existingAllocation != null) return new AccommodationFeedback { Status = false, Message = $"Already has allocation: {existingAllocation.Bed.BedIdentifier}" };

                var config = await _context.AccomodationConfigurations.FirstOrDefaultAsync();
                if (config == null) return new AccommodationFeedback { Status = false, Message = "Config not found." };

                var currentDate = DateTime.Now;
                var accommodationPeriod = await _context.AccommodationPeriods.Where(p => p.Status == Status.Active && p.ApplicationStartDate <= currentDate && p.ApplicationEndDate >= currentDate).OrderByDescending(p => p.ApplicationStartDate).FirstOrDefaultAsync();
                if (accommodationPeriod == null) return new AccommodationFeedback { Status = false, Message = "No active period." };

                decimal accommodationFee = accommodationPeriod.TypeOfPaymentAmount;

                var selectedBed = await _context.BedSpaces.Include(b => b.Room).ThenInclude(r => r.Hostel).ThenInclude(h => h.Campus).FirstOrDefaultAsync(b => b.BedId == bedId);
                if (selectedBed == null) return new AccommodationFeedback { Status = false, Message = "Bed not found." };
                if (selectedBed.Status != Status.Available) return new AccommodationFeedback { Status = false, Message = $"Bed not available. Status: {selectedBed.Status}" };
                if (selectedBed.Room.Gender != student.Gender && selectedBed.Room.Gender != "Mixed") return new AccommodationFeedback { Status = false, Message = $"Bed is for {selectedBed.Room.Gender}." };

                var application = new AccommodationApplication
                {
                    StudentId = studentId,
                    PeriodId = accommodationPeriod.PeriodId,
                    ApplicationDate = DateTime.Now,
                    Status = Status.Approved,
                    Notes = $"[DIRECT-ALLOCATION] by {allocatedBy}. {notes ?? ""}",
                    CreatedAt = DateTime.Now,
                    CreatedBy = allocatedBy
                };
                _context.AccommodationApplications.Add(application);
                await _context.SaveChangesAsync();

                var invoice = await CreateAccommodationInvoice(student, accommodationPeriod, application, accommodationFee, config);

                var allocation = new Allocation
                {
                    ApplicationId = application.ApplicationId,
                    BedId = selectedBed.BedId,
                    AllocationType = "direct_allocation",
                    AllocatedById = allocatedBy,
                    AllocationDate = DateTime.Now,
                    StartDate = accommodationPeriod.StartDate,
                    EndDate = accommodationPeriod.EndDate,
                    IsGraduationBased = !accommodationPeriod.EndDate.HasValue,
                    Status = Status.Active,
                    CreatedAt = DateTime.Now,
                    CreatedBy = allocatedBy
                };
                _context.Allocations.Add(allocation);

                selectedBed.Status = Status.Occupied; selectedBed.UpdatedAt = DateTime.Now; selectedBed.UpdatedBy = allocatedBy;
                student.BedId = selectedBed.BedId; student.BedAllocationEndDate = accommodationPeriod.EndDate; student.HasAccommodationClearance = true;
                student.UpdatedAt = DateTime.Now; student.UpdatedBy = allocatedBy;

                var user = await _userManager.FindByEmailAsync(student.Email);
                if (user != null && !await _userManager.IsInRoleAsync(user, "AccommodatedStudent"))
                    await _userManager.AddToRoleAsync(user, "AccommodatedStudent");

                await _context.SaveChangesAsync();
                return new AccommodationFeedback { Status = true, Message = $"Success! Direct allocation: {selectedBed.Room.Hostel.HostelName}, Room {selectedBed.Room.RoomNumber}. Invoice #{invoice.InvoiceReference} for K{accommodationFee:N2}" };
            }
            catch (Exception ex) { return new AccommodationFeedback { Status = false, Message = $"Error: {ex.Message}" }; }
        }

        public async Task<AccommodationFeedback> RemoveStudentFromAccommodation(int studentId, string removedBy, string reason)
        {
            try
            {
                var student = await _context.Students.FirstOrDefaultAsync(s => s.Id == studentId);
                if (student == null) return new AccommodationFeedback { Status = false, Message = "Student not found." };

                var activeAllocation = await _context.Allocations.Include(a => a.Application).Include(a => a.Bed).ThenInclude(b => b.Room).ThenInclude(r => r.Hostel)
                    .Where(a => a.Application.StudentId == studentId && a.Status == Status.Active).FirstOrDefaultAsync();
                if (activeAllocation == null) return new AccommodationFeedback { Status = false, Message = "No active allocation." };

                activeAllocation.Status = Status.Canceled; activeAllocation.EndDate = DateTime.Now; activeAllocation.UpdatedAt = DateTime.Now; activeAllocation.UpdatedBy = removedBy;
                if (activeAllocation.Bed != null) { activeAllocation.Bed.Status = Status.Available; activeAllocation.Bed.UpdatedAt = DateTime.Now; activeAllocation.Bed.UpdatedBy = removedBy; }

                string bedInfo = activeAllocation.Bed != null ? $"{activeAllocation.Bed.Room.Hostel.HostelName}, Room {activeAllocation.Bed.Room.RoomNumber}, Bed {activeAllocation.Bed.BedIdentifier}" : "Unknown";
                student.BedId = null; student.BedAllocationEndDate = null; student.HasAccommodationClearance = false; student.UpdatedAt = DateTime.Now; student.UpdatedBy = removedBy;

                if (activeAllocation.Application != null) { activeAllocation.Application.Status = Status.Canceled; activeAllocation.Application.Notes += $" [REMOVED: {reason}]"; activeAllocation.Application.UpdatedAt = DateTime.Now; activeAllocation.Application.UpdatedBy = removedBy; }

                var user = await _userManager.FindByEmailAsync(student.Email);
                if (user != null && await _userManager.IsInRoleAsync(user, "AccommodatedStudent"))
                    await _userManager.RemoveFromRoleAsync(user, "AccommodatedStudent");

                await _context.SaveChangesAsync();
                return new AccommodationFeedback { Status = true, Message = $"Removed from {bedInfo}. Reason: {reason}" };
            }
            catch (Exception ex) { return new AccommodationFeedback { Status = false, Message = $"Error: {ex.Message}" }; }
        }

        public async Task<AccommodationFeedback> ReallocateStudentToBedSpace(int studentId, int newBedId, string reallocatedBy, string reason)
        {
            try
            {
                var student = await _context.Students.FirstOrDefaultAsync(s => s.Id == studentId);
                if (student == null) return new AccommodationFeedback { Status = false, Message = "Student not found." };

                var currentAllocation = await _context.Allocations.Include(a => a.Application).Include(a => a.Bed).ThenInclude(b => b.Room).ThenInclude(r => r.Hostel)
                    .Where(a => a.Application.StudentId == studentId && a.Status == Status.Active).FirstOrDefaultAsync();
                if (currentAllocation == null) return new AccommodationFeedback { Status = false, Message = "No active allocation." };

                var newBed = await _context.BedSpaces.Include(b => b.Room).ThenInclude(r => r.Hostel).ThenInclude(h => h.Campus).FirstOrDefaultAsync(b => b.BedId == newBedId);
                if (newBed == null) return new AccommodationFeedback { Status = false, Message = "New bed not found." };
                if (newBed.Status != Status.Available) return new AccommodationFeedback { Status = false, Message = $"New bed not available. Status: {newBed.Status}" };
                if (newBed.Room.Gender != student.Gender && newBed.Room.Gender != "Mixed") return new AccommodationFeedback { Status = false, Message = $"New bed is for {newBed.Room.Gender}." };

                string oldBedInfo = currentAllocation.Bed != null ? $"{currentAllocation.Bed.Room.Hostel.HostelName}, Bed {currentAllocation.Bed.BedIdentifier}" : "Unknown";
                if (currentAllocation.Bed != null) { currentAllocation.Bed.Status = Status.Available; currentAllocation.Bed.UpdatedAt = DateTime.Now; currentAllocation.Bed.UpdatedBy = reallocatedBy; }

                currentAllocation.BedId = newBedId; currentAllocation.AllocationType = "reallocated"; currentAllocation.UpdatedAt = DateTime.Now; currentAllocation.UpdatedBy = reallocatedBy;
                newBed.Status = Status.Occupied; newBed.UpdatedAt = DateTime.Now; newBed.UpdatedBy = reallocatedBy;
                student.BedId = newBedId; student.UpdatedAt = DateTime.Now; student.UpdatedBy = reallocatedBy;

                if (currentAllocation.Application != null) { currentAllocation.Application.Notes += $" [REALLOCATED: {oldBedInfo} to {newBed.Room.Hostel.HostelName}. Reason: {reason}]"; currentAllocation.Application.UpdatedAt = DateTime.Now; currentAllocation.Application.UpdatedBy = reallocatedBy; }

                await _context.SaveChangesAsync();
                return new AccommodationFeedback { Status = true, Message = $"Reallocated from {oldBedInfo} to {newBed.Room.Hostel.HostelName}, Room {newBed.Room.RoomNumber}. Reason: {reason}" };
            }
            catch (Exception ex) { return new AccommodationFeedback { Status = false, Message = $"Error: {ex.Message}" }; }
        }

        private async Task<StudentInvoice> CreateAccommodationInvoice(Student student, AccommodationPeriod period, AccommodationApplication application, decimal accommodationFee, AccomodationConfiguration config)
        {
            string invoiceRef = $"INV-ACCOM-{student.StudentId_Number}-{DateTime.Now:yyyyMMddHHmmss}";
            string description = period.TypeOfPayment == "PerDay" && application.NumberOfDays.HasValue
                ? $"Accommodation fee for {application.NumberOfDays} days (K{period.TypeOfPaymentAmount:N2}/day)"
                : $"Accommodation fee ({period.TypeOfPayment}) for {period.StartDate:yyyy-MM-dd} - {(period.EndDate?.ToString("yyyy-MM-dd") ?? "Graduation")}";

            var invoice = new StudentInvoice { AcademicYearId=student.AcademicYearId, StudentId = student.Id, InvoiceReference = invoiceRef, TotalAmount = accommodationFee, CreatedDate = DateTime.Now, Status = Status.Pending, AccountingSystemPostStatus = "Pending" };
            var invoiceItem = new StudentInvoiceItem { FeeTypeName = "Accommodation Fee", Description = description, Amount = accommodationFee, FeeConfigurationId = null, StudentInvoice = invoice };
            invoice.InvoiceItems.Add(invoiceItem);
            _context.StudentInvoices.Add(invoice);
            await _context.SaveChangesAsync();
            return invoice;
        }

        private decimal GetStudentAvailableBalance(int studentId)
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            decimal totalPaid = context.OnlinePayments.Where(op => op.StudentId == studentId && op.Status == "Paid").Sum(p => p.Amount ?? 0);
            decimal totalInvoiced = context.StudentInvoices.Where(si => si.StudentId == studentId).Sum(i => i.TotalAmount);
            return totalPaid - totalInvoiced;
        }

        public decimal GetStudentOutstandingBalance(int studentId) => GetStudentAvailableBalance(studentId);

        private string? GetLoggedInUserId()
        {
            return _httpContextAccessor.HttpContext?
                .User?
                .FindFirstValue(ClaimTypes.NameIdentifier);
        }

    }
}