using Microsoft.EntityFrameworkCore;
using SIS.Data;
using SIS.Models.StudentApplication;

namespace SIS.Services.StudentApplication
{
    public class ProgrammeService : IProgrammeService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<ProgrammeService> _logger;

        public ProgrammeService(ApplicationDbContext context, ILogger<ProgrammeService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<ProgrammeListViewModel> GetProgrammesGroupedBySchoolAsync(string search = "", int? schoolId = null, int? programmeLevelId = null)
        {
            try
            {
                // Get all schools with their departments and programmes
                var query = _context.Schools
                    .Include(s => s.Departments)
                        .ThenInclude(d => d.Programmes)
                            .ThenInclude(p => p.ProgrammeLevel)
                    .Include(s => s.Departments)
                        .ThenInclude(d => d.Programmes)
                            .ThenInclude(p => p.ModeOfStudy)
                    .Where(s => s.Departments.Any(d => d.Programmes.Any()));

                // Apply search filter
                if (!string.IsNullOrEmpty(search))
                {
                    query = query.Where(s =>
                        s.Name.Contains(search) ||
                        s.Departments.Any(d => d.Name.Contains(search)) ||
                        s.Departments.Any(d => d.Programmes.Any(p => p.Name.Contains(search) || p.Description.Contains(search))));
                }

                // Apply school filter
                if (schoolId.HasValue)
                {
                    query = query.Where(s => s.Id == schoolId.Value);
                }

                var schools = await query.ToListAsync();

                // Build the view model
                var viewModel = new ProgrammeListViewModel
                {
                    SearchTerm = search,
                    SelectedSchoolId = schoolId,
                    SelectedProgrammeLevelId = programmeLevelId,
                    Schools = schools.Select(school => new SchoolWithProgrammesViewModel
                    {
                        SchoolId = school.Id,
                        SchoolName = school.Name,
                        SchoolDescription = school.Description ?? "",
                        Departments = school.Departments
                            .Where(d => d.Programmes.Any())
                            .Select(dept => new DepartmentWithProgrammesViewModel
                            {
                                DepartmentId = dept.Id,
                                DepartmentName = dept.Name,
                                DepartmentDescription = dept.Description ?? "",
                                Programmes = dept.Programmes
                                    .Where(p => !programmeLevelId.HasValue || p.ProgrammeLevelId == programmeLevelId.Value)
                                    .Select(prog => new ProgrammeSummaryViewModel
                                    {
                                        Id = prog.Id,
                                        Name = prog.Name,
                                        Description = prog.Description,
                                        DurationYears = prog.DurationYears,
                                        ProgrammeLevelName = prog.ProgrammeLevel?.Name ?? "N/A",
                                        ModeOfStudyName = prog.ModeOfStudy?.ModeName ?? "N/A",
                                        MinimumPointsTop5Subjects = prog.MinimumPointsTop5Subjects,
                                        EnrollmentCount = prog.EnrollmentCount
                                    }).ToList()
                            })
                            .Where(d => d.Programmes.Any())
                            .ToList(),
                        TotalProgrammes = school.Departments
                            .SelectMany(d => d.Programmes)
                            .Count(p => !programmeLevelId.HasValue || p.ProgrammeLevelId == programmeLevelId.Value)
                    }).ToList()
                };

                // Set totals
                viewModel.TotalSchools = viewModel.Schools.Count;
                viewModel.TotalProgrammes = viewModel.Schools.Sum(s => s.TotalProgrammes);

                return viewModel;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting programmes grouped by school");
                throw;
            }
        }

        public async Task<ProgrammeDetailsViewModel> GetProgrammeDetailsWithCoursesAsync(int id)
        {
            try
            {
                // Get programme with all related data
                var programme = await _context.Programmes
                    .Include(p => p.Department)
                        .ThenInclude(d => d.School)
                    .Include(p => p.ProgrammeLevel)
                    .Include(p => p.ModeOfStudy)
                    .FirstOrDefaultAsync(p => p.Id == id);

                if (programme == null)
                {
                    throw new ArgumentException($"Programme with ID {id} not found");
                }

                // Get coordinator name
                var coordinatorName = "TBA";
                if (!string.IsNullOrEmpty(programme.CoordinatorId))
                {
                    var coordinator = await _context.Users.FirstOrDefaultAsync(u => u.Id == programme.CoordinatorId);
                    coordinatorName = coordinator?.FullName ?? "TBA";
                }

                // Get courses organized by year
                var coursesByYear = await GetCoursesGroupedByYearAsync(id);

                // Get fee breakdown
                var feeBreakdown = await GetFeeBreakdownForProgramme(id);

                // Build the view model
                var viewModel = new ProgrammeDetailsViewModel
                {
                    Id = programme.Id,
                    Name = programme.Name,
                    Description = programme.Description,
                    DurationYears = programme.DurationYears,
                    MinimumPointsTop5Subjects = programme.MinimumPointsTop5Subjects,
                    SchoolName = programme.Department?.School?.Name ?? "N/A",
                    DepartmentName = programme.Department?.Name ?? "N/A",
                    ProgrammeLevelName = programme.ProgrammeLevel?.Name ?? "N/A",
                    ModeOfStudyName = programme.ModeOfStudy?.ModeName ?? "N/A",
                    CoordinatorName = coordinatorName,
                    EnrollmentCount = programme.EnrollmentCount,
                    YearlyRequirements = programme.YearlyRequirements ?? "",
                    CoursesByYear = coursesByYear,
                    FeeBreakdown = feeBreakdown,
                    TotalFeesPerYear = feeBreakdown.Sum(f => f.Amount),
                    SchoolId = programme.Department?.SchoolId ?? 0,
                    DepartmentId = programme.DepartmentId
                };

                return viewModel;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting programme details for ID: {ProgrammeId}", id);
                throw;
            }
        }

        public async Task<List<CoursesGroupedByYearViewModel>> GetCoursesGroupedByYearAsync(int programmeId)
        {
            try
            {
                // Get courses for this programme
                var courses = await _context.Courses
                    .Include(c => c.Programme)
                    .Where(c => c.ProgrammeID == programmeId)
                    .OrderBy(c => c.YearTaken)
                    .ThenBy(c => c.PeriodTakenId)
                    .ThenBy(c => c.CourseName)
                    .ToListAsync();

                // Group courses by year and semester
                var coursesByYear = new List<CoursesGroupedByYearViewModel>();

                foreach (var yearGroup in courses.GroupBy(c => c.YearTaken))
                {
                    var yearViewModel = new CoursesGroupedByYearViewModel
                    {
                        YearOfStudy = yearGroup.Key,
                        YearDisplayName = GetYearDisplayName(yearGroup.Key),
                        Semesters = new List<CourseSemesterViewModel>(),
                        TotalCourses = yearGroup.Count(),
                        MandatoryCourses = yearGroup.Count(c => c.IsMandatory),
                        ElectiveCourses = yearGroup.Count(c => !c.IsMandatory)
                    };

                    foreach (var semesterGroup in yearGroup.GroupBy(c => c.PeriodTakenId))
                    {
                        var semesterViewModel = new CourseSemesterViewModel
                        {
                            SemesterNumber = semesterGroup.Key,
                            SemesterDisplayName = $"Semester {semesterGroup.Key}",
                            Courses = new List<CourseDetailViewModel>()
                        };

                        foreach (var course in semesterGroup)
                        {
                            var prerequisiteCourses = await GetPrerequisiteCourseNamesAsync(course.PrerequisiteCourseIds);

                            var courseDetail = new CourseDetailViewModel
                            {
                                Id = course.Id,
                                CourseCode = course.CourseCode,
                                CourseName = course.CourseName,
                                CourseDescription = course.CourseDescription ?? "",
                                CourseType = course.CourseType ?? "",
                                YearTaken = course.YearTaken,
                                PeriodTakenId = course.PeriodTakenId,
                                PeriodTakenLabel = course.PeriodTakenLabel,
                                IsMandatory = course.IsMandatory,
                                IsExaminable = course.IsExaminable,
                                PassMark = (int)course.PassMark,
                                InstructorName = GetInstructorName(course.InstructorId),
                                MeetingFrequencyPerWeek = course.MeetingFrequencyPerWeek,
                                CapacityRequired = course.CapacityRequired.ToString(),
                                PrerequisiteCourses = prerequisiteCourses
                            };

                            semesterViewModel.Courses.Add(courseDetail);
                        }

                        // Sort semesters
                        yearViewModel.Semesters.Add(semesterViewModel);
                    }

                    // Sort semesters by number
                    yearViewModel.Semesters = yearViewModel.Semesters.OrderBy(s => s.SemesterNumber).ToList();
                    coursesByYear.Add(yearViewModel);
                }

                // Sort years by year of study
                return coursesByYear.OrderBy(y => y.YearOfStudy).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting courses grouped by year for programme ID: {ProgrammeId}", programmeId);
                throw;
            }
        }

        public async Task<List<ProgrammeFeeDetailViewModel>> GetFeeBreakdownForProgramme(int programmeId, int? yearOfStudy = null)
        {
            try
            {
                // Get current academic year
                var currentAcademicYear = await _context.AcademicYears
                    .Where(a => a.IsActive)
                    .FirstOrDefaultAsync();

                if (currentAcademicYear == null)
                    return new List<ProgrammeFeeDetailViewModel>();

                // Get programme details
                var programme = await _context.Programmes
                    .Include(p => p.Department)
                    .Include(p => p.ProgrammeLevel)
                    .Include(p => p.ModeOfStudy)
                    .FirstOrDefaultAsync(p => p.Id == programmeId);

                if (programme == null)
                    return new List<ProgrammeFeeDetailViewModel>();

                // Build query for applicable fees
                var query = _context.FeeConfigurations
                    .Include(f => f.FeeType)
                    .Where(f => f.AcademicYearId == currentAcademicYear.YearId &&
                               f.FeeType.ApplicableFor == "Student" &&
                               f.FeeType.IsActive);

                // Apply programme-specific filters
                query = query.Where(f =>
                    f.AppliesUniversally ||
                    f.ProgrammeId == programmeId ||
                    f.SchoolId == programme.Department.SchoolId ||
                    f.ProgramLevelId == programme.ProgrammeLevelId ||
                    f.ModeOfStudyId == programme.ModeOfStudyId);

                // Filter by year of study if specified
                if (yearOfStudy.HasValue)
                {
                    query = query.Where(f => f.YearOfStudy == null || f.YearOfStudy == yearOfStudy.Value);
                }

                var fees = await query.ToListAsync();

                // Convert to view model
                var feeBreakdown = fees.Select(f => new ProgrammeFeeDetailViewModel
                {
                    FeeName = f.FeeType.Name,
                    Description = f.FeeType.Description ?? f.FeeType.Name,
                    Amount = f.Amount,
                    YearApplicable = f.YearOfStudy ?? 1
                }).ToList();

                return feeBreakdown;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting fee breakdown for programme ID: {ProgrammeId}", programmeId);
                throw;
            }
        }

        // Helper methods
        private string GetYearDisplayName(int year)
        {
            return year switch
            {
                1 => "First Year",
                2 => "Second Year",
                3 => "Third Year",
                4 => "Fourth Year",
                5 => "Fifth Year",
                _ => $"Year {year}"
            };
        }

        private string GetInstructorName(string instructorId)
        {
            if (string.IsNullOrEmpty(instructorId))
                return "TBA";

            try
            {
                var instructor = _context.Users.FirstOrDefault(u => u.Id == instructorId);
                return instructor?.FullName ?? "TBA";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting instructor name for ID: {InstructorId}", instructorId);
                return "TBA";
            }
        }

        private async Task<List<string>> GetPrerequisiteCourseNamesAsync(string prerequisiteCourseIds)
        {
            if (string.IsNullOrEmpty(prerequisiteCourseIds))
                return new List<string>();

            try
            {
                // Assuming prerequisiteCourseIds is a comma-separated string of IDs
                var ids = prerequisiteCourseIds.Split(',', StringSplitOptions.RemoveEmptyEntries)
                                             .Select(id => int.TryParse(id.Trim(), out var courseId) ? courseId : (int?)null)
                                             .Where(id => id.HasValue)
                                             .Select(id => id.Value)
                                             .ToList();

                if (!ids.Any())
                    return new List<string>();

                var courseNames = await _context.Courses
                                                .Where(c => ids.Contains(c.Id))
                                                .Select(c => $"{c.CourseCode} - {c.CourseName}")
                                                .ToListAsync();

                return courseNames;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing prerequisite course IDs: {PrerequisiteIds}", prerequisiteCourseIds);
                return new List<string>();
            }
        }
    }
}