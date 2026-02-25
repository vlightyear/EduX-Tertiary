// ==================== WORKFLOW DTOs ====================

using SIS.Models.Results;
using System.ComponentModel.DataAnnotations;

using System.ComponentModel.DataAnnotations.Schema;

/// <summary>
/// DTO for creating and updating workflow stages
/// </summary>
public class WorkflowStageDto
{
    /// <summary>
    /// Stage ID (null for new stages)
    /// </summary>
    public int? Id { get; set; }

    /// <summary>
    /// Name of the stage (e.g., "HOD Approval", "Dean Review")
    /// </summary>
    [Required(ErrorMessage = "Stage name is required")]
    [StringLength(100, ErrorMessage = "Stage name cannot exceed 100 characters")]
    public string StageName { get; set; }

    /// <summary>
    /// Order in which this stage appears in the workflow (1, 2, 3, etc.)
    /// </summary>
    [Required(ErrorMessage = "Stage order is required")]
    [Range(1, 100, ErrorMessage = "Stage order must be between 1 and 100")]
    public int StageOrder { get; set; }

    /// <summary>
    /// Role required for approval at this stage (e.g., "HOD", "Dean", "Senate", "Registrar")
    /// </summary>
    [StringLength(100)]
    public string? RequiredRole { get; set; }

    /// <summary>
    /// Specific user ID if a particular user must approve
    /// </summary>
    [StringLength(450)]
    public string? RequiredUserId { get; set; }

    /// <summary>
    /// Property path to dynamically determine approver 
    /// (e.g., "Course.Programme.Department.HODId")
    /// </summary>
    [StringLength(500)]
    public string? ApproverPropertyPath { get; set; }

    /// <summary>
    /// Whether to send email/notification when this stage is reached
    /// </summary>
    public bool SendNotification { get; set; } = true;

    /// <summary>
    /// Number of days after which to auto-approve if no action is taken (null = no auto-approval)
    /// </summary>
    [Range(1, 365, ErrorMessage = "Auto-approve days must be between 1 and 365")]
    public int? AutoApproveAfterDays { get; set; }

    /// <summary>
    /// Whether this stage is optional (can be skipped)
    /// </summary>
    public bool IsOptional { get; set; } = false;

    /// <summary>
    /// Whether approvers can delegate this approval to someone else
    /// </summary>
    public bool AllowDelegation { get; set; } = true;

    /// <summary>
    /// Description of what should be reviewed at this stage
    /// </summary>
    [StringLength(500)]
    public string? Description { get; set; }
}

/// <summary>
/// DTO for submitting assessment scores
/// </summary>
public class AssessmentScoreSubmissionDto
{
    /// <summary>
    /// Student ID
    /// </summary>
    [Required(ErrorMessage = "Student ID is required")]
    public int StudentId { get; set; }

    /// <summary>
    /// Score obtained by the student
    /// </summary>
    [Required(ErrorMessage = "Score is required")]
    [Range(0, 100, ErrorMessage = "Score must be between 0 and 100")]
    [Column(TypeName = "decimal(5,2)")]
    public decimal Score { get; set; }

    /// <summary>
    /// Maximum possible score for this assessment
    /// </summary>
    [Range(0, 100, ErrorMessage = "Max score must be between 0 and 100")]
    [Column(TypeName = "decimal(5,2)")]
    public decimal MaxScore { get; set; } = 100;

    /// <summary>
    /// Weight of this assessment in final grade calculation
    /// </summary>
    [Required(ErrorMessage = "Weight percentage is required")]
    [Range(0, 100, ErrorMessage = "Weight must be between 0 and 100")]
    [Column(TypeName = "decimal(5,2)")]
    public decimal WeightPercentage { get; set; }

    /// <summary>
    /// Additional comments or remarks
    /// </summary>
    [StringLength(500)]
    public string? Remarks { get; set; }
}

/// <summary>
/// DTO for submitting course results
/// </summary>
public class CourseResultSubmissionDto
{
    /// <summary>
    /// Student ID
    /// </summary>
    [Required(ErrorMessage = "Student ID is required")]
    public int StudentId { get; set; }

    /// <summary>
    /// Weighted total score before normalization
    /// </summary>
    [Required(ErrorMessage = "Weighted total is required")]
    [Range(0, 100, ErrorMessage = "Weighted total must be between 0 and 100")]
    [Column(TypeName = "decimal(5,2)")]
    public decimal WeightedTotal { get; set; }

    /// <summary>
    /// Normalized total score (final score)
    /// </summary>
    [Required(ErrorMessage = "Normalized total is required")]
    [Range(0, 100, ErrorMessage = "Normalized total must be between 0 and 100")]
    [Column(TypeName = "decimal(5,2)")]
    public decimal NormalizedTotal { get; set; }

    /// <summary>
    /// Letter grade (A+, A, B+, etc.)
    /// </summary>
    [Required(ErrorMessage = "Grade letter is required")]
    [MaxLength(5)]
    public string GradeLetter { get; set; }

    /// <summary>
    /// GPA points for this course
    /// </summary>
    [Required(ErrorMessage = "Grade points are required")]
    [Range(0, 5, ErrorMessage = "Grade points must be between 0 and 5")]
    [Column(TypeName = "decimal(3,2)")]
    public decimal GradePoints { get; set; }

    /// <summary>
    /// Whether the student passed
    /// </summary>
    [Required]
    public bool IsPassed { get; set; }

    /// <summary>
    /// Number of credits
    /// </summary>
    [Required(ErrorMessage = "Credits are required")]
    [Range(1, 10, ErrorMessage = "Credits must be between 1 and 10")]
    public int Credits { get; set; }

    /// <summary>
    /// Total weight percentage of all assessments
    /// </summary>
    [Required]
    [Range(0, 100, ErrorMessage = "Total weight must be between 0 and 100")]
    [Column(TypeName = "decimal(5,2)")]
    public decimal TotalWeightPercentage { get; set; }

    /// <summary>
    /// Number of assessments that contributed
    /// </summary>
    [Required]
    [Range(1, 20, ErrorMessage = "Assessment count must be between 1 and 20")]
    public int AssessmentCount { get; set; }

    /// <summary>
    /// Optional remarks
    /// </summary>
    [StringLength(1000)]
    public string? Remarks { get; set; }

    /// <summary>
    /// Whether this is a carryover/repeat
    /// </summary>
    public bool IsCarryover { get; set; } = false;

    /// <summary>
    /// Attempt number
    /// </summary>
    [Range(1, 5, ErrorMessage = "Attempt number must be between 1 and 5")]
    public int AttemptNumber { get; set; } = 1;
}

/// <summary>
/// DTO for workflow instance summary
/// </summary>
public class WorkflowInstanceDto
{
    public int Id { get; set; }
    public string EntityType { get; set; }
    public int EntityId { get; set; }
    public string TemplateName { get; set; }
    public WorkflowStatus Status { get; set; }
    public int? CurrentStageOrder { get; set; }
    public string? CurrentStageName { get; set; }
    public string InitiatedByName { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public List<WorkflowApprovalDto> Approvals { get; set; } = new List<WorkflowApprovalDto>();
}

/// <summary>
/// DTO for workflow approval details
/// </summary>
public class WorkflowApprovalDto
{
    public int Id { get; set; }
    public string StageName { get; set; }
    public int StageOrder { get; set; }
    public string ApproverName { get; set; }
    public string ApproverId { get; set; }
    public ApprovalAction Action { get; set; }
    public DateTime AssignedAt { get; set; }
    public DateTime? ActionedAt { get; set; }
    public string? Comments { get; set; }
    public bool IsCompleted { get; set; }
    public string? DelegatedToName { get; set; }
    public List<string>? Attachments { get; set; }
}

/// <summary>
/// DTO for pending approval items
/// </summary>
public class PendingApprovalDto
{
    public int WorkflowInstanceId { get; set; }
    public int ApprovalId { get; set; }
    public string EntityType { get; set; }
    public int EntityId { get; set; }
    public string StageName { get; set; }
    public string InitiatedByName { get; set; }
    public DateTime AssignedAt { get; set; }
    public int DaysWaiting { get; set; }
    public bool IsOverdue { get; set; }
    public string? EntityDescription { get; set; }
    public int AcademicYearId { get; set; }
    public int Semester { get; set; }
    public int YearOfStudy { get; set; }
}

public class WorkflowDetailsViewModel
{
    public WorkflowInstanceDto WorkflowInstance { get; set; }
    public List<ImportedResultInfo> SubmittedResults { get; set; }
}

// ==================== REQUEST MODELS ====================

/// <summary>
/// Request to create a new assessment score batch
/// </summary>
public class CreateAssessmentScoreBatchRequest
{
    [Required(ErrorMessage = "Course ID is required")]
    public int CourseId { get; set; }

    [Required(ErrorMessage = "Assessment ID is required")]
    public int AssessmentId { get; set; }

    [Required(ErrorMessage = "Academic year ID is required")]
    public int AcademicYearId { get; set; }

    [Required(ErrorMessage = "Semester is required")]
    [Range(1, 2, ErrorMessage = "Semester must be 1 or 2")]
    public int Semester { get; set; }
}

/// <summary>
/// Request to create a new course result batch
/// </summary>
public class CreateCourseResultBatchRequest
{
    [Required(ErrorMessage = "Course ID is required")]
    public int CourseId { get; set; }

    [Required(ErrorMessage = "Academic year ID is required")]
    public int AcademicYearId { get; set; }

    [Required(ErrorMessage = "Semester is required")]
    [Range(1, 2, ErrorMessage = "Semester must be 1 or 2")]
    public int Semester { get; set; }
}

/// <summary>
/// Request to upload assessment scores
/// </summary>
public class UploadAssessmentScoresRequest
{
    [Required(ErrorMessage = "At least one score is required")]
    [MinLength(1, ErrorMessage = "At least one score is required")]
    public List<AssessmentScoreSubmissionDto> Scores { get; set; }
}

/// <summary>
/// Request to upload course results
/// </summary>
public class UploadCourseResultsRequest
{
    [Required(ErrorMessage = "At least one result is required")]
    [MinLength(1, ErrorMessage = "At least one result is required")]
    public List<CourseResultSubmissionDto> Results { get; set; }
}

/// <summary>
/// Request to approve a workflow
/// </summary>
public class ApprovalRequest
{
    /// <summary>
    /// Comments explaining the approval decision
    /// </summary>
    [StringLength(1000, ErrorMessage = "Comments cannot exceed 1000 characters")]
    public string? Comments { get; set; }

    /// <summary>
    /// File paths or URLs of supporting documents
    /// </summary>
    public List<string>? Attachments { get; set; }
}

/// <summary>
/// Request to reject a workflow
/// </summary>
public class RejectionRequest
{
    /// <summary>
    /// Mandatory comments explaining the rejection
    /// </summary>
    [Required(ErrorMessage = "Comments are required when rejecting")]
    [StringLength(1000, ErrorMessage = "Comments cannot exceed 1000 characters")]
    [MinLength(10, ErrorMessage = "Please provide a detailed reason for rejection (at least 10 characters)")]
    public string Comments { get; set; }
}

/// <summary>
/// Request to return a workflow for revision
/// </summary>
public class ReturnForRevisionRequest
{
    /// <summary>
    /// Mandatory comments explaining what needs to be revised
    /// </summary>
    [Required(ErrorMessage = "Comments are required when returning for revision")]
    [StringLength(1000, ErrorMessage = "Comments cannot exceed 1000 characters")]
    [MinLength(10, ErrorMessage = "Please provide detailed revision requirements (at least 10 characters)")]
    public string Comments { get; set; }

    /// <summary>
    /// Specific issues that need to be addressed
    /// </summary>
    public List<string>? Issues { get; set; }
}

/// <summary>
/// Request to delegate an approval
/// </summary>
public class DelegationRequest
{
    /// <summary>
    /// User ID to delegate the approval to
    /// </summary>
    [Required(ErrorMessage = "Target user ID is required")]
    public string ToUserId { get; set; }

    /// <summary>
    /// Reason for delegation
    /// </summary>
    [Required(ErrorMessage = "Reason for delegation is required")]
    [StringLength(500, ErrorMessage = "Reason cannot exceed 500 characters")]
    [MinLength(5, ErrorMessage = "Please provide a reason for delegation (at least 5 characters)")]
    public string Reason { get; set; }
}

/// <summary>
/// Request to cancel a workflow
/// </summary>
public class CancelWorkflowRequest
{
    /// <summary>
    /// Reason for cancellation
    /// </summary>
    [Required(ErrorMessage = "Reason for cancellation is required")]
    [StringLength(500, ErrorMessage = "Reason cannot exceed 500 characters")]
    [MinLength(10, ErrorMessage = "Please provide a detailed reason for cancellation (at least 10 characters)")]
    public string Reason { get; set; }
}

/// <summary>
/// Request to create a new workflow template
/// </summary>
public class CreateTemplateRequest
{
    /// <summary>
    /// Template name
    /// </summary>
    [Required(ErrorMessage = "Template name is required")]
    [StringLength(100, ErrorMessage = "Template name cannot exceed 100 characters")]
    public string Name { get; set; }

    /// <summary>
    /// Template description
    /// </summary>
    [StringLength(500, ErrorMessage = "Description cannot exceed 500 characters")]
    public string? Description { get; set; }

    /// <summary>
    /// Entity type this template applies to (e.g., "AssessmentResultBatch")
    /// </summary>
    [Required(ErrorMessage = "Entity type is required")]
    [StringLength(100, ErrorMessage = "Entity type cannot exceed 100 characters")]
    public string EntityType { get; set; }

    /// <summary>
    /// Whether this template is active
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Whether all stages must approve
    /// </summary>
    public bool RequiresAllStages { get; set; } = true;

    /// <summary>
    /// Whether rejections are allowed
    /// </summary>
    public bool AllowRejection { get; set; } = true;

    /// <summary>
    /// Workflow stages
    /// </summary>
    [Required(ErrorMessage = "At least one stage is required")]
    [MinLength(1, ErrorMessage = "At least one stage is required")]
    public List<WorkflowStageDto> Stages { get; set; }
}

/// <summary>
/// Request to update an existing workflow template
/// </summary>
public class UpdateTemplateRequest : CreateTemplateRequest
{
    // Inherits all properties from CreateTemplateRequest
}

// ==================== RESPONSE MODELS ====================

/// <summary>
/// Standard API response wrapper
/// </summary>
public class ApiResponse<T>
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public T? Data { get; set; }
    public List<string>? Errors { get; set; }
}

/// <summary>
/// Response for batch creation
/// </summary>
public class CreateBatchResponse
{
    public int BatchId { get; set; }
    public string Message { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// Response for result upload
/// </summary>
public class UploadResultsResponse
{
    public int BatchId { get; set; }
    public int TotalResults { get; set; }
    public int SuccessfulUploads { get; set; }
    public int FailedUploads { get; set; }
    public List<string>? Errors { get; set; }
    public string Message { get; set; }
}

/// <summary>
/// Response for workflow submission
/// </summary>
public class SubmitWorkflowResponse
{
    public int WorkflowInstanceId { get; set; }
    public string CurrentStage { get; set; }
    public string CurrentApprover { get; set; }
    public string Message { get; set; }
}

/// <summary>
/// Response for approval action
/// </summary>
public class ApprovalActionResponse
{
    public int WorkflowInstanceId { get; set; }
    public WorkflowStatus WorkflowStatus { get; set; }
    public string? NextStage { get; set; }
    public string? NextApprover { get; set; }
    public string Message { get; set; }
    public DateTime ActionedAt { get; set; }
}

// ==================== MAPPING EXTENSIONS ====================

/// <summary>
/// Extension methods for mapping entities to DTOs
/// </summary>
public static class WorkflowMappingExtensions
{
    public static WorkflowInstanceDto ToDto(this WorkflowInstance workflow)
    {
        return new WorkflowInstanceDto
        {
            Id = workflow.Id,
            EntityType = workflow.EntityType,
            EntityId = workflow.EntityId,
            TemplateName = workflow.WorkflowTemplate?.Name ?? "Unknown",
            Status = workflow.Status,
            CurrentStageOrder = workflow.CurrentStageOrder,
            CurrentStageName = workflow.Approvals?
                .FirstOrDefault(a => !a.IsCompleted)?
                .WorkflowStage?.StageName,
            InitiatedByName = workflow.InitiatedBy?.UserName ?? "Unknown",
            StartedAt = workflow.StartedAt,
            CompletedAt = workflow.CompletedAt,
            Approvals = workflow.Approvals?
                .OrderBy(a => a.AssignedAt)
                .Select(a => a.ToDto())
                .ToList() ?? new List<WorkflowApprovalDto>()
        };
    }

    public static WorkflowApprovalDto ToDto(this WorkflowApproval approval)
    {
        return new WorkflowApprovalDto
        {
            Id = approval.Id,
            StageName = approval.WorkflowStage?.StageName ?? "Unknown",
            StageOrder = approval.WorkflowStage?.StageOrder ?? 0,
            ApproverName = approval.Approver?.UserName ?? "Unknown",
            ApproverId = approval.ApproverId,
            Action = approval.Action,
            AssignedAt = approval.AssignedAt,
            ActionedAt = approval.ActionedAt,
            Comments = approval.Comments,
            IsCompleted = approval.IsCompleted,
            DelegatedToName = approval.DelegatedTo?.UserName,
            Attachments = string.IsNullOrEmpty(approval.Attachments)
                ? null
                : System.Text.Json.JsonSerializer.Deserialize<List<string>>(approval.Attachments)
        };
    }

    public static PendingApprovalDto ToPendingDto(this WorkflowApproval approval)
    {
        var daysWaiting = (int)(DateTime.Now - approval.AssignedAt).TotalDays;
        var isOverdue = approval.WorkflowStage?.AutoApproveAfterDays.HasValue == true &&
                       daysWaiting >= approval.WorkflowStage.AutoApproveAfterDays.Value;

        return new PendingApprovalDto
        {
            WorkflowInstanceId = approval.WorkflowInstanceId,
            ApprovalId = approval.Id,
            EntityType = approval.WorkflowInstance?.EntityType ?? "Unknown",
            EntityId = approval.WorkflowInstance?.EntityId ?? 0,
            StageName = approval.WorkflowStage?.StageName ?? "Unknown",
            InitiatedByName = approval.WorkflowInstance?.InitiatedBy?.UserName ?? "Unknown",
            AssignedAt = approval.AssignedAt,
            DaysWaiting = daysWaiting,
            IsOverdue = isOverdue,
            EntityDescription = GetEntityDescription(approval.WorkflowInstance)
        };
    }

    private static string? GetEntityDescription(WorkflowInstance? workflow)
    {
        if (workflow == null) return null;

        return workflow.EntityType switch
        {
            "AssessmentResultBatch" => $"Assessment Results - Batch #{workflow.EntityId}",
            "CourseModification" => $"Course Modification - #{workflow.EntityId}",
            _ => $"{workflow.EntityType} - #{workflow.EntityId}"
        };
    }
}

// ==================== VALIDATION ATTRIBUTES ====================

/// <summary>
/// Custom validation attribute to ensure at least one approver resolution method is specified
/// </summary>
public class RequireApproverResolutionAttribute : ValidationAttribute
{
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        var stage = (WorkflowStageDto?)validationContext.ObjectInstance;

        if (stage == null)
            return new ValidationResult("Invalid stage object");

        if (string.IsNullOrWhiteSpace(stage.RequiredUserId) &&
            string.IsNullOrWhiteSpace(stage.RequiredRole) &&
            string.IsNullOrWhiteSpace(stage.ApproverPropertyPath))
        {
            return new ValidationResult(
                "At least one approver resolution method must be specified: RequiredUserId, RequiredRole, or ApproverPropertyPath");
        }

        return ValidationResult.Success;
    }
}

/// <summary>
/// Validation attribute to ensure rejection reason is detailed enough
/// </summary>
public class DetailedCommentsAttribute : ValidationAttribute
{
    private readonly int _minimumWords;

    public DetailedCommentsAttribute(int minimumWords = 3)
    {
        _minimumWords = minimumWords;
    }

    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value == null || string.IsNullOrWhiteSpace(value.ToString()))
            return ValidationResult.Success; // Let [Required] handle this

        var comments = value.ToString()!;
        var wordCount = comments.Split(new[] { ' ', '\t', '\n', '\r' },
            StringSplitOptions.RemoveEmptyEntries).Length;

        if (wordCount < _minimumWords)
        {
            return new ValidationResult(
                $"Please provide more detailed comments (at least {_minimumWords} words)");
        }

        return ValidationResult.Success;
    }
}