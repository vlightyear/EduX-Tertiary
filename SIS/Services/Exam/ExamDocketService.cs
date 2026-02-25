using Microsoft.EntityFrameworkCore;
using SIS.Data;
using SIS.Models.Admin;
using SIS.Models.StudentApplication;
using SIS.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SIS.Models.Registration;

namespace SIS.Services
{
    public class ExamDocketService
    {
        private readonly ApplicationDbContext _context;
        private readonly IInstitutionConfigService _institutionConfig;

        public ExamDocketService(ApplicationDbContext context, IInstitutionConfigService institutionConfig)
        {
            _context = context;
            _institutionConfig = institutionConfig;
        }

        // Get upcoming exam events within the specified window
        public async Task<List<ExamEventDto>> GetUpcomingExamEvents(int studentId, int daysAhead = 14)
        {
            var student = await _context.Students
                .Include(s => s.School)
                .Include(s => s.Programme)
                .FirstOrDefaultAsync(s => s.Id == studentId);

            if (student == null) return new List<ExamEventDto>();

            var examEventTypes = new[] {
                "Final Examination",
                "Deferred Examination",
                "Supplementary Examination",
                "Make-up Examination",
                "Continuous Assessment"
            };

            var currentDate = DateTime.Now;
            var endDate = currentDate.AddDays(daysAhead);

            var examEvents = await _context.AcademicCalendarEvents
                .Include(e => e.EventType)
                .Where(e => examEventTypes.Contains(e.EventType.Name))
                .Where(e => e.IsPublished)
                .Where(e => (e.StartDateTime <= endDate && e.StartDateTime >= currentDate.AddDays(-7)) ||
                           (e.StartDateTime <= currentDate && e.EndDateTime >= currentDate))
                .Where(e => e.SchoolId == null || e.SchoolId == student.SchoolId)
                .Where(e => e.ProgrammeId == null || e.ProgrammeId == student.ProgrammeId)
                .Where(e => e.AcademicYearId == student.AcademicYearId)
                .Select(e => new ExamEventDto
                {
                    Id = e.Id,
                    Title = e.Title,
                    EventTypeName = e.EventType.Name,
                    StartDateTime = e.StartDateTime,
                    EndDateTime = e.EndDateTime,
                    Location = e.Location,
                    Description = e.Description,
                    Semester = e.Semester ?? 0
                })
                .OrderBy(e => e.StartDateTime)
                .ToListAsync();

            // Check eligibility for each exam
            foreach (var exam in examEvents)
            {
                exam.IsEligible = await CheckExamEligibility(studentId, exam.Id, exam.EventTypeName);
                exam.RequiresApproval = IsSpecialExamType(exam.EventTypeName);

                if (exam.RequiresApproval)
                {
                    var request = await GetApprovedExamRequest(studentId, exam.EventTypeName);
                    exam.HasApprovedRequest = request != null;
                    exam.RequestStatus = request?.Status.ToString() ?? "Not Requested";
                }
            }

            return examEvents;
        }

        // Check if student is eligible for a specific exam
        public async Task<bool> CheckExamEligibility(int studentId, int examEventId, string examTypeName = null)
        {
            var student = await _context.Students.FindAsync(studentId);
            if (student == null || student.RegistrationStatus != Status.Registered)
                return false;

            // For final exams, check if student is registered and has paid fees
            if (examTypeName == null)
            {
                var examEvent = await _context.AcademicCalendarEvents
                    .Include(e => e.EventType)
                    .FirstOrDefaultAsync(e => e.Id == examEventId);
                examTypeName = examEvent?.EventType?.Name;
            }

            if (examTypeName == "Final Examination")
            {
                // Check if student has paid at least 75% of fees
                return student.HasPaid75PercentFees || student.HasPaidFullFees;
            }

            // For special exams, check for approved request
            if (IsSpecialExamType(examTypeName))
            {
                var approvedRequest = await GetApprovedExamRequest(studentId, examTypeName);
                return approvedRequest != null;
            }

            return true;
        }

        // Get approved exam requests for special exam types
        public async Task<AcademicRequest> GetApprovedExamRequest(int studentId, string examTypeName)
        {
            var requestType = MapExamTypeToRequestType(examTypeName);
            if (string.IsNullOrEmpty(requestType))
                return null;

            return await _context.AcademicRequests
                .Where(r => r.StudentId == studentId)
                .Where(r => r.RequestType == requestType)
                .Where(r => r.Status == Status.Approved)
                .OrderByDescending(r => r.UpdatedAt)
                .FirstOrDefaultAsync();
        }

        private bool IsSpecialExamType(string examTypeName)
        {
            var specialTypes = new[] {
                "Deferred Examination",
                "Supplementary Examination",
                "Make-up Examination"
            };
            return specialTypes.Contains(examTypeName);
        }

        private string MapExamTypeToRequestType(string examTypeName)
        {
            return examTypeName switch
            {
                "Deferred Examination" => "Deferred Exam",
                "Supplementary Examination" => "Supplementary Exam",
                "Make-up Examination" => "Make-up Exam",
                _ => null
            };
        }
    }

    // DTO for exam events
    public class ExamEventDto
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public string EventTypeName { get; set; }
        public DateTime StartDateTime { get; set; }
        public DateTime? EndDateTime { get; set; }
        public string Location { get; set; }
        public string Description { get; set; }
        public int Semester { get; set; }
        public bool IsEligible { get; set; }
        public bool RequiresApproval { get; set; }
        public bool HasApprovedRequest { get; set; }
        public string RequestStatus { get; set; }
    }
}