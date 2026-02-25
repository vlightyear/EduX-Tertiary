// ==================== INTERFACES ====================

using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SIS.Data;
using SIS.Models.StudentResults;
using SIS.Services.Emails;
using System.Text.Json;

public interface IWorkflowService
{
    Task<WorkflowInstance> InitiateWorkflowAsync(int templateId, string entityType, int entityId, string initiatedById);
    Task<WorkflowApproval> ApproveAsync(int workflowInstanceId, string approverId, string comments, List<string>? attachments = null);
    Task<WorkflowApproval> RejectAsync(int workflowInstanceId, string approverId, string comments);
    Task<WorkflowApproval> ReturnForRevisionAsync(int workflowInstanceId, string approverId, string comments);
    Task<WorkflowApproval> DelegateApprovalAsync(int workflowInstanceId, string fromApproverId, string toApproverId, string reason);
    Task<WorkflowInstance?> GetWorkflowByEntityAsync(string entityType, int entityId);
    Task<WorkflowInstance?> GetWorkflowByIdAsync(int workflowInstanceId);
    Task<List<WorkflowApproval>> GetPendingApprovalsForUserAsync(string userId);
    Task<List<WorkflowInstance>> GetWorkflowsByStatusAsync(WorkflowStatus status);
    Task<List<WorkflowInstance>> GetWorkflowHistoryForEntityAsync(string entityType, int entityId);
    Task<bool> CancelWorkflowAsync(int workflowInstanceId, string cancelledById, string reason);
    Task<bool> CanUserApproveAsync(int workflowInstanceId, string userId);
    Task ProcessAutoApprovalsAsync();
}

// ==================== WORKFLOW SERVICE IMPLEMENTATION ====================

public class WorkflowService : IWorkflowService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<WorkflowService> _logger;
    private readonly IEmailService _notificationService;
    private readonly UserManager<ApplicationUser> _userManager;

    public WorkflowService(
        ApplicationDbContext context,
        ILogger<WorkflowService> logger,
        IEmailService notificationService,
        UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _logger = logger;
        _notificationService = notificationService;
        _userManager = userManager;
    }

    public async Task<WorkflowInstance> InitiateWorkflowAsync(
    int templateId,
    string entityType,
    int entityId,
    string initiatedById)
    {
        // Create execution strategy to handle retries safely
        var strategy = _context.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                // Validate template exists and is active
                var template = await _context.WorkflowTemplates
                    .Include(t => t.Stages.OrderBy(s => s.StageOrder))
                    .FirstOrDefaultAsync(t => t.Id == templateId && t.IsActive);

                if (template == null)
                {
                    throw new InvalidOperationException("Workflow template not found or inactive.");
                }

                if (!template.Stages.Any())
                {
                    throw new InvalidOperationException("Workflow template has no stages configured.");
                }

                // Check if workflow already exists for this entity
                var existingWorkflow = await _context.WorkflowInstances
                    .FirstOrDefaultAsync(w => w.EntityType == entityType
                        && w.EntityId == entityId
                        && w.Status != WorkflowStatus.Cancelled
                        && w.Status != WorkflowStatus.Approved
                        && w.Status != WorkflowStatus.Rejected);

                if (existingWorkflow != null)
                {
                    throw new InvalidOperationException(
                        $"An active workflow already exists for this {entityType}. Workflow ID: {existingWorkflow.Id}");
                }

                // Create workflow instance
                var workflowInstance = new WorkflowInstance
                {
                    WorkflowTemplateId = templateId,
                    EntityType = entityType,
                    EntityId = entityId,
                    InitiatedById = initiatedById,
                    Status = WorkflowStatus.Pending,
                    CurrentStageOrder = 1,
                    StartedAt = DateTime.Now,
                    CreatedBy = initiatedById,
                    CreatedAt = DateTime.Now
                };

                _context.WorkflowInstances.Add(workflowInstance);
                await _context.SaveChangesAsync();

                // Create approval record for the first stage
                var firstStage = template.Stages.OrderBy(s => s.StageOrder).First();
                await CreateApprovalForStageAsync(workflowInstance, firstStage, entityType, entityId);

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                _logger.LogInformation(
                    "Workflow {WorkflowId} initiated by {UserId} for {EntityType} {EntityId}. Template: {TemplateName}",
                    workflowInstance.Id, initiatedById, entityType, entityId, template.Name);

                return workflowInstance;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error initiating workflow for {EntityType} {EntityId}", entityType, entityId);
                throw;
            }
        });
    }

    public async Task<WorkflowApproval> ApproveAsync(
    int workflowInstanceId,
    string approverId,
    string comments,
    List<string>? attachments = null)
    {
        var strategy = _context.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _context.Database.BeginTransactionAsync();
            bool transactionComplete = false;
            try
            {
                var workflowInstance = await LoadWorkflowInstanceAsync(workflowInstanceId);
                if (workflowInstance == null)
                    throw new InvalidOperationException("Workflow instance not found.");

                if (workflowInstance.Status is WorkflowStatus.Approved or WorkflowStatus.Rejected or WorkflowStatus.Cancelled)
                    throw new InvalidOperationException($"This workflow is already {workflowInstance.Status}.");

                var currentApproval = workflowInstance.Approvals
                    .FirstOrDefault(a => a.ApproverId == approverId && !a.IsCompleted)
                    ?? throw new InvalidOperationException("No pending approval found for this user.");

                if (!await CanUserApproveAsync(workflowInstanceId, approverId))
                    throw new UnauthorizedAccessException("User does not have permission to approve at this stage.");

                // Update approval details
                currentApproval.Action = ApprovalAction.Approved;
                currentApproval.ActionedAt = DateTime.Now;
                currentApproval.Comments = comments;
                currentApproval.IsCompleted = true;
                currentApproval.UpdatedBy = approverId;
                currentApproval.UpdatedAt = DateTime.Now;
                if (attachments?.Any() == true)
                    currentApproval.Attachments = JsonSerializer.Serialize(attachments);

                workflowInstance.Status = WorkflowStatus.InProgress;
                workflowInstance.UpdatedBy = approverId;
                workflowInstance.UpdatedAt = DateTime.Now;

                // Workflow progression
                var allStages = workflowInstance.WorkflowTemplate.Stages.OrderBy(s => s.StageOrder).ToList();
                var currentStageIndex = allStages.FindIndex(s => s.StageOrder == workflowInstance.CurrentStageOrder);

                WorkflowStage? nextStage = allStages.Skip(currentStageIndex + 1).FirstOrDefault(s => !s.IsOptional);

                if (nextStage != null)
                {
                    workflowInstance.CurrentStageOrder = nextStage.StageOrder;
                    await CreateApprovalForStageAsync(workflowInstance, nextStage, workflowInstance.EntityType, workflowInstance.EntityId);
                }
                else
                {
                    workflowInstance.Status = WorkflowStatus.Approved;
                    workflowInstance.CompletedAt = DateTime.Now;

                    await UpdateEntityStatusAsync(workflowInstance.EntityType, workflowInstance.EntityId, WorkflowStatus.Approved);
                    _logger.LogInformation($"Workflow {workflowInstanceId} completed successfully. All stages approved.");
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                transactionComplete = true;

                // Outside transaction: notifications
                await _notificationService.NotifyApprovalActionAsync(workflowInstance, currentApproval);
                if (workflowInstance.Status == WorkflowStatus.Approved)
                    await _notificationService.NotifyWorkflowCompletionAsync(workflowInstance);

                _logger.LogInformation($"Approval granted by {approverId} for workflow {workflowInstanceId} at stage {currentApproval.WorkflowStage.StageName}");

                return currentApproval;
            }
            catch (Exception ex)
            {
                if(!transactionComplete)
                    await transaction.RollbackAsync();

                _logger.LogError(ex, $"Error approving workflow {workflowInstanceId}");
                throw;
            }
        });
    }

    public async Task<WorkflowApproval> RejectAsync(
    int workflowInstanceId,
    string approverId,
    string comments)
    {
        var strategy = _context.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                if (string.IsNullOrWhiteSpace(comments))
                    throw new ArgumentException("Comments are required when rejecting.");

                var workflowInstance = await LoadWorkflowInstanceAsync(workflowInstanceId)
                    ?? throw new InvalidOperationException("Workflow instance not found.");

                if (workflowInstance.Status == WorkflowStatus.Approved)
                    throw new InvalidOperationException("Cannot reject an approved workflow.");

                if (workflowInstance.Status == WorkflowStatus.Rejected)
                    throw new InvalidOperationException("This workflow has already been rejected.");

                if (!workflowInstance.WorkflowTemplate.AllowRejection)
                    throw new InvalidOperationException("This workflow template does not allow rejections.");

                var currentApproval = workflowInstance.Approvals
                    .FirstOrDefault(a => a.ApproverId == approverId && !a.IsCompleted)
                    ?? throw new InvalidOperationException("No pending approval found for this user.");

                if (!await CanUserApproveAsync(workflowInstanceId, approverId))
                    throw new UnauthorizedAccessException("User does not have permission to reject at this stage.");

                currentApproval.Action = ApprovalAction.Rejected;
                currentApproval.ActionedAt = DateTime.Now;
                currentApproval.Comments = comments;
                currentApproval.IsCompleted = true;
                currentApproval.UpdatedBy = approverId;
                currentApproval.UpdatedAt = DateTime.Now;

                workflowInstance.Status = WorkflowStatus.Rejected;
                workflowInstance.CompletedAt = DateTime.Now;
                workflowInstance.UpdatedBy = approverId;
                workflowInstance.UpdatedAt = DateTime.Now;

                await UpdateEntityStatusAsync(workflowInstance.EntityType, workflowInstance.EntityId, WorkflowStatus.Rejected);

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                // Outside the transaction — send notifications
                await _notificationService.NotifyApprovalActionAsync(workflowInstance, currentApproval);
                await _notificationService.NotifyWorkflowRejectionAsync(workflowInstance, currentApproval);

                _logger.LogInformation(
                    $"Workflow {workflowInstanceId} rejected by {approverId} at stage {currentApproval.WorkflowStage.StageName}. Reason: {comments}");

                return currentApproval;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, $"Error rejecting workflow {workflowInstanceId}");
                throw;
            }
        });
    }

    public async Task<WorkflowApproval> ReturnForRevisionAsync(
    int workflowInstanceId,
    string approverId,
    string comments)
    {
        var strategy = _context.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                if (string.IsNullOrWhiteSpace(comments))
                    throw new ArgumentException("Comments are required when returning for revision.");

                var workflowInstance = await LoadWorkflowInstanceAsync(workflowInstanceId)
                    ?? throw new InvalidOperationException("Workflow instance not found.");

                var currentApproval = workflowInstance.Approvals
                    .FirstOrDefault(a => a.ApproverId == approverId && !a.IsCompleted)
                    ?? throw new InvalidOperationException("No pending approval found for this user.");

                currentApproval.Action = ApprovalAction.Returned;
                currentApproval.ActionedAt = DateTime.Now;
                currentApproval.Comments = comments;
                currentApproval.IsCompleted = true;
                currentApproval.UpdatedBy = approverId;
                currentApproval.UpdatedAt = DateTime.Now;

                workflowInstance.Status = WorkflowStatus.Returned;
                workflowInstance.UpdatedBy = approverId;
                workflowInstance.UpdatedAt = DateTime.Now;

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                // Outside transaction — notify user(s)
                await _notificationService.NotifyApprovalActionAsync(workflowInstance, currentApproval);

                _logger.LogInformation(
                    $"Workflow {workflowInstanceId} returned for revision by {approverId}. Comments: {comments}");

                return currentApproval;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, $"Error returning workflow {workflowInstanceId} for revision");
                throw;
            }
        });
    }

    public async Task<WorkflowApproval> DelegateApprovalAsync(
    int workflowInstanceId,
    string fromApproverId,
    string toApproverId,
    string reason)
    {
        var strategy = _context.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                if (string.IsNullOrWhiteSpace(reason))
                    throw new ArgumentException("Reason is required for delegation.");

                if (fromApproverId == toApproverId)
                    throw new InvalidOperationException("Cannot delegate to yourself.");

                var approval = await _context.WorkflowApprovals
                    .Include(a => a.WorkflowStage)
                    .Include(a => a.WorkflowInstance)
                    .FirstOrDefaultAsync(a =>
                        a.WorkflowInstanceId == workflowInstanceId &&
                        a.ApproverId == fromApproverId &&
                        !a.IsCompleted)
                    ?? throw new InvalidOperationException("No pending approval found for this user.");

                if (!approval.WorkflowStage.AllowDelegation)
                    throw new InvalidOperationException("This stage does not allow delegation.");

                var targetUser = await _userManager.FindByIdAsync(toApproverId)
                    ?? throw new InvalidOperationException("Target user not found.");

                var originalApproverId = approval.ApproverId;

                approval.DelegatedToId = toApproverId;
                approval.ApproverId = toApproverId;
                approval.Comments = $"Delegated from {originalApproverId} to {toApproverId}. Reason: {reason}";
                approval.UpdatedBy = fromApproverId;
                approval.UpdatedAt = DateTime.Now;

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                // Notifications outside the transaction
                await _notificationService.NotifyDelegationAsync(workflowInstanceId, fromApproverId, toApproverId, reason);

                _logger.LogInformation(
                    $"Approval for workflow {workflowInstanceId} delegated from {fromApproverId} to {toApproverId}. Reason: {reason}");

                return approval;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, $"Error delegating approval for workflow {workflowInstanceId}");
                throw;
            }
        });
    }

    public async Task<WorkflowInstance?> GetWorkflowByEntityAsync(string entityType, int entityId)
    {
        return await _context.WorkflowInstances
            .Include(w => w.WorkflowTemplate)
                .ThenInclude(t => t.Stages.OrderBy(s => s.StageOrder))
            .Include(w => w.Approvals.OrderBy(a => a.AssignedAt))
                .ThenInclude(a => a.Approver)
            .Include(w => w.Approvals)
                .ThenInclude(a => a.WorkflowStage)
            .Include(w => w.InitiatedBy)
            .OrderByDescending(w => w.StartedAt)
            .FirstOrDefaultAsync(w => w.EntityType == entityType && w.EntityId == entityId);
    }

    public async Task<WorkflowInstance?> GetWorkflowByIdAsync(int workflowInstanceId)
    {
        return await LoadWorkflowInstanceAsync(workflowInstanceId);
    }

    public async Task<List<WorkflowApproval>> GetPendingApprovalsForUserAsync(string userId)
    {
        return await _context.WorkflowApprovals
            .Include(a => a.WorkflowInstance)
                .ThenInclude(w => w.WorkflowTemplate)
            .Include(a => a.WorkflowInstance)
                .ThenInclude(w => w.InitiatedBy)
            .Include(a => a.WorkflowStage)
            .Where(a => a.ApproverId == userId
                && !a.IsCompleted
                && a.WorkflowInstance.Status != WorkflowStatus.Cancelled
                && a.WorkflowInstance.Status != WorkflowStatus.Approved
                && a.WorkflowInstance.Status != WorkflowStatus.Rejected)
            .OrderBy(a => a.AssignedAt)
            .ToListAsync();
    }

    public async Task<List<WorkflowInstance>> GetWorkflowsByStatusAsync(WorkflowStatus status)
    {
        return await _context.WorkflowInstances
            .Include(w => w.WorkflowTemplate)
            .Include(w => w.InitiatedBy)
            .Include(w => w.Approvals)
                .ThenInclude(a => a.Approver)
            .Where(w => w.Status == status)
            .OrderByDescending(w => w.StartedAt)
            .ToListAsync();
    }

    public async Task<List<WorkflowInstance>> GetWorkflowHistoryForEntityAsync(
        string entityType,
        int entityId)
    {
        return await _context.WorkflowInstances
            .Include(w => w.WorkflowTemplate)
            .Include(w => w.InitiatedBy)
            .Include(w => w.Approvals.OrderBy(a => a.AssignedAt))
                .ThenInclude(a => a.Approver)
            .Include(w => w.Approvals)
                .ThenInclude(a => a.WorkflowStage)
            .Where(w => w.EntityType == entityType && w.EntityId == entityId)
            .OrderByDescending(w => w.StartedAt)
            .ToListAsync();
    }

    public async Task<bool> CancelWorkflowAsync(
        int workflowInstanceId,
        string cancelledById,
        string reason)
    {
        using var transaction = await _context.Database.BeginTransactionAsync();

        try
        {
            var workflow = await _context.WorkflowInstances
                .FirstOrDefaultAsync(w => w.Id == workflowInstanceId);

            if (workflow == null)
            {
                return false;
            }

            if (workflow.Status == WorkflowStatus.Approved ||
                workflow.Status == WorkflowStatus.Rejected)
            {
                throw new InvalidOperationException(
                    "Cannot cancel a workflow that has already been completed.");
            }

            workflow.Status = WorkflowStatus.Cancelled;
            workflow.CompletedAt = DateTime.Now;
            workflow.UpdatedBy = cancelledById;
            workflow.UpdatedAt = DateTime.Now;

            // Cancel pending approvals
            var pendingApprovals = await _context.WorkflowApprovals
                .Where(a => a.WorkflowInstanceId == workflowInstanceId && !a.IsCompleted)
                .ToListAsync();

            foreach (var approval in pendingApprovals)
            {
                approval.IsCompleted = true;
                approval.Action = ApprovalAction.Cancelled;
                approval.Comments = $"Workflow cancelled by {cancelledById}. Reason: {reason}";
                approval.ActionedAt = DateTime.Now;
            }

            await UpdateEntityStatusAsync(
                workflow.EntityType,
                workflow.EntityId,
                WorkflowStatus.Cancelled);

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            _logger.LogInformation(
                $"Workflow {workflowInstanceId} cancelled by {cancelledById}. Reason: {reason}");

            return true;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, $"Error cancelling workflow {workflowInstanceId}");
            throw;
        }
    }

    public async Task<bool> CanUserApproveAsync(int workflowInstanceId, string userId)
    {
        var approval = await _context.WorkflowApprovals
            .Include(a => a.WorkflowInstance)
            .Include(a => a.WorkflowStage)
            .FirstOrDefaultAsync(a => a.WorkflowInstanceId == workflowInstanceId
                && a.ApproverId == userId
                && !a.IsCompleted);

        if (approval == null)
        {
            return false;
        }

        // Check if workflow is in a state that allows approval
        if (approval.WorkflowInstance.Status != WorkflowStatus.Pending &&
            approval.WorkflowInstance.Status != WorkflowStatus.InProgress)
        {
            return false;
        }

        // Verify it's the current stage
        if (approval.WorkflowInstance.CurrentStageOrder != approval.WorkflowStage.StageOrder)
        {
            return false;
        }

        return true;
    }

    public async Task ProcessAutoApprovalsAsync()
    {
        var autoApprovalCandidates = await _context.WorkflowApprovals
            .Include(a => a.WorkflowStage)
            .Include(a => a.WorkflowInstance)
            .Where(a => !a.IsCompleted
                && a.WorkflowStage.AutoApproveAfterDays.HasValue
                && a.WorkflowInstance.Status == WorkflowStatus.Pending
                && a.AssignedAt.AddDays(a.WorkflowStage.AutoApproveAfterDays.Value) <= DateTime.Now)
            .ToListAsync();

        foreach (var approval in autoApprovalCandidates)
        {
            try
            {
                await ApproveAsync(
                    approval.WorkflowInstanceId,
                    approval.ApproverId,
                    $"Auto-approved after {approval.WorkflowStage.AutoApproveAfterDays} days timeout");

                _logger.LogInformation(
                    $"Auto-approved workflow {approval.WorkflowInstanceId} at stage {approval.WorkflowStage.StageName}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    $"Error auto-approving workflow {approval.WorkflowInstanceId}");
            }
        }
    }

    // ==================== PRIVATE HELPER METHODS ====================

    private async Task<WorkflowInstance?> LoadWorkflowInstanceAsync(int workflowInstanceId)
    {
        return await _context.WorkflowInstances
            .Include(w => w.WorkflowTemplate)
                .ThenInclude(t => t.Stages.OrderBy(s => s.StageOrder))
            .Include(w => w.Approvals.OrderBy(a => a.AssignedAt))
                .ThenInclude(a => a.Approver)
            .Include(w => w.Approvals)
                .ThenInclude(a => a.WorkflowStage)
            .Include(w => w.InitiatedBy)
            .FirstOrDefaultAsync(w => w.Id == workflowInstanceId);
    }

    private async Task CreateApprovalForStageAsync(
        WorkflowInstance workflowInstance,
        WorkflowStage stage,
        string entityType,
        int entityId)
    {
        string? approverId = null;

        // Determine approver based on configuration priority:
        // 1. Specific User ID
        // 2. Property Path (dynamic resolution)
        // 3. Role-based resolution

        if (!string.IsNullOrEmpty(stage.RequiredUserId))
        {
            approverId = stage.RequiredUserId;
        }
        else if (!string.IsNullOrEmpty(stage.ApproverPropertyPath))
        {
            approverId = await ResolveApproverFromPathAsync(entityType, entityId, stage.ApproverPropertyPath);
        }
        else if (!string.IsNullOrEmpty(stage.RequiredRole))
        {
            approverId = await GetUserByRoleAsync(stage.RequiredRole, entityType, entityId);
        }

        if (string.IsNullOrEmpty(approverId))
        {
            throw new InvalidOperationException(
                $"Could not determine approver for stage '{stage.StageName}'. " +
                $"Please check the workflow configuration.");
        }

        // Verify approver exists
        var approver = await _userManager.FindByIdAsync(approverId);
        if (approver == null)
        {
            throw new InvalidOperationException(
                $"Approver with ID {approverId} not found for stage '{stage.StageName}'.");
        }

        var approval = new WorkflowApproval
        {
            WorkflowInstanceId = workflowInstance.Id,
            WorkflowStageId = stage.Id,
            ApproverId = approverId,
            Action = ApprovalAction.Submitted,
            AssignedAt = DateTime.Now,
            IsCompleted = false,
            CreatedBy = workflowInstance.InitiatedById,
            CreatedAt = DateTime.Now
        };

        _context.WorkflowApprovals.Add(approval);

        if (stage.SendNotification)
        {
            await _notificationService.NotifyApprovalRequestAsync(workflowInstance, approval);
        }

        _logger.LogInformation(
            $"Created approval record for workflow {workflowInstance.Id}, " +
            $"stage '{stage.StageName}', assigned to {approverId}");
    }

    private async Task<string?> ResolveApproverFromPathAsync(
        string entityType,
        int entityId,
        string propertyPath)
    {
        try
        {
            if (entityType == "ResultSubmissionBatch")
            {
                var batch = await _context.Set<ResultSubmissionBatch>()
                    .Include(b => b.Course)
                        .ThenInclude(c => c.Programme)
                            .ThenInclude(p => p.Department)
                                .ThenInclude(d => d.HOD)
                    .Include(b => b.Course)
                        .ThenInclude(c => c.Programme)
                            .ThenInclude(p => p.Department)
                                .ThenInclude(d => d.School)
                                    .ThenInclude(s => s.Dean)
                    .Include(b => b.Course)
                        .ThenInclude(c => c.Programme)
                            .ThenInclude(p => p.Department)
                                .ThenInclude(d => d.School)
                                    .ThenInclude(s => s.AssistantDean)
                    .Include(b => b.Course)
                        .ThenInclude(c => c.Programme)
                            .ThenInclude(p => p.Coordinator)
                    .FirstOrDefaultAsync(b => b.Id == entityId);

                if (batch == null)
                {
                    _logger.LogWarning($"ResultSubmissionBatch {entityId} not found");
                    return null;
                }

                return propertyPath switch
                {
                    "Course.Programme.Department.HODId" => batch.Course?.Programme?.Department?.HODId,
                    "Course.Programme.Department.School.DeanId" => batch.Course?.Programme?.Department?.School?.DeanId,
                    "Course.Programme.Department.School.AssistantDeanId" => batch.Course?.Programme?.Department?.School?.AssistantDeanId,
                    "Course.Programme.CoordinatorId" => batch.Course?.Programme?.CoordinatorId,
                    "Course.InstructorId" => batch.Course?.InstructorId,
                    _ => null
                };
            }

            if (entityType == "StudentAssessmentScore")
            {
                var score = await _context.Set<StudentAssessmentScore>()
                    .Include(s => s.Course)
                        .ThenInclude(c => c.Programme)
                            .ThenInclude(p => p.Department)
                                .ThenInclude(d => d.HOD)
                    .Include(s => s.Course)
                        .ThenInclude(c => c.Programme)
                            .ThenInclude(p => p.Department)
                                .ThenInclude(d => d.School)
                                    .ThenInclude(sc => sc.Dean)
                    .FirstOrDefaultAsync(s => s.Id == entityId);

                if (score == null)
                {
                    _logger.LogWarning($"StudentAssessmentScore {entityId} not found");
                    return null;
                }

                return propertyPath switch
                {
                    "Course.Programme.Department.HODId" => score.Course?.Programme?.Department?.HODId,
                    "Course.Programme.Department.School.DeanId" => score.Course?.Programme?.Department?.School?.DeanId,
                    "Course.InstructorId" => score.Course?.InstructorId,
                    _ => null
                };
            }

            if (entityType == "StudentCourseResult")
            {
                var result = await _context.Set<StudentCourseResult>()
                    .Include(r => r.Course)
                        .ThenInclude(c => c.Programme)
                            .ThenInclude(p => p.Department)
                                .ThenInclude(d => d.HOD)
                    .Include(r => r.Course)
                        .ThenInclude(c => c.Programme)
                            .ThenInclude(p => p.Department)
                                .ThenInclude(d => d.School)
                                    .ThenInclude(sc => sc.Dean)
                    .FirstOrDefaultAsync(r => r.Id == entityId);

                if (result == null)
                {
                    _logger.LogWarning($"StudentCourseResult {entityId} not found");
                    return null;
                }

                return propertyPath switch
                {
                    "Course.Programme.Department.HODId" => result.Course?.Programme?.Department?.HODId,
                    "Course.Programme.Department.School.DeanId" => result.Course?.Programme?.Department?.School?.DeanId,
                    "Course.InstructorId" => result.Course?.InstructorId,
                    _ => null
                };
            }

            // Add more entity types as needed
            _logger.LogWarning($"Entity type {entityType} not supported for property path resolution");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                $"Error resolving approver from path {propertyPath} for {entityType} {entityId}");
            return null;
        }
    }

    private async Task<string?> GetUserByRoleAsync(string role, string entityType, int entityId)
    {
        try
        {
            // For Senate or Registrar roles, get any user with that role
            if (role == "Senate" || role == "Registrar")
            {
                var usersInRole = await _userManager.GetUsersInRoleAsync(role);
                var user = usersInRole.FirstOrDefault();

                if (user != null)
                {
                    return user.Id;
                }

                _logger.LogWarning($"No users found with role {role}");
                return null;
            }

            // For context-specific roles like HOD or Dean, resolve based on entity
            if (entityType == "ResultSubmissionBatch")
            {
                if (role == "HOD")
                {
                    return await ResolveApproverFromPathAsync(
                        entityType,
                        entityId,
                        "Course.Programme.Department.HODId");
                }

                if (role == "Dean")
                {
                    return await ResolveApproverFromPathAsync(
                        entityType,
                        entityId,
                        "Course.Programme.Department.School.DeanId");
                }

                if (role == "ProgrammeCoordinator")
                {
                    return await ResolveApproverFromPathAsync(
                        entityType,
                        entityId,
                        "Course.Programme.CoordinatorId");
                }
            }

            _logger.LogWarning($"Role {role} not supported for entity type {entityType}");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error getting user by role {role} for {entityType} {entityId}");
            return null;
        }
    }

    private async Task UpdateEntityStatusAsync(
        string entityType,
        int entityId,
        WorkflowStatus status)
    {
        try
        {
            if (entityType == "ResultSubmissionBatch")
            {
                var batch = await _context.Set<ResultSubmissionBatch>()
                    .FirstOrDefaultAsync(b => b.Id == entityId);

                if (batch != null)
                {
                    batch.ApprovalStatus = status;
                    batch.UpdatedAt = DateTime.Now;

                    _logger.LogInformation(
                        $"Updated ResultSubmissionBatch {entityId} status to {status}");
                }
            }

            // Add more entity types as needed
            // Example for Course modifications:
            // if (entityType == "CourseModification")
            // {
            //     var modification = await _context.Set<CourseModification>()
            //         .FirstOrDefaultAsync(m => m.Id == entityId);
            //     if (modification != null)
            //     {
            //         modification.ApprovalStatus = status;
            //     }
            // }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                $"Error updating entity status for {entityType} {entityId}");
            // Don't throw - entity status update failure shouldn't break the workflow
        }
    }
}

// ==================== WORKFLOW STATISTICS SERVICE ====================

public interface IWorkflowStatisticsService
{
    Task<WorkflowStatistics> GetStatisticsAsync(string? userId = null);
    Task<List<WorkflowPerformanceMetric>> GetPerformanceMetricsAsync(DateTime startDate, DateTime endDate);
    Task<List<ApproverWorkloadMetric>> GetApproverWorkloadAsync();
}

public class WorkflowStatistics
{
    public int TotalWorkflows { get; set; }
    public int PendingWorkflows { get; set; }
    public int InProgressWorkflows { get; set; }
    public int ApprovedWorkflows { get; set; }
    public int RejectedWorkflows { get; set; }
    public int CancelledWorkflows { get; set; }
    public double AverageCompletionTimeHours { get; set; }
    public int PendingApprovals { get; set; }
    public int OverdueApprovals { get; set; }
}

public class WorkflowPerformanceMetric
{
    public string EntityType { get; set; }
    public string TemplateName { get; set; }
    public int TotalInstances { get; set; }
    public int ApprovedCount { get; set; }
    public int RejectedCount { get; set; }
    public double ApprovalRate { get; set; }
    public double AverageCompletionTimeHours { get; set; }
    public double AverageStageTimeHours { get; set; }
}

public class ApproverWorkloadMetric
{
    public string ApproverId { get; set; }
    public string ApproverName { get; set; }
    public int PendingCount { get; set; }
    public int CompletedCount { get; set; }
    public int OverdueCount { get; set; }
    public double AverageResponseTimeHours { get; set; }
}

public class WorkflowStatisticsService : IWorkflowStatisticsService
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    public WorkflowStatisticsService(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    public async Task<WorkflowStatistics> GetStatisticsAsync(string? userId = null)
    {
        var query = _context.WorkflowInstances.AsQueryable();

        if (!string.IsNullOrEmpty(userId))
        {
            query = query.Where(w => w.InitiatedById == userId);
        }

        var totalWorkflows = await query.CountAsync();
        var pendingWorkflows = await query.CountAsync(w => w.Status == WorkflowStatus.Pending);
        var inProgressWorkflows = await query.CountAsync(w => w.Status == WorkflowStatus.InProgress);
        var approvedWorkflows = await query.CountAsync(w => w.Status == WorkflowStatus.Approved);
        var rejectedWorkflows = await query.CountAsync(w => w.Status == WorkflowStatus.Rejected);
        var cancelledWorkflows = await query.CountAsync(w => w.Status == WorkflowStatus.Cancelled);

        var completedWorkflows = await query
            .Where(w => w.CompletedAt.HasValue)
            .ToListAsync();

        var avgCompletionTime = completedWorkflows.Any()
            ? completedWorkflows.Average(w => (w.CompletedAt!.Value - w.StartedAt).TotalHours)
            : 0;

        var pendingApprovalsQuery = _context.WorkflowApprovals
            .Where(a => !a.IsCompleted
                && a.WorkflowInstance.Status != WorkflowStatus.Cancelled);

        if (!string.IsNullOrEmpty(userId))
        {
            pendingApprovalsQuery = pendingApprovalsQuery.Where(a => a.ApproverId == userId);
        }

        var pendingApprovals = await pendingApprovalsQuery.CountAsync();

        var overdueApprovals = await pendingApprovalsQuery
            .Include(a => a.WorkflowStage)
            .Where(a => a.WorkflowStage.AutoApproveAfterDays.HasValue
                && a.AssignedAt.AddDays(a.WorkflowStage.AutoApproveAfterDays.Value) <= DateTime.Now)
            .CountAsync();

        return new WorkflowStatistics
        {
            TotalWorkflows = totalWorkflows,
            PendingWorkflows = pendingWorkflows,
            InProgressWorkflows = inProgressWorkflows,
            ApprovedWorkflows = approvedWorkflows,
            RejectedWorkflows = rejectedWorkflows,
            CancelledWorkflows = cancelledWorkflows,
            AverageCompletionTimeHours = avgCompletionTime,
            PendingApprovals = pendingApprovals,
            OverdueApprovals = overdueApprovals
        };
    }

    public async Task<List<WorkflowPerformanceMetric>> GetPerformanceMetricsAsync(
        DateTime startDate,
        DateTime endDate)
    {
        var workflows = await _context.WorkflowInstances
            .Include(w => w.WorkflowTemplate)
            .Include(w => w.Approvals)
            .Where(w => w.StartedAt >= startDate && w.StartedAt <= endDate)
            .ToListAsync();

        var metrics = workflows
            .GroupBy(w => new { w.EntityType, w.WorkflowTemplate.Name })
            .Select(g => new WorkflowPerformanceMetric
            {
                EntityType = g.Key.EntityType,
                TemplateName = g.Key.Name,
                TotalInstances = g.Count(),
                ApprovedCount = g.Count(w => w.Status == WorkflowStatus.Approved),
                RejectedCount = g.Count(w => w.Status == WorkflowStatus.Rejected),
                ApprovalRate = g.Count() > 0
                    ? (double)g.Count(w => w.Status == WorkflowStatus.Approved) / g.Count() * 100
                    : 0,
                AverageCompletionTimeHours = g.Where(w => w.CompletedAt.HasValue).Any()
                    ? g.Where(w => w.CompletedAt.HasValue)
                       .Average(w => (w.CompletedAt!.Value - w.StartedAt).TotalHours)
                    : 0,
                AverageStageTimeHours = g.SelectMany(w => w.Approvals)
                    .Where(a => a.IsCompleted && a.ActionedAt.HasValue)
                    .Any()
                    ? g.SelectMany(w => w.Approvals)
                       .Where(a => a.IsCompleted && a.ActionedAt.HasValue)
                       .Average(a => (a.ActionedAt!.Value - a.AssignedAt).TotalHours)
                    : 0
            })
            .ToList();

        return metrics;
    }

    public async Task<List<ApproverWorkloadMetric>> GetApproverWorkloadAsync()
    {
        var approvals = await _context.WorkflowApprovals
            .Include(a => a.Approver)
            .Include(a => a.WorkflowStage)
            .ToListAsync();

        var metrics = approvals
            .GroupBy(a => new { a.ApproverId, a.Approver.UserName })
            .Select(g => new ApproverWorkloadMetric
            {
                ApproverId = g.Key.ApproverId,
                ApproverName = g.Key.UserName ?? "Unknown",
                PendingCount = g.Count(a => !a.IsCompleted),
                CompletedCount = g.Count(a => a.IsCompleted),
                OverdueCount = g.Count(a => !a.IsCompleted
                    && a.WorkflowStage.AutoApproveAfterDays.HasValue
                    && a.AssignedAt.AddDays(a.WorkflowStage.AutoApproveAfterDays.Value) <= DateTime.Now),
                AverageResponseTimeHours = g.Where(a => a.IsCompleted && a.ActionedAt.HasValue).Any()
                    ? g.Where(a => a.IsCompleted && a.ActionedAt.HasValue)
                       .Average(a => (a.ActionedAt!.Value - a.AssignedAt).TotalHours)
                    : 0
            })
            .OrderByDescending(m => m.PendingCount)
            .ToList();

        return metrics;
    }
}

// ==================== WORKFLOW VALIDATION SERVICE ====================

public interface IWorkflowValidationService
{
    Task<WorkflowValidationResult> ValidateWorkflowTemplateAsync(WorkflowTemplate template, List<WorkflowStageDto> stages);
    Task<WorkflowValidationResult> ValidateWorkflowInitiationAsync(int templateId, string entityType, int entityId);
}

public class WorkflowValidationResult
{
    public bool IsValid { get; set; }
    public List<string> Errors { get; set; } = new List<string>();
    public List<string> Warnings { get; set; } = new List<string>();
}

public class WorkflowValidationService : IWorkflowValidationService
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    public WorkflowValidationService(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    public async Task<WorkflowValidationResult> ValidateWorkflowTemplateAsync(
        WorkflowTemplate template,
        List<WorkflowStageDto> stages)
    {
        var result = new WorkflowValidationResult { IsValid = true };

        // Validate template
        if (string.IsNullOrWhiteSpace(template.Name))
        {
            result.Errors.Add("Template name is required");
            result.IsValid = false;
        }

        if (string.IsNullOrWhiteSpace(template.EntityType))
        {
            result.Errors.Add("Entity type is required");
            result.IsValid = false;
        }

        // Validate stages
        if (stages == null || !stages.Any())
        {
            result.Errors.Add("At least one stage is required");
            result.IsValid = false;
            return result;
        }

        // Check for duplicate stage orders
        var duplicateOrders = stages
            .GroupBy(s => s.StageOrder)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        if (duplicateOrders.Any())
        {
            result.Errors.Add($"Duplicate stage orders found: {string.Join(", ", duplicateOrders)}");
            result.IsValid = false;
        }

        // Check sequential stage orders
        var orderedStages = stages.OrderBy(s => s.StageOrder).ToList();
        for (int i = 0; i < orderedStages.Count; i++)
        {
            if (orderedStages[i].StageOrder != i + 1)
            {
                result.Warnings.Add($"Stage orders are not sequential. Expected {i + 1}, found {orderedStages[i].StageOrder}");
            }
        }

        // Validate each stage
        foreach (var stage in stages)
        {
            if (string.IsNullOrWhiteSpace(stage.StageName))
            {
                result.Errors.Add($"Stage name is required for stage order {stage.StageOrder}");
                result.IsValid = false;
            }

            // Ensure at least one approver resolution method is specified
            if (string.IsNullOrWhiteSpace(stage.RequiredUserId) &&
                string.IsNullOrWhiteSpace(stage.RequiredRole) &&
                string.IsNullOrWhiteSpace(stage.ApproverPropertyPath))
            {
                result.Errors.Add(
                    $"Stage '{stage.StageName}' must specify at least one approver resolution method: " +
                    "RequiredUserId, RequiredRole, or ApproverPropertyPath");
                result.IsValid = false;
            }

            // Validate specific user exists
            if (!string.IsNullOrWhiteSpace(stage.RequiredUserId))
            {
                var user = await _userManager.FindByIdAsync(stage.RequiredUserId);
                if (user == null)
                {
                    result.Errors.Add($"User with ID {stage.RequiredUserId} not found for stage '{stage.StageName}'");
                    result.IsValid = false;
                }
            }

            // Warn about auto-approval
            if (stage.AutoApproveAfterDays.HasValue && stage.AutoApproveAfterDays.Value < 1)
            {
                result.Warnings.Add(
                    $"Stage '{stage.StageName}' has auto-approval set to less than 1 day. " +
                    "This may result in immediate auto-approval.");
            }
        }

        return result;
    }

    public async Task<WorkflowValidationResult> ValidateWorkflowInitiationAsync(
        int templateId,
        string entityType,
        int entityId)
    {
        var result = new WorkflowValidationResult { IsValid = true };

        // Check if template exists
        var template = await _context.WorkflowTemplates
            .Include(t => t.Stages)
            .FirstOrDefaultAsync(t => t.Id == templateId);

        if (template == null)
        {
            result.Errors.Add("Workflow template not found");
            result.IsValid = false;
            return result;
        }

        if (!template.IsActive)
        {
            result.Errors.Add("Workflow template is not active");
            result.IsValid = false;
        }

        if (template.EntityType != entityType)
        {
            result.Errors.Add(
                $"Template entity type mismatch. Expected: {template.EntityType}, Provided: {entityType}");
            result.IsValid = false;
        }

        // Check if entity exists
        if (entityType == "ResultSubmissionBatch")
        {
            var batchExists = await _context.Set<ResultSubmissionBatch>()
                .AnyAsync(b => b.Id == entityId);

            if (!batchExists)
            {
                result.Errors.Add($"ResultSubmissionBatch with ID {entityId} not found");
                result.IsValid = false;
            }
        }

        // Check for existing active workflows
        var existingWorkflow = await _context.WorkflowInstances
            .FirstOrDefaultAsync(w => w.EntityType == entityType
                && w.EntityId == entityId
                && w.Status != WorkflowStatus.Approved
                && w.Status != WorkflowStatus.Rejected
                && w.Status != WorkflowStatus.Cancelled);

        if (existingWorkflow != null)
        {
            result.Errors.Add(
                $"An active workflow (ID: {existingWorkflow.Id}) already exists for this {entityType}");
            result.IsValid = false;
        }

        return result;
    }
}
