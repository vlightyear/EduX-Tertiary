using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.Web.CodeGeneration.Design;
using SIS.Data;
using SIS.Enums;
using SIS.Models.Admin;
using SIS.Models.Registration;
using SIS.Models.Reports;
using SIS.Models.StudentApplication;
using SIS.Services.Progression;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text.Json;

namespace SIS.Services.Reports
{
    public class SenateReportService : ISenateReportService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<SenateReportService> _logger;
        private readonly IStudentProgressionService _studentProgressionService;

        public SenateReportService(ApplicationDbContext context, ILogger<SenateReportService> logger, IStudentProgressionService studentProgressionService)
        {
            _context = context;
            _logger = logger;
            _studentProgressionService = studentProgressionService;
        }

        /** View optimized method **/
        public async Task<ProgrammeGradingOverview> GetProgrammeGradingOverviewAsync(
            int programmeId,
            int academicYearId,
            int semester,
            int yearOfStudy)
        {
            // Get programme details
            var programme = await _context.Programmes
                .AsNoTracking()
                .Include(p => p.Department)
                    .ThenInclude(d => d.School)
                .Include(p => p.ModeOfStudy)
                .FirstOrDefaultAsync(p => p.Id == programmeId);

            if (programme == null)
            {
                throw new Exception("Programme not found");
            }

            // Get academic year
            var academicYear = await _context.AcademicYears
                .AsNoTracking()
                .FirstOrDefaultAsync(ay => ay.YearId == academicYearId);

            if (academicYear == null)
            {
                throw new Exception("Academic year not found");
            }

            // **OPTIMIZATION: Single query to get all student results from view**
            var studentResults = await _context.Set<StudentResultView>()
                .FromSqlRaw(@"
                    WITH RankedResults AS
                    (
                        SELECT *,
                               ROW_NUMBER() OVER
                               (
                                   PARTITION BY StudentId_Number, CourseCode
                                   ORDER BY Attempt DESC
                               ) AS rn
                        FROM VW_StudentResults
                        WHERE ProgrammeId = {0}
                          AND AcademicYearId = {1}
                          AND Semester = {2}
                          AND YearOfStudy = {3}
                    )
                    SELECT *
                    FROM RankedResults
                    WHERE rn = 1",
                    programmeId,
                    academicYearId,
                    semester,
                    yearOfStudy)
                .AsNoTracking()
                .ToListAsync();

            if (!studentResults.Any())
            {
                _logger.LogWarning($"No student results found for programme {programmeId}");
                return new ProgrammeGradingOverview
                {
                    ProgrammeName = programme.Name,
                    ProgrammeId = programme.Id,
                    ModeOfStudy = programme.ModeOfStudy?.ModeName ?? "Unknown",
                    AcademicYear = academicYear.YearValue,
                    Semester = semester,
                    Courses = new List<CourseGradingOverview>()
                };
            }

            // Group results by course
            var courseGroups = studentResults
                .GroupBy(r => r.CourseCode)
                .OrderBy(g => g.Key)
                .ToList();

            var courseGradingData = new List<CourseGradingOverview>();

            foreach (var courseGroup in courseGroups)
            {
                var courseCode = courseGroup.Key;
                var courseResults = courseGroup.ToList();
                var firstResult = courseResults.First();

                // Initialize grade distribution
                var gradeDistribution = new Dictionary<string, int>
                {
                    { "A+", 0 }, { "A", 0 }, { "B+", 0 }, { "B", 0 },
                    { "C+", 0 }, { "C", 0 }, { "D+", 0 }, { "D", 0 },
                    { "EXP", 0 }, { "NE/INC", 0 }, { "DEF", 0 }, { "P", 0 }, { "F", 0 }
                };

                int totalPassed = 0;
                int totalFailed = 0;

                // Process each student result for this course
                foreach (var result in courseResults)
                {
                    string gradeLetter = result.GradeLetter;

                    // Handle special cases
                    if (gradeLetter == "NE")
                    {
                        gradeDistribution["NE/INC"]++;
                        continue; // Don't count as pass or fail
                    }
                    else if (gradeLetter == "DEF")
                    {
                        gradeDistribution["DEF"]++;
                        totalFailed++; // DEF is NOT a clear pass
                        continue;
                    }
                    else if (gradeLetter == "EXP" || gradeLetter == "SUP")
                    {
                        gradeDistribution["EXP"]++;
                        totalFailed++; // EXP/SUP is NOT a clear pass
                        continue;
                    }

                    // Update grade distribution for normal grades
                    if (gradeDistribution.ContainsKey(gradeLetter))
                    {
                        gradeDistribution[gradeLetter]++;
                    }
                    else
                    {
                        // Fallback for any unexpected grades
                        if (!gradeDistribution.ContainsKey("F"))
                            gradeDistribution["F"] = 0;
                        gradeDistribution["F"]++;
                    }

                    // Determine pass/fail based on IsPassingGrade from view
                    if (result.IsPassingGrade == 1)
                    {
                        totalPassed++;
                    }
                    else
                    {
                        totalFailed++;
                    }
                }

                // Calculate pass rate
                int totalStudents = totalPassed + totalFailed;
                double passRate = totalStudents > 0
                    ? Math.Round((double)totalPassed / totalStudents * 100, 1)
                    : 0;

                courseGradingData.Add(new CourseGradingOverview
                {
                    CourseNo = courseCode,
                    CourseName = firstResult.CourseName,
                    GradeDistribution = gradeDistribution,
                    TotalPassed = totalPassed,
                    TotalFailed = totalFailed,
                    PassRate = (decimal)passRate
                });
            }

            return new ProgrammeGradingOverview
            {
                ProgrammeName = programme.Name,
                ProgrammeId = programme.Id,
                ModeOfStudy = programme.ModeOfStudy?.ModeName ?? "Unknown",
                AcademicYear = academicYear.YearValue,
                Semester = semester,
                Courses = courseGradingData
            };
        }

        /* Old previous method
         * public async Task<ProgrammeGradingOverview> GetProgrammeGradingOverviewAsync(
            int programmeId,
            int academicYearId,
            int semester,
            int yearOfStudy)
        {
            // Get programme details
            var programme = await _context.Programmes
                .AsNoTracking()
                .Include(p => p.Department)
                .ThenInclude(d => d.School)
                .Include(p => p.ModeOfStudy)
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == programmeId);

            if (programme == null)
            {
                throw new Exception("Programme not found");
            }

            // Get academic year
            var academicYear = await _context.AcademicYears
                .AsNoTracking()
                .FirstOrDefaultAsync(ay => ay.YearId == academicYearId);

            if (academicYear == null)
            {
                throw new Exception("Academic year not found");
            }

            // Get courses for this programme, semester, and year of study
            var courses = await _context.Courses.AsNoTracking()
                .Where(c => c.ProgrammeID == programmeId &&
                           c.SemesterTaken == semester &&
                           c.YearTaken == yearOfStudy)
                .OrderBy(c => c.CourseCode)
                .AsNoTracking()
                .ToListAsync();

            // Get grade configurations
            var gradeConfigs = await _studentProgressionService.GetGradeConfigurationAsync(programme.Department.SchoolId, academicYearId);

            var courseGradingData = new List<CourseGradingOverview>();

            foreach (var course in courses)
            {
                // Get enrolled students
                var enrolledStudents = await _context.StudentExaminableCourses
                    .AsNoTracking()
                    .Where(sec => sec.CourseId == course.Id &&
                                 sec.AcademicYearId == academicYearId &&
                                 sec.Semester == semester)
                    .Select(sec => sec.StudentId)
                    .Distinct()
                    .ToListAsync();

                if (enrolledStudents.Count == 0)
                {
                    continue;
                }

                // Get approved batch
                var approvedBatch = await _context.ResultSubmissionBatches
                    .AsNoTracking()
                    .Where(rsb => rsb.CourseId == course.Id &&
                                 rsb.AcademicYearId == academicYearId &&
                                 rsb.Semester == semester &&
                                 (rsb.ApprovalStatus == WorkflowStatus.Approved || rsb.ApprovalStatus == WorkflowStatus.Published))
                    .OrderByDescending(rsb => rsb.CreatedAt)
                    .FirstOrDefaultAsync();

                if (approvedBatch == null)
                {
                    continue;
                }

                // Initialize grade distribution
                var gradeDistribution = new Dictionary<string, int>
                {
                    { "A+", 0 }, { "A", 0 }, { "B+", 0 }, { "B", 0 },
                    { "C+", 0 }, { "C", 0 }, { "D+", 0 }, { "D", 0 },
                    { "EXP", 0 }, { "NE/INC", 0 }, { "DEF", 0 }, { "P", 0 }, { "F", 0 }
                };

                int totalPassed = 0;
                int totalFailed = 0;

                // Process each enrolled student
                foreach (var studentId in enrolledStudents)
                {
                    // Get assessment scores from approved batches
                    var assessmentScores = await _context.StudentAssessmentScores
                        .AsNoTracking()
                        .Where(s =>
                            s.StudentId == studentId &&
                            s.IsActive &&
                            s.ResultSubmissionBatch.CourseId == course.Id &&
                            s.ResultSubmissionBatch.AcademicYearId == academicYearId &&
                            s.ResultSubmissionBatch.Semester == semester &&
                            (s.ResultSubmissionBatch.ApprovalStatus == WorkflowStatus.Approved || s.ResultSubmissionBatch.ApprovalStatus == WorkflowStatus.Published)
                        )
                        .GroupBy(s => s.AssessmentId)
                        .Select(g => g.OrderByDescending(x => x.Attempt).First())
                        .ToListAsync();

                    // Determine normal grade
                    string gradeLetter = "F";
                    if (assessmentScores.Count == 0)
                    {
                        gradeDistribution["NE/INC"]++;
                        gradeLetter = "-";
                        continue;
                    }
                    else if (!assessmentScores.Any(score => score.AssessmentId == 27))
                    {
                        gradeDistribution["DEF"]++;
                        gradeLetter = "-";
                        continue;
                    }

                    // Calculate total score (capped at 100)
                    decimal totalScore = Math.Round(Math.Min(assessmentScores.Sum(s => s.Score), 100));

                    // Check for special grades first
                    *//*bool hasDeferredExam = assessmentScores.Any(s => s.Grade?.ToUpper() == "DEF");
                    bool hasSupplementaryExam = assessmentScores.Any(s =>
                        s.Grade?.ToUpper() == "SUP" || s.Grade?.ToUpper() == "EXP");*//*

                    if (false)
                    {
                        gradeDistribution["DEF"]++;
                        totalFailed++; // DEF is NOT a clear pass
                    }
                    else if (false)
                    {
                        gradeDistribution["EXP"]++;
                        totalFailed++; // EXP is NOT a clear pass
                    }
                    else
                    {
                        if (assessmentScores.Any(s => s.Attempt == 2))
                        {
                            if(totalScore >= (decimal)assessmentScores.FirstOrDefault().Course.PassMark)
                            {
                                gradeLetter = "P";
                            }
                            else
                            {
                                gradeLetter = "F";
                            }
                        }
                        else
                        {
                            var gradeConfig = gradeConfigs.FirstOrDefault(g => totalScore >= (decimal)g.MinScore);
                            gradeLetter = gradeConfig?.GradeLetter ?? "F";
                        }

                        // Update grade distribution
                        if (gradeDistribution.ContainsKey(gradeLetter))
                        {
                            gradeDistribution[gradeLetter]++;
                        }

                        // CLEAR PASS: Only grades A+, A, B+, B, C+, C, D+, D, P
                        // Score must be >= pass mark AND grade must not be F
                        bool isPassed = totalScore >= (decimal)course.PassMark && gradeLetter != "F";

                        if (isPassed)
                        {
                            totalPassed++; // This is a CLEAR PASS
                        }
                        else
                        {
                            totalFailed++;
                        }
                    }
                }

                // Calculate pass rate
                int totalStudents = totalPassed + totalFailed;
                double passRate = totalStudents > 0 ? Math.Round((double)totalPassed / totalStudents * 100, 1) : 0;

                courseGradingData.Add(new CourseGradingOverview
                {
                    CourseNo = course.CourseCode,
                    CourseName = course.CourseName,
                    GradeDistribution = gradeDistribution,
                    TotalPassed = totalPassed,
                    TotalFailed = totalFailed,
                    PassRate = (decimal)passRate
                });
            }

            return new ProgrammeGradingOverview
            {
                ProgrammeName = programme.Name,
                ProgrammeId = programme.Id,
                ModeOfStudy = programme.ModeOfStudy?.ModeName ?? "Unknown",
                AcademicYear = academicYear.YearValue,
                Semester = semester,
                Courses = courseGradingData
            };
        }*/

        /*public async Task<ProgrammeGradingOverview> GetProgrammeGradingOverviewAsync(
            int programmeId,
            int academicYearId,
            int semester,
            int yearOfStudy)
        {
            try
            {
                _logger.LogInformation($"Getting grading overview for programme {programmeId}, year {academicYearId}, semester {semester}");

                // Get programme details
                var programme = await _context.Programmes
                    .Include(p => p.ModeOfStudy)
                    //.Include(p => p.AcademicYear)
                    .FirstOrDefaultAsync(p => p.Id == programmeId);

                if (programme == null)
                {
                    throw new InvalidOperationException($"Programme {programmeId} not found");
                }

                var academicYear = await _context.AcademicYears.FindAsync(academicYearId);
                if (academicYear == null)
                {
                    throw new InvalidOperationException($"Academic year {academicYearId} not found");
                }

                // Get all courses for this programme
                var programmeCourses = await _context.Courses
                    .Where(c => c.ProgrammeID == programmeId && c.YearTaken == yearOfStudy && c.SemesterTaken == semester)
                    .ToListAsync();

                if (!programmeCourses.Any())
                {
                    _logger.LogWarning($"No courses found for programme {programmeId}");
                    return new ProgrammeGradingOverview
                    {
                        ProgrammeId = programmeId,
                        ProgrammeName = programme.Name,
                        //ProgrammeCode = programme.ProgrammeCode,
                        ModeOfStudy = programme.ModeOfStudy?.ModeName ?? "N/A",
                        AcademicYear = academicYear.YearValue,
                        Semester = semester,
                        Courses = new List<CourseGradingOverview>()
                    };
                }

                // Get all students enrolled in these courses
                var courseGrades = new List<CourseGradingOverview>();

                foreach (var courseId in programmeCourses)
                {
                    var course = await _context.Courses.FindAsync(courseId.Id);
                    if (course == null) continue;

                    // Get all students enrolled in this course for this academic year and semester
                    var enrolledStudents = await _context.StudentExaminableCourses
                        .Where(sec =>
                            sec.CourseId == courseId.Id &&
                            sec.AcademicYearId == academicYearId &&
                            sec.Semester == semester)
                        .Select(sec => sec.StudentId)
                        .Distinct()
                        .ToListAsync();

                    if (!enrolledStudents.Any()) continue;

                    // Initialize grade counts
                    var gradeDistribution = new Dictionary<string, int>
                        {
                            { "A+", 0 }, { "A", 0 }, { "B+", 0 }, { "B", 0 },
                            { "C+", 0 }, { "C", 0 }, { "D+", 0 }, { "D", 0 },
                            { "EXP", 0 }, { "NE/INC", 0 }, { "DEF", 0 },
                            { "P", 0 }, { "F", 0 }
                        };

                    int totalPassed = 0;
                    int totalFailed = 0;

                    // Get grade configurations for reference
                    var gradeConfigs = await _context.GradeConfigurations
                        .Where(g => g.IsActive)
                        .OrderByDescending(g => g.MinScore)
                        .ToListAsync();

                    double passMark = course.PassMark;

                    // Calculate grades for each student
                    foreach (var studentId in enrolledStudents)
                    {
                        // Check if there's a published batch for this course
                        var hasPublishedBatch = await _context.ResultSubmissionBatches
                            .AnyAsync(rsb =>
                                rsb.CourseId == courseId.Id &&
                                rsb.AcademicYearId == academicYearId &&
                                rsb.Semester == semester &&
                                rsb.ApprovalStatus == WorkflowStatus.Approved);

                        if (!hasPublishedBatch)
                        {
                            // No published results - count as NE/INC (Not Entered/Incomplete)
                            gradeDistribution["NE/INC"]++;
                            continue;
                        }

                        // Get assessment scores from published batches only
                        var assessmentScores = await _context.StudentAssessmentScores
                            .Where(s =>
                                s.StudentId == studentId &&
                                s.CourseId == courseId.Id &&
                                s.AcademicYearId == academicYearId &&
                                s.Semester == semester &&
                                s.IsActive &&
                                _context.ResultSubmissionBatches.Any(rsb =>
                                    rsb.Id == s.rsbId &&
                                    (rsb.ApprovalStatus == WorkflowStatus.Approved || rsb.ApprovalStatus == WorkflowStatus.Published)))
                            .ToListAsync();

                        if (!assessmentScores.Any())
                        {
                            // Student enrolled but no scores - NE/INC
                            gradeDistribution["NE/INC"]++;
                            continue;
                        }

                        // Calculate total score (direct sum of pre-weighted scores, capped at 100)
                        decimal totalScore = Math.Min(assessmentScores.Sum(s => s.Score), 100);

                        // Determine grade
                        var gradeConfig = gradeConfigs.FirstOrDefault(g => totalScore >= (decimal)g.MinScore);

                        if (gradeConfig != null)
                        {
                            string gradeLetter = gradeConfig.GradeLetter;

                            // Map grade letter to display format
                            if (gradeDistribution.ContainsKey(gradeLetter))
                            {
                                gradeDistribution[gradeLetter]++;
                            }
                            else
                            {
                                // Handle any unmapped grades
                                gradeDistribution["F"]++;
                            }

                            // Check pass/fail
                            if (totalScore >= (decimal)passMark)
                            {
                                totalPassed++;
                            }
                            else
                            {
                                totalFailed++;
                            }
                        }
                        else
                        {
                            // No grade found - treat as F
                            gradeDistribution["F"]++;
                            totalFailed++;
                        }
                    }

                    // Calculate pass rate
                    int totalStudents = totalPassed + totalFailed;
                    decimal passRate = totalStudents > 0 ? Math.Round((decimal)totalPassed / totalStudents * 100, 0) : 0;

                    courseGrades.Add(new CourseGradingOverview
                    {
                        CourseNo = course.CourseCode,
                        CourseName = course.CourseName,
                        GradeDistribution = gradeDistribution,
                        TotalFailed = totalFailed,
                        TotalPassed = totalPassed,
                        PassRate = passRate
                    });
                }

                return new ProgrammeGradingOverview
                {
                    ProgrammeId = programmeId,
                    ProgrammeName = programme.Name,
                    //ProgrammeCode = programme.ProgrammeCode,
                    ModeOfStudy = programme.ModeOfStudy?.ModeName ?? "N/A",
                    AcademicYear = academicYear.YearValue,
                    Semester = semester,
                    Courses = courseGrades.OrderBy(c => c.CourseNo).ToList()
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting grading overview for programme {programmeId}");
                throw;
            }
        }*/

        public async Task<SenateReportViewModel> GenerateReportAsync(SenateReportFilters filters)
        {
            try
            {
                _logger.LogInformation($"Generating senate report with filters: {JsonSerializer.Serialize(filters)}");

                var viewModel = new SenateReportViewModel
                {
                    AppliedFilters = filters,
                    ReportLevel = filters.ReportLevel,
                    GeneratedAt = DateTime.Now
                };

                // Build the base query
                var baseQuery = await BuildBaseQueryAsync(filters);

                // Generate report based on level
                switch (filters.ReportLevel.ToLower())
                {
                    case "school":
                        await GenerateSchoolLevelReportAsync(viewModel, baseQuery, filters);
                        break;
                    case "department":
                        await GenerateDepartmentLevelReportAsync(viewModel, baseQuery, filters);
                        break;
                    case "programme":
                        await GenerateProgrammeLevelReportAsync(viewModel, baseQuery, filters);
                        break;
                    default:
                        await GenerateSchoolLevelReportAsync(viewModel, baseQuery, filters);
                        break;
                }

                // Calculate totals
                CalculateReportTotals(viewModel);

                // Generate breadcrumbs
                await GenerateBreadcrumbsAsync(viewModel, filters);

                return viewModel;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating senate report");
                throw;
            }
        }

        private async Task<List<StudentCourseData>> BuildBaseQueryAsync(SenateReportFilters filters)
        {
            // Query to get students with their courses and approved assessment scores
            var query = from sec in _context.StudentExaminableCourses
                        join student in _context.Students on sec.StudentId equals student.Id
                        join programme in _context.Programmes on student.ProgrammeId equals programme.Id
                        join department in _context.Departments on programme.DepartmentId equals department.Id
                        join school in _context.Schools on department.SchoolId equals school.Id
                        join modeOfStudy in _context.ModesOfStudy on student.ModeOfStudyId equals modeOfStudy.ModeId
                        join academicYear in _context.AcademicYears on student.AcademicYearId equals academicYear.YearId
                        join course in _context.Courses on sec.CourseId equals course.Id
                        select new
                        {
                            StudentExaminableCourse = sec,
                            Student = student,
                            Programme = programme,
                            Department = department,
                            School = school,
                            ModeOfStudy = modeOfStudy,
                            AcademicYear = academicYear,
                            Course = course
                        };

            // Apply filters
            if (filters.AcademicYearId.HasValue)
                query = query.Where(x => x.StudentExaminableCourse.AcademicYearId == filters.AcademicYearId.Value);

            if (filters.AcademicPeriod.HasValue)
                query = query.Where(x => x.StudentExaminableCourse.YearPeriodId == filters.AcademicPeriod.Value);

            if (filters.SchoolId.HasValue)
                query = query.Where(x => x.School.Id == filters.SchoolId.Value);

            if (filters.DepartmentId.HasValue)
                query = query.Where(x => x.Department.Id == filters.DepartmentId.Value);

            if (filters.ProgrammeId.HasValue)
                query = query.Where(x => x.Programme.Id == filters.ProgrammeId.Value);

            if (filters.ModeOfStudyId.HasValue)
                query = query.Where(x => x.ModeOfStudy.ModeId == filters.ModeOfStudyId.Value);

            if (filters.YearOfStudy.HasValue)
                query = query.Where(x => x.Student.StudentCurrentYear == filters.YearOfStudy.Value);

            var baseData = await query.AsNoTracking().ToListAsync();

            // Now get assessment scores with batch approval status
            var result = new List<StudentCourseData>();

            foreach (var item in baseData)
            {
                var assessmentScores = await (
                    from sas in _context.StudentAssessmentScores
                    join rsb in _context.ResultSubmissionBatches on sas.rsbId equals rsb.Id
                    where sas.StudentId == item.Student.Id
                       && sas.CourseId == item.Course.Id
                       && sas.AcademicYearId == item.StudentExaminableCourse.AcademicYearId
                       && sas.YearPeriodId == item.StudentExaminableCourse.YearPeriodId
                       && sas.IsActive
                       && (rsb.ApprovalStatus == WorkflowStatus.Approved || rsb.ApprovalStatus == WorkflowStatus.Published)
                    group sas by sas.AssessmentId into g
                    select g
                        .OrderByDescending(x => x.Attempt)
                        .Select(x => new AssessmentScoreDetail
                        {
                            AssessmentId = x.AssessmentId,
                            Score = x.Score,
                            MaxScore = x.MaxScore,
                            WeightPercentage = x.WeightPercentage,
                            Attempt = x.Attempt
                        })
                        .FirstOrDefault()
                ).AsNoTracking().ToListAsync();



                result.Add(new StudentCourseData
                {
                    StudentExaminableCourse = item.StudentExaminableCourse,
                    Student = item.Student,
                    Programme = item.Programme,
                    Department = item.Department,
                    School = item.School,
                    Course = item.Course,
                    AssessmentScores = assessmentScores
                });
            }

            return result;
        }

        private async Task GenerateSchoolLevelReportAsync(SenateReportViewModel viewModel,
            List<StudentCourseData> baseData, SenateReportFilters filters)
        {
            var schoolGroups = baseData
                .GroupBy(x => new {
                    SchoolId = x.School.Id,
                    SchoolName = x.School.Name
                })
                .ToList();

            foreach (var schoolGroup in schoolGroups)
            {
                var studentProgressions = await CalculateStudentProgressionsAsync(
                    schoolGroup.GroupBy(x => x.Student.Id).ToList());

                var summary = new EntityProgressionSummary
                {
                    EntityId = schoolGroup.Key.SchoolId,
                    EntityName = schoolGroup.Key.SchoolName,
                    EntityType = "School",
                    TotalStudents = studentProgressions.Count,
                    StudentsWithResults = studentProgressions.Count(sp => sp.HasPublishedResults),
                    StudentsWithoutResults = studentProgressions.Count(sp => !sp.HasPublishedResults),
                    ProgressionRuleCounts = studentProgressions
                        .Where(sp => sp.HasPublishedResults)
                        .GroupBy(sp => sp.ProgressionRule)
                        .ToDictionary(g => g.Key, g => g.Count()),
                    AverageGPA = studentProgressions.Where(sp => sp.HasPublishedResults).Any()
                        ? studentProgressions.Where(sp => sp.HasPublishedResults).Average(sp => sp.GPA)
                        : 0,
                    CanDrillDown = true,
                    DrillDownLevel = "Department"
                };

                summary.PassRate = summary.StudentsWithResults > 0
                    ? (decimal)(summary.ProgressionRuleCounts.GetValueOrDefault("Proceed", 0) +
                               summary.ProgressionRuleCounts.GetValueOrDefault("ProceedWithRepeat", 0) +
                               summary.ProgressionRuleCounts.GetValueOrDefault("ProceedOnProbation", 0)) /
                               summary.StudentsWithResults * 100
                    : 0;

                viewModel.Summaries.Add(summary);
            }
        }

        private async Task GenerateDepartmentLevelReportAsync(SenateReportViewModel viewModel,
            List<StudentCourseData> baseData, SenateReportFilters filters)
        {
            var departmentGroups = baseData
                .GroupBy(x => new {
                    DepartmentId = x.Department.Id,
                    DepartmentName = x.Department.Name,
                    SchoolName = x.School.Name
                })
                .ToList();

            foreach (var deptGroup in departmentGroups)
            {
                var studentProgressions = await CalculateStudentProgressionsAsync(
                    deptGroup.GroupBy(x => x.Student.Id).ToList());

                var summary = new EntityProgressionSummary
                {
                    EntityId = deptGroup.Key.DepartmentId,
                    EntityName = $"{deptGroup.Key.DepartmentName} ({deptGroup.Key.SchoolName})",
                    EntityType = "Department",
                    TotalStudents = studentProgressions.Count,
                    StudentsWithResults = studentProgressions.Count(sp => sp.HasPublishedResults),
                    StudentsWithoutResults = studentProgressions.Count(sp => !sp.HasPublishedResults),
                    ProgressionRuleCounts = studentProgressions
                        .Where(sp => sp.HasPublishedResults)
                        .GroupBy(sp => sp.ProgressionRule)
                        .ToDictionary(g => g.Key, g => g.Count()),
                    AverageGPA = studentProgressions.Where(sp => sp.HasPublishedResults).Any()
                        ? studentProgressions.Where(sp => sp.HasPublishedResults).Average(sp => sp.GPA)
                        : 0,
                    CanDrillDown = true,
                    DrillDownLevel = "Programme"
                };

                summary.PassRate = summary.StudentsWithResults > 0
                    ? (decimal)(summary.ProgressionRuleCounts.GetValueOrDefault("Proceed", 0) +
                               summary.ProgressionRuleCounts.GetValueOrDefault("ProceedWithRepeat", 0) +
                               summary.ProgressionRuleCounts.GetValueOrDefault("ProceedOnProbation", 0)) /
                               summary.StudentsWithResults * 100
                    : 0;

                viewModel.Summaries.Add(summary);
            }
        }

        private async Task GenerateProgrammeLevelReportAsync(SenateReportViewModel viewModel,
            List<StudentCourseData> baseData, SenateReportFilters filters)
        {
            var programmeGroups = baseData
                .GroupBy(x => new {
                    ProgrammeId = x.Programme.Id,
                    ProgrammeName = x.Programme.Name,
                    DepartmentName = x.Department.Name,
                    SchoolName = x.School.Name
                })
                .ToList();

            foreach (var progGroup in programmeGroups)
            {
                var studentProgressions = await CalculateStudentProgressionsAsync(
                    progGroup.GroupBy(x => x.Student.Id).ToList());

                var summary = new EntityProgressionSummary
                {
                    EntityId = progGroup.Key.ProgrammeId,
                    EntityName = $"{progGroup.Key.ProgrammeName} ({progGroup.Key.DepartmentName})",
                    EntityType = "Programme",
                    TotalStudents = studentProgressions.Count,
                    StudentsWithResults = studentProgressions.Count(sp => sp.HasPublishedResults),
                    StudentsWithoutResults = studentProgressions.Count(sp => !sp.HasPublishedResults),
                    ProgressionRuleCounts = studentProgressions
                        .Where(sp => sp.HasPublishedResults)
                        .GroupBy(sp => sp.ProgressionRule)
                        .ToDictionary(g => g.Key, g => g.Count()),
                    AverageGPA = studentProgressions.Where(sp => sp.HasPublishedResults).Any()
                        ? studentProgressions.Where(sp => sp.HasPublishedResults).Average(sp => sp.GPA)
                        : 0,
                    CanDrillDown = false,
                    DrillDownLevel = ""
                };

                summary.PassRate = summary.StudentsWithResults > 0
                    ? (decimal)(summary.ProgressionRuleCounts.GetValueOrDefault("Proceed", 0) +
                               summary.ProgressionRuleCounts.GetValueOrDefault("ProceedWithRepeat", 0) +
                               summary.ProgressionRuleCounts.GetValueOrDefault("ProceedOnProbation", 0)) /
                               summary.StudentsWithResults * 100
                    : 0;

                viewModel.Summaries.Add(summary);
            }
        }

        private async Task<List<StudentProgressionData>> CalculateStudentProgressionsAsync(
            List<IGrouping<int, StudentCourseData>> studentGroups)
        {
            var progressions = new List<StudentProgressionData>();
            //var grades = await _context.GradeConfigurations.Where(g => g.IsActive).OrderBy(g => g.MinScore).ToListAsync();
            var grades = await _studentProgressionService.GetGradeConfigurationAsync(studentGroups.First()?.First()?.Student?.Programme?.Department?.SchoolId, studentGroups.First()?.First()?.Student?.AcademicYearId);


            foreach (var studentGroup in studentGroups)
            {
                var student = studentGroup.First().Student;
                var courses = studentGroup.ToList();

                var progression = new StudentProgressionData
                {
                    StudentId = student.Id,
                    StudentNumber = student.StudentId_Number,
                    StudentName = student.FullName,
                    TotalCourses = courses.Count,
                    HasPublishedResults = courses.Any(c => c.AssessmentScores.Any())
                };

                if (progression.HasPublishedResults)
                {
                    decimal yearGpaPoints = 0;
                    int yearCreditsAttempted = 0;
                    int totalFailedCourses = 0;

                    foreach (var courseData in courses)
                    {
                        try
                        {
                            if (!courseData.AssessmentScores.Any())
                                continue;

                            // Calculate weighted total from actual assessment scores
                            decimal totalScore = Math.Round(CalculateWeightedTotal(courseData.AssessmentScores));
                            bool isPassed = totalScore >= (decimal)courseData.Course.PassMark;

                            if (!isPassed) totalFailedCourses++;

                            string grade = DetermineGrade(totalScore, grades, courseData);
                            yearGpaPoints += GetGpaPoints(grade, grades) * 3; // Assuming 3 credits per course
                            yearCreditsAttempted += 3;
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning($"Error calculating scores for student {student.Id}, course {courseData.Course.Id}: {ex.Message}");
                        }
                    }

                    progression.GPA = yearCreditsAttempted > 0 ? yearGpaPoints / yearCreditsAttempted : 0;
                    progression.FailedCourses = totalFailedCourses;

                    // Get progression rule
                    var progressionRule = await _studentProgressionService.GetApplicableProgressionRuleAsync(
                        student,
                        (int)Math.Floor(((double)totalFailedCourses / progression.TotalCourses) * 100)
                    );
                    progression.ProgressionRule = progressionRule?.Action ?? "Repeat Semester";
                }
                else
                {
                    progression.ProgressionRule = "Results Pending";
                }

                progressions.Add(progression);
            }

            return progressions;
        }

        private decimal CalculateWeightedTotal(List<AssessmentScoreDetail> assessmentScores)
        {
            if (!assessmentScores.Any()) return 0;

            decimal weightedTotal = 0;
            decimal totalWeight = 0;

            foreach (var assessment in assessmentScores)
            {
                // Calculate percentage score for this assessment
                /*decimal percentageScore = assessment.MaxScore > 0
                    ? (assessment.Score / assessment.MaxScore) * 100
                    : 0;

                // Apply weight
                weightedTotal += (percentageScore * assessment.WeightPercentage / 100);*/
                //Using score directly as results are already weighted.
                totalWeight += assessment.Score;
            }

            // Normalize to 100%
            decimal normalizedTotal = totalWeight; //Using score directly > 0 ? (weightedTotal / totalWeight) * 100 : 0;
            return Math.Min(normalizedTotal, 100);
        }

        private string DetermineGrade(decimal totalScore, List<GradeConfiguration> grades, StudentCourseData? scd = null)
        {
            if (scd != null)
                if (!scd.AssessmentScores.Any(s => s.AssessmentId == 27))
                {
                    return "DEF";
                }
                else if (scd.AssessmentScores.Any(s => s.Attempt == 2))
                {
                    if (totalScore >= (decimal)scd.Course.PassMark)
                    {
                        return "P";
                    }
                    else
                    {
                        return "F";
                    }
                }
                else
                {
                    foreach (var grade in grades)
                    {
                        if (totalScore >= grade.MinScore && totalScore <= grade.MaxScore)
                        {
                            return grade.GradeLetter;
                        }
                    }
                }
                    
            return "F";
        }

        private decimal GetGpaPoints(string gradeLetter, List<GradeConfiguration> grades)
        {
            var grade = grades.FirstOrDefault(g => g.GradeLetter == gradeLetter);
            return grade?.GPAValue ?? 0;
        }

        private void CalculateReportTotals(SenateReportViewModel viewModel)
        {
            viewModel.Totals = new ReportTotals
            {
                TotalStudents = viewModel.Summaries.Sum(s => s.TotalStudents),
                TotalWithResults = viewModel.Summaries.Sum(s => s.StudentsWithResults),
                TotalWithoutResults = viewModel.Summaries.Sum(s => s.StudentsWithoutResults),
                OverallProgressionCounts = new Dictionary<string, int>(),
                OverallAverageGPA = viewModel.Summaries.Any() ? viewModel.Summaries.Average(s => s.AverageGPA) : 0
            };

            // Aggregate progression counts
            foreach (var summary in viewModel.Summaries)
            {
                foreach (var kvp in summary.ProgressionRuleCounts)
                {
                    if (viewModel.Totals.OverallProgressionCounts.ContainsKey(kvp.Key))
                        viewModel.Totals.OverallProgressionCounts[kvp.Key] += kvp.Value;
                    else
                        viewModel.Totals.OverallProgressionCounts[kvp.Key] = kvp.Value;
                }
            }

            // Calculate overall pass rate
            var totalPassed = viewModel.Totals.OverallProgressionCounts.GetValueOrDefault("Proceed", 0) +
                             viewModel.Totals.OverallProgressionCounts.GetValueOrDefault("ProceedWithRepeat", 0) +
                             viewModel.Totals.OverallProgressionCounts.GetValueOrDefault("ProceedOnProbation", 0);

            viewModel.Totals.OverallPassRate = viewModel.Totals.TotalWithResults > 0
                ? (decimal)totalPassed / viewModel.Totals.TotalWithResults * 100
                : 0;
        }

        private async Task GenerateBreadcrumbsAsync(SenateReportViewModel viewModel, SenateReportFilters filters)
        {
            viewModel.Breadcrumbs.Add(new BreadcrumbItem { Text = "Senate Reports", Url = "/Reports/Senate", IsActive = false });

            if (filters.SchoolId.HasValue)
            {
                var school = await _context.Schools.FindAsync(filters.SchoolId.Value);
                viewModel.Breadcrumbs.Add(new BreadcrumbItem { Text = school?.Name ?? "School", Url = "#", IsActive = false });
            }

            if (filters.DepartmentId.HasValue)
            {
                var department = await _context.Departments.FindAsync(filters.DepartmentId.Value);
                viewModel.Breadcrumbs.Add(new BreadcrumbItem { Text = department?.Name ?? "Department", Url = "#", IsActive = false });
            }

            if (filters.ProgrammeId.HasValue)
            {
                var programme = await _context.Programmes.FindAsync(filters.ProgrammeId.Value);
                viewModel.Breadcrumbs.Add(new BreadcrumbItem { Text = programme?.Name ?? "Programme", Url = "#", IsActive = true });
            }
        }

        public async Task<SenateReportViewModel> GetFilterOptionsAsync()
        {
            return new SenateReportViewModel
            {
                AcademicYears = await _context.AcademicYears.AsNoTracking().Where(ay => ay.IsActive).OrderByDescending(ay => ay.YearValue).ToListAsync(),
                Schools = await _context.Schools.AsNoTracking().OrderBy(s => s.Name).ToListAsync(),
                ModesOfStudy = await _context.ModesOfStudy.AsNoTracking().OrderBy(m => m.ModeName).ToListAsync()
            };
        }

        public async Task<List<Department>> GetDepartmentsBySchoolAsync(int schoolId)
        {
            return await _context.Departments
                .AsNoTracking()
                .Where(d => d.SchoolId == schoolId)
                .OrderBy(d => d.Name)
                .ToListAsync();
        }

        public async Task<List<Programme>> GetProgrammesByDepartmentAsync(int departmentId)
        {
            return await _context.Programmes
                .AsNoTracking()
                .Where(p => p.DepartmentId == departmentId)
                .OrderBy(p => p.Name)
                .ToListAsync();
        }

        /*public async Task<List<StudentProgressionData>> GetEntityStudentDetailsAsync(int entityId, string entityType, SenateReportFilters filters)
        {
            try
            {
                _logger.LogInformation($"Getting student details for {entityType} {entityId}");

                var baseQuery = await BuildBaseQueryAsync(filters);

                // Filter by entity type
                List<StudentCourseData> filteredData = entityType.ToLower() switch
                {
                    "school" => baseQuery.Where(x => x.School.Id == entityId).ToList(),
                    "department" => baseQuery.Where(x => x.Department.Id == entityId).ToList(),
                    "programme" => baseQuery.Where(x => x.Programme.Id == entityId).ToList(),
                    _ => throw new ArgumentException($"Invalid entity type: {entityType}")
                };

                // Group by student and calculate progressions
                var studentGroups = filteredData.GroupBy(x => x.Student.Id).ToList();
                var studentProgressions = await CalculateStudentProgressionsAsync(studentGroups);

                return studentProgressions.OrderBy(sp => sp.StudentNumber).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting student details for {entityType} {entityId}");
                throw;
            }
        }

        public async Task<StudentProgressionDetailViewModel> GetStudentProgressionDetailAsync(int studentId, SenateReportFilters filters)
        {
            try
            {
                _logger.LogInformation($"Getting progression detail for student {studentId}");

                var baseQuery = await BuildBaseQueryAsync(filters);
                var studentCourses = baseQuery.Where(x => x.Student.Id == studentId).ToList();

                if (!studentCourses.Any())
                {
                    throw new InvalidOperationException($"No data found for student {studentId}");
                }

                var student = studentCourses.First().Student;
                //var grades = await _context.GradeConfigurations.Where(g => g.IsActive).OrderBy(g => g.MinScore).ToListAsync();
                var grades = await _studentProgressionService.GetGradeConfigurationAsync(student?.Programme?.Department?.SchoolId, student.AcademicYearId);

                decimal yearGpaPoints = 0;
                int yearCreditsAttempted = 0;
                int totalFailedCourses = 0;
                string progressionStr = null;

                var courseDetails = new List<CourseProgressionDetail>();

                foreach (var courseData in studentCourses)
                {
                    var courseDetail = new CourseProgressionDetail
                    {
                        CourseCode = courseData.Course.CourseCode,
                        CourseName = courseData.Course.CourseName,
                        Credits = 3, // Assuming 3 credits per course
                        Semester = courseData.StudentExaminableCourse.Semester
                    };

                    try
                    {
                        if (courseData.AssessmentScores.Any())
                        {
                            decimal totalScore = Math.Round(CalculateWeightedTotal(courseData.AssessmentScores));
                            string grade = DetermineGrade(totalScore, grades, courseData);
                            decimal gpaPoints = GetGpaPoints(grade, grades);

                            if (!courseData.AssessmentScores.Any(score => score.AssessmentId == 27))
                            {
                                progressionStr = "DEF";
                                grade = "NE";
                            }

                            courseDetail.TotalScore = totalScore;
                            courseDetail.Grade = grade;
                            courseDetail.GradePoints = gpaPoints;
                            courseDetail.Status = totalScore >= (decimal)courseData.Course.PassMark ? "Pass" : "Fail";
                            courseDetail.IsPassed = totalScore >= (decimal)courseData.Course.PassMark;

                            if (!courseDetail.IsPassed) totalFailedCourses++;

                            yearGpaPoints += gpaPoints * 3;
                            yearCreditsAttempted += 3;
                        }
                        else
                        {
                            courseDetail.Status = "No Results";
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning($"Error calculating scores for student {studentId}, course {courseData.Course.Id}: {ex.Message}");
                        courseDetail.Status = "Error";
                    }

                    courseDetails.Add(courseDetail);
                }

                var gpa = yearCreditsAttempted > 0 ? yearGpaPoints / yearCreditsAttempted : 0;
                var progressionRule = await _studentProgressionService.GetApplicableProgressionRuleAsync(
                    student,
                    (int)Math.Floor(((double)totalFailedCourses / studentCourses.Count) * 100)
                 );

                var detailViewModel = new StudentProgressionDetailViewModel
                {
                    StudentNumber = student.StudentId_Number,
                    StudentName = student.FullName,
                    Programme = studentCourses.First().Programme.Name,
                    School = studentCourses.First().School.Name,
                    Department = studentCourses.First().Department.Name,
                    YearOfStudy = student.StudentCurrentYear.Value,
                    Semester = filters.Semester ?? 0,
                    GPA = gpa,
                    FailedCourses = totalFailedCourses,
                    TotalCourses = studentCourses.Count,
                    ProgressionRule = progressionRule?.Action ?? "Repeat Semester",
                    AcademicStanding = DetermineAcademicStanding(gpa, totalFailedCourses),
                    Courses = courseDetails
                };

                return detailViewModel;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting progression detail for student {studentId}");
                throw;
            }
        }*/

        /** Optimised methods **/
        /*public async Task<List<StudentProgressionData>> GetEntityStudentDetailsAsync(int entityId, string entityType, SenateReportFilters filters)
        {
            try
            {
                _logger.LogInformation($"Getting student details for {entityType} {entityId}");

                // Build the WHERE clause based on entity type
                var whereClause = entityType.ToLower() switch
                {
                    "school" => "SchoolId = {0}",
                    "department" => "DepartmentId = {0}",
                    "programme" => "ProgrammeId = {0}",
                    _ => throw new ArgumentException($"Invalid entity type: {entityType}")
                };

                // Query the view once for all students
                var studentResults = await _context.Set<StudentResultView>()
                    .FromSqlRaw($@"
                        SELECT * FROM VW_StudentResults 
                        WHERE {whereClause}
                        AND AcademicYearId = {{1}} 
                        AND Semester = {{2}}
                        AND YearOfStudy = {{3}}",
                        entityId,
                        filters.AcademicYearId,
                        filters.Semester ?? 0,
                        filters.YearOfStudy ?? 0)
                    .ToListAsync();

                if (!studentResults.Any())
                {
                    _logger.LogWarning($"No student results found for {entityType} {entityId}");
                    return new List<StudentProgressionData>();
                }

                // Group by student
                var studentGroups = studentResults
                    .GroupBy(r => r.StudentId_Number)
                    .ToList();

                // **OPTIMIZATION: Fetch all students in ONE query - O(1) database calls**
                var studentNumbers = studentGroups.Select(g => g.Key).ToList();
                var studentsDict = await _context.Students
                    .AsNoTracking()
                    .Include(s => s.Programme)
                        .ThenInclude(p => p.Department)
                            .ThenInclude(d => d.School)
                    .Where(s => studentNumbers.Contains(s.StudentId_Number))
                    .ToDictionaryAsync(s => s.StudentId_Number);

                var studentProgressions = new List<StudentProgressionData>();

                foreach (var studentGroup in studentGroups)
                {
                    var studentCourses = studentGroup.ToList();
                    var firstRecord = studentCourses.First();

                    // O(1) dictionary lookup instead of database query
                    if (!studentsDict.TryGetValue(firstRecord.StudentId_Number, out var student))
                    {
                        _logger.LogWarning($"Student {firstRecord.StudentId_Number} not found in database");
                        continue;
                    }

                    int failedCourses = 0;
                    bool hasDeferredExams = false;
                    int? highestAttempt = null;
                    decimal totalGpaPoints = 0;
                    int totalCredits = 0;

                    foreach (var result in studentCourses)
                    {
                        // Track highest attempt
                        if (highestAttempt == null || result.Attempt > highestAttempt)
                            highestAttempt = result.Attempt;

                        // Check for deferred exams
                        if (result.GradeLetter == "NE")
                        {
                            hasDeferredExams = true;
                            continue;
                        }

                        // Calculate GPA (assuming 3 credits per course)
                        int credits = 3;
                        totalGpaPoints += (decimal)result.GPAValue * credits;
                        totalCredits += credits;

                        // Count failed courses
                        if (result.IsPassingGrade == 0 && result.Exam != null)
                        {
                            failedCourses++;
                        }
                    }

                    // Calculate progression
                    int totalCourses = studentCourses.Count;
                    int failedPercentage = totalCourses > 0
                        ? (int)Math.Floor((decimal)failedCourses / totalCourses * 100)
                        : 0;

                    var progressionRule = await GetStudentProgressionDetailAsync(
                        student.Id,
                        filters
                    );

                    decimal gpa = totalCredits > 0 ? Math.Round(totalGpaPoints / totalCredits, 2) : 0;

                    var progressionData = new StudentProgressionData
                    {
                        StudentId = student.Id,
                        StudentNumber = firstRecord.StudentId_Number,
                        StudentName = firstRecord.FullName,
                        Programme = firstRecord.Programme,
                        YearOfStudy = firstRecord.YearOfStudy,
                        GPA = gpa,
                        TotalCourses = totalCourses,
                        FailedCourses = failedCourses,
                        ProgressionStatus = hasDeferredExams ? "DEF" : (progressionRule?.ProgressionRule ?? "Repeat Semester"),
                        AcademicStanding = DetermineAcademicStanding(gpa, failedCourses),
                        StudentProgression = progressionRule
                    };

                    studentProgressions.Add(progressionData);
                }

                return studentProgressions.OrderBy(sp => sp.StudentNumber).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting student details for {entityType} {entityId}");
                throw;
            }
        }*/

        public async Task<List<StudentProgressionData>> GetEntityStudentDetailsAsync(int entityId, string entityType, SenateReportFilters filters, string? studentNumber = null)
        {
            try
            {
                _logger.LogInformation($"Getting student details for {entityType} {entityId}");

                /*// Build the WHERE clause based on entity type
                var whereClause = entityType.ToLower() switch
                {
                    "school" => "SchoolId = {0}",
                    "department" => "DepartmentId = {0}",
                    "programme" => "ProgrammeId = {0}",
                    _ => throw new ArgumentException($"Invalid entity type: {entityType}")
                };

                // Query the view once for all students
                List<StudentResultView> studentResults = new();
                if(studentNumber == null)
                {
                    studentResults = await _context.Set<StudentResultView>()
                    .FromSqlRaw($@"
                        SELECT * FROM VW_StudentResults 
                        WHERE {whereClause}
                        AND AcademicYearId = {{1}} 
                        AND Semester = {{2}}
                        AND YearOfStudy = {{3}}",
                        entityId,
                        filters.AcademicYearId,
                        filters.Semester ?? 0,
                        filters.YearOfStudy ?? 0)
                    .ToListAsync();
                }
                else
                {
                    studentResults = await _context.Set<StudentResultView>()
                    .FromSqlRaw($@"
                        SELECT * FROM VW_StudentResults 
                        WHERE {whereClause}
                        AND AcademicYearId = {{1}} 
                        AND Semester = {{2}}
                        AND YearOfStudy = {{3}}
                        AND StudentId_Number = {{4}}",
                        entityId,
                        filters.AcademicYearId,
                        filters.Semester ?? 0,
                        filters.YearOfStudy ?? 0,
                        studentNumber)
                    .ToListAsync();
                }*/

                // Build the WHERE clause based on entity type
                var whereClause = entityType.ToLower() switch
                {
                    "school" => "SchoolId = {0}",
                    "department" => "DepartmentId = {0}",
                    "programme" => "ProgrammeId = {0}",
                    _ => throw new ArgumentException($"Invalid entity type: {entityType}")
                };

                List<StudentResultView> studentResults;

                if (studentNumber == null)
                {
                    studentResults = await _context.Set<StudentResultView>()
                        .FromSqlRaw($@"
                            WITH RankedResults AS
                            (
                                SELECT *,
                                       ROW_NUMBER() OVER
                                       (
                                           PARTITION BY StudentId_Number, CourseCode
                                           ORDER BY Attempt DESC
                                       ) AS rn
                                FROM VW_StudentResults
                                WHERE {whereClause}
                                  AND AcademicYearId = {{1}}
                                  AND Semester = {{2}}
                                  AND YearOfStudy = {{3}}
                            )
                            SELECT *
                            FROM RankedResults
                            WHERE rn = 1",
                            entityId,
                            filters.AcademicYearId,
                            filters.AcademicPeriod ?? 0,
                            filters.YearOfStudy ?? 0)
                        .AsNoTracking()
                        .ToListAsync();
                }
                else
                {
                    studentResults = await _context.Set<StudentResultView>()
                        .FromSqlRaw($@"
                            WITH RankedResults AS
                            (
                                SELECT *,
                                       ROW_NUMBER() OVER
                                       (
                                           PARTITION BY StudentId_Number, CourseCode
                                           ORDER BY Attempt DESC
                                       ) AS rn
                                FROM VW_StudentResults
                                WHERE {whereClause}
                                  AND AcademicYearId = {{1}}
                                  AND Semester = {{2}}
                                  AND YearOfStudy = {{3}}
                                  AND StudentId_Number = {{4}}
                            )
                            SELECT *
                            FROM RankedResults
                            WHERE rn = 1",
                            entityId,
                            filters.AcademicYearId,
                            filters.AcademicPeriod ?? 0,
                            filters.YearOfStudy ?? 0,
                            studentNumber)
                        .AsNoTracking()
                        .ToListAsync();
                }

                if (!studentResults.Any())
                {
                    _logger.LogWarning($"No student results found for {entityType} {entityId}");
                    return new List<StudentProgressionData>();
                }

                // Group by student
                var studentGroups = studentResults
                    .GroupBy(r => r.StudentId_Number)
                    .ToList();

                // **OPTIMIZATION: Fetch all students in ONE query - O(1) database calls**
                var studentNumbers = studentGroups.Select(g => g.Key).ToList();
                var studentsDict = await _context.Students
                    .AsNoTracking()
                    .Include(s => s.Programme)
                        .ThenInclude(p => p.Department)
                            .ThenInclude(d => d.School)
                    .Where(s => studentNumbers.Contains(s.StudentId_Number))
                    .ToDictionaryAsync(s => s.StudentId_Number);

                var studentProgressions = new List<StudentProgressionData>();

                foreach (var studentGroup in studentGroups)
                {
                    var studentCourses = studentGroup.ToList();
                    var firstRecord = studentCourses.First();

                    // O(1) dictionary lookup instead of database query
                    if (!studentsDict.TryGetValue(firstRecord.StudentId_Number, out var student))
                    {
                        _logger.LogWarning($"Student {firstRecord.StudentId_Number} not found in database");
                        continue;
                    }

                    int failedCourses = 0;
                    bool hasDeferredExams = false;
                    int? highestAttempt = null;
                    decimal totalGpaPoints = 0;
                    int totalCredits = 0;

                    // **NEW: Collection to store failed course details**
                    var failedCoursesList = new List<FailedCourseInfo>();

                    foreach (var result in studentCourses)
                    {
                        // Track highest attempt
                        if (highestAttempt == null || result.Attempt > highestAttempt)
                            highestAttempt = result.Attempt;

                        // Check for deferred exams
                        if (result.GradeLetter == "NE")
                        {
                            hasDeferredExams = true;
                            continue;
                        }

                        // Calculate GPA (assuming 3 credits per course)
                        int credits = 3;
                        totalGpaPoints += (decimal)result.GPAValue * credits;
                        totalCredits += credits;

                        // Count failed courses and capture details
                        if (result.IsPassingGrade == 0 && result.Exam != null)
                        {
                            failedCourses++;

                            // **NEW: Add failed course details to collection**
                            failedCoursesList.Add(new FailedCourseInfo
                            {
                                CourseCode = result.CourseCode,
                                CourseName = result.CourseName,
                                GradeLetter = result.GradeLetter,
                                Marks = result.Exam,
                                Attempt = result.Attempt,
                                Credits = credits
                            });
                        }
                        else if(result.IsPassingGrade == 0 && result.Exam == null)
                        {
                            failedCoursesList.Add(new FailedCourseInfo
                            {
                                CourseCode = result.CourseCode,
                                CourseName = result.CourseName,
                                GradeLetter = result.GradeLetter,
                                Marks = result.Exam,
                                Attempt = result.Attempt,
                                Credits = credits
                            });
                        }
                    }

                    // Calculate progression
                    int totalCourses = studentCourses.Count;
                    int failedPercentage = totalCourses > 0
                        ? (int)Math.Floor((decimal)failedCourses / totalCourses * 100)
                        : 0;

                    var progressionRule = await GetStudentProgressionDetailAsync(
                        student.Id,
                        filters
                    );

                    decimal gpa = totalCredits > 0 ? Math.Round(totalGpaPoints / totalCredits, 2) : 0;

                    var progressionData = new StudentProgressionData
                    {
                        StudentId = student.Id,
                        StudentNumber = firstRecord.StudentId_Number,
                        StudentName = firstRecord.FullName,
                        Programme = firstRecord.Programme,
                        YearOfStudy = firstRecord.YearOfStudy,
                        GPA = gpa,
                        TotalCourses = totalCourses,
                        FailedCourses = failedCourses,
                        FailedCourseDetails = failedCoursesList, // **NEW: Add the collection**
                        ProgressionStatus = hasDeferredExams ? "DEF" : (progressionRule?.ProgressionRule ?? "Repeat Semester"),
                        AcademicStanding = DetermineAcademicStanding(gpa, failedCourses),
                        StudentProgression = progressionRule
                    };

                    studentProgressions.Add(progressionData);
                }

                return studentProgressions.OrderBy(sp => sp.StudentNumber).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting student details for {entityType} {entityId}");
                throw;
            }
        }

        public async Task<StudentProgressionDetailViewModel> GetStudentProgressionDetailAsync(int studentId, SenateReportFilters filters)
        {
            try
            {
                _logger.LogInformation($"Getting progression detail for student {studentId}");

                // Get student info first
                var student = await _context.Students
                    .AsNoTracking()
                    .Include(s => s.Programme)
                        .ThenInclude(p => p.Department)
                            .ThenInclude(d => d.School)
                    .FirstOrDefaultAsync(s => s.Id == studentId);

                if (student == null)
                {
                    throw new InvalidOperationException($"Student {studentId} not found");
                }

                // Query the view for this student's results
                var studentResults = await _context.Set<StudentResultView>()
                    .FromSqlRaw(@"
                        WITH RankedResults AS
                        (
                            SELECT *,
                                   ROW_NUMBER() OVER
                                   (
                                       PARTITION BY StudentId_Number, CourseCode
                                       ORDER BY Attempt DESC
                                   ) AS rn
                            FROM VW_StudentResults
                            WHERE StudentId_Number = {0}
                              AND AcademicYearId = {1}
                              AND Semester = {2}
                              AND YearOfStudy = {3}
                        )
                        SELECT *
                        FROM RankedResults
                        WHERE rn = 1",
                        student.StudentId_Number,
                        filters.AcademicYearId,
                        filters.AcademicPeriod ?? 0,
                        filters.YearOfStudy ?? student.StudentCurrentYear ?? 0)
                    .AsNoTracking()
                    .ToListAsync();

                if (!studentResults.Any())
                {
                    throw new InvalidOperationException($"No data found for student {studentId}");
                }

                decimal yearGpaPoints = 0;
                int yearCreditsAttempted = 0;
                int totalFailedCourses = 0;
                bool hasDeferredExams = false;
                int? highestAttempt = null;

                var courseDetails = new List<CourseProgressionDetail>();

                foreach (var result in studentResults)
                {
                    // Track highest attempt
                    if (highestAttempt == null || result.Attempt > highestAttempt)
                        highestAttempt = result.Attempt;

                    var courseDetail = new CourseProgressionDetail
                    {
                        CourseCode = result.CourseCode,
                        CourseName = result.CourseName,
                        Credits = 3, // Assuming 3 credits per course
                        Semester = result.Semester,
                        TotalScore = (decimal)result.TotalScore,
                        CaScore = result.CA.GetValueOrDefault().ToString(),
                        ExamScore = result.Exam.GetValueOrDefault().ToString(),
                        Grade = result.GradeLetter,
                        Status = result.Description,
                        GradePoints = (decimal)result.GPAValue
                    };

                    // Check for deferred exams
                    if (result.GradeLetter == "NE")
                    {
                        hasDeferredExams = true;
                        courseDetail.Status = "Deferred";
                        courseDetail.IsPassed = false;
                    }
                    else
                    {
                        courseDetail.Status = result.Description;
                        courseDetail.IsPassed = result.IsPassingGrade == 1;

                        if (!courseDetail.IsPassed)
                            totalFailedCourses++;

                        // Calculate GPA
                        yearGpaPoints += (decimal)result.GPAValue * 3;
                        yearCreditsAttempted += 3;
                    }

                    courseDetails.Add(courseDetail);
                }

                var gpa = yearCreditsAttempted > 0
                    ? Math.Round(yearGpaPoints / yearCreditsAttempted, 2)
                    : 0;

                // Calculate progression
                int totalCourses = studentResults.Count;
                int failedPercentage = totalCourses > 0
                    ? (int)Math.Floor((decimal)totalFailedCourses / totalCourses * 100)
                    : 0;

                var progressionRule = await _studentProgressionService.GetApplicableProgressionRuleAsync(
                    student,
                    failedPercentage,
                    filters.AcademicPeriod ?? 0,
                    highestAttempt
                );

                var detailViewModel = new StudentProgressionDetailViewModel
                {
                    StudentNumber = student.StudentId_Number,
                    StudentName = student.FullName,
                    Programme = student.Programme.Name,
                    School = student.Programme.Department.School.Name,
                    Department = student.Programme.Department.Name,
                    YearOfStudy = student.StudentCurrentYear ?? 0,
                    Semester = filters.AcademicPeriod ?? 0,
                    GPA = gpa,
                    FailedCourses = totalFailedCourses,
                    TotalCourses = totalCourses,
                    ProgressionRule = hasDeferredExams ? "DEF" : (progressionRule?.Action ?? "Repeat Semester"),
                    AcademicStanding = DetermineAcademicStanding(gpa, totalFailedCourses),
                    Courses = courseDetails.OrderBy(c => c.Semester).ThenBy(c => c.CourseCode).ToList()
                };

                return detailViewModel;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting progression detail for student {studentId}");
                throw;
            }
        }

        private string DetermineAcademicStanding(decimal gpa, int failedCourses)
        {
            if (failedCourses == 0 && gpa >= 3.5m)
                return "Dean's List";
            else if (failedCourses == 0 && gpa >= 3.0m)
                return "Good Standing";
            else if (failedCourses <= 2 && gpa >= 2.0m)
                return "Academic Probation";
            else if (failedCourses > 2 || gpa < 2.0m)
                return "At Risk";
            else
                return "Regular Standing";
        }

        public async Task<List<ResultSubmissionBatchSummary>> GetPendingBatchesForProgrammeAsync(
            int programmeId,
            int academicYearId,
            int semester)
        {
            try
            {
                _logger.LogInformation($"Getting pending batches for programme {programmeId}, year {academicYearId}, semester {semester}");

                // Get all courses for this programme
                var programmeCourses = await _context.Courses
                    .AsNoTracking()
                    .Where(c => c.ProgrammeID == programmeId)
                    .Select(c => c.Id)
                    .ToListAsync();

                if (!programmeCourses.Any())
                {
                    _logger.LogWarning($"No courses found for programme {programmeId}");
                    return new List<ResultSubmissionBatchSummary>();
                }

                // Get all batches for these courses
                var batches = await (
                    from rsb in _context.ResultSubmissionBatches
                    join course in _context.Courses on rsb.CourseId equals course.Id
                    join uploader in _context.Users on rsb.UploadedById equals uploader.Id
                    where programmeCourses.Contains(rsb.CourseId)
                       && rsb.AcademicYearId == academicYearId
                       && rsb.YearPeriodId == semester
                    orderby rsb.CourseId descending
                    select new ResultSubmissionBatchSummary
                    {
                        Id = rsb.Id,
                        CourseName = course.CourseName,
                        CourseCode = course.CourseCode,
                        AssessmentName = "Final Results",
                        SubmissionType = rsb.SubmissionType,
                        ApprovalStatus = rsb.ApprovalStatus,
                        ApprovalStatusText = rsb.ApprovalStatus.ToString(),
                        TotalRecords = rsb.TotalRecords,
                        UploadedAt = rsb.UploadedAt,
                        UploadedByName = uploader.FullName ?? uploader.UserName,
                        SubmittedForApprovalAt = rsb.SubmittedForApprovalAt,
                        ApprovedAt = rsb.ApprovedAt,
                        ApprovedByName = null,
                        CanPublish = rsb.ApprovalStatus == WorkflowStatus.Approved
                    }).AsNoTracking().ToListAsync();

                _logger.LogInformation($"Found {batches.Count} batches for programme {programmeId}");

                return batches;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting pending batches for programme {programmeId}");
                throw;
            }
        }

        public async Task<PublishBatchesResult> PublishBatchesAsync(
            List<int> batchIds,
            string approvedById)
        {
            var strategy = _context.Database.CreateExecutionStrategy();

            return await strategy.ExecuteAsync(async () =>
            {
                var result = new PublishBatchesResult { Success = true };
                var now = DateTime.Now;

                using var transaction = await _context.Database.BeginTransactionAsync();

                try
                {
                    _logger.LogInformation($"Publishing {batchIds.Count} batches by user {approvedById}");

                    var batches = await _context.ResultSubmissionBatches
                        .Where(b => batchIds.Contains(b.Id))
                        .ToListAsync();

                    if (!batches.Any())
                    {
                        result.Success = false;
                        result.Message = "No batches found with the provided IDs.";
                        return result;
                    }

                    foreach (var batch in batches)
                    {
                        try
                        {
                            // Validate batch can be published
                            if (batch.ApprovalStatus == WorkflowStatus.Published)
                            {
                                result.Errors.Add($"Batch {batch.Id} is already published.");
                                result.FailedCount++;
                                continue;
                            }

                            if (batch.ApprovalStatus != WorkflowStatus.Approved)
                            {
                                result.Errors.Add($"Batch {batch.Id} cannot be published from status {batch.ApprovalStatus}.");
                                result.FailedCount++;
                                continue;
                            }

                            batch.ApprovalStatus = WorkflowStatus.Published;
                            batch.ApprovedAt = now;
                            batch.ApprovedById = approvedById;
                            batch.UpdatedAt = now;
                            batch.UpdatedBy = approvedById;

                            _context.ResultSubmissionBatches.Update(batch);

                            result.PublishedCount++;
                            result.PublishedBatchIds.Add(batch.Id);

                            _logger.LogInformation($"Published batch {batch.Id}");
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, $"Error publishing batch {batch.Id}");
                            result.Errors.Add($"Batch {batch.Id}: {ex.Message}");
                            result.FailedCount++;
                        }
                    }

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    if (result.PublishedCount > 0)
                    {
                        result.Message = $"Successfully published {result.PublishedCount} batch(es).";
                        if (result.FailedCount > 0)
                            result.Message += $" {result.FailedCount} batch(es) failed.";
                    }
                    else
                    {
                        result.Success = false;
                        result.Message = "No batches were published.";
                    }

                    return result;
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    _logger.LogError(ex, "Error publishing batches");
                    return new PublishBatchesResult
                    {
                        Success = false,
                        Message = $"Error publishing batches: {ex.Message}"
                    };
                }
            });
        }

        public async Task<PublishBatchesResult> PublishAllProgrammeBatchesAsync(
            int programmeId,
            int academicYearId,
            int semester,
            string approvedById)
        {
            try
            {
                _logger.LogInformation($"Publishing all batches for programme {programmeId}, year {academicYearId}, semester {semester}");

                var pendingBatches = await GetPendingBatchesForProgrammeAsync(programmeId, academicYearId, semester);

                var publishableBatchIds = pendingBatches
                    .Where(b => b.CanPublish)
                    .Select(b => b.Id)
                    .ToList();

                if (!publishableBatchIds.Any())
                {
                    return new PublishBatchesResult
                    {
                        Success = false,
                        Message = "No publishable batches found for this programme."
                    };
                }

                return await PublishBatchesAsync(publishableBatchIds, approvedById);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error publishing all batches for programme {programmeId}");
                return new PublishBatchesResult
                {
                    Success = false,
                    Message = $"Error: {ex.Message}"
                };
            }
        }

        /*public async Task<PerformanceSummaryDto> GetPerformanceSummaryAsync(
            int programmeId,
            int academicYearId,
            int semester,
            int yearOfStudy)
        {
            try
            {
                _logger.LogInformation($"Getting performance summary for programme {programmeId}, year {academicYearId}, semester {semester}");

                var summary = new PerformanceSummaryDto();

                // Get all courses for this programme
                var programmeCourses = await _context.Courses
                    .AsNoTracking()
                    .Where(c => c.ProgrammeID == programmeId)
                    .Select(c => c.Id)
                    .ToListAsync();

                if (!programmeCourses.Any())
                {
                    _logger.LogWarning($"No courses found for programme {programmeId}");
                    return summary;
                }

                // Get all students enrolled in these courses for this academic year and semester
                var enrolledStudentIds = await _context.StudentExaminableCourses
                    .Include(sec => sec.Student)
                    .AsNoTracking()
                    .Where(sec =>
                        programmeCourses.Contains(sec.CourseId) &&
                        sec.AcademicYearId == academicYearId &&
                        sec.Semester == semester &&
                        sec.Student.StudentCurrentYear == yearOfStudy &&
                        sec.Semester == semester)
                    .Select(sec => sec.StudentId)
                    .Distinct()
                    .ToListAsync();

                if (!enrolledStudentIds.Any())
                {
                    _logger.LogWarning($"No enrolled students found for programme {programmeId}");
                    return summary;
                }

                summary.TotalStudents = enrolledStudentIds.Count;

                // Get grade configurations
                var programme = await _context.Programmes
                    .AsNoTracking()
                    .Include(p => p.Department)
                    .FirstOrDefaultAsync(p => p.Id == programmeId);

                var gradeConfigs = await _studentProgressionService.GetGradeConfigurationAsync(programme.Department.SchoolId, academicYearId);
                var grades = gradeConfigs; //await _studentProgressionService.GetGradeConfigurationAsync(programme.Department.SchoolId, academicYearId);

                // Process each student
                foreach (var studentId in enrolledStudentIds)
                {
                    var student = await _context.Students
                        .AsNoTracking()
                        .Include(s => s.Programme)
                            .ThenInclude(p => p.Department)
                                .ThenInclude(d => d.School)
                        .FirstOrDefaultAsync(s => s.Id == studentId);

                    if (student == null) continue;

                    var studentCourses = await _context.StudentExaminableCourses
                        .AsNoTracking()
                        .Include(sec => sec.Course)
                        .Where(sec =>
                            sec.StudentId == studentId &&
                            programmeCourses.Contains(sec.CourseId) &&
                            sec.AcademicYearId == academicYearId &&
                            sec.Semester == semester)
                        .ToListAsync();

                    if (!studentCourses.Any())
                        continue;

                    int failedCourses = 0;
                    bool hasDeferredExams = false;
                    bool hasSupplementaryExams = false;
                    int? highestAttemptAcrossCourses = null;

                    foreach (var studentCourse in studentCourses)
                    {
                        var course = studentCourse.Course;

                        var hasApprovedBatch = await _context.ResultSubmissionBatches.AnyAsync(rsb =>
                            rsb.CourseId == course.Id &&
                            rsb.AcademicYearId == academicYearId &&
                            rsb.Semester == semester &&
                            (rsb.ApprovalStatus == WorkflowStatus.Approved || rsb.ApprovalStatus == WorkflowStatus.Published));

                        if (!hasApprovedBatch)
                            continue;

                        var assessmentScores = await _context.StudentAssessmentScores
                            .AsNoTracking()
                            .Where(s =>
                                s.StudentId == studentId &&
                                s.IsActive &&
                                s.ResultSubmissionBatch.CourseId == course.Id &&
                                s.ResultSubmissionBatch.AcademicYearId == academicYearId &&
                                s.ResultSubmissionBatch.Semester == semester &&
                                (s.ResultSubmissionBatch.ApprovalStatus == WorkflowStatus.Approved || s.ResultSubmissionBatch.ApprovalStatus == WorkflowStatus.Published))
                            .GroupBy(s => s.AssessmentId)
                            .Select(g => g.OrderByDescending(x => x.Attempt).First())
                            .ToListAsync();

                        if (!assessmentScores.Any())
                            continue;

                        // Track highest attempt for progression rule
                        int courseAttempt = (int)assessmentScores.Max(s => s.Attempt);
                        if (highestAttemptAcrossCourses == null || courseAttempt > highestAttemptAcrossCourses)
                            highestAttemptAcrossCourses = courseAttempt;

                        decimal totalScore = Math.Round(Math.Min(assessmentScores.Sum(s => s.Score), 100));

                        // Resolve grading rules
                        string gradeLetter = "F";
                        if (!assessmentScores.Any(score => score.AssessmentId == 27))
                        {
                            hasDeferredExams = true;
                            gradeLetter = "NE";
                        }

                        if (assessmentScores.Any(s => s.Attempt == 2))
                        {
                            gradeLetter = totalScore >= (decimal)course.PassMark ? "P" : "F";
                        }
                        else
                        {
                            var gradeConfig = gradeConfigs
                                .FirstOrDefault(g => totalScore >= g.MinScore);
                            gradeLetter = gradeConfig?.GradeLetter ?? "F";
                        }

                        if ((totalScore < (decimal)course.PassMark || gradeLetter == "F") && assessmentScores.Any(score => score.AssessmentId == 27))
                        {
                            failedCourses++;
                        }
                    }

                    // -------------------------------------------
                    //  APPLY PROGRESSION RULES HERE
                    // -------------------------------------------
                    int totalCourses = studentCourses.Count;
                    int failedPercentage = (int)Math.Floor((decimal)failedCourses / totalCourses * 100);

                    var progressionRule = await _studentProgressionService.GetApplicableProgressionRuleAsync(
                        student,
                        failedPercentage,
                        semester,
                        highestAttemptAcrossCourses
                    );

                    if (hasDeferredExams)
                    {
                        summary.StudentsWithDeferredExams++;
                    }
                    else if (progressionRule != null)
                    {
                        switch (progressionRule.Action.ToLower())
                        {
                            case "proceed":
                                summary.StudentsWithClearPass++;
                                break;

                            case "proceed and repeat":
                                summary.StudentsWithProceedWithRepeats++;
                                break;

                            case "repeat semester":
                                summary.StudentsWithRepeatSemester++;
                                break;

                            case "sup":
                                summary.StudentsWithSupplementaryExams++;
                                break;

                            default:
                                summary.StudentsDisqualified++; // safe fallback
                                break;
                        }
                    }
                    else
                    {
                        // No progression rule matched → treat as fail-safe “disqualified”
                        summary.StudentsDisqualified++;
                    }
                }


                // Calculate percentage with clear pass
                if (summary.TotalStudents > 0)
                {
                    try
                    {
                        summary.PercentageWithClearPass = Math.Round(
                        (decimal)summary.StudentsWithClearPass / (summary.TotalStudents - summary.StudentsWithDeferredExams) * 100,
                        0); // Round to whole number
                    }
                    catch(Exception ex)
                    {
                        summary.PercentageWithClearPass = 0;
                    }
                }

                _logger.LogInformation($"Performance summary calculated: {summary.TotalStudents} total students, {summary.StudentsWithClearPass} with clear pass ({summary.PercentageWithClearPass}%)");

                return summary;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting performance summary for programme {programmeId}");
                throw;
            }
        }*/


        /** Compute using the view instead **/
        public async Task<PerformanceSummaryDto> GetPerformanceSummaryAsync(
            int programmeId,
            int academicYearId,
            int semester,
            int yearOfStudy)
        {
            try
            {
                _logger.LogInformation($"Getting performance summary for programme {programmeId}, year {academicYearId}, semester {semester}");

                var summary = new PerformanceSummaryDto();

                // Get programme details for validation
                var programme = await _context.Programmes
                    .AsNoTracking()
                    .Include(p => p.Department)
                    .FirstOrDefaultAsync(p => p.Id == programmeId);

                if (programme == null)
                {
                    _logger.LogWarning($"Programme {programmeId} not found");
                    return summary;
                }

                // Query the view for all student results
                var studentResults = await _context.Set<StudentResultView>()
                    .FromSqlRaw(@"
                        WITH RankedResults AS
                        (
                            SELECT *,
                                   ROW_NUMBER() OVER
                                   (
                                       PARTITION BY StudentId_Number, CourseCode
                                       ORDER BY Attempt DESC
                                   ) AS rn
                            FROM VW_StudentResults
                            WHERE ProgrammeId = {0}
                              AND AcademicYearId = {1}
                              AND Semester = {2}
                              AND YearOfStudy = {3}
                        )
                        SELECT *
                        FROM RankedResults
                        WHERE rn = 1",
                        programmeId,
                        academicYearId,
                        semester,
                        yearOfStudy)
                    .AsNoTracking()
                    .ToListAsync();

                if (!studentResults.Any())
                {
                    _logger.LogWarning($"No student results found for programme {programmeId}");
                    return summary;
                }

                // Group results by student
                var studentGroups = studentResults
                    .GroupBy(r => r.StudentId_Number)
                    .ToList();

                summary.TotalStudents = studentGroups.Count;

                // Process each student
                foreach (var studentGroup in studentGroups)
                {
                    var studentCourses = studentGroup.ToList();
                    var firstRecord = studentCourses.First();

                    int failedCourses = 0;
                    bool hasDeferredExams = false;
                    int? highestAttemptAcrossCourses = null;

                    foreach (var result in studentCourses)
                    {
                        // Track highest attempt for progression rule
                        if (highestAttemptAcrossCourses == null || result.Attempt > highestAttemptAcrossCourses)
                            highestAttemptAcrossCourses = result.Attempt;

                        // Check for deferred exams (NE grade)
                        if (result.GradeLetter == "NE")
                        {
                            hasDeferredExams = true;
                            continue; // Don't count as failed
                        }

                        // Check if course is failed
                        if (result.IsPassingGrade == 0 && result.Exam != null)
                        {
                            failedCourses++;
                        }
                    }

                    // -------------------------------------------
                    //  APPLY PROGRESSION RULES HERE
                    // -------------------------------------------
                    int totalCourses = studentCourses.Count;
                    int failedPercentage = totalCourses > 0
                        ? (int)Math.Floor((decimal)failedCourses / totalCourses * 100)
                        : 0;

                    // Get student entity for progression rule
                    var student = await _context.Students
                        .AsNoTracking()
                        .Include(s => s.Programme)
                            .ThenInclude(p => p.Department)
                                .ThenInclude(d => d.School)
                        .FirstOrDefaultAsync(s => s.StudentId_Number == firstRecord.StudentId_Number);

                    if (student == null)
                    {
                        _logger.LogWarning($"Student {firstRecord.StudentId_Number} not found in database");
                        continue;
                    }

                    var progressionRule = await _studentProgressionService.GetApplicableProgressionRuleAsync(
                        student,
                        failedPercentage,
                        semester,
                        highestAttemptAcrossCourses

                    );
                    summary.StudentsDisqualified = await _context.StudentDisqualifications
                        .Where(sq => sq.Student.ProgrammeId == programmeId)
                        .CountAsync();

                    if (hasDeferredExams)
                    {
                        summary.StudentsWithDeferredExams++;
                    }
                    else if (progressionRule != null)
                    {
                        switch (progressionRule.Action.ToLower())
                        {
                            case "proceed":
                                summary.StudentsWithClearPass++;
                                break;

                            case "proceedandrepeat":
                                summary.StudentsWithProceedWithRepeats++;
                                break;

                            case "repeatsemester":
                                summary.StudentsWithRepeatSemester++;
                                break;

                            case "sup":
                                summary.StudentsWithSupplementaryExams++;
                                break;

                            default:
                                summary.StudentsDisqualified++; // safe fallback
                                break;
                        }
                    }
                    else
                    {
                        // No progression rule matched → treat as fail-safe "disqualified"
                        summary.StudentsDisqualified++;
                    }
                }

                // Calculate percentage with clear pass
                if (summary.TotalStudents > 0)
                {
                    try
                    {
                        var eligibleStudents = summary.TotalStudents - summary.StudentsWithDeferredExams;
                        if (eligibleStudents > 0)
                        {
                            summary.PercentageWithClearPass = Math.Round((decimal)summary.StudentsWithClearPass / eligibleStudents * 100);
                        }
                        else
                        {
                            summary.PercentageWithClearPass = 0;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error calculating percentage with clear pass");
                        summary.PercentageWithClearPass = 0;
                    }
                }

                _logger.LogInformation($"Performance summary calculated: {summary.TotalStudents} total students, {summary.StudentsWithClearPass} with clear pass ({summary.PercentageWithClearPass}%)");

                return summary;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting performance summary for programme {programmeId}");
                throw;
            }
        }
    }

    // Helper classes for the new approach
    public class StudentCourseData
    {
        public StudentExaminableCourse StudentExaminableCourse { get; set; }
        public Student Student { get; set; }
        public Programme Programme { get; set; }
        public Department Department { get; set; }
        public School School { get; set; }
        public Course Course { get; set; }
        public List<AssessmentScoreDetail> AssessmentScores { get; set; } = new List<AssessmentScoreDetail>();
    }

    public class AssessmentScoreDetail
    {
        public int AssessmentId { get; set; }
        public decimal Score { get; set; }
        public decimal MaxScore { get; set; }
        public decimal WeightPercentage { get; set; }
        public int? Attempt { get; set; }
    }

    public class ProgrammeGradingOverview
    {
        public int ProgrammeId { get; set; }
        public string ProgrammeName { get; set; }
        public string ProgrammeCode { get; set; }
        public string ModeOfStudy { get; set; }
        public string AcademicYear { get; set; }
        public int Semester { get; set; }
        public List<CourseGradingOverview> Courses { get; set; }
    }

    public class CourseGradingOverview
    {
        public string CourseNo { get; set; }
        public string CourseName { get; set; }
        public Dictionary<string, int> GradeDistribution { get; set; }
        public int TotalFailed { get; set; }
        public int TotalPassed { get; set; }
        public decimal PassRate { get; set; }
    }

    public class PerformanceSummaryDto
    {
        public int StudentsWithClearPass { get; set; }
        public decimal PercentageWithClearPass { get; set; }
        public int StudentsWithProceedWithRepeats { get; set; }
        public int StudentsWithRepeatSemester { get; set; }
        public int StudentsExcluded { get; set; }
        public int StudentsDisqualified { get; set; }
        public int StudentsWithSupplementaryExams { get; set; }
        public int StudentsWithDeferredExams { get; set; }
        public int TotalStudents { get; set; }
    }
}