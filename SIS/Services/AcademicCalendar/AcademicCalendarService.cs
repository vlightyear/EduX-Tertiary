using Microsoft.EntityFrameworkCore;
using SIS.Data;
using SIS.Models.Admin;
using SIS.Models.Registration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SIS.Services
{
    public class AcademicCalendarService
    {
        private readonly ApplicationDbContext _context;

        public AcademicCalendarService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task GenerateSystemEventsForAcademicYear(int academicYearId)
        {
            // Get the academic year
            var academicYear = await _context.AcademicYears
                .FirstOrDefaultAsync(y => y.YearId == academicYearId);

            if (academicYear == null)
                throw new ArgumentException("Academic year not found", nameof(academicYearId));

            // Remove existing system events for this academic year
            var existingEvents = await _context.AcademicCalendarEvents
                .Where(e => e.AcademicYearId == academicYearId && e.IsSystemEvent)
                .ToListAsync();

            _context.AcademicCalendarEvents.RemoveRange(existingEvents);

            // Create list for new system events
            var systemEvents = new List<AcademicCalendarEvent>();

            // Get event types
            var registrationEventType = await _context.AcademicEventTypes
                .FirstOrDefaultAsync(t => t.Name.Contains("Registration"));
            var examEventType = await _context.AcademicEventTypes
                .FirstOrDefaultAsync(t => t.Name.Contains("Exam") || t.Name.Contains("Final"));
            var deadlineEventType = await _context.AcademicEventTypes
                .FirstOrDefaultAsync(t => t.Name.Contains("Deadline"));
            var semesterStartType = await _context.AcademicEventTypes
                .FirstOrDefaultAsync(t => t.Name.Contains("Semester") && t.Name.Contains("Start"));
            var semesterEndType = await _context.AcademicEventTypes
                .FirstOrDefaultAsync(t => t.Name.Contains("Semester") && t.Name.Contains("End"));

            // Default event types if not found
            registrationEventType ??= await _context.AcademicEventTypes.FirstOrDefaultAsync();
            examEventType ??= registrationEventType;
            deadlineEventType ??= registrationEventType;
            semesterStartType ??= registrationEventType;
            semesterEndType ??= registrationEventType;

            // Registration period
            if (academicYear.RegistrationStartDate.HasValue && academicYear.RegistrationEndDate.HasValue)
            {
                systemEvents.Add(new AcademicCalendarEvent
                {
                    Title = "Registration Period",
                    Description = "Official registration period for the academic year",
                    StartDateTime = academicYear.RegistrationStartDate.Value,
                    EndDateTime = academicYear.RegistrationEndDate.Value,
                    IsAllDay = true,
                    EventTypeId = registrationEventType.Id,
                    AcademicYearId = academicYearId,
                    IsSystemEvent = true,
                    IsPublished = true,
                    Color = registrationEventType.DefaultColor,
                    CreatedBy = "System",
                    CreatedAt = DateTime.Now
                });
            }

            // Final exam period
            if (academicYear.FinalExamStartDate.HasValue && academicYear.FinalExamEndDate.HasValue)
            {
                systemEvents.Add(new AcademicCalendarEvent
                {
                    Title = "Final Examination Period",
                    Description = "Final examination period for the academic year",
                    StartDateTime = academicYear.FinalExamStartDate.Value,
                    EndDateTime = academicYear.FinalExamEndDate.Value,
                    IsAllDay = true,
                    EventTypeId = examEventType.Id,
                    AcademicYearId = academicYearId,
                    IsSystemEvent = true,
                    IsPublished = true,
                    Color = examEventType.DefaultColor,
                    CreatedBy = "System",
                    CreatedAt = DateTime.Now
                });
            }

            // Grade submission period
            if (academicYear.GradeSubmissionStartDate.HasValue && academicYear.GradeSubmissionEndDate.HasValue)
            {
                systemEvents.Add(new AcademicCalendarEvent
                {
                    Title = "Grade Submission Period",
                    Description = "Period for instructors to submit final grades",
                    StartDateTime = academicYear.GradeSubmissionStartDate.Value,
                    EndDateTime = academicYear.GradeSubmissionEndDate.Value,
                    IsAllDay = true,
                    EventTypeId = deadlineEventType.Id,
                    AcademicYearId = academicYearId,
                    IsSystemEvent = true,
                    IsPublished = true,
                    Color = deadlineEventType.DefaultColor,
                    CreatedBy = "System",
                    CreatedAt = DateTime.Now
                });
            }

            // Academic year start and end
            systemEvents.Add(new AcademicCalendarEvent
            {
                Title = "Academic Year Begins",
                Description = $"Start of the {academicYear.YearValue} academic year",
                StartDateTime = academicYear.StartDate,
                IsAllDay = true,
                EventTypeId = semesterStartType.Id,
                AcademicYearId = academicYearId,
                IsSystemEvent = true,
                IsPublished = true,
                Color = semesterStartType.DefaultColor,
                CreatedBy = "System",
                CreatedAt = DateTime.Now
            });

            systemEvents.Add(new AcademicCalendarEvent
            {
                Title = "Academic Year Ends",
                Description = $"End of the {academicYear.YearValue} academic year",
                StartDateTime = academicYear.EndDate,
                IsAllDay = true,
                EventTypeId = semesterEndType.Id,
                AcademicYearId = academicYearId,
                IsSystemEvent = true,
                IsPublished = true,
                Color = semesterEndType.DefaultColor,
                CreatedBy = "System",
                CreatedAt = DateTime.Now
            });

            // Add all system events to the database
            await _context.AcademicCalendarEvents.AddRangeAsync(systemEvents);
            await _context.SaveChangesAsync();
        }
    }
}