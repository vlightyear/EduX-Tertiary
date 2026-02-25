using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using SIS.Data;
using SIS.Models.Results;
using SIS.Models.StudentResults;

namespace SIS.Services
{
    public interface IResultAuditService
    {
        Task LogActionAsync(string entityType, int entityId, int studentId, int courseId,
            int academicYearId, string actionType, string? oldValue, string newValue,
            string userId, string? reason = null, string? sessionId = null, bool isBatch = false,
            string? batchId = null);
        Task<List<ResultAuditLog>> GetAuditHistoryAsync(string entityType, int entityId);
        Task<List<ResultAuditLog>> GetStudentAuditHistoryAsync(int studentId, int? courseId = null);
        Task<List<ResultAuditLog>> GetAuditsByUserAsync(string userId, DateTime? fromDate = null, DateTime? toDate = null);
        Task<List<ResultAuditLog>> GetRecentChangesAsync(int count = 50);
        Task<Dictionary<string, int>> GetAuditSummaryAsync(DateTime fromDate, DateTime toDate);
    }

    public class ResultAuditService : IResultAuditService
    {
        private readonly ApplicationDbContext _context;
        private readonly IResultIntegrityService _integrityService;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ILogger<ResultAuditService> _logger;

        public ResultAuditService(
            ApplicationDbContext context,
            IResultIntegrityService integrityService,
            IHttpContextAccessor httpContextAccessor,
            ILogger<ResultAuditService> logger)
        {
            _context = context;
            _integrityService = integrityService;
            _httpContextAccessor = httpContextAccessor;
            _logger = logger;
        }

        /// <summary>
        /// Log an action to the audit trail
        /// </summary>
        public async Task LogActionAsync(
            string entityType,
            int entityId,
            int studentId,
            int courseId,
            int academicYearId,
            string actionType,
            string? oldValue,
            string newValue,
            string userId,
            string? reason = null,
            string? sessionId = null,
            bool isBatch = false,
            string? batchId = null)
        {
            try
            {
                // Get IP address and user agent from HTTP context
                var httpContext = _httpContextAccessor.HttpContext;
                var ipAddress = httpContext?.Connection?.RemoteIpAddress?.ToString();
                var userAgent = httpContext?.Request?.Headers["User-Agent"].ToString();

                // Generate hashes for old and new values
                var oldValueHash = !string.IsNullOrEmpty(oldValue)
                    ? _integrityService.GenerateAuditHash(oldValue)
                    : null;
                var newValueHash = _integrityService.GenerateAuditHash(newValue);

                var auditLog = new ResultAuditLog
                {
                    EntityType = entityType,
                    EntityId = entityId,
                    StudentId = studentId,
                    CourseId = courseId,
                    AcademicYearId = academicYearId,
                    ActionType = actionType,
                    OldValue = oldValue,
                    NewValue = newValue,
                    ChangedBy = userId,
                    ChangedAt = DateTime.Now,
                    IPAddress = ipAddress,
                    Reason = reason,
                    UserAgent = userAgent,
                    SessionId = sessionId ?? Guid.NewGuid().ToString(),
                    IsBatchOperation = isBatch,
                    BatchId = batchId,
                    OldValueHash = oldValueHash,
                    NewValueHash = newValueHash
                };

                _context.ResultAuditLogs.Add(auditLog);
                await _context.SaveChangesAsync();

                _logger.LogDebug(
                    "Audit log created: EntityType={EntityType}, EntityId={EntityId}, Action={Action}, User={User}",
                    entityType, entityId, actionType, userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Error creating audit log for EntityType={EntityType}, EntityId={EntityId}",
                    entityType, entityId);
                // Don't throw - audit logging should not break the main operation
            }
        }

        /// <summary>
        /// Get complete audit history for a specific entity
        /// </summary>
        public async Task<List<ResultAuditLog>> GetAuditHistoryAsync(string entityType, int entityId)
        {
            try
            {
                return await _context.ResultAuditLogs
                    .Include(a => a.ChangedByUser)
                    .Include(a => a.Student)
                    .Include(a => a.Course)
                    .Where(a => a.EntityType == entityType && a.EntityId == entityId)
                    .OrderByDescending(a => a.ChangedAt)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Error retrieving audit history for EntityType={EntityType}, EntityId={EntityId}",
                    entityType, entityId);
                throw;
            }
        }

        /// <summary>
        /// Get audit history for a specific student
        /// </summary>
        public async Task<List<ResultAuditLog>> GetStudentAuditHistoryAsync(int studentId, int? courseId = null)
        {
            try
            {
                var query = _context.ResultAuditLogs
                    .Include(a => a.ChangedByUser)
                    .Include(a => a.Course)
                    .Include(a => a.AcademicYear)
                    .Where(a => a.StudentId == studentId);

                if (courseId.HasValue)
                {
                    query = query.Where(a => a.CourseId == courseId.Value);
                }

                return await query
                    .OrderByDescending(a => a.ChangedAt)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Error retrieving student audit history. StudentId={StudentId}",
                    studentId);
                throw;
            }
        }

        /// <summary>
        /// Get all audits by a specific user
        /// </summary>
        public async Task<List<ResultAuditLog>> GetAuditsByUserAsync(
            string userId,
            DateTime? fromDate = null,
            DateTime? toDate = null)
        {
            try
            {
                var query = _context.ResultAuditLogs
                    .Include(a => a.Student)
                    .Include(a => a.Course)
                    .Where(a => a.ChangedBy == userId);

                if (fromDate.HasValue)
                {
                    query = query.Where(a => a.ChangedAt >= fromDate.Value);
                }

                if (toDate.HasValue)
                {
                    query = query.Where(a => a.ChangedAt <= toDate.Value);
                }

                return await query
                    .OrderByDescending(a => a.ChangedAt)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Error retrieving audits by user. UserId={UserId}",
                    userId);
                throw;
            }
        }

        /// <summary>
        /// Get most recent changes across all entities
        /// </summary>
        public async Task<List<ResultAuditLog>> GetRecentChangesAsync(int count = 50)
        {
            try
            {
                return await _context.ResultAuditLogs
                    .Include(a => a.ChangedByUser)
                    .Include(a => a.Student)
                    .Include(a => a.Course)
                    .OrderByDescending(a => a.ChangedAt)
                    .Take(count)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving recent changes");
                throw;
            }
        }

        /// <summary>
        /// Get audit summary statistics for a date range
        /// </summary>
        public async Task<Dictionary<string, int>> GetAuditSummaryAsync(DateTime fromDate, DateTime toDate)
        {
            try
            {
                var audits = await _context.ResultAuditLogs
                    .Where(a => a.ChangedAt >= fromDate && a.ChangedAt <= toDate)
                    .ToListAsync();

                return new Dictionary<string, int>
                {
                    { "TotalChanges", audits.Count },
                    { "Created", audits.Count(a => a.ActionType == "Created") },
                    { "Updated", audits.Count(a => a.ActionType == "Updated") },
                    { "Published", audits.Count(a => a.ActionType == "Published") },
                    { "Deleted", audits.Count(a => a.ActionType == "Deleted") },
                    { "UniqueUsers", audits.Select(a => a.ChangedBy).Distinct().Count() },
                    { "UniqueStudents", audits.Select(a => a.StudentId).Distinct().Count() },
                    { "AssessmentScores", audits.Count(a => a.EntityType == "AssessmentScore") },
                    { "CourseResults", audits.Count(a => a.EntityType == "CourseResult") },
                    { "BatchOperations", audits.Count(a => a.IsBatchOperation) }
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating audit summary");
                throw;
            }
        }
    }
}