using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SIS.Data;
using SIS.Models.Admin;
using SIS.Models.Results;
using SIS.Services;
using System.Security.Claims;

[Authorize]
public class WorkflowController : Controller
{
    private readonly IWorkflowService _workflowService;
    private readonly IWorkflowTemplateService _templateService;
    private readonly IWorkflowStatisticsService _statisticsService;
    private readonly IWorkflowValidationService _validationService;
    //private readonly IResultSubmissionService _resultSubmissionService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<WorkflowController> _logger;
    private readonly ApplicationDbContext _context;

    public WorkflowController(
        IWorkflowService workflowService,
        IWorkflowTemplateService templateService,
        IWorkflowStatisticsService statisticsService,
        IWorkflowValidationService validationService,
        //IResultSubmissionService resultSubmissionService,
        UserManager<ApplicationUser> userManager,
        ILogger<WorkflowController> logger,
        ApplicationDbContext context)
    {
        _workflowService = workflowService;
        _templateService = templateService;
        _statisticsService = statisticsService;
        _validationService = validationService;
        //_resultSubmissionService = resultSubmissionService;
        _userManager = userManager;
        _logger = logger;
        _context = context;
    }

    #region Workflow Templates

    [Authorize(Roles = "Admin,Registrar")]
    public async Task<IActionResult> Index()
    {
        try
        {
            var templates = await _templateService.GetAllTemplatesAsync();
            return View(templates);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading workflow templates");
            TempData["Error"] = "An error occurred while loading workflow templates.";
            return View(new List<WorkflowTemplate>());
        }
    }

    [Authorize(Roles = "Admin,Registrar")]
    [HttpGet]
    public IActionResult CreateTemplate()
    {
        ViewBag.EntityTypes = new List<string>
        {
            "ResultSubmissionBatch",
            "StudentAssessmentScore",
            "StudentCourseResult",
            "CourseModification"
        };
        return View();
    }

    [Authorize(Roles = "Admin,Registrar")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateTemplate(CreateTemplateRequest request)
    {
        try
        {
            if (request.Stages.Any())
            { 
                int i = 1;
                foreach(var stage in request.Stages)
                {
                    stage.StageOrder = i;
                    i++;
                }
            }
            /*if (!ModelState.IsValid)
            {
                ViewBag.EntityTypes = GetEntityTypes();
                return View(request);
            }*/

            // Validate template
            var validation = await _validationService.ValidateWorkflowTemplateAsync(
                new WorkflowTemplate
                {
                    Name = request.Name,
                    Description = request.Description,
                    EntityType = request.EntityType,
                    IsActive = request.IsActive,
                    RequiresAllStages = request.RequiresAllStages,
                    AllowRejection = request.AllowRejection,
                    CreatedBy = User.FindFirstValue(ClaimTypes.NameIdentifier),
                    CreatedAt = DateTime.Now
                },
                request.Stages);

            if (!validation.IsValid)
            {
                foreach (var error in validation.Errors)
                {
                    ModelState.AddModelError("", error);
                }
                ViewBag.EntityTypes = GetEntityTypes();
                return View(request);
            }

            // Create template
            var template = new WorkflowTemplate
            {
                Name = request.Name,
                Description = request.Description,
                EntityType = request.EntityType,
                IsActive = request.IsActive,
                RequiresAllStages = request.RequiresAllStages,
                AllowRejection = request.AllowRejection,
                CreatedBy = User.FindFirstValue(ClaimTypes.NameIdentifier),
                CreatedAt = DateTime.Now
            };

            var result = await _templateService.CreateTemplateAsync(template, request.Stages);

            TempData["Success"] = "Workflow template created successfully.";
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating workflow template");
            ModelState.AddModelError("", "An error occurred while creating the template.");
            ViewBag.EntityTypes = GetEntityTypes();
            return View(request);
        }
    }

    [Authorize(Roles = "Admin,Registrar")]
    [HttpGet]
    public async Task<IActionResult> EditTemplate(int id)
    {
        try
        {
            var templates = await _templateService.GetAllTemplatesAsync();
            var template = templates.FirstOrDefault(t => t.Id == id);

            if (template == null)
            {
                TempData["Error"] = "Template not found.";
                return RedirectToAction(nameof(Index));
            }

            var request = new UpdateTemplateRequest
            {
                Name = template.Name,
                Description = template.Description,
                EntityType = template.EntityType,
                IsActive = template.IsActive,
                RequiresAllStages = template.RequiresAllStages,
                AllowRejection = template.AllowRejection,
                Stages = template.Stages.Select(s => new WorkflowStageDto
                {
                    Id = s.Id,
                    StageName = s.StageName,
                    StageOrder = s.StageOrder,
                    RequiredRole = s.RequiredRole,
                    RequiredUserId = s.RequiredUserId,
                    ApproverPropertyPath = s.ApproverPropertyPath,
                    SendNotification = s.SendNotification,
                    AutoApproveAfterDays = s.AutoApproveAfterDays,
                    IsOptional = s.IsOptional,
                    AllowDelegation = s.AllowDelegation
                }).ToList()
            };

            ViewBag.EntityTypes = GetEntityTypes();
            ViewBag.TemplateId = id;
            return View(request);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading template for edit");
            TempData["Error"] = "An error occurred while loading the template.";
            return RedirectToAction(nameof(Index));
        }
    }

    [Authorize(Roles = "Admin,Registrar")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditTemplate(int id, UpdateTemplateRequest request)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                ViewBag.EntityTypes = GetEntityTypes();
                ViewBag.TemplateId = id;
                return View(request);
            }

            var template = new WorkflowTemplate
            {
                Name = request.Name,
                Description = request.Description,
                EntityType = request.EntityType,
                IsActive = request.IsActive,
                RequiresAllStages = request.RequiresAllStages,
                AllowRejection = request.AllowRejection,
                CreatedBy = User.FindFirstValue(ClaimTypes.NameIdentifier),
                CreatedAt = DateTime.Now,
                UpdatedBy = User.FindFirstValue(ClaimTypes.NameIdentifier),
                UpdatedAt = DateTime.Now
            };

            await _templateService.UpdateTemplateAsync(id, template, request.Stages);

            TempData["Success"] = "Workflow template updated successfully.";
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating workflow template");
            ModelState.AddModelError("", "An error occurred while updating the template.");
            ViewBag.EntityTypes = GetEntityTypes();
            ViewBag.TemplateId = id;
            return View(request);
        }
    }

    [Authorize(Roles = "Admin,Registrar")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteTemplate(int id)
    {
        try
        {
            var templates = await _templateService.GetAllTemplatesAsync();
            var template = templates.FirstOrDefault(t => t.Id == id);

            if (template == null)
            {
                TempData["Error"] = "Template not found.";
                return RedirectToAction(nameof(Index));
            }

            // Check if template is being used
            var activeWorkflows = await _context.WorkflowInstances
                .AnyAsync(w => w.WorkflowTemplateId == id &&
                    (w.Status == WorkflowStatus.Pending || w.Status == WorkflowStatus.InProgress));

            if (activeWorkflows)
            {
                TempData["Error"] = "Cannot delete template. There are active workflows using this template.";
                return RedirectToAction(nameof(Index));
            }

            _context.WorkflowTemplates.Remove(template);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Workflow template deleted successfully.";
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting workflow template");
            TempData["Error"] = "An error occurred while deleting the template.";
            return RedirectToAction(nameof(Index));
        }
    }

    #endregion

    #region Pending Approvals

    public async Task<IActionResult> PendingApprovals()
    {
        try
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var approvals = await _workflowService.GetPendingApprovalsForUserAsync(userId);
            var pendingDtos = approvals.Select(a => a.ToPendingDto()).ToList();

            // Populate AcademicYearId and Semester for ResultSubmissionBatch approvals
            var batchApprovals = pendingDtos.Where(dto => dto.EntityType == "ResultSubmissionBatch").ToList();

            if (batchApprovals.Any())
            {
                var batchIds = batchApprovals.Select(dto => dto.EntityId).ToList();

                var batchData = await _context.ResultSubmissionBatches
                    .Where(rsb => batchIds.Contains(rsb.Id))
                    .Select(rsb => new
                    {
                        rsb.Id,
                        rsb.AcademicYearId,
                        rsb.YearPeriodId
                    })
                    .ToListAsync();

                foreach (var dto in batchApprovals)
                {
                    var batch = batchData.FirstOrDefault(b => b.Id == dto.EntityId);
                    if (batch != null)
                    {
                        dto.AcademicYearId = batch.AcademicYearId;
                        dto.Semester = batch.YearPeriodId;
                    }
                }
            }

            // Load available users for delegation
            var users = await _userManager.Users
                .Where(u => u.Id != userId)
                .Select(u => new { u.Id, u.UserName, u.Email })
                .ToListAsync();

            ViewBag.AvailableUsers = users;

            return View(pendingDtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading pending approvals");
            TempData["Error"] = "An error occurred while loading pending approvals.";
            return View(new List<PendingApprovalDto>());
        }
    }

    [HttpGet]
    public async Task<IActionResult> PendingBatchesGradingOverview()
    {
        try
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            // Get all pending result batch approvals for this user
            var pendingBatches = await _context.ResultSubmissionBatches
                .Where(rsb => rsb.ApprovalStatus == WorkflowStatus.Draft || rsb.ApprovalStatus == WorkflowStatus.Pending || rsb.ApprovalStatus == WorkflowStatus.InProgress)
                .ToListAsync();

            if (!pendingBatches.Any())
            {
                TempData["Info"] = "You have no pending result batch approvals at the moment.";
                return RedirectToAction("PendingApprovals");
            }

            // Get the result submission batches with programme info and student year
            var batchesWithStudents = await _context.ResultSubmissionBatches
                .Include(rsb => rsb.Course)
                    .ThenInclude(c => c.Programme)
                        .ThenInclude(p => p.ModeOfStudy)
                .Include(rsb => rsb.AcademicYear)
                .Where(rsb => rsb.ApprovalStatus == WorkflowStatus.Draft || rsb.ApprovalStatus == WorkflowStatus.Pending || rsb.ApprovalStatus == WorkflowStatus.InProgress)
                .Select(rsb => new
                {
                    Batch = rsb,
                    // Get the most common year of study for students in this course
                    StudentYear = _context.StudentExaminableCourses
                        .Where(sec =>
                            sec.CourseId == rsb.CourseId &&
                            sec.AcademicYearId == rsb.AcademicYearId &&
                            sec.YearPeriodId == rsb.YearPeriodId)
                        .Join(_context.Students,
                            sec => sec.StudentId,
                            s => s.Id,
                            (sec, s) => s.StudentCurrentYear)
                        .GroupBy(y => y)
                        .OrderByDescending(g => g.Count())
                        .Select(g => g.Key)
                        .FirstOrDefault() ?? 1 // Default to Year 1 if no students found
                })
                .ToListAsync();

            // Group by Programme, Year of Study, and Semester
            var programmeGroups = batchesWithStudents
                .GroupBy(b => new
                {
                    ProgrammeId = b.Batch.Course.ProgrammeID,
                    ProgrammeName = b.Batch.Course.Programme.Name,
                    //ProgrammeCode = b.Batch.Course.Programme.ProgrammeCode,
                    ModeOfStudy = b.Batch.Course.Programme.ModeOfStudy.ModeName,
                    AcademicYearId = b.Batch.AcademicYearId,
                    AcademicYear = b.Batch.AcademicYear.YearValue,
                    Semester = b.Batch.YearPeriodId,
                    YearOfStudy = b.StudentYear
                })
                .Select(g => new PendingProgrammeGradingOverviewDto
                {
                    ProgrammeId = g.Key.ProgrammeId,
                    ProgrammeName = g.Key.ProgrammeName,
                    //ProgrammeCode = g.Key.ProgrammeCode,
                    ModeOfStudy = g.Key.ModeOfStudy,
                    AcademicYearId = g.Key.AcademicYearId,
                    AcademicYear = g.Key.AcademicYear,
                    Semester = g.Key.Semester,
                    YearOfStudy = g.Key.YearOfStudy,
                    CoursesCount = g.Count(),
                    Courses = g.Select(c => new PendingCourseInfoDto
                    {
                        RsbId = c.Batch.Id,
                        CourseId = c.Batch.CourseId,
                        CourseCode = c.Batch.Course.CourseCode,
                        CourseName = c.Batch.Course.CourseName,
                        PassMark = c.Batch.Course.PassMark
                    }).ToList()
                })
                .OrderBy(p => p.YearOfStudy)           // First by year
                .ThenBy(p => p.Semester)                // Then by semester
                .ThenBy(p => p.ProgrammeName)           // Then by programme name
                .ToList();

            ViewBag.TotalProgrammes = programmeGroups.Select(p => p.ProgrammeId).Distinct().Count();
            ViewBag.TotalCourses = batchesWithStudents.Count;
            ViewBag.TotalYears = programmeGroups.Select(p => p.YearOfStudy).Distinct().Count();

            return View(programmeGroups);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading pending batches grading overview");
            TempData["Error"] = "An error occurred while loading the grading overview.";
            return RedirectToAction("PendingApprovals");
        }
    }

    // Updated DTO with YearOfStudy
    public class PendingProgrammeGradingOverviewDto
    {
        public int ProgrammeId { get; set; }
        public string ProgrammeName { get; set; }
        public string ProgrammeCode { get; set; }
        public string ModeOfStudy { get; set; }
        public int AcademicYearId { get; set; }
        public string AcademicYear { get; set; }
        public int Semester { get; set; }
        public int YearOfStudy { get; set; }
        public int CoursesCount { get; set; }
        public List<PendingCourseInfoDto> Courses { get; set; }
    }

    public class PendingCourseInfoDto
    {
        public int RsbId { get; set; }
        public int CourseId { get; set; }
        public string CourseCode { get; set; }
        public string CourseName { get; set; }
        public double PassMark { get; set; }
    }

    /*public async Task<IActionResult> PendingApprovals()
    {
        try
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var approvals = await _workflowService.GetPendingApprovalsForUserAsync(userId);

            var pendingDtos = approvals.Select(a => a.ToPendingDto()).ToList();

            // Load available users for delegation
            var users = await _userManager.Users
                .Where(u => u.Id != userId)
                .Select(u => new { u.Id, u.UserName, u.Email })
                .ToListAsync();

            ViewBag.AvailableUsers = users;

            return View(pendingDtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading pending approvals");
            TempData["Error"] = "An error occurred while loading pending approvals.";
            return View(new List<PendingApprovalDto>());
        }
    }*/

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Approve(int workflowInstanceId, string comments)
    {
        try
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            // Check if user can approve
            if (!await _workflowService.CanUserApproveAsync(workflowInstanceId, userId))
            {
                TempData["Error"] = "You do not have permission to approve this workflow.";
                return RedirectToAction(nameof(PendingApprovals));
            }

            await _workflowService.ApproveAsync(workflowInstanceId, userId, comments);

            TempData["Success"] = "Approval submitted successfully.";
            return RedirectToAction(nameof(PendingApprovals));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error approving workflow {WorkflowId}", workflowInstanceId);
            TempData["Error"] = "An error occurred while processing your approval.";
            return RedirectToAction(nameof(PendingApprovals));
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reject(int workflowInstanceId, string comments)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(comments) || comments.Length < 10)
            {
                TempData["Error"] = "Please provide a detailed reason for rejection (at least 10 characters).";
                return RedirectToAction(nameof(PendingApprovals));
            }

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (!await _workflowService.CanUserApproveAsync(workflowInstanceId, userId))
            {
                TempData["Error"] = "You do not have permission to reject this workflow.";
                return RedirectToAction(nameof(PendingApprovals));
            }

            await _workflowService.RejectAsync(workflowInstanceId, userId, comments);

            TempData["Success"] = "Rejection submitted successfully.";
            return RedirectToAction(nameof(PendingApprovals));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error rejecting workflow {WorkflowId}", workflowInstanceId);
            TempData["Error"] = "An error occurred while processing your rejection.";
            return RedirectToAction(nameof(PendingApprovals));
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delegate(int workflowInstanceId, string toUserId, string reason)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(toUserId))
            {
                TempData["Error"] = "Please select a user to delegate to.";
                return RedirectToAction(nameof(PendingApprovals));
            }

            if (string.IsNullOrWhiteSpace(reason) || reason.Length < 5)
            {
                TempData["Error"] = "Please provide a reason for delegation (at least 5 characters).";
                return RedirectToAction(nameof(PendingApprovals));
            }

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            await _workflowService.DelegateApprovalAsync(workflowInstanceId, userId, toUserId, reason);

            TempData["Success"] = "Approval delegated successfully.";
            return RedirectToAction(nameof(PendingApprovals));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error delegating workflow {WorkflowId}", workflowInstanceId);
            TempData["Error"] = "An error occurred while delegating the approval.";
            return RedirectToAction(nameof(PendingApprovals));
        }
    }

    #endregion

    #region Workflow Details

    public async Task<IActionResult> WorkflowDetails(int id)
    {
        try
        {
            var workflow = await _workflowService.GetWorkflowByIdAsync(id);

            if (workflow == null)
            {
                TempData["Error"] = "Workflow not found.";
                return RedirectToAction(nameof(PendingApprovals));
            }

            var workflowDto = workflow.ToDto();

            // Load submitted results based on entity type
            List<ImportedResultInfo> submittedResults = null;

            if (workflow.EntityType == "ResultSubmissionBatch")
            {
                submittedResults = await LoadSubmittedResultsAsync(workflow.EntityId);
            }

            var viewModel = new WorkflowDetailsViewModel
            {
                WorkflowInstance = workflowDto,
                SubmittedResults = submittedResults ?? new List<ImportedResultInfo>()
            };

            return View(viewModel);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading workflow details for {WorkflowId}", id);
            TempData["Error"] = "An error occurred while loading workflow details.";
            return RedirectToAction(nameof(PendingApprovals));
        }
    }

    private async Task<List<ImportedResultInfo>> LoadSubmittedResultsAsync(int batchId)
    {
        try
        {
            // Get the batch to determine if it's assessment scores or course results
            var batch = await _context.ResultSubmissionBatches
                .FirstOrDefaultAsync(b => b.Id == batchId);

            if (batch == null)
            {
                return new List<ImportedResultInfo>();
            }

            if (batch.SubmissionType == "AssessmentScores")
            {
                return await LoadAssessmentScoresResultsAsync(batchId);
            }
            else if (batch.SubmissionType == "CourseResults")
            {
                return await LoadCourseResultsAsync(batchId);
            }

            return new List<ImportedResultInfo>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading submitted results for batch {BatchId}", batchId);
            return new List<ImportedResultInfo>();
        }
    }

    private async Task<List<ImportedResultInfo>> LoadAssessmentScoresResultsAsync(int batchId)
    {
        try
        {
            // Get all assessment scores for this batch
            var results = await _context.StudentAssessmentScores
                                        .Include(s => s.Student)
                                            .ThenInclude(st => st.Programme)
                                        .Include(s => s.Course)
                                        .Include(s => s.AcademicYear)
                                        .Where(s => s.IsActive && s.rsbId == batchId)
                                        .GroupBy(s => new
                                        {
                                            s.StudentId,
                                            s.Student.StudentId_Number,
                                            s.Student.FullName
                                        })
                                        .Select(g => new
                                        {
                                            g.Key.StudentId,
                                            g.Key.StudentId_Number,
                                            g.Key.FullName,
                                            ScoresRecorded = g.Count(),
                                            CalculatedTotal = g.Sum(x => x.Score /* / x.MaxScore * x.WeightPercentage */),
                                            Scores = g.ToList(),
                                            Programme = g.FirstOrDefault().Student.Programme.Name,
                                            StudentStudyPeriod = g.FirstOrDefault().Student.CurrentYearPeriodLabel,
                                            Course = g.FirstOrDefault().Course.CourseCode + " - " + g.FirstOrDefault().Course.CourseName,
                                            Year = g.FirstOrDefault().AcademicYear.YearValue,
                                            Semester = g.FirstOrDefault().YearPeriodId,
                                            PassMark = g.FirstOrDefault().Course.PassMark
                                        })
                                        .ToListAsync();

            var resultsList = new List<ImportedResultInfo>();
            int rowNumber = 1;

            foreach (var result in results.OrderBy(r => r.StudentId_Number))
            {
                var grade = GetGradeFromPercentage(result.CalculatedTotal);
                var isPassed = result.CalculatedTotal >= (decimal)result.PassMark;

                // Check for integrity issues
                bool hasIntegrityIssue = false;
                foreach (var score in result.Scores)
                {
                    var expectedHash = CalculateScoreHash(
                        score.StudentId,
                        score.CourseId,
                        score.AssessmentId,
                        score.Score,
                        score.WeightPercentage);

                    if (score.ScoreHash != expectedHash)
                    {
                        //hasIntegrityIssue = true;
                        break;
                    }
                }

                resultsList.Add(new ImportedResultInfo
                {
                    RowNumber = rowNumber++,
                    StudentNumber = result.StudentId_Number,
                    StudentName = result.FullName,
                    StudentStudyPeriod = result.StudentStudyPeriod,
                    ScoresRecorded = result.ScoresRecorded,
                    CalculatedTotal = result.CalculatedTotal,
                    Grade = grade,
                    IsPassed = isPassed,
                    HadIntegrityIssue = hasIntegrityIssue,
                    Programme = result.Programme,
                    Course = result.Course,
                    Year = result.Year,
                    Semester = result.Semester
                });
            }

            return resultsList;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading assessment scores results");
            throw;
        }
    }

    private async Task<List<ImportedResultInfo>> LoadCourseResultsAsync(int batchId)
    {
        try
        {
            // Get result IDs from batch
            var batch = await _context.ResultSubmissionBatches
                .FirstOrDefaultAsync(b => b.Id == batchId);

            if (batch == null || string.IsNullOrEmpty(batch.CourseResultIds))
            {
                return new List<ImportedResultInfo>();
            }

            var resultIds = System.Text.Json.JsonSerializer
                .Deserialize<List<int>>(batch.CourseResultIds) ?? new List<int>();

            // Load course results
            var results = await _context.StudentCourseResults
                .Include(r => r.Student)
                .Where(r => resultIds.Contains(r.Id))
                .OrderBy(r => r.Student.StudentId_Number)
                .ToListAsync();

            var resultsList = new List<ImportedResultInfo>();
            int rowNumber = 1;

            foreach (var result in results)
            {
                // Check integrity
                var expectedHash = CalculateResultHash(
                    result.StudentId,
                    result.CourseId,
                    result.NormalizedTotal,
                    result.GradeLetter,
                    result.Credits);

                var hasIntegrityIssue = result.ResultHash != expectedHash;

                resultsList.Add(new ImportedResultInfo
                {
                    RowNumber = rowNumber++,
                    StudentNumber = result.Student.StudentId_Number,
                    StudentName = result.Student.FullName,
                    ScoresRecorded = result.AssessmentCount,
                    CalculatedTotal = result.NormalizedTotal,
                    Grade = result.GradeLetter,
                    IsPassed = result.IsPassed,
                    HadIntegrityIssue = hasIntegrityIssue
                });
            }

            return resultsList;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading course results");
            throw;
        }
    }

    private string GetGradeFromPercentage(decimal percentage)
    {
        // Get grade configurations
        var grades = _context.GradeConfigurations
            .Where(g => g.IsActive)
            .OrderByDescending(g => g.MinScore)
            .ToList();

        // Determine grade
        var gradeConfig = grades.FirstOrDefault(g =>
            percentage >= g.MinScore && percentage <= g.MaxScore);

        if(gradeConfig != null)
        {
            return gradeConfig.GradeLetter;
        }

        return "F";
    }

    private string CalculateScoreHash(int studentId, int courseId, int assessmentId, decimal score, decimal weight)
    {
        const string Salt = "YourSecretSalt_ChangeThis"; // Should match the salt used during creation
        var data = $"{studentId}|{courseId}|{assessmentId}|{score}|{weight}|{Salt}";

        using (var sha256 = System.Security.Cryptography.SHA256.Create())
        {
            var bytes = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(data));
            var builder = new System.Text.StringBuilder();
            foreach (var b in bytes)
            {
                builder.Append(b.ToString("x2"));
            }
            return builder.ToString();
        }
    }

    private string CalculateResultHash(int studentId, int courseId, decimal normalizedTotal, string gradeLetter, int credits)
    {
        const string Salt = "YourSecretSalt_ChangeThis"; // Should match the salt used during creation
        var data = $"{studentId}|{courseId}|{normalizedTotal}|{gradeLetter}|{credits}|{Salt}";

        using (var sha256 = System.Security.Cryptography.SHA256.Create())
        {
            var bytes = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(data));
            var builder = new System.Text.StringBuilder();
            foreach (var b in bytes)
            {
                builder.Append(b.ToString("x2"));
            }
            return builder.ToString();
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CancelWorkflow(int workflowInstanceId, string reason)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(reason) || reason.Length < 10)
            {
                TempData["Error"] = "Please provide a detailed reason for cancellation (at least 10 characters).";
                return RedirectToAction(nameof(WorkflowDetails), new { id = workflowInstanceId });
            }

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var workflow = await _workflowService.GetWorkflowByIdAsync(workflowInstanceId);

            if (workflow == null)
            {
                TempData["Error"] = "Workflow not found.";
                return RedirectToAction(nameof(PendingApprovals));
            }

            // Check if user is the initiator or has admin rights
            if (workflow.InitiatedById != userId && !User.IsInRole("Admin") && !User.IsInRole("Registrar"))
            {
                TempData["Error"] = "You do not have permission to cancel this workflow.";
                return RedirectToAction(nameof(WorkflowDetails), new { id = workflowInstanceId });
            }

            await _workflowService.CancelWorkflowAsync(workflowInstanceId, userId, reason);

            TempData["Success"] = "Workflow cancelled successfully.";
            return RedirectToAction(nameof(WorkflowDetails), new { id = workflowInstanceId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cancelling workflow {WorkflowId}", workflowInstanceId);
            TempData["Error"] = "An error occurred while cancelling the workflow.";
            return RedirectToAction(nameof(WorkflowDetails), new { id = workflowInstanceId });
        }
    }

    #endregion

    #region Approval History

    public async Task<IActionResult> ApprovalHistory(DateTime? startDate, DateTime? endDate, string status, string entityType)
    {
        try
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var isAdmin = User.IsInRole("Admin") || User.IsInRole("Registrar") || User.IsInRole("HOD") || User.IsInRole("Dean");

            // Get workflows
            var query = _context.WorkflowInstances
                .Include(w => w.WorkflowTemplate)
                .Include(w => w.InitiatedBy)
                .Include(w => w.Approvals)
                    .ThenInclude(a => a.Approver)
                .Include(w => w.Approvals)
                    .ThenInclude(a => a.WorkflowStage)
                .AsQueryable();

            // Filter by user if not admin
            if (!isAdmin)
            {
                query = query.Where(w => w.InitiatedById == userId ||
                    w.Approvals.Any(a => a.ApproverId == userId));
            }

            // Apply filters
            if (startDate.HasValue)
            {
                query = query.Where(w => w.StartedAt >= startDate.Value);
            }

            if (endDate.HasValue)
            {
                query = query.Where(w => w.StartedAt <= endDate.Value.AddDays(1));
            }

            if (!string.IsNullOrEmpty(status) && Enum.TryParse<WorkflowStatus>(status, out var workflowStatus))
            {
                query = query.Where(w => w.Status == workflowStatus);
            }

            if (!string.IsNullOrEmpty(entityType))
            {
                query = query.Where(w => w.EntityType == entityType);
            }

            var workflows = await query
                .OrderByDescending(w => w.StartedAt)
                .Take(100) // Limit to recent 100
                .ToListAsync();

            var workflowDtos = workflows.Select(w => w.ToDto()).ToList();

            ViewBag.StartDate = startDate?.ToString("yyyy-MM-dd");
            ViewBag.EndDate = endDate?.ToString("yyyy-MM-dd");
            ViewBag.Status = status;
            ViewBag.EntityType = entityType;

            return View(workflowDtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading approval history");
            TempData["Error"] = "An error occurred while loading approval history.";
            return View(new List<WorkflowInstanceDto>());
        }
    }

    [HttpGet]
    public async Task<IActionResult> ExportHistory()
    {
        try
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var isAdmin = User.IsInRole("Admin") || User.IsInRole("Registrar");

            var query = _context.WorkflowInstances
                .Include(w => w.WorkflowTemplate)
                .Include(w => w.InitiatedBy)
                .Include(w => w.Approvals)
                    .ThenInclude(a => a.Approver)
                .AsQueryable();

            if (!isAdmin)
            {
                query = query.Where(w => w.InitiatedById == userId);
            }

            var workflows = await query
                .OrderByDescending(w => w.StartedAt)
                .ToListAsync();

            // Generate CSV
            var csv = new System.Text.StringBuilder();
            csv.AppendLine("Workflow ID,Entity Type,Template,Status,Initiated By,Started,Completed,Duration (Days)");

            foreach (var workflow in workflows)
            {
                var duration = workflow.CompletedAt.HasValue
                    ? (workflow.CompletedAt.Value - workflow.StartedAt).TotalDays.ToString("0.0")
                    : "N/A";

                csv.AppendLine($"{workflow.Id},{workflow.EntityType},{workflow.WorkflowTemplate?.Name},{workflow.Status},{workflow.InitiatedBy?.UserName},{workflow.StartedAt:yyyy-MM-dd},{workflow.CompletedAt?.ToString("yyyy-MM-dd") ?? "N/A"},{duration}");
            }

            var bytes = System.Text.Encoding.UTF8.GetBytes(csv.ToString());
            return File(bytes, "text/csv", $"ApprovalHistory_{DateTime.Now:yyyyMMdd}.csv");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting approval history");
            TempData["Error"] = "An error occurred while exporting history.";
            return RedirectToAction(nameof(ApprovalHistory));
        }
    }

    #endregion

    #region Statistics

    [Authorize(Roles = "Admin,Registrar,HOD,Dean")]
    public async Task<IActionResult> Statistics()
    {
        try
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var isAdmin = User.IsInRole("Admin") || User.IsInRole("Registrar");

            var statistics = await _statisticsService.GetStatisticsAsync(isAdmin ? null : userId);
            var performanceMetrics = await _statisticsService.GetPerformanceMetricsAsync(
                DateTime.Now.AddMonths(-6), DateTime.Now);
            var workloadMetrics = await _statisticsService.GetApproverWorkloadAsync();

            ViewBag.Statistics = statistics;
            ViewBag.PerformanceMetrics = performanceMetrics;
            ViewBag.WorkloadMetrics = workloadMetrics;

            return View();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading workflow statistics");
            TempData["Error"] = "An error occurred while loading statistics.";
            return View();
        }
    }

    #endregion

    #region Helper Methods

    private List<string> GetEntityTypes()
    {
        return new List<string>
        {
            "ResultSubmissionBatch",
            "StudentAssessmentScore",
            "StudentCourseResult",
            "CourseModification",
            "ProgrammeModification"
        };
    }

    #endregion
}
