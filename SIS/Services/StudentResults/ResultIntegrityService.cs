using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using SIS.Models.StudentResults;

namespace SIS.Services
{
    /// <summary>
    /// Service for managing data integrity through hashing
    /// Provides hash generation and verification for assessment scores and results
    /// </summary>
    public interface IResultIntegrityService
    {
        string GenerateScoreHash(StudentAssessmentScore score);
        string GenerateResultHash(StudentCourseResult result);
        bool VerifyScoreHash(StudentAssessmentScore score);
        bool VerifyResultHash(StudentCourseResult result);
        string GenerateAuditHash(string jsonValue);
    }

    public class ResultIntegrityService : IResultIntegrityService
    {
        private readonly string _salt;
        private readonly ILogger<ResultIntegrityService> _logger;

        public ResultIntegrityService(IConfiguration configuration, ILogger<ResultIntegrityService> logger)
        {
            // Salt should be stored in appsettings.json or Azure Key Vault
            _salt = configuration["Security:ResultHashSalt"] ?? throw new InvalidOperationException("ResultHashSalt not configured");
            _logger = logger;
        }

        /// <summary>
        /// Generate SHA256 hash for StudentAssessmentScore
        /// Format: SHA256(StudentId|CourseId|AssessmentId|Score|WeightPercentage|Salt)
        /// </summary>
        public string GenerateScoreHash(StudentAssessmentScore score)
        {
            try
            {
                var dataToHash = $"{score.StudentId}|{score.CourseId}|{score.AssessmentId}|" +
                                $"{score.Score:F2}|{score.WeightPercentage:F2}|{_salt}";

                return ComputeSHA256Hash(dataToHash);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating score hash for StudentId: {StudentId}, CourseId: {CourseId}",
                    score.StudentId, score.CourseId);
                throw;
            }
        }

        /// <summary>
        /// Generate SHA256 hash for StudentCourseResult
        /// Format: SHA256(StudentId|CourseId|NormalizedTotal|GradeLetter|Credits|Salt)
        /// </summary>
        public string GenerateResultHash(StudentCourseResult result)
        {
            try
            {
                var dataToHash = $"{result.StudentId}|{result.CourseId}|{result.NormalizedTotal:F2}|" +
                                $"{result.GradeLetter}|{result.Credits}|{_salt}";

                return ComputeSHA256Hash(dataToHash);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating result hash for StudentId: {StudentId}, CourseId: {CourseId}",
                    result.StudentId, result.CourseId);
                throw;
            }
        }

        /// <summary>
        /// Verify the integrity of a StudentAssessmentScore
        /// </summary>
        public bool VerifyScoreHash(StudentAssessmentScore score)
        {
            try
            {
                var computedHash = GenerateScoreHash(score);
                var isValid = computedHash.Equals(score.ScoreHash, StringComparison.OrdinalIgnoreCase);

                if (!isValid)
                {
                    _logger.LogWarning("Hash mismatch detected for assessment score. StudentId: {StudentId}, CourseId: {CourseId}, AssessmentId: {AssessmentId}",
                        score.StudentId, score.CourseId, score.AssessmentId);
                }

                return isValid;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error verifying score hash for StudentId: {StudentId}, CourseId: {CourseId}",
                    score.StudentId, score.CourseId);
                return false;
            }
        }

        /// <summary>
        /// Verify the integrity of a StudentCourseResult
        /// </summary>
        public bool VerifyResultHash(StudentCourseResult result)
        {
            try
            {
                var computedHash = GenerateResultHash(result);
                var isValid = computedHash.Equals(result.ResultHash, StringComparison.OrdinalIgnoreCase);

                if (!isValid)
                {
                    _logger.LogWarning("Hash mismatch detected for course result. StudentId: {StudentId}, CourseId: {CourseId}",
                        result.StudentId, result.CourseId);
                }

                return isValid;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error verifying result hash for StudentId: {StudentId}, CourseId: {CourseId}",
                    result.StudentId, result.CourseId);
                return false;
            }
        }

        /// <summary>
        /// Generate hash for audit log JSON values
        /// </summary>
        public string GenerateAuditHash(string jsonValue)
        {
            try
            {
                if (string.IsNullOrEmpty(jsonValue))
                    return string.Empty;

                var dataToHash = $"{jsonValue}|{_salt}";
                return ComputeSHA256Hash(dataToHash);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating audit hash");
                throw;
            }
        }

        /// <summary>
        /// Core SHA256 hashing function
        /// </summary>
        private string ComputeSHA256Hash(string input)
        {
            using (var sha256 = SHA256.Create())
            {
                var bytes = Encoding.UTF8.GetBytes(input);
                var hashBytes = sha256.ComputeHash(bytes);

                // Convert to hexadecimal string
                var sb = new StringBuilder();
                foreach (var b in hashBytes)
                {
                    sb.Append(b.ToString("x2"));
                }

                return sb.ToString();
            }
        }
    }
}