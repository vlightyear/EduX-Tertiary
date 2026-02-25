using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SIS.Data;

namespace SIS.Services
{
    // ==================== INTERFACE ====================

    public interface IWorkflowTemplateService
    {
        Task<WorkflowTemplate> CreateTemplateAsync(WorkflowTemplate template, List<WorkflowStageDto> stages);
        Task<WorkflowTemplate> UpdateTemplateAsync(int templateId, WorkflowTemplate template, List<WorkflowStageDto> stages);
        Task<WorkflowTemplate?> GetTemplateByIdAsync(int templateId);
        Task<WorkflowTemplate?> GetActiveTemplateForEntityAsync(string entityType);
        Task<List<WorkflowTemplate>> GetAllTemplatesAsync();
        Task<List<WorkflowTemplate>> GetTemplatesByEntityTypeAsync(string entityType);
        Task<bool> DeleteTemplateAsync(int templateId);
        Task<bool> ToggleTemplateStatusAsync(int templateId);
        Task<bool> TemplateHasActiveWorkflowsAsync(int templateId);
        Task<WorkflowTemplate> CloneTemplateAsync(int templateId, string newName);
    }

    // ==================== IMPLEMENTATION ====================

    public class WorkflowTemplateService : IWorkflowTemplateService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<WorkflowTemplateService> _logger;
        private readonly UserManager<ApplicationUser> _userManager;

        public WorkflowTemplateService(
            ApplicationDbContext context,
            ILogger<WorkflowTemplateService> logger,
            UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _logger = logger;
            _userManager = userManager;
        }

        public async Task<WorkflowTemplate> CreateTemplateAsync(
            WorkflowTemplate template,
            List<WorkflowStageDto> stages)
        {
            // Create an execution strategy
            var strategy = _context.Database.CreateExecutionStrategy();

            return await strategy.ExecuteAsync(async () =>
            {
                // Begin manual transaction inside execution strategy
                await using var transaction = await _context.Database.BeginTransactionAsync();

                try
                {
                    // Validate template doesn't already exist
                    var existingTemplate = await _context.WorkflowTemplates
                        .FirstOrDefaultAsync(t => t.Name == template.Name && t.EntityType == template.EntityType);

                    if (existingTemplate != null)
                    {
                        throw new InvalidOperationException(
                            $"A template with name '{template.Name}' for entity type '{template.EntityType}' already exists.");
                    }

                    // Validate stages
                    if (stages == null || !stages.Any())
                    {
                        throw new InvalidOperationException("At least one stage is required.");
                    }

                    // Ensure stages are properly ordered
                    var orderedStages = stages.OrderBy(s => s.StageOrder).ToList();
                    for (int i = 0; i < orderedStages.Count; i++)
                    {
                        if (orderedStages[i].StageOrder != i + 1)
                        {
                            _logger.LogWarning(
                                "Stage order was not sequential. Adjusting stage '{StageName}' from order {OldOrder} to {NewOrder}",
                                orderedStages[i].StageName, orderedStages[i].StageOrder, i + 1);
                            orderedStages[i].StageOrder = i + 1;
                        }
                    }

                    // Add template
                    _context.WorkflowTemplates.Add(template);
                    await _context.SaveChangesAsync();

                    _logger.LogInformation("Created workflow template {TemplateId}: {TemplateName}", template.Id, template.Name);

                    // Add stages
                    foreach (var stageDto in orderedStages)
                    {
                        // Validate approver configuration
                        if (string.IsNullOrEmpty(stageDto.RequiredUserId) &&
                            string.IsNullOrEmpty(stageDto.RequiredRole) &&
                            string.IsNullOrEmpty(stageDto.ApproverPropertyPath))
                        {
                            throw new InvalidOperationException(
                                $"Stage '{stageDto.StageName}' must have at least one approver resolution method specified.");
                        }

                        // Validate specific user if provided
                        if (!string.IsNullOrEmpty(stageDto.RequiredUserId))
                        {
                            var user = await _userManager.FindByIdAsync(stageDto.RequiredUserId);
                            if (user == null)
                            {
                                throw new InvalidOperationException(
                                    $"User with ID '{stageDto.RequiredUserId}' not found for stage '{stageDto.StageName}'.");
                            }
                        }

                        var stage = new WorkflowStage
                        {
                            WorkflowTemplateId = template.Id,
                            StageName = stageDto.StageName,
                            StageOrder = stageDto.StageOrder,
                            RequiredRole = stageDto.RequiredRole,
                            RequiredUserId = stageDto.RequiredUserId,
                            ApproverPropertyPath = stageDto.ApproverPropertyPath,
                            SendNotification = stageDto.SendNotification,
                            AutoApproveAfterDays = stageDto.AutoApproveAfterDays,
                            IsOptional = stageDto.IsOptional,
                            AllowDelegation = stageDto.AllowDelegation,
                            CreatedBy = template.CreatedBy,
                            CreatedAt = DateTime.Now
                        };

                        _context.WorkflowStages.Add(stage);
                    }

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    _logger.LogInformation(
                        "Successfully created workflow template '{TemplateName}' with {StageCount} stages",
                        template.Name, stages.Count);

                    return template;
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    _logger.LogError(ex, "Error creating workflow template '{TemplateName}'", template.Name);
                    throw;
                }
            });
        }

        /*public async Task<WorkflowTemplate> CreateTemplateAsync(
            WorkflowTemplate template,
            List<WorkflowStageDto> stages)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                // Validate template doesn't already exist
                var existingTemplate = await _context.WorkflowTemplates
                    .FirstOrDefaultAsync(t => t.Name == template.Name && t.EntityType == template.EntityType);

                if (existingTemplate != null)
                {
                    throw new InvalidOperationException(
                        $"A template with name '{template.Name}' for entity type '{template.EntityType}' already exists.");
                }

                // Validate stages
                if (stages == null || !stages.Any())
                {
                    throw new InvalidOperationException("At least one stage is required.");
                }

                // Ensure stages are properly ordered
                var orderedStages = stages.OrderBy(s => s.StageOrder).ToList();
                for (int i = 0; i < orderedStages.Count; i++)
                {
                    if (orderedStages[i].StageOrder != i + 1)
                    {
                        _logger.LogWarning(
                            "Stage order was not sequential. Adjusting stage '{StageName}' from order {OldOrder} to {NewOrder}",
                            orderedStages[i].StageName, orderedStages[i].StageOrder, i + 1);
                        orderedStages[i].StageOrder = i + 1;
                    }
                }

                // Add template
                _context.WorkflowTemplates.Add(template);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Created workflow template {TemplateId}: {TemplateName}", template.Id, template.Name);

                // Add stages
                foreach (var stageDto in orderedStages)
                {
                    // Validate approver configuration
                    if (string.IsNullOrEmpty(stageDto.RequiredUserId) &&
                        string.IsNullOrEmpty(stageDto.RequiredRole) &&
                        string.IsNullOrEmpty(stageDto.ApproverPropertyPath))
                    {
                        throw new InvalidOperationException(
                            $"Stage '{stageDto.StageName}' must have at least one approver resolution method specified.");
                    }

                    // Validate specific user if provided
                    if (!string.IsNullOrEmpty(stageDto.RequiredUserId))
                    {
                        var user = await _userManager.FindByIdAsync(stageDto.RequiredUserId);
                        if (user == null)
                        {
                            throw new InvalidOperationException(
                                $"User with ID '{stageDto.RequiredUserId}' not found for stage '{stageDto.StageName}'.");
                        }
                    }

                    var stage = new WorkflowStage
                    {
                        WorkflowTemplateId = template.Id,
                        StageName = stageDto.StageName,
                        StageOrder = stageDto.StageOrder,
                        RequiredRole = stageDto.RequiredRole,
                        RequiredUserId = stageDto.RequiredUserId,
                        ApproverPropertyPath = stageDto.ApproverPropertyPath,
                        SendNotification = stageDto.SendNotification,
                        AutoApproveAfterDays = stageDto.AutoApproveAfterDays,
                        IsOptional = stageDto.IsOptional,
                        AllowDelegation = stageDto.AllowDelegation,
                        CreatedBy = template.CreatedBy,
                        CreatedAt = DateTime.Now
                    };

                    _context.WorkflowStages.Add(stage);
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                _logger.LogInformation(
                    "Successfully created workflow template '{TemplateName}' with {StageCount} stages",
                    template.Name, stages.Count);

                return template;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error creating workflow template '{TemplateName}'", template.Name);
                throw;
            }
        }*/

        public async Task<WorkflowTemplate> UpdateTemplateAsync(
            int templateId,
            WorkflowTemplate template,
            List<WorkflowStageDto> stages)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                var existingTemplate = await _context.WorkflowTemplates
                    .Include(t => t.Stages)
                    .FirstOrDefaultAsync(t => t.Id == templateId);

                if (existingTemplate == null)
                {
                    throw new InvalidOperationException($"Template with ID {templateId} not found.");
                }

                // Check if template is being used in active workflows
                var hasActiveWorkflows = await TemplateHasActiveWorkflowsAsync(templateId);
                if (hasActiveWorkflows)
                {
                    _logger.LogWarning(
                        "Attempting to update template {TemplateId} which has active workflows", templateId);
                    // Allow update but log warning - consider blocking if needed
                }

                // Update template properties
                existingTemplate.Name = template.Name;
                existingTemplate.Description = template.Description;
                existingTemplate.EntityType = template.EntityType;
                existingTemplate.IsActive = template.IsActive;
                existingTemplate.RequiresAllStages = template.RequiresAllStages;
                existingTemplate.AllowRejection = template.AllowRejection;
                existingTemplate.UpdatedBy = template.UpdatedBy;
                existingTemplate.UpdatedAt = DateTime.Now;

                // Remove old stages
                _context.WorkflowStages.RemoveRange(existingTemplate.Stages);
                await _context.SaveChangesAsync();

                // Add new stages
                var orderedStages = stages.OrderBy(s => s.StageOrder).ToList();
                for (int i = 0; i < orderedStages.Count; i++)
                {
                    orderedStages[i].StageOrder = i + 1; // Ensure sequential ordering
                }

                foreach (var stageDto in orderedStages)
                {
                    // Validate approver configuration
                    if (string.IsNullOrEmpty(stageDto.RequiredUserId) &&
                        string.IsNullOrEmpty(stageDto.RequiredRole) &&
                        string.IsNullOrEmpty(stageDto.ApproverPropertyPath))
                    {
                        throw new InvalidOperationException(
                            $"Stage '{stageDto.StageName}' must have at least one approver resolution method specified.");
                    }

                    var stage = new WorkflowStage
                    {
                        WorkflowTemplateId = existingTemplate.Id,
                        StageName = stageDto.StageName,
                        StageOrder = stageDto.StageOrder,
                        RequiredRole = stageDto.RequiredRole,
                        RequiredUserId = stageDto.RequiredUserId,
                        ApproverPropertyPath = stageDto.ApproverPropertyPath,
                        SendNotification = stageDto.SendNotification,
                        AutoApproveAfterDays = stageDto.AutoApproveAfterDays,
                        IsOptional = stageDto.IsOptional,
                        AllowDelegation = stageDto.AllowDelegation,
                        CreatedBy = template.UpdatedBy,
                        CreatedAt = DateTime.Now
                    };

                    _context.WorkflowStages.Add(stage);
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                _logger.LogInformation(
                    "Successfully updated workflow template {TemplateId}: '{TemplateName}'",
                    templateId, existingTemplate.Name);

                return existingTemplate;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error updating workflow template {TemplateId}", templateId);
                throw;
            }
        }

        public async Task<WorkflowTemplate?> GetTemplateByIdAsync(int templateId)
        {
            try
            {
                return await _context.WorkflowTemplates
                    .Include(t => t.Stages.OrderBy(s => s.StageOrder))
                    .FirstOrDefaultAsync(t => t.Id == templateId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving workflow template {TemplateId}", templateId);
                throw;
            }
        }

        public async Task<WorkflowTemplate?> GetActiveTemplateForEntityAsync(string entityType)
        {
            try
            {
                var template = await _context.WorkflowTemplates
                    .Include(t => t.Stages.OrderBy(s => s.StageOrder))
                    .Where(t => t.EntityType == entityType && t.IsActive)
                    .OrderByDescending(t => t.CreatedAt)
                    .FirstOrDefaultAsync();

                if (template == null)
                {
                    _logger.LogWarning(
                        "No active workflow template found for entity type '{EntityType}'", entityType);
                }

                return template;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving active template for entity type '{EntityType}'", entityType);
                throw;
            }
        }

        public async Task<List<WorkflowTemplate>> GetAllTemplatesAsync()
        {
            try
            {
                return await _context.WorkflowTemplates
                    .Include(t => t.Stages.OrderBy(s => s.StageOrder))
                    .OrderBy(t => t.EntityType)
                    .ThenBy(t => t.Name)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving all workflow templates");
                throw;
            }
        }

        public async Task<List<WorkflowTemplate>> GetTemplatesByEntityTypeAsync(string entityType)
        {
            try
            {
                return await _context.WorkflowTemplates
                    .Include(t => t.Stages.OrderBy(s => s.StageOrder))
                    .Where(t => t.EntityType == entityType)
                    .OrderBy(t => t.Name)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving templates for entity type '{EntityType}'", entityType);
                throw;
            }
        }

        public async Task<bool> DeleteTemplateAsync(int templateId)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                var template = await _context.WorkflowTemplates
                    .Include(t => t.Stages)
                    .FirstOrDefaultAsync(t => t.Id == templateId);

                if (template == null)
                {
                    _logger.LogWarning("Attempted to delete non-existent template {TemplateId}", templateId);
                    return false;
                }

                // Check for active workflows
                var hasActiveWorkflows = await TemplateHasActiveWorkflowsAsync(templateId);
                if (hasActiveWorkflows)
                {
                    throw new InvalidOperationException(
                        "Cannot delete template. There are active workflows using this template.");
                }

                // Check for any historical workflows
                var hasAnyWorkflows = await _context.WorkflowInstances
                    .AnyAsync(w => w.WorkflowTemplateId == templateId);

                if (hasAnyWorkflows)
                {
                    // Soft delete by marking as inactive instead of hard delete
                    template.IsActive = false;
                    template.Name = $"[DELETED] {template.Name}";
                    template.UpdatedAt = DateTime.Now;

                    _logger.LogInformation(
                        "Soft deleted template {TemplateId} (has historical workflows)", templateId);
                }
                else
                {
                    // Hard delete if no workflows exist
                    _context.WorkflowStages.RemoveRange(template.Stages);
                    _context.WorkflowTemplates.Remove(template);

                    _logger.LogInformation("Hard deleted template {TemplateId}", templateId);
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return true;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error deleting workflow template {TemplateId}", templateId);
                throw;
            }
        }

        public async Task<bool> ToggleTemplateStatusAsync(int templateId)
        {
            try
            {
                var template = await _context.WorkflowTemplates
                    .FirstOrDefaultAsync(t => t.Id == templateId);

                if (template == null)
                {
                    return false;
                }

                template.IsActive = !template.IsActive;
                template.UpdatedAt = DateTime.Now;

                await _context.SaveChangesAsync();

                _logger.LogInformation(
                    "Toggled template {TemplateId} status to {Status}",
                    templateId, template.IsActive ? "Active" : "Inactive");

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error toggling template status for {TemplateId}", templateId);
                throw;
            }
        }

        public async Task<bool> TemplateHasActiveWorkflowsAsync(int templateId)
        {
            try
            {
                return await _context.WorkflowInstances
                    .AnyAsync(w => w.WorkflowTemplateId == templateId &&
                        (w.Status == WorkflowStatus.Pending ||
                         w.Status == WorkflowStatus.InProgress));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking active workflows for template {TemplateId}", templateId);
                throw;
            }
        }

        public async Task<WorkflowTemplate> CloneTemplateAsync(int templateId, string newName)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                var sourceTemplate = await _context.WorkflowTemplates
                    .Include(t => t.Stages.OrderBy(s => s.StageOrder))
                    .FirstOrDefaultAsync(t => t.Id == templateId);

                if (sourceTemplate == null)
                {
                    throw new InvalidOperationException($"Source template {templateId} not found.");
                }

                // Create new template
                var clonedTemplate = new WorkflowTemplate
                {
                    Name = newName,
                    Description = $"Cloned from: {sourceTemplate.Name}",
                    EntityType = sourceTemplate.EntityType,
                    IsActive = false, // Start as inactive
                    RequiresAllStages = sourceTemplate.RequiresAllStages,
                    AllowRejection = sourceTemplate.AllowRejection,
                    CreatedBy = sourceTemplate.CreatedBy,
                    CreatedAt = DateTime.Now
                };

                _context.WorkflowTemplates.Add(clonedTemplate);
                await _context.SaveChangesAsync();

                // Clone stages
                foreach (var sourceStage in sourceTemplate.Stages.OrderBy(s => s.StageOrder))
                {
                    var clonedStage = new WorkflowStage
                    {
                        WorkflowTemplateId = clonedTemplate.Id,
                        StageName = sourceStage.StageName,
                        StageOrder = sourceStage.StageOrder,
                        RequiredRole = sourceStage.RequiredRole,
                        RequiredUserId = sourceStage.RequiredUserId,
                        ApproverPropertyPath = sourceStage.ApproverPropertyPath,
                        SendNotification = sourceStage.SendNotification,
                        AutoApproveAfterDays = sourceStage.AutoApproveAfterDays,
                        IsOptional = sourceStage.IsOptional,
                        AllowDelegation = sourceStage.AllowDelegation,
                        CreatedBy = sourceStage.CreatedBy,
                        CreatedAt = DateTime.Now
                    };

                    _context.WorkflowStages.Add(clonedStage);
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                _logger.LogInformation(
                    "Successfully cloned template {SourceTemplateId} to new template {NewTemplateId}: '{NewName}'",
                    templateId, clonedTemplate.Id, newName);

                return clonedTemplate;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error cloning workflow template {TemplateId}", templateId);
                throw;
            }
        }
    }

    // ==================== TEMPLATE VALIDATION SERVICE ====================

    public interface IWorkflowTemplateValidationService
    {
        Task<ValidationResult> ValidateTemplateAsync(WorkflowTemplate template, List<WorkflowStageDto> stages);
        Task<ValidationResult> ValidateStageConfigurationAsync(WorkflowStageDto stage, string entityType);
        Task<List<string>> GetAvailablePropertyPathsAsync(string entityType);
    }

    public class ValidationResult
    {
        public bool IsValid { get; set; }
        public List<string> Errors { get; set; } = new List<string>();
        public List<string> Warnings { get; set; } = new List<string>();
    }

    public class WorkflowTemplateValidationService : IWorkflowTemplateValidationService
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<WorkflowTemplateValidationService> _logger;

        public WorkflowTemplateValidationService(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            ILogger<WorkflowTemplateValidationService> logger)
        {
            _context = context;
            _userManager = userManager;
            _logger = logger;
        }

        public async Task<ValidationResult> ValidateTemplateAsync(
            WorkflowTemplate template,
            List<WorkflowStageDto> stages)
        {
            var result = new ValidationResult { IsValid = true };

            // Validate template basic properties
            if (string.IsNullOrWhiteSpace(template.Name))
            {
                result.Errors.Add("Template name is required.");
                result.IsValid = false;
            }

            if (string.IsNullOrWhiteSpace(template.EntityType))
            {
                result.Errors.Add("Entity type is required.");
                result.IsValid = false;
            }

            // Check for duplicate template name
            var existingTemplate = await _context.WorkflowTemplates
                .FirstOrDefaultAsync(t => t.Name == template.Name &&
                    t.EntityType == template.EntityType &&
                    t.Id != template.Id);

            if (existingTemplate != null)
            {
                result.Errors.Add($"A template named '{template.Name}' already exists for entity type '{template.EntityType}'.");
                result.IsValid = false;
            }

            // Validate stages
            if (stages == null || !stages.Any())
            {
                result.Errors.Add("At least one workflow stage is required.");
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

            // Check sequential ordering
            var orderedStages = stages.OrderBy(s => s.StageOrder).ToList();
            for (int i = 0; i < orderedStages.Count; i++)
            {
                if (orderedStages[i].StageOrder != i + 1)
                {
                    result.Warnings.Add(
                        $"Stage orders are not sequential. Stage '{orderedStages[i].StageName}' has order {orderedStages[i].StageOrder} but expected {i + 1}.");
                }
            }

            // Validate each stage
            foreach (var stage in stages)
            {
                var stageValidation = await ValidateStageConfigurationAsync(stage, template.EntityType);
                result.Errors.AddRange(stageValidation.Errors);
                result.Warnings.AddRange(stageValidation.Warnings);

                if (!stageValidation.IsValid)
                {
                    result.IsValid = false;
                }
            }

            return result;
        }

        public async Task<ValidationResult> ValidateStageConfigurationAsync(
            WorkflowStageDto stage,
            string entityType)
        {
            var result = new ValidationResult { IsValid = true };

            // Validate stage name
            if (string.IsNullOrWhiteSpace(stage.StageName))
            {
                result.Errors.Add("Stage name is required.");
                result.IsValid = false;
            }

            // Validate approver resolution
            if (string.IsNullOrWhiteSpace(stage.RequiredUserId) &&
                string.IsNullOrWhiteSpace(stage.RequiredRole) &&
                string.IsNullOrWhiteSpace(stage.ApproverPropertyPath))
            {
                result.Errors.Add(
                    $"Stage '{stage.StageName}' must specify at least one approver resolution method: " +
                    "RequiredUserId, RequiredRole, or ApproverPropertyPath.");
                result.IsValid = false;
            }

            // Validate specific user if provided
            if (!string.IsNullOrWhiteSpace(stage.RequiredUserId))
            {
                var user = await _userManager.FindByIdAsync(stage.RequiredUserId);
                if (user == null)
                {
                    result.Errors.Add($"User with ID '{stage.RequiredUserId}' not found for stage '{stage.StageName}'.");
                    result.IsValid = false;
                }
            }

            // Validate property path if provided
            if (!string.IsNullOrWhiteSpace(stage.ApproverPropertyPath))
            {
                var validPaths = await GetAvailablePropertyPathsAsync(entityType);
                if (!validPaths.Contains(stage.ApproverPropertyPath))
                {
                    result.Warnings.Add(
                        $"Property path '{stage.ApproverPropertyPath}' for stage '{stage.StageName}' " +
                        $"may not be valid for entity type '{entityType}'.");
                }
            }

            // Validate auto-approval days
            if (stage.AutoApproveAfterDays.HasValue)
            {
                if (stage.AutoApproveAfterDays.Value < 1)
                {
                    result.Errors.Add($"Auto-approve days must be at least 1 for stage '{stage.StageName}'.");
                    result.IsValid = false;
                }
                else if (stage.AutoApproveAfterDays.Value < 3)
                {
                    result.Warnings.Add(
                        $"Stage '{stage.StageName}' has auto-approval set to less than 3 days. " +
                        "This may result in premature auto-approval.");
                }
            }

            return result;
        }

        public async Task<List<string>> GetAvailablePropertyPathsAsync(string entityType)
        {
            // Return available property paths based on entity type
            return entityType switch
            {
                "ResultSubmissionBatch" or "StudentAssessmentScore" or "StudentCourseResult" => new List<string>
            {
                "Course.Programme.Department.HODId",
                "Course.Programme.Department.School.DeanId",
                "Course.Programme.Department.School.AssistantDeanId",
                "Course.Programme.CoordinatorId",
                "Course.InstructorId"
            },
                "CourseModification" => new List<string>
            {
                "Course.Programme.Department.HODId",
                "Course.Programme.Department.School.DeanId"
            },
                "ProgrammeModification" => new List<string>
            {
                "Programme.Department.HODId",
                "Programme.Department.School.DeanId",
                "Programme.CoordinatorId"
            },
                _ => new List<string>()
            };
        }
    }
}
