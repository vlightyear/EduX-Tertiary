// ==================== ENUMS ====================

using SIS.Data;
using SIS.Enums;
using SIS.Models;
using SIS.Models.Admin;
using SIS.Models.Registration;
using SIS.Models.StudentApplication;
using SIS.Models.StudentResults;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

public enum WorkflowStatus
{
    Draft,
    Pending,
    InProgress,
    Approved,
    Rejected,
    Returned,
    Cancelled,
    Published
}

public enum ApprovalStage
{
    Lecturer = 1,
    HOD = 2,
    Dean = 3,
    Senate = 4
}

public enum ApprovalAction
{
    Submitted,
    Approved,
    Rejected,
    Returned,
    Cancelled
}

// ==================== WORKFLOW MODELS ====================

/// <summary>
/// Defines configurable workflow templates for different entity types
/// </summary>
public class WorkflowTemplate : AuditClass
{
    [Key]
    public int Id { get; set; }

    [Required]
    [StringLength(100)]
    public string Name { get; set; }

    [StringLength(500)]
    public string Description { get; set; }

    /// <summary>
    /// The entity type this workflow applies to (e.g., "StudentAssessmentScore", "StudentCourseResult")
    /// </summary>
    [Required]
    [StringLength(100)]
    public string EntityType { get; set; }

    /// <summary>
    /// Is this template currently active?
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Should all stages approve, or just one?
    /// </summary>
    public bool RequiresAllStages { get; set; } = true;

    /// <summary>
    /// Can approvers at any stage reject and send back?
    /// </summary>
    public bool AllowRejection { get; set; } = true;

    /// <summary>
    /// Navigation property for stages
    /// </summary>
    public virtual ICollection<WorkflowStage> Stages { get; set; } = new List<WorkflowStage>();
}

/// <summary>
/// Defines individual stages in a workflow template
/// </summary>
public class WorkflowStage : AuditClass
{
    [Key]
    public int Id { get; set; }

    [Required]
    public int WorkflowTemplateId { get; set; }

    [ForeignKey("WorkflowTemplateId")]
    public virtual WorkflowTemplate WorkflowTemplate { get; set; }

    [Required]
    [StringLength(100)]
    public string StageName { get; set; }

    [Required]
    public int StageOrder { get; set; }

    /// <summary>
    /// Role required for this stage (e.g., "HOD", "Dean", "Senate")
    /// </summary>
    [StringLength(100)]
    public string? RequiredRole { get; set; }

    /// <summary>
    /// Specific user ID if a particular user must approve
    /// </summary>
    public string? RequiredUserId { get; set; }

    /// <summary>
    /// Property path to determine approver dynamically (e.g., "Course.Programme.Department.HODId")
    /// </summary>
    [StringLength(500)]
    public string? ApproverPropertyPath { get; set; }

    /// <summary>
    /// Should this stage send email notifications?
    /// </summary>
    public bool SendNotification { get; set; } = true;

    /// <summary>
    /// Auto-approve after X days if no action taken?
    /// </summary>
    public int? AutoApproveAfterDays { get; set; }

    /// <summary>
    /// Is this stage optional?
    /// </summary>
    public bool IsOptional { get; set; } = false;

    /// <summary>
    /// Can this stage delegate to another user?
    /// </summary>
    public bool AllowDelegation { get; set; } = true;
}

/// <summary>
/// Represents an active workflow instance for a specific entity
/// </summary>
public class WorkflowInstance : AuditClass
{
    [Key]
    public int Id { get; set; }

    [Required]
    public int WorkflowTemplateId { get; set; }

    [ForeignKey("WorkflowTemplateId")]
    public virtual WorkflowTemplate WorkflowTemplate { get; set; }

    /// <summary>
    /// The type of entity this workflow is for
    /// </summary>
    [Required]
    [StringLength(100)]
    public string EntityType { get; set; }

    /// <summary>
    /// The ID of the entity being approved (e.g., ResultSubmissionBatchId)
    /// </summary>
    [Required]
    public int EntityId { get; set; }

    /// <summary>
    /// User who initiated the workflow
    /// </summary>
    [Required]
    public string InitiatedById { get; set; }

    [ForeignKey("InitiatedById")]
    public virtual ApplicationUser InitiatedBy { get; set; }

    /// <summary>
    /// Current status of the workflow
    /// </summary>
    [Required]
    public WorkflowStatus Status { get; set; } = WorkflowStatus.Pending;

    /// <summary>
    /// Current stage the workflow is at
    /// </summary>
    public int? CurrentStageOrder { get; set; }

    /// <summary>
    /// When was this workflow started?
    /// </summary>
    public DateTime StartedAt { get; set; } = DateTime.Now;

    /// <summary>
    /// When was this workflow completed?
    /// </summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// Additional metadata as JSON
    /// </summary>
    [Column(TypeName = "nvarchar(max)")]
    public string? Metadata { get; set; }

    /// <summary>
    /// Navigation property for approval steps
    /// </summary>
    public virtual ICollection<WorkflowApproval> Approvals { get; set; } = new List<WorkflowApproval>();
}

/// <summary>
/// Represents individual approval actions within a workflow instance
/// </summary>
public class WorkflowApproval : AuditClass
{
    [Key]
    public int Id { get; set; }

    [Required]
    public int WorkflowInstanceId { get; set; }

    [ForeignKey("WorkflowInstanceId")]
    public virtual WorkflowInstance WorkflowInstance { get; set; }

    [Required]
    public int WorkflowStageId { get; set; }

    [ForeignKey("WorkflowStageId")]
    public virtual WorkflowStage WorkflowStage { get; set; }

    /// <summary>
    /// User who needs to approve (or has approved)
    /// </summary>
    [Required]
    public string ApproverId { get; set; }

    [ForeignKey("ApproverId")]
    public virtual ApplicationUser Approver { get; set; }

    /// <summary>
    /// The action taken
    /// </summary>
    [Required]
    public ApprovalAction Action { get; set; }

    /// <summary>
    /// When was this assigned to the approver?
    /// </summary>
    public DateTime AssignedAt { get; set; } = DateTime.Now;

    /// <summary>
    /// When did the approver take action?
    /// </summary>
    public DateTime? ActionedAt { get; set; }

    /// <summary>
    /// Comments from the approver
    /// </summary>
    [StringLength(1000)]
    public string? Comments { get; set; }

    /// <summary>
    /// Has this approval been completed?
    /// </summary>
    public bool IsCompleted { get; set; } = false;

    /// <summary>
    /// If delegated, who was it delegated to?
    /// </summary>
    public string? DelegatedToId { get; set; }

    [ForeignKey("DelegatedToId")]
    public virtual ApplicationUser? DelegatedTo { get; set; }

    /// <summary>
    /// Attachments or documents uploaded during approval
    /// </summary>
    [Column(TypeName = "nvarchar(max)")]
    public string? Attachments { get; set; } // JSON array of file paths
}

// ==================== RESULT SUBMISSION BATCH MODEL ====================

/// <summary>
/// Represents a batch of assessment scores or course results submitted for approval
/// Replaces the previous AssessmentResultBatch model
/// </summary>
public class ResultSubmissionBatch : AuditClass
{
    [Key]
    public int Id { get; set; }

    [Required]
    public int CourseId { get; set; }

    [ForeignKey("CourseId")]
    public virtual Course Course { get; set; }

    /// <summary>
    /// Optional: Specific assessment ID if this batch is for a single assessment
    /// Null if this is a final course result batch
    /// </summary>
    public int? AssessmentId { get; set; }

    [ForeignKey("AssessmentId")]
    public virtual Assessment? Assessment { get; set; }

    /// <summary>
    /// Type of submission: "AssessmentScores" or "CourseResults"
    /// </summary>
    [Required]
    [StringLength(50)]
    public string SubmissionType { get; set; } // "AssessmentScores" or "CourseResults"

    /// <summary>
    /// Lecturer who uploaded the results
    /// </summary>
    [Required]
    public string UploadedById { get; set; }

    [ForeignKey("UploadedById")]
    public virtual ApplicationUser UploadedBy { get; set; }

    /// <summary>
    /// Academic year and semester
    /// </summary>
    [Required]
    public int AcademicYearId { get; set; }

    [ForeignKey("AcademicYearId")]
    public virtual AcademicYear AcademicYear { get; set; }

    [Required]
    public int YearPeriodId { get; set; }

    /// <summary>
    /// Total number of students/records in this batch
    /// </summary>
    public int TotalRecords { get; set; }

    /// <summary>
    /// When were results uploaded?
    /// </summary>
    public DateTime UploadedAt { get; set; } = DateTime.Now;

    /// <summary>
    /// Current approval status
    /// </summary>
    [Required]
    public WorkflowStatus ApprovalStatus { get; set; } = WorkflowStatus.Draft;

    /// <summary>
    /// Link to the workflow instance
    /// </summary>
    public int? WorkflowInstanceId { get; set; }

    [ForeignKey("WorkflowInstanceId")]
    public virtual WorkflowInstance? WorkflowInstance { get; set; }

    /// <summary>
    /// Batch-level remarks
    /// </summary>
    [StringLength(1000)]
    public string? Remarks { get; set; }

    /// <summary>
    /// Batch verification hash for integrity
    /// </summary>
    [Required]
    [MaxLength(64)]
    public string BatchHash { get; set; }

    /// <summary>
    /// IDs of StudentAssessmentScore records in this batch (JSON array)
    /// Used when SubmissionType = "AssessmentScores"
    /// </summary>
    [Column(TypeName = "nvarchar(max)")]
    public string? AssessmentScoreIds { get; set; }

    /// <summary>
    /// IDs of StudentCourseResult records in this batch (JSON array)
    /// Used when SubmissionType = "CourseResults"
    /// </summary>
    [Column(TypeName = "nvarchar(max)")]
    public string? CourseResultIds { get; set; }

    /// <summary>
    /// When was this batch submitted for approval?
    /// </summary>
    public DateTime? SubmittedForApprovalAt { get; set; }

    /// <summary>
    /// When was this batch approved and published?
    /// </summary>
    public DateTime? ApprovedAt { get; set; }

    /// <summary>
    /// Who approved and published this batch?
    /// </summary>
    public string? ApprovedById { get; set; }

    [ForeignKey("ApprovedById")]
    public virtual ApplicationUser? ApprovedBy { get; set; }
}

// ==================== RESULT APPROVAL HISTORY ====================

/// <summary>
/// Tracks the approval history for individual assessment scores and course results
/// Provides an audit trail of changes and approvals
/// </summary>
public class ResultApprovalHistory : AuditClass
{
    [Key]
    public int Id { get; set; }

    /// <summary>
    /// Type of result: "AssessmentScore" or "CourseResult"
    /// </summary>
    [Required]
    [StringLength(50)]
    public string ResultType { get; set; }

    /// <summary>
    /// ID of the StudentAssessmentScore or StudentCourseResult
    /// </summary>
    [Required]
    public int ResultId { get; set; }

    [Required]
    public int StudentId { get; set; }

    [ForeignKey("StudentId")]
    public virtual Student Student { get; set; }

    [Required]
    public int CourseId { get; set; }

    [ForeignKey("CourseId")]
    public virtual Course Course { get; set; }

    /// <summary>
    /// Optional: Assessment ID if this is an assessment score
    /// </summary>
    public int? AssessmentId { get; set; }

    [ForeignKey("AssessmentId")]
    public virtual Assessment? Assessment { get; set; }

    [Required]
    public int ResultSubmissionBatchId { get; set; }

    [ForeignKey("ResultSubmissionBatchId")]
    public virtual ResultSubmissionBatch Batch { get; set; }

    /// <summary>
    /// The workflow approval that was performed
    /// </summary>
    public int? WorkflowApprovalId { get; set; }

    [ForeignKey("WorkflowApprovalId")]
    public virtual WorkflowApproval? WorkflowApproval { get; set; }

    /// <summary>
    /// Action taken: "Submitted", "Approved", "Rejected", "Modified"
    /// </summary>
    [Required]
    [StringLength(50)]
    public string Action { get; set; }

    /// <summary>
    /// User who performed the action
    /// </summary>
    [Required]
    public string ActionById { get; set; }

    [ForeignKey("ActionById")]
    public virtual ApplicationUser ActionBy { get; set; }

    /// <summary>
    /// Timestamp of the action
    /// </summary>
    [Required]
    public DateTime ActionAt { get; set; } = DateTime.Now;

    /// <summary>
    /// Previous value (JSON) before the action
    /// </summary>
    [Column(TypeName = "nvarchar(max)")]
    public string? PreviousValue { get; set; }

    /// <summary>
    /// New value (JSON) after the action
    /// </summary>
    [Column(TypeName = "nvarchar(max)")]
    public string? NewValue { get; set; }

    /// <summary>
    /// Comments/remarks about the action
    /// </summary>
    [StringLength(1000)]
    public string? Comments { get; set; }

    /// <summary>
    /// IP address of the user who performed the action
    /// </summary>
    [StringLength(50)]
    public string? IpAddress { get; set; }
}

// ==================== NOTIFICATION MODEL ====================

public class WorkflowNotification : AuditClass
{
    [Key]
    public int Id { get; set; }

    [Required]
    public int WorkflowInstanceId { get; set; }

    [ForeignKey("WorkflowInstanceId")]
    public virtual WorkflowInstance WorkflowInstance { get; set; }

    [Required]
    public string RecipientId { get; set; }

    [ForeignKey("RecipientId")]
    public virtual ApplicationUser Recipient { get; set; }

    [Required]
    [StringLength(200)]
    public string Subject { get; set; }

    [Required]
    public string Message { get; set; }

    public bool IsRead { get; set; } = false;

    public DateTime SentAt { get; set; } = DateTime.Now;

    public DateTime? ReadAt { get; set; }

    [StringLength(50)]
    public string NotificationType { get; set; } = "Email"; // Email, SMS, InApp
}

// ==================== HELPER EXTENSIONS ====================

/// <summary>
/// Extension methods for result models
/// </summary>
public static class ResultWorkflowExtensions
{
    /// <summary>
    /// Check if assessment score can be submitted for approval
    /// </summary>
    public static bool CanSubmitForApproval(this StudentAssessmentScore score)
    {
        return score.IsActive && score.Score >= 0 && score.Score <= 100;
    }

    /// <summary>
    /// Check if course result can be submitted for approval
    /// </summary>
    public static bool CanSubmitForApproval(this StudentCourseResult result)
    {
        return result.Status == Status.Draft
            && result.NormalizedTotal >= 0
            && result.NormalizedTotal <= 100
            && !string.IsNullOrEmpty(result.GradeLetter);
    }

    /// <summary>
    /// Get entity description for workflow display
    /// </summary>
    public static string GetWorkflowDescription(this StudentAssessmentScore score)
    {
        return $"Assessment Score - Student: {score.StudentId}, Course: {score.CourseId}, Assessment: {score.AssessmentId}";
    }

    /// <summary>
    /// Get entity description for workflow display
    /// </summary>
    public static string GetWorkflowDescription(this StudentCourseResult result)
    {
        return $"Course Result - Student: {result.StudentId}, Course: {result.CourseId}, Grade: {result.GradeLetter}";
    }
}
