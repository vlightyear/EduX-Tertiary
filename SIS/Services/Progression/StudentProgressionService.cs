using Microsoft.EntityFrameworkCore;
using SIS.Data;
using SIS.Enums;
using SIS.Models.Admin;
using SIS.Models.Registration;
using SIS.Models.Results;
using SIS.Models.StudentApplication;
using SIS.Models.StudentResults;
using SIS.Services.Progression;
using System.Text.Json;

namespace SIS.Services
{
    public class StudentProgressionService : IStudentProgressionService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<StudentProgressionService> _logger;

        public StudentProgressionService(
            ApplicationDbContext context,
            ILogger<StudentProgressionService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<List<GradeConfiguration>> GetGradeConfigurationAsync(
            int? schoolId = null,
            int? academicYearId = null)
        {
            // 1️⃣ Try filtered configs first
            var filteredQuery = _context.GradeConfigurations.AsNoTracking()
                .Where(g => g.IsActive);

            if (schoolId.HasValue && academicYearId.HasValue)
            {
                filteredQuery = filteredQuery.Where(g => g.SchoolId == schoolId);
                filteredQuery = filteredQuery.Where(g => g.AcademicYearId == academicYearId);
            }
            else if (academicYearId.HasValue)
            {
                //filteredQuery = filteredQuery.Where(g => g.SchoolId == schoolId);
                filteredQuery = filteredQuery.Where(g => g.AcademicYearId == academicYearId);
            }

            var filteredConfigs = await filteredQuery
                    .OrderByDescending(g => g.MinScore)
                    .ToListAsync();

            if (filteredConfigs.Any())
                return filteredConfigs;

            var configs = await _context.GradeConfigurations
                .AsNoTracking()
                .Where(g => g.IsActive
                    && g.SchoolId == null)
                .OrderByDescending(g => g.MinScore)
                .ToListAsync();
            return configs;
        }

        public async Task<ProgressionResult> ValidateProgressionAsync(int studentId, int currentAcademicYearId)
        {
            var result = new ProgressionResult();

            try
            {
                // Simple queries without complex includes to avoid conflicts
                var student = await _context.Students
                    .AsNoTracking()
                    .FirstOrDefaultAsync(s => s.Id == studentId);

                if (student == null)
                {
                    result.Errors.Add("Student not found");
                    return result;
                }

                // Check outstanding fees
                if (student.OutstandingFees > 0)
                {
                    result.Errors.Add($"Outstanding fees of {student.OutstandingFees:C2} must be cleared first");
                    return result;
                }

                // Check if current academic year exists
                var currentAcademicYear = await _context.AcademicYears
                    .AsNoTracking()
                    .FirstOrDefaultAsync(ay => ay.YearId == currentAcademicYearId);

                if (currentAcademicYear == null)
                {
                    result.Errors.Add("Current academic year not found");
                    return result;
                }

                // Check if NextAcademicYearId is configured
                if (!currentAcademicYear.NextAcademicYearId.HasValue)
                {
                    result.Errors.Add($"Next academic year has not been configured for {currentAcademicYear.YearValue}. Please contact the academic office.");
                    return result;
                }

                // Verify next academic year exists
                var nextAcademicYearExists = await _context.AcademicYears
                    .AsNoTracking()
                    .AnyAsync(ay => ay.YearId == currentAcademicYear.NextAcademicYearId.Value);

                if (!nextAcademicYearExists)
                {
                    result.Errors.Add("Next academic year configuration is invalid");
                    return result;
                }

                // Check for published results
                var hasPublishedResults = await _context.StudentCourseResults
                    .AsNoTracking()
                    .AnyAsync(r => r.StudentId == studentId &&
                                  r.AcademicYearId == currentAcademicYearId &&
                                  r.Status == Status.Published);

                if (!hasPublishedResults)
                {
                    result.Errors.Add("No published results found. Please wait for results to be published.");
                    return result;
                }

                result.Success = result.Errors.Count == 0;
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating progression for student {StudentId}", studentId);
                result.Errors.Add("An error occurred during validation");
                return result;
            }
        }

        public async Task<ProgressionResult> ExecuteProgressionAsync(int studentId, int currentAcademicYearId, string userId)
        {
            var result = new ProgressionResult();

            // Validate first (outside transaction)
            var validationResult = await ValidateProgressionAsync(studentId, currentAcademicYearId);
            if (!validationResult.Success)
            {
                return validationResult;
            }

            // Use execution strategy for transaction
            var strategy = _context.Database.CreateExecutionStrategy();

            return await strategy.ExecuteAsync(async () =>
            {
                // Start transaction inside execution strategy
                using var transaction = await _context.Database.BeginTransactionAsync();

                try
                {
                    // Load student with relationships
                    var student = await _context.Students
                        .Include(s => s.Programme)
                        .Include(s => s.AcademicYear)
                            .ThenInclude(ay => ay.NextAcademicYear)
                        .FirstOrDefaultAsync(s => s.Id == studentId);

                    if (student == null)
                    {
                        result.Errors.Add("Student not found");
                        return result;
                    }

                    // Get current academic year
                    var currentAcademicYear = await _context.AcademicYears
                        .Include(ay => ay.NextAcademicYear)
                        .FirstOrDefaultAsync(ay => ay.YearId == currentAcademicYearId);

                    if (currentAcademicYear?.NextAcademicYear == null)
                    {
                        result.Errors.Add("Next academic year configuration is missing");
                        return result;
                    }

                    // Get student results for current year
                    var studentResults = await _context.StudentCourseResults
                        .Include(r => r.Course)
                        .Where(r => r.StudentId == studentId &&
                                   r.AcademicYearId == currentAcademicYearId &&
                                   r.Status == Status.Published)
                        .ToListAsync();

                    if (!studentResults.Any())
                    {
                        result.Errors.Add("No published results found for progression");
                        return result;
                    }

                    // Calculate GPA and failed courses
                    decimal yearGpaPoints = 0;
                    int yearCreditsAttempted = 0;
                    int totalFailedCourses = 0;
                    var failedCourseIds = new List<int>();

                    foreach (var courseResult in studentResults)
                    {
                        yearGpaPoints += courseResult.GradePoints * courseResult.Credits;
                        yearCreditsAttempted += courseResult.Credits;

                        if (!courseResult.IsPassed)
                        {
                            totalFailedCourses++;
                            failedCourseIds.Add(courseResult.CourseId);
                        }
                    }

                    decimal yearGPA = yearCreditsAttempted > 0 ? yearGpaPoints / yearCreditsAttempted : 0;

                    // Get applicable progression rule
                    var progressionRule = await GetApplicableProgressionRuleAsync(student, totalFailedCourses);
                    string progressionAction = progressionRule?.Action ?? "Invalid";

                    if (progressionAction == "Invalid")
                    {
                        result.Errors.Add("No valid progression rule found for your performance");
                        return result;
                    }

                    result.Action = progressionAction;

                    // Determine next academic year and semester
                    var nextAcademicYear = currentAcademicYear.NextAcademicYear;
                    int nextYearOfStudy = student.StudentCurrentYear ?? 1;
                    int nextSemester = student.CurrentYearPeriodId ?? 1;

                    // Calculate progression based on action and academic type
                    CalculateNextYearAndSemester(
                        student,
                        progressionAction,
                        currentAcademicYear,
                        nextAcademicYear,
                        ref nextYearOfStudy,
                        ref nextSemester);

                    // Create carryover courses for failed courses
                    await CreateCarryoverCoursesAsync(student, failedCourseIds, progressionAction, userId, studentResults);

                    // Create performance archive
                    await CreatePerformanceArchiveAsync(
                        student,
                        currentAcademicYearId,
                        studentResults,
                        yearGPA,
                        totalFailedCourses,
                        progressionAction,
                        userId);

                    // Update student record
                    UpdateStudentRecord(student, nextAcademicYear, nextYearOfStudy, nextSemester, progressionAction);

                    // Archive student results (change status instead of deleting)
                    foreach (var courseResult in studentResults)
                    {
                        courseResult.Status = Status.Archived;
                    }

                    // Remove old examinable courses if they exist
                    var oldExaminableCourses = await _context.StudentExaminableCourses
                        .Where(sec => sec.StudentId == studentId)
                        .ToListAsync();

                    if (oldExaminableCourses.Any())
                    {
                        _context.StudentExaminableCourses.RemoveRange(oldExaminableCourses);
                    }

                    // Save all changes
                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    // Set success result
                    result.Success = true;
                    result.Message = GetProgressionMessage(progressionAction, student.Programme.IsSemesterBased);
                    result.NextAcademicYearId = nextAcademicYear.YearId;
                    result.NextYearOfStudy = nextYearOfStudy;
                    result.NextSemester = nextSemester;

                    _logger.LogInformation(
                        "Student {StudentId} progressed with action {Action} to Year {Year}, Semester {Semester}",
                        studentId, progressionAction, nextYearOfStudy, nextSemester);

                    return result;
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    _logger.LogError(ex, "Error executing progression for student {StudentId}", studentId);
                    result.Success = false;
                    result.Errors.Add("An error occurred while processing progression");
                    return result;
                }
            });
        }

        #region Helper Methods

        private void CalculateNextYearAndSemester(
            Student student,
            string progressionAction,
            AcademicYear currentAcademicYear,
            AcademicYear nextAcademicYear,
            ref int nextYearOfStudy,
            ref int nextSemester)
        {
            bool isSemesterBased = student.Programme.IsSemesterBased;
            int currentYearOfStudy = student.StudentCurrentYear ?? 1;
            int currentSemester = student.CurrentYearPeriodId ?? 1;

            switch (progressionAction)
            {
                case "Proceed":
                case "ProceedWithRepeat":
                case "ProceedOnProbation":
                    if (isSemesterBased)
                    {
                        // For semester-based programmes
                        if (currentSemester == 1)
                        {
                            // Move to semester 2 of the same year
                            nextYearOfStudy = currentYearOfStudy;
                            nextSemester = 2;
                        }
                        else
                        {
                            // Move to semester 1 of next year
                            nextYearOfStudy = currentYearOfStudy + 1;
                            nextSemester = 1;
                        }
                    }
                    else
                    {
                        // For annual programmes
                        nextYearOfStudy = currentYearOfStudy + 1;
                        nextSemester = 1;
                    }
                    break;

                case "RepeatYear":
                    // Stay in the same year, semester 1
                    nextYearOfStudy = currentYearOfStudy;
                    nextSemester = 1;
                    break;

                case "RepeatSemester":
                    if (isSemesterBased)
                    {
                        // Stay in current semester
                        nextYearOfStudy = currentYearOfStudy;
                        nextSemester = currentSemester;
                    }
                    else
                    {
                        // For annual, treat as repeat year
                        nextYearOfStudy = currentYearOfStudy;
                        nextSemester = 1;
                    }
                    break;

                case "Exclude":
                case "Withdraw":
                    // Stay in current position
                    nextYearOfStudy = currentYearOfStudy;
                    nextSemester = currentSemester;
                    break;
            }
        }

        private void UpdateStudentRecord(
            Student student,
            AcademicYear nextAcademicYear,
            int nextYearOfStudy,
            int nextSemester,
            string progressionAction)
        {
            // Update academic year (use NextAcademicYearId from the chain)
            student.AcademicYearId = nextAcademicYear.YearId;
            student.StudentCurrentYear = nextYearOfStudy;
            student.CurrentYearPeriodId = nextSemester;

            // Update registration status based on action
            switch (progressionAction)
            {
                case "Proceed":
                case "ProceedWithRepeat":
                    student.RegistrationStatus = Status.Unregistered;
                    student.IsRegistered = false;
                    student.RegistrationDate = null;
                    break;

                case "ProceedOnProbation":
                    student.RegistrationStatus = Status.AcademicProbation;
                    student.IsRegistered = false;
                    student.RegistrationDate = null;
                    break;

                case "RepeatYear":
                case "RepeatSemester":
                    student.RegistrationStatus = Status.Unregistered;
                    student.IsRegistered = false;
                    student.RegistrationDate = null;
                    break;

                case "Exclude":
                    student.RegistrationStatus = Status.Excluded;
                    student.IsRegistered = false;
                    student.RegistrationDate = null;
                    break;

                case "Withdraw":
                    student.RegistrationStatus = Status.Withdrawn;
                    student.IsRegistered = false;
                    student.RegistrationDate = null;
                    break;
            }
        }
        
        /*public async Task<ProgressionRule?> GetApplicableProgressionRuleAsync(
            Student student,
            int totalFailedCourses,
            int? semester = null,
            int? attempt = null)
        {
            var studentSchoolId = await _context.Students.AsNoTracking()
                .Where(s => s.Id == student.Id)
                .Include(s => s.Programme)
                    .ThenInclude(p => p.Department)
                        .ThenInclude(d => d.School)
                .Select(s => s.Programme.Department.School.Id)
                .FirstOrDefaultAsync();

            IQueryable<ProgressionRule> baseQuery = _context.ProgressionRules.AsNoTracking()
                .Where(r => r.IsActive &&
                            r.PercentFailedOfCourseLoad >= totalFailedCourses);

            // Apply optional filters
            if (semester.HasValue)
                baseQuery = baseQuery.Where(r => r.Semester == semester.Value);

            if (attempt.HasValue)
                baseQuery = baseQuery.Where(r => r.Attempt == attempt.Value);

            ProgressionRule? progressionRule = null;

            // Try school-specific rule
            if (studentSchoolId > 0)
            {
                progressionRule = await baseQuery
                    .Where(r => r.SchoolId == studentSchoolId)
                    .OrderBy(r => r.PercentFailedOfCourseLoad)
                    .FirstOrDefaultAsync();
            }

            // Fall back to global rule
            if (progressionRule == null)
            {
                progressionRule = await baseQuery
                    .Where(r => r.SchoolId == null)
                    .OrderBy(r => r.PercentFailedOfCourseLoad)
                    .FirstOrDefaultAsync();
            }

            return progressionRule;
        }*/

        public async Task<ProgressionRule?> GetApplicableProgressionRuleAsync(
            Student student,
            int failedPercentage,  // Renamed for clarity - this is a percentage (0-100), not a count
            int? period = null,
            int? attempt = null)
        {
            var studentSchoolId = await _context.Students.AsNoTracking()
                .Where(s => s.Id == student.Id)
                .Include(s => s.Programme)
                    .ThenInclude(p => p.Department)
                        .ThenInclude(d => d.School)
                .Select(s => s.Programme.Department.School.Id)
                .FirstOrDefaultAsync();

            // Find rules where student's failure percentage meets or exceeds the rule's threshold
            IQueryable<ProgressionRule> baseQuery = _context.ProgressionRules.AsNoTracking()
                .Where(r => r.IsActive &&
                            failedPercentage >= r.PercentFailedOfCourseLoad && r.Attempt == attempt);  // FIXED: reversed condition

            // Apply optional filters
            if (period.HasValue)
                baseQuery = baseQuery.Where(r => r.AcademicPeriodId == period.Value);

            if (attempt.HasValue)
                baseQuery = baseQuery.Where(r => r.Attempt == attempt.Value);

            ProgressionRule? progressionRule = null;

            // Try school-specific rule first
            if (studentSchoolId > 0)
            {
                progressionRule = await baseQuery
                    .Where(r => r.SchoolId == studentSchoolId)
                    .OrderByDescending(r => r.PercentFailedOfCourseLoad)  // FIXED: Get highest threshold that applies
                    .FirstOrDefaultAsync();
            }

            // Fall back to global rule if no school-specific rule found
            if (progressionRule == null)
            {
                progressionRule = await baseQuery
                    .Where(r => r.SchoolId == null)
                    .OrderByDescending(r => r.PercentFailedOfCourseLoad)  // FIXED: Get highest threshold that applies
                    .FirstOrDefaultAsync();
            }

            return progressionRule;
        }

        private async Task CreateCarryoverCoursesAsync(
            Student student,
            List<int> failedCourseIds,
            string progressionAction,
            string userId,
            List<StudentCourseResult> failedResults)
        {
            var actionsWithCarryover = new[] { "ProceedWithRepeat", "RepeatYear", "RepeatSemester", "ProceedOnProbation" };

            if (!actionsWithCarryover.Contains(progressionAction) || !failedCourseIds.Any())
            {
                return;
            }

            var carryoverCourses = new List<StudentCarryoverCourse>();

            foreach (var failedCourseId in failedCourseIds)
            {
                // Check if carryover already exists
                var existingCarryover = await _context.StudentCarryoverCourses.AsNoTracking()
                    .FirstOrDefaultAsync(scc => scc.StudentId == student.Id &&
                                               scc.CourseId == failedCourseId &&
                                               scc.IsActive);

                if (existingCarryover == null)
                {
                    var failedResult = failedResults.First(r => r.CourseId == failedCourseId);

                    var carryover = new StudentCarryoverCourse
                    {
                        StudentId = student.Id,
                        CourseId = failedCourseId,
                        OriginalAcademicYearId = failedResult.AcademicYearId,
                        OriginalSemester = failedResult.Semester,
                        Reason = "Failed",
                        IsActive = true,
                        CarryoverDate = DateTime.Now,
                        Notes = $"Failed during {progressionAction} - carried over from academic year {student.AcademicYear?.YearValue}",
                        CreatedAt = DateTime.Now,
                        CreatedBy = userId
                    };

                    carryoverCourses.Add(carryover);
                }
            }

            if (carryoverCourses.Any())
            {
                _context.StudentCarryoverCourses.AddRange(carryoverCourses);
                _logger.LogInformation(
                    "Created {Count} carryover courses for student {StudentId}",
                    carryoverCourses.Count, student.Id);
            }
        }

        private async Task CreatePerformanceArchiveAsync(
            Student student,
            int currentAcademicYearId,
            List<StudentCourseResult> studentResults,
            decimal yearGPA,
            int totalFailedCourses,
            string progressionAction,
            string userId)
        {
            var archive = new StudentAcademicPerformanceArchive
            {
                StudentId = student.Id,
                StudentNumber = student.StudentId_Number,
                ProgrammeId = student.ProgrammeId,
                AcademicYearId = currentAcademicYearId,
                YearOfStudy = student.StudentCurrentYear ?? 1,
                TotalCoursesTaken = studentResults.Count,
                CoursesPassed = studentResults.Count - totalFailedCourses,
                CoursesFailed = totalFailedCourses,
                OverallGrade = CalculateOverallGrade(yearGPA),
                GPA = yearGPA,
                ProgressionStatus = progressionAction,
                Remarks = GetProgressionRemarks(
                    progressionAction,
                    totalFailedCourses,
                    student.Programme.IsSemesterBased,
                    student.CurrentYearPeriodId),
                CourseResults = JsonSerializer.Serialize(new
                {
                    Courses = studentResults.Select(r => new
                    {
                        CourseId = r.CourseId,
                        CourseCode = r.Course.CourseCode,
                        CourseName = r.Course.CourseName,
                        NormalizedTotal = r.NormalizedTotal,
                        GradeLetter = r.GradeLetter,
                        IsPassed = r.IsPassed,
                        Status = r.Status.ToString()
                    }),
                    YearGPA = yearGPA,
                    TotalFailedCourses = totalFailedCourses,
                    ProgrammeType = student.Programme.IsSemesterBased ? "Semester" : "Annual",
                    CurrentSemester = student.CurrentYearPeriodId
                }),
                CreatedBy = userId,
                CreatedAt = DateTime.Now
            };

            _context.StudentAcademicPerformanceArchives.Add(archive);
        }

        private string GetProgressionMessage(string progressionAction, bool isSemesterBased) => progressionAction switch
        {
            "Proceed" => isSemesterBased ? "Successfully progressed to next semester/year" : "Successfully progressed to next academic year",
            "ProceedWithRepeat" => isSemesterBased ? "Progressed to next semester/year with units to repeat" : "Progressed to next year with units to repeat",
            "ProceedOnProbation" => isSemesterBased ? "Progressed to next semester/year on academic probation" : "Progressed to next year on academic probation",
            "RepeatYear" => "Set to repeat current academic year",
            "RepeatSemester" => "Set to repeat current semester",
            "Exclude" => "Academic exclusion processed - please contact academic office",
            "Withdraw" => "Withdrawal processed - please contact academic office",
            _ => "Progression status updated"
        };

        private string GetProgressionRemarks(string progressionAction, int failedCourses, bool isSemesterBased, int? currentSemester) => progressionAction switch
        {
            "Proceed" => isSemesterBased ?
                $"Clear pass - proceed to {(currentSemester == 1 ? "semester 2" : "next academic year")}" :
                "Clear pass - proceed to next academic year",
            "ProceedWithRepeat" => isSemesterBased ?
                $"Proceed to {(currentSemester == 1 ? "semester 2" : "next academic year")} with {failedCourses} failed units to repeat" :
                $"Proceed to next year with {failedCourses} failed units to repeat",
            "ProceedOnProbation" => isSemesterBased ?
                $"Proceed to {(currentSemester == 1 ? "semester 2" : "next academic year")} on academic probation - must improve performance" :
                "Proceed to next year on academic probation - must improve performance",
            "RepeatYear" => "Must repeat current academic year",
            "RepeatSemester" => "Must repeat current semester",
            "Exclude" => "Academic exclusion due to poor performance",
            "Withdraw" => "Withdrawal recommended based on academic performance",
            _ => "Contact academic advisor for guidance"
        };

        private string CalculateOverallGrade(decimal gpa)
        {
            if (gpa >= 4.0m) return "A";
            if (gpa >= 3.5m) return "B+";
            if (gpa >= 3.0m) return "B";
            if (gpa >= 2.5m) return "C+";
            if (gpa >= 2.0m) return "C";
            if (gpa >= 1.5m) return "D+";
            if (gpa >= 1.0m) return "D";
            return "F";
        }

        #endregion
    }
}