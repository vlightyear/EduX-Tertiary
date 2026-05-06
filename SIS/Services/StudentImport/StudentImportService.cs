using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SIS.Data;
using SIS.Enums;
using SIS.Models;
using SIS.Models.Registration;
using SIS.Models.StudentApplication;
using SIS.Models.Admin;
using SIS.Services.Emails;
using ClosedXML.Excel;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Text;
using System.Collections.Concurrent;
using System.Transactions;

namespace SIS.Services.StudentImport
{
    public class StudentImportService : IStudentImportService
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly EmailService _emailService;
        private readonly ILogger<StudentImportService> _logger;
        private readonly ConcurrentDictionary<string, ImportProgress> _progressTracker;

        // Optimized batch processing configuration
        private const int BATCH_SIZE = 10; // Reduced from 15 to 10 for better connection management
        private const int CONNECTION_TIMEOUT = 180; // Reduced to 3 minutes
        private const int MAX_RETRY_ATTEMPTS = 3;

        public StudentImportService(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager,
            EmailService emailService,
            ILogger<StudentImportService> logger)
        {
            _context = context;
            _userManager = userManager;
            _roleManager = roleManager;
            _emailService = emailService;
            _logger = logger;
            _progressTracker = new ConcurrentDictionary<string, ImportProgress>();

            // Configure database timeout
            ConfigureDatabase();
        }

        private void ConfigureDatabase()
        {
            try
            {
                if (_context.Database.IsRelational())
                {
                    _context.Database.SetCommandTimeout(CONNECTION_TIMEOUT);
                }

                // Optimize EF Core for bulk operations
                _context.ChangeTracker.AutoDetectChangesEnabled = false;
                _context.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to configure database settings");
            }
        }

        public async Task<ImportPreviewResult> PreviewImportDataAsync(IFormFile file, CancellationToken cancellationToken = default)
        {
            var result = new ImportPreviewResult
            {
                Success = false,
                ValidStudents = new List<StudentImportDto>(),
                InvalidStudents = new List<StudentImportDto>(),
                ValidationResults = new List<StudentValidationResult>()
            };

            try
            {
                _logger.LogInformation($"Starting preview for file: {file.FileName}, Size: {file.Length} bytes");

                using var stream = new MemoryStream();
                await file.CopyToAsync(stream, cancellationToken);
                stream.Position = 0;

                using var workbook = new XLWorkbook(stream);
                var worksheet = workbook.Worksheet(1);

                if (worksheet == null)
                {
                    result.Message = "No worksheet found in the Excel file.";
                    return result;
                }

                // Get the range of data
                var range = worksheet.RangeUsed();
                if (range == null || range.RowCount() < 2)
                {
                    result.Message = "The Excel file appears to be empty or contains only headers.";
                    return result;
                }

                // Parse headers and validate structure
                var headers = GetHeadersFromWorksheet(worksheet);
                var headerValidation = ValidateHeaders(headers);
                if (!headerValidation.IsValid)
                {
                    result.Message = headerValidation.ErrorMessage;
                    return result;
                }

                // Parse data rows
                var students = new List<StudentImportDto>();
                var totalRows = range.RowCount();
                result.TotalRows = totalRows - 1; // Exclude header row

                _logger.LogInformation($"Processing {result.TotalRows} data rows");

                for (int row = 2; row <= totalRows; row++) // Start from row 2 (skip header)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    try
                    {
                        var student = ParseStudentFromRow(worksheet, row, headers);
                        if (student != null)
                        {
                            student.RowNumber = row;
                            students.Add(student);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, $"Error parsing row {row}");
                        // Create a basic student object for error tracking
                        students.Add(new StudentImportDto
                        {
                            RowNumber = row,
                            FullName = $"Error in row {row}",
                            Email = $"error.row.{row}@invalid.com",
                            ValidationErrors = new List<string> { $"Failed to parse row: {ex.Message}" }
                        });
                    }
                }

                // Validate all students in smaller batches to manage memory
                var validationResults = await ValidateStudentDataBatchedAsync(students, cancellationToken);
                result.ValidationResults = validationResults;

                // Separate valid and invalid students
                result.ValidStudents = students.Where(s => validationResults.First(vr => vr.RowNumber == s.RowNumber).IsValid).ToList();
                result.InvalidStudents = students.Where(s => !validationResults.First(vr => vr.RowNumber == s.RowNumber).IsValid).ToList();

                result.Success = true;
                result.Message = $"Preview completed. {result.ValidStudents.Count} valid students, {result.InvalidStudents.Count} invalid students.";

                _logger.LogInformation($"Preview completed successfully. Valid: {result.ValidStudents.Count}, Invalid: {result.InvalidStudents.Count}");

                return result;
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Preview import operation was cancelled");
                result.Message = "Preview operation was cancelled.";
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during import preview");
                result.Message = $"Error processing file: {ex.Message}";
                return result;
            }
        }

        public async Task<ImportProcessResult> ProcessImportAsync(List<StudentImportDto> validStudents, string importedBy, string progressKey, CancellationToken cancellationToken = default)
        {
            var result = new ImportProcessResult
            {
                Success = false,
                TotalProcessed = validStudents.Count,
                SuccessfulImports = 0,
                FailedImports = 0,
                ImportedStudents = new List<ImportedStudentResult>(),
                FailedRows = new List<FailedImportRow>(),
                EmailsSent = 0
            };

            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            try
            {
                _logger.LogInformation($"Starting import process for {validStudents.Count} students");

                // Initialize progress tracking
                UpdateProgress(progressKey, "Initializing import process...", 0, 0, 0);

                // Split students into smaller batches for better connection management
                var batches = validStudents
                    .Select((student, index) => new { student, index })
                    .GroupBy(x => x.index / BATCH_SIZE)
                    .Select(g => g.Select(x => x.student).ToList())
                    .ToList();

                var totalBatches = batches.Count;
                _logger.LogInformation($"Processing {validStudents.Count} students in {totalBatches} batches of {BATCH_SIZE}");

                // Process each batch with retry logic
                for (int batchIndex = 0; batchIndex < batches.Count; batchIndex++)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var batch = batches[batchIndex];
                    var batchNumber = batchIndex + 1;
                    var progressPercent = (int)((double)batchIndex / totalBatches * 100);

                    UpdateProgress(progressKey, $"Processing batch {batchNumber} of {totalBatches}...",
                        progressPercent, batchNumber, totalBatches);

                    // Process batch with retry logic
                    await ProcessBatchWithRetryAsync(batch, importedBy, result, batchNumber, cancellationToken);

                    // Small delay between batches to prevent connection exhaustion
                    if (batchIndex < batches.Count - 1)
                    {
                        await Task.Delay(100, cancellationToken); // 100ms delay
                    }
                }

                result.Success = true;
                result.Message = $"Import completed. {result.SuccessfulImports} students imported successfully, {result.FailedImports} failed.";

                stopwatch.Stop();
                result.ProcessingTime = stopwatch.Elapsed;

                // Create import summary
                result.ImportSummary = new ImportSummary
                {
                    TotalRowsInFile = validStudents.Count,
                    ValidRowsForImport = validStudents.Count,
                    InvalidRowsSkipped = 0,
                    UsersCreated = result.SuccessfulImports,
                    EmailsSent = result.EmailsSent,
                    ErrorsEncountered = result.FailedImports
                };

                UpdateProgress(progressKey, "Import completed successfully!", 100, totalBatches, totalBatches);

                _logger.LogInformation($"Import completed: {result.SuccessfulImports} successful, {result.FailedImports} failed, Duration: {result.ProcessingTime}");

                return result;
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Import process was cancelled");
                result.Success = false;
                result.Message = "Import process was cancelled.";
                UpdateProgress(progressKey, "Import cancelled", 0, 0, 0);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Critical error during student import");
                result.Success = false;
                result.Message = $"Import failed due to a critical error: {ex.Message}";
                UpdateProgress(progressKey, $"Import failed: {ex.Message}", 0, 0, 0);
                return result;
            }
            finally
            {
                // Immediate cleanup
                try
                {
                    // Reset EF Core context
                    _context.ChangeTracker.Clear();

                    // Force cleanup of large collections
                    result.ImportedStudents?.Clear();
                    result.FailedRows?.Clear();

                    _logger.LogDebug("Service cleanup completed");
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error during service cleanup");
                }

                // Clean up progress tracking after delay
                _ = Task.Delay(TimeSpan.FromMinutes(5), CancellationToken.None)
                    .ContinueWith(_ => CleanupProgressAsync(progressKey), TaskScheduler.Default);
            }
        }

        private async Task ProcessBatchWithRetryAsync(List<StudentImportDto> batch, string importedBy, ImportProcessResult result, int batchNumber, CancellationToken cancellationToken)
        {
            for (int attempt = 1; attempt <= MAX_RETRY_ATTEMPTS; attempt++)
            {
                try
                {
                    await ProcessBatchAsync(batch, importedBy, result, cancellationToken);
                    _logger.LogDebug($"Batch {batchNumber} processed successfully on attempt {attempt}");
                    return; // Success, exit retry loop
                }
                catch (Exception ex) when (attempt < MAX_RETRY_ATTEMPTS && IsRetryableException(ex))
                {
                    _logger.LogWarning(ex, $"Batch {batchNumber} failed on attempt {attempt}, retrying...");

                    // Exponential backoff
                    var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt));
                    await Task.Delay(delay, cancellationToken);

                    // Reset context for retry
                    await ResetDatabaseContextAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Batch {batchNumber} failed permanently on attempt {attempt}");

                    // Mark all students in this batch as failed
                    foreach (var studentData in batch)
                    {
                        result.FailedRows.Add(new FailedImportRow
                        {
                            RowNumber = studentData.RowNumber,
                            StudentData = studentData,
                            ErrorMessage = $"Batch processing failed after {attempt} attempts: {ex.Message}"
                        });
                        result.FailedImports++;
                    }
                    return;
                }
            }
        }

        private bool IsRetryableException(Exception ex)
        {
            return ex is DbUpdateException ||
                   ex is InvalidOperationException ||
                   ex is TimeoutException ||
                   (ex.InnerException != null && IsRetryableException(ex.InnerException));
        }

        private async Task ResetDatabaseContextAsync()
        {
            try
            {
                // Dispose and recreate context if needed
                await _context.Database.CloseConnectionAsync();
                ConfigureDatabase();
                _logger.LogDebug("Database context reset successfully");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to reset database context");
            }
        }

        private async Task ProcessBatchAsync(List<StudentImportDto> batch, string importedBy, ImportProcessResult result, CancellationToken cancellationToken)
        {
            int localEmailsSent = 0;

            // Use EF Core's execution strategy instead of manual TransactionScope
            var strategy = _context.Database.CreateExecutionStrategy();

            await strategy.ExecuteAsync(async () =>
            {
                using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

                try
                {
                    foreach (var studentData in batch)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        try
                        {
                            // Generate random password
                            var password = GenerateSecurePassword();

                            // Create ApplicationUser
                            var user = new ApplicationUser
                            {
                                FullName = studentData.FullName,
                                Email = studentData.Email,
                                UserName = studentData.Email,
                                EmailConfirmed = true,
                                PhoneNumber = studentData.Phone
                            };

                            var userResult = await _userManager.CreateAsync(user, password);
                            if (!userResult.Succeeded)
                            {
                                var errors = string.Join(", ", userResult.Errors.Select(e => e.Description));
                                result.FailedRows.Add(new FailedImportRow
                                {
                                    RowNumber = studentData.RowNumber,
                                    StudentData = studentData,
                                    ErrorMessage = $"Failed to create user: {errors}"
                                });
                                result.FailedImports++;
                                continue;
                            }

                            // Add Student role
                            await _userManager.AddToRoleAsync(user, "Student");

                            // Create Student record with ApplicationReferenceNumber
                            var applicationRefNumber = GenerateApplicationReferenceNumber();

                            var student = new Student
                            {
                                // Required fields from your model
                                ApplicationReferenceNumber = applicationRefNumber,
                                FullName = studentData.FullName,
                                Email = studentData.Email,
                                StudentId_Number = studentData.StudentId_Number,
                                NrcOrPassportNumber = studentData.NrcOrPassportNumber,
                                NrcOrPassportCopy = string.Empty,
                                DateOfBirth = studentData.DateOfBirth,
                                Gender = studentData.Gender,
                                Phone = studentData.Phone ?? "+260000000000",

                                // Default values for required fields
                                MaritalStatus = studentData.MaritalStatus ?? "Single",
                                Nationality = studentData.Nationality ?? "Zambian",
                                Religion = studentData.Religion ?? "Not Specified",
                                IsForeigner = studentData.IsForeigner,

                                // Academic information
                                ProgrammeId = studentData.ProgrammeId,
                                ProgrammeLevelId = studentData.ProgrammeLevelId,
                                SchoolId = studentData.SchoolId,
                                ModeOfStudyId = studentData.ModeOfStudyId,
                                AcademicYearId = studentData.AcademicYearId,
                                StudentCurrentYear = studentData.StudentCurrentYear,
                                CurrentYearPeriodId = studentData.CurrentSemester,

                                // Status fields
                                StudentStatus = Status.Admitted,
                                IsAdmitted = true,
                                IsRegistered = false,
                                RegistrationStatus = Status.Unregistered,
                                OutstandingFees = 0,

                                // Identity fields
                                Username = user.UserName,
                                AdmissionDate = DateTime.Now,
                                StudyPermission = null,

                                // Audit fields
                                CreatedBy = importedBy,
                                CreatedAt = DateTime.Now
                            };

                            // Create related entities
                            student.StudentAddress = new StudentAddress
                            {
                                AddressLine1 = studentData.AddressLine1 ?? string.Empty,
                                AddressLine2 = studentData.AddressLine2,
                                City = studentData.City ?? string.Empty,
                                State = studentData.State ?? string.Empty,
                                Country = studentData.Country ?? "Zambia",
                                PostalCode = studentData.PostalCode
                            };

                            student.NextOfKin = new StudNextOfKin
                            {
                                Name = studentData.NextOfKinName ?? string.Empty,
                                Relationship = studentData.NextOfKinRelation ?? string.Empty,
                                PhoneNumber = studentData.NextOfKinPhone ?? string.Empty,
                                Email = studentData.NextOfKinEmail,
                                Address = studentData.NextOfKinAddress
                            };

                            student.FormerSchool = new StudFormerSchool
                            {
                                SchoolName = studentData.FormerSchoolName ?? "Not Specified",
                                SchoolLevel = studentData.FormerSchoolLevel ?? "Secondary",
                                SchoolAddress = studentData.FormerSchoolAddress ?? "Not Specified",
                                YearOfCompletion = null
                            };

                            _context.Students.Add(student);
                            await _context.SaveChangesAsync(cancellationToken);

                            // Send welcome email asynchronously without blocking
                            _ = Task.Run(async () =>
                            {
                                try
                                {
                                    var emailSent = await _emailService.SendUserCreationEmailAsync(
                                        fullName: studentData.FullName,
                                        email: studentData.Email,
                                        role: "Student",
                                        password: password
                                    );

                                    if (emailSent)
                                    {
                                        Interlocked.Increment(ref localEmailsSent);
                                    }
                                }
                                catch (Exception emailEx)
                                {
                                    _logger.LogWarning(emailEx, $"Failed to send welcome email to {studentData.Email}");
                                }
                            }, CancellationToken.None);

                            // Record successful import
                            result.ImportedStudents.Add(new ImportedStudentResult
                            {
                                RowNumber = studentData.RowNumber,
                                StudentId = student.StudentId_Number,
                                FullName = student.FullName,
                                Email = student.Email,
                                ProgrammeName = await GetProgrammeName(studentData.ProgrammeId, cancellationToken),
                                SchoolName = await GetSchoolName(studentData.SchoolId, cancellationToken)
                            });

                            result.SuccessfulImports++;

                            _logger.LogDebug($"Successfully imported student: {studentData.FullName} ({studentData.Email})");
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, $"Error importing student from row {studentData.RowNumber}");
                            result.FailedRows.Add(new FailedImportRow
                            {
                                RowNumber = studentData.RowNumber,
                                StudentData = studentData,
                                ErrorMessage = $"Import failed: {ex.Message}"
                            });
                            result.FailedImports++;
                        }
                    }

                    await transaction.CommitAsync(cancellationToken);

                    // Update the result with local counter after batch completion
                    result.EmailsSent += localEmailsSent;

                    _logger.LogDebug($"Batch committed successfully: {batch.Count} students processed");
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    _logger.LogError(ex, $"Batch processing failed, rolling back {batch.Count} students");
                    throw;
                }
            });
        }

        private async Task<List<StudentValidationResult>> ValidateStudentDataBatchedAsync(List<StudentImportDto> students, CancellationToken cancellationToken = default)
        {
            var results = new List<StudentValidationResult>();
            const int validationBatchSize = 100; // Process validation in smaller batches

            try
            {
                // Get existing data for validation once
                var existingEmails = await _context.Users.Select(u => u.Email.ToLower()).ToListAsync(cancellationToken);
                var existingStudentIds = await _context.Students.Select(s => s.StudentId_Number).ToListAsync(cancellationToken);
                var validSchoolIds = await _context.Schools.Select(s => s.Id).ToListAsync(cancellationToken);
                var validDepartmentIds = await _context.Departments.Where(d => d.IsActive).Select(d => d.Id).ToListAsync(cancellationToken);
                var validProgrammeIds = await _context.Programmes.Select(p => p.Id).ToListAsync(cancellationToken);
                var validProgrammeLevelIds = await _context.ProgramLevels.Where(pl => pl.IsActive).Select(pl => pl.Id).ToListAsync(cancellationToken);
                var validModeOfStudyIds = await _context.ModesOfStudy.Select(m => m.ModeId).ToListAsync(cancellationToken);
                var validAcademicYearIds = await _context.AcademicYears.Select(ay => ay.YearId).ToListAsync(cancellationToken);

                // Get hierarchical relationships for validation
                var schoolProgrammeMap = await _context.Programmes
                    .Include(p => p.Department)
                    .Select(p => new { p.Id, SchoolId = p.Department.SchoolId })
                    .ToDictionaryAsync(p => p.Id, p => p.SchoolId, cancellationToken);

                // Process students in batches
                for (int i = 0; i < students.Count; i += validationBatchSize)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var batch = students.Skip(i).Take(validationBatchSize);

                    foreach (var student in batch)
                    {
                        var validation = ValidateStudent(student, existingEmails, existingStudentIds,
                            validSchoolIds, validProgrammeIds, validProgrammeLevelIds,
                            validModeOfStudyIds, validAcademicYearIds, schoolProgrammeMap);

                        results.Add(validation);
                    }
                }

                return results;
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Student validation was cancelled");
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during student validation");
                throw;
            }
        }

        public async Task<List<StudentValidationResult>> ValidateStudentDataAsync(List<StudentImportDto> students, CancellationToken cancellationToken = default)
        {
            return await ValidateStudentDataBatchedAsync(students, cancellationToken);
        }

        private StudentValidationResult ValidateStudent(StudentImportDto student,
            List<string> existingEmails, List<string> existingStudentIds,
            List<int> validSchoolIds, List<int> validProgrammeIds,
            List<int> validProgrammeLevelIds, List<int> validModeOfStudyIds,
            List<int> validAcademicYearIds, Dictionary<int, int> schoolProgrammeMap)
        {
            var validation = new StudentValidationResult
            {
                RowNumber = student.RowNumber,
                Errors = new List<string>(),
                Warnings = new List<string>()
            };

            // Required field validations
            if (string.IsNullOrWhiteSpace(student.FullName))
                validation.Errors.Add("Full Name is required");

            if (string.IsNullOrWhiteSpace(student.Email))
                validation.Errors.Add("Email is required");
            else if (!IsValidEmail(student.Email))
                validation.Errors.Add("Email format is invalid");
            else if (existingEmails.Contains(student.Email.ToLower()))
                validation.Errors.Add("Email already exists in the system");

            if (string.IsNullOrWhiteSpace(student.StudentId_Number))
                validation.Errors.Add("Student ID is required");
            else if (existingStudentIds.Contains(student.StudentId_Number))
                validation.Errors.Add("Student ID already exists in the system");

            if (string.IsNullOrWhiteSpace(student.NrcOrPassportNumber))
                validation.Errors.Add("NRC/Passport Number is required");

            if (student.DateOfBirth == default)
                validation.Errors.Add("Date of Birth is required");
            else if (student.DateOfBirth > DateTime.Now.AddYears(-15))
                validation.Warnings.Add("Student appears to be under 15 years old");

            if (string.IsNullOrWhiteSpace(student.Gender))
                validation.Errors.Add("Gender is required");
            else if (!new[] { "M", "F", "Male", "Female" }.Contains(student.Gender, StringComparer.OrdinalIgnoreCase))
                validation.Errors.Add("Gender must be M, F, Male, or Female");

            // School validation
            if (student.SchoolId <= 0)
                validation.Errors.Add("School ID is required");
            else if (!validSchoolIds.Contains(student.SchoolId))
                validation.Errors.Add("Invalid School ID");

            // Programme validation with hierarchical check
            if (student.ProgrammeId <= 0)
                validation.Errors.Add("Programme ID is required");
            else if (!validProgrammeIds.Contains(student.ProgrammeId))
                validation.Errors.Add("Invalid Programme ID");
            else
            {
                // Validate School -> Programme relationship
                if (schoolProgrammeMap.TryGetValue(student.ProgrammeId, out var programmeSchoolId))
                {
                    if (programmeSchoolId != student.SchoolId)
                    {
                        validation.Errors.Add($"Programme (ID: {student.ProgrammeId}) does not belong to School (ID: {student.SchoolId})");
                    }
                }
                else
                {
                    validation.Errors.Add("Could not validate School-Programme relationship");
                }
            }

            // Other validations
            if (student.ProgrammeLevelId <= 0)
                validation.Errors.Add("Programme Level ID is required");
            else if (!validProgrammeLevelIds.Contains(student.ProgrammeLevelId))
                validation.Errors.Add("Invalid Programme Level ID");

            if (student.ModeOfStudyId <= 0)
                validation.Errors.Add("Mode of Study ID is required");
            else if (!validModeOfStudyIds.Contains(student.ModeOfStudyId))
                validation.Errors.Add("Invalid Mode of Study ID");

            if (student.AcademicYearId <= 0)
                validation.Errors.Add("Academic Year ID is required");
            else if (!validAcademicYearIds.Contains(student.AcademicYearId))
                validation.Errors.Add("Invalid Academic Year ID");

            // Numeric field validations
            if (student.StudentCurrentYear < 1 || student.StudentCurrentYear > 7)
                validation.Errors.Add("Student Current Year must be between 1 and 7");

            if (student.CurrentSemester < 1 || student.CurrentSemester > 2)
                validation.Errors.Add("Current Semester must be 1 or 2");

            // Optional field warnings
            if (string.IsNullOrWhiteSpace(student.Phone))
                validation.Warnings.Add("Phone number not provided");

            if (string.IsNullOrWhiteSpace(student.AddressLine1))
                validation.Warnings.Add("Address information not provided");

            if (string.IsNullOrWhiteSpace(student.NextOfKinName))
                validation.Warnings.Add("Next of Kin information not provided");

            validation.IsValid = !validation.Errors.Any();
            return validation;
        }

        public async Task<byte[]> GenerateImportTemplateAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                using var workbook = new XLWorkbook();
                var worksheet = workbook.Worksheets.Add("Student Import Template");

                // Define headers based on hierarchical structure
                var headers = new[]
                {
                    "FullName", "Email", "StudentId_Number", "NrcOrPassportNumber",
                    "DateOfBirth (YYYY-MM-DD)", "Gender", "Phone", "MaritalStatus",
                    "Nationality", "Religion", "IsForeigner", "SchoolId", "DepartmentId",
                    "ProgrammeId", "ProgrammeLevelId", "ModeOfStudyId", "AcademicYearId",
                    "StudentCurrentYear", "CurrentSemester", "AddressLine1", "City",
                    "State", "Country", "NextOfKinName", "NextOfKinRelation", "NextOfKinPhone"
                };

                // Add headers
                for (int i = 0; i < headers.Length; i++)
                {
                    var cell = worksheet.Cell(1, i + 1);
                    cell.Value = headers[i];
                    cell.Style.Font.Bold = true;
                    cell.Style.Fill.BackgroundColor = XLColor.LightBlue;
                    cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                }

                // Add sample data row
                var sampleRow = new object[]
                {
                    "John Doe", "john.doe@example.com", "2024000001", "123456/12/1",
                    "2000-01-15", "M", "+260971234567", "Single",
                    "Zambian", "Christian", "0", "1", "1",
                    "1", "1", "1", "1",
                    "1", "1", "123 Main Street", "Lusaka",
                    "Lusaka", "Zambia", "Jane Doe", "Mother", "+260971234568"
                };

                for (int i = 0; i < sampleRow.Length; i++)
                {
                    worksheet.Cell(2, i + 1).Value = XLCellValue.FromObject(sampleRow[i]);
                }

                // Add reference data sheets
                await AddReferenceDataSheetsAsync(workbook, cancellationToken);

                // Auto-fit columns
                worksheet.ColumnsUsed().AdjustToContents();

                using var stream = new MemoryStream();
                workbook.SaveAs(stream);
                return stream.ToArray();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating import template");
                throw;
            }
        }

        public async Task<byte[]> GenerateErrorReportAsync(ImportPreviewResult previewResult, CancellationToken cancellationToken = default)
        {
            try
            {
                using var workbook = new XLWorkbook();
                var worksheet = workbook.Worksheets.Add("Import Errors");

                // Add headers
                var headers = new[] { "Row", "FullName", "Email", "StudentId_Number", "Errors", "Warnings" };

                for (int i = 0; i < headers.Length; i++)
                {
                    var cell = worksheet.Cell(1, i + 1);
                    cell.Value = headers[i];
                    cell.Style.Font.Bold = true;
                    cell.Style.Fill.BackgroundColor = XLColor.Red;
                    cell.Style.Font.FontColor = XLColor.White;
                }

                int row = 2;
                foreach (var invalidStudent in previewResult.InvalidStudents)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var validation = previewResult.ValidationResults.FirstOrDefault(vr => vr.RowNumber == invalidStudent.RowNumber);

                    worksheet.Cell(row, 1).Value = invalidStudent.RowNumber;
                    worksheet.Cell(row, 2).Value = invalidStudent.FullName;
                    worksheet.Cell(row, 3).Value = invalidStudent.Email;
                    worksheet.Cell(row, 4).Value = invalidStudent.StudentId_Number;
                    worksheet.Cell(row, 5).Value = string.Join("; ", validation?.Errors ?? new List<string>());
                    worksheet.Cell(row, 6).Value = string.Join("; ", validation?.Warnings ?? new List<string>());

                    row++;
                }

                worksheet.ColumnsUsed().AdjustToContents();

                using var stream = new MemoryStream();
                workbook.SaveAs(stream);
                return stream.ToArray();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating error report");
                throw;
            }
        }

        public async Task<ImportProgress> GetImportProgressAsync(string progressKey, CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;
            return _progressTracker.TryGetValue(progressKey, out var progress) ? progress : null;
        }

        public async Task CleanupProgressAsync(string progressKey)
        {
            try
            {
                await Task.Delay(100); // Small delay
                _progressTracker.TryRemove(progressKey, out _);
                _logger.LogDebug($"Cleaned up progress tracking for key: {progressKey}");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, $"Error cleaning up progress for key: {progressKey}");
            }
        }

        #region Private Helper Methods

        private Dictionary<string, int> GetHeadersFromWorksheet(IXLWorksheet worksheet)
        {
            var headers = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var headerRow = worksheet.Row(1);

            for (int col = 1; col <= headerRow.CellsUsed().Count(); col++)
            {
                var headerValue = headerRow.Cell(col).GetString().Trim();
                if (!string.IsNullOrEmpty(headerValue))
                {
                    headers[headerValue] = col;
                }
            }

            return headers;
        }

        private ValidationResult ValidateHeaders(Dictionary<string, int> headers)
        {
            var requiredHeaders = new[]
            {
                "FullName", "Email", "StudentId_Number", "NrcOrPassportNumber",
                "DateOfBirth (YYYY-MM-DD)", "Gender", "SchoolId", "ProgrammeId",
                "ProgrammeLevelId", "ModeOfStudyId", "AcademicYearId",
                "StudentCurrentYear", "CurrentSemester"
            };

            var missingHeaders = requiredHeaders.Where(rh => !headers.ContainsKey(rh)).ToList();

            if (missingHeaders.Any())
            {
                return new ValidationResult
                {
                    IsValid = false,
                    ErrorMessage = $"Missing required columns: {string.Join(", ", missingHeaders)}"
                };
            }

            return new ValidationResult { IsValid = true };
        }

        private StudentImportDto ParseStudentFromRow(IXLWorksheet worksheet, int rowNumber, Dictionary<string, int> headers)
        {
            var row = worksheet.Row(rowNumber);

            var student = new StudentImportDto
            {
                RowNumber = rowNumber,
                ValidationErrors = new List<string>()
            };

            // Parse required fields
            student.FullName = GetCellValue(row, headers, "FullName");
            student.Email = GetCellValue(row, headers, "Email");
            student.StudentId_Number = GetCellValue(row, headers, "StudentId_Number");
            student.NrcOrPassportNumber = GetCellValue(row, headers, "NrcOrPassportNumber");

            // Parse date of birth
            if (headers.ContainsKey("DateOfBirth (YYYY-MM-DD)"))
            {
                var dobValue = GetCellValue(row, headers, "DateOfBirth (YYYY-MM-DD)");
                if (DateTime.TryParse(dobValue, out var dob))
                {
                    student.DateOfBirth = dob;
                }
                else if (double.TryParse(dobValue, out var excelDate))
                {
                    student.DateOfBirth = DateTime.FromOADate(excelDate);
                }
            }

            student.Gender = GetCellValue(row, headers, "Gender");
            student.Phone = GetCellValue(row, headers, "Phone");
            student.MaritalStatus = GetCellValue(row, headers, "MaritalStatus");
            student.Nationality = GetCellValue(row, headers, "Nationality");
            student.Religion = GetCellValue(row, headers, "Religion");

            // Parse boolean for IsForeigner
            var isForeignerValue = GetCellValue(row, headers, "IsForeigner");
            student.IsForeigner = isForeignerValue == "1" || isForeignerValue.Equals("true", StringComparison.OrdinalIgnoreCase);

            // Parse integer fields
            student.SchoolId = ParseInt(GetCellValue(row, headers, "SchoolId"));
            student.DepartmentId = ParseIntNullable(GetCellValue(row, headers, "DepartmentId"));
            student.ProgrammeId = ParseInt(GetCellValue(row, headers, "ProgrammeId"));
            student.ProgrammeLevelId = ParseInt(GetCellValue(row, headers, "ProgrammeLevelId"));
            student.ModeOfStudyId = ParseInt(GetCellValue(row, headers, "ModeOfStudyId"));
            student.AcademicYearId = ParseInt(GetCellValue(row, headers, "AcademicYearId"));
            student.StudentCurrentYear = ParseInt(GetCellValue(row, headers, "StudentCurrentYear"));
            student.CurrentSemester = ParseInt(GetCellValue(row, headers, "CurrentSemester"));

            // Parse optional address fields
            student.AddressLine1 = GetCellValue(row, headers, "AddressLine1");
            student.AddressLine2 = GetCellValue(row, headers, "AddressLine2");
            student.City = GetCellValue(row, headers, "City");
            student.State = GetCellValue(row, headers, "State");
            student.Country = GetCellValue(row, headers, "Country");
            student.PostalCode = GetCellValue(row, headers, "PostalCode");

            // Parse optional next of kin fields
            student.NextOfKinName = GetCellValue(row, headers, "NextOfKinName");
            student.NextOfKinRelation = GetCellValue(row, headers, "NextOfKinRelation");
            student.NextOfKinPhone = GetCellValue(row, headers, "NextOfKinPhone");
            student.NextOfKinEmail = GetCellValue(row, headers, "NextOfKinEmail");
            student.NextOfKinAddress = GetCellValue(row, headers, "NextOfKinAddress");

            // Parse optional former school fields
            student.FormerSchoolName = GetCellValue(row, headers, "FormerSchoolName");
            student.FormerSchoolLevel = GetCellValue(row, headers, "FormerSchoolLevel");
            student.FormerSchoolAddress = GetCellValue(row, headers, "FormerSchoolAddress");
            var yearCompletionValue = GetCellValue(row, headers, "YearOfCompletion");
            student.YearOfCompletion = ParseIntNullable(yearCompletionValue)?.ToString();

            return student;
        }

        private string GetCellValue(IXLRow row, Dictionary<string, int> headers, string headerName)
        {
            if (headers.TryGetValue(headerName, out var columnIndex))
            {
                return row.Cell(columnIndex).GetString().Trim();
            }
            return string.Empty;
        }

        private int ParseInt(string value)
        {
            return int.TryParse(value, out var result) ? result : 0;
        }

        private int? ParseIntNullable(string value)
        {
            return int.TryParse(value, out var result) ? result : null;
        }

        private bool IsValidEmail(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
                return false;

            try
            {
                var addr = new System.Net.Mail.MailAddress(email);
                return addr.Address == email;
            }
            catch
            {
                return false;
            }
        }

        private string GenerateSecurePassword()
        {
            const string lowercase = "abcdefghijklmnopqrstuvwxyz";
            const string uppercase = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
            const string digits = "0123456789";
            const string specialChars = "!@#$%^&*";

            var random = new Random();
            var password = new StringBuilder();

            // Ensure at least one character from each required category
            password.Append(lowercase[random.Next(lowercase.Length)]);
            password.Append(uppercase[random.Next(uppercase.Length)]);
            password.Append(digits[random.Next(digits.Length)]);
            password.Append(specialChars[random.Next(specialChars.Length)]);

            // Fill the rest randomly from all categories
            const string allChars = lowercase + uppercase + digits + specialChars;
            for (int i = 4; i < 12; i++) // Total length of 12 characters
            {
                password.Append(allChars[random.Next(allChars.Length)]);
            }

            // Shuffle the password to avoid predictable patterns
            return new string(password.ToString().OrderBy(x => random.Next()).ToArray());
        }

        private string GenerateApplicationReferenceNumber()
        {
            return $"APP{DateTime.Now:yyyyMMdd}{new Random().Next(1000, 9999)}";
        }

        private async Task<string> GetProgrammeName(int programmeId, CancellationToken cancellationToken = default)
        {
            try
            {
                var programme = await _context.Programmes.FindAsync(new object[] { programmeId }, cancellationToken);
                return programme?.Name ?? "Unknown Programme";
            }
            catch
            {
                return "Unknown Programme";
            }
        }

        private async Task<string> GetSchoolName(int schoolId, CancellationToken cancellationToken = default)
        {
            try
            {
                var school = await _context.Schools.FindAsync(new object[] { schoolId }, cancellationToken);
                return school?.Name ?? "Unknown School";
            }
            catch
            {
                return "Unknown School";
            }
        }

        private async Task AddReferenceDataSheetsAsync(XLWorkbook workbook, CancellationToken cancellationToken = default)
        {
            try
            {
                // Add Schools reference sheet
                var schoolsSheet = workbook.Worksheets.Add("Schools");
                schoolsSheet.Cell(1, 1).Value = "SchoolId";
                schoolsSheet.Cell(1, 2).Value = "SchoolName";

                var schools = await _context.Schools.Select(s => new { s.Id, s.Name }).ToListAsync(cancellationToken);
                for (int i = 0; i < schools.Count; i++)
                {
                    schoolsSheet.Cell(i + 2, 1).Value = schools[i].Id;
                    schoolsSheet.Cell(i + 2, 2).Value = schools[i].Name;
                }

                // Add Programmes reference sheet
                var programmesSheet = workbook.Worksheets.Add("Programmes");
                programmesSheet.Cell(1, 1).Value = "ProgrammeId";
                programmesSheet.Cell(1, 2).Value = "ProgrammeName";
                programmesSheet.Cell(1, 3).Value = "SchoolId";
                programmesSheet.Cell(1, 4).Value = "SchoolName";

                var programmes = await _context.Programmes
                    .Include(p => p.Department)
                        .ThenInclude(d => d.School)
                    .Select(p => new {
                        p.Id,
                        p.Name,
                        SchoolId = p.Department.SchoolId,
                        SchoolName = p.Department.School.Name
                    })
                    .ToListAsync(cancellationToken);

                for (int i = 0; i < programmes.Count; i++)
                {
                    programmesSheet.Cell(i + 2, 1).Value = programmes[i].Id;
                    programmesSheet.Cell(i + 2, 2).Value = programmes[i].Name;
                    programmesSheet.Cell(i + 2, 3).Value = programmes[i].SchoolId;
                    programmesSheet.Cell(i + 2, 4).Value = programmes[i].SchoolName;
                }

                // Add Programme Levels reference sheet
                var levelsSheet = workbook.Worksheets.Add("Programme Levels");
                levelsSheet.Cell(1, 1).Value = "ProgrammeLevelId";
                levelsSheet.Cell(1, 2).Value = "LevelName";

                var levels = await _context.ProgramLevels
                    .Where(pl => pl.IsActive)
                    .Select(pl => new { pl.Id, pl.Name })
                    .ToListAsync(cancellationToken);

                for (int i = 0; i < levels.Count; i++)
                {
                    levelsSheet.Cell(i + 2, 1).Value = levels[i].Id;
                    levelsSheet.Cell(i + 2, 2).Value = levels[i].Name;
                }

                // Add Modes of Study reference sheet
                var modesSheet = workbook.Worksheets.Add("Modes of Study");
                modesSheet.Cell(1, 1).Value = "ModeOfStudyId";
                modesSheet.Cell(1, 2).Value = "ModeName";

                var modes = await _context.ModesOfStudy
                    .Select(m => new { m.ModeId, m.ModeName })
                    .ToListAsync(cancellationToken);

                for (int i = 0; i < modes.Count; i++)
                {
                    modesSheet.Cell(i + 2, 1).Value = modes[i].ModeId;
                    modesSheet.Cell(i + 2, 2).Value = modes[i].ModeName;
                }

                // Add Academic Years reference sheet
                var yearsSheet = workbook.Worksheets.Add("Academic Years");
                yearsSheet.Cell(1, 1).Value = "AcademicYearId";
                yearsSheet.Cell(1, 2).Value = "YearValue";
                yearsSheet.Cell(1, 3).Value = "StartDate";

                var academicYears = await _context.AcademicYears
                    .Where(ay => ay.IsActive || ay.StartDate >= DateTime.Now.AddYears(-2))
                    .Select(ay => new { ay.YearId, ay.YearValue, ay.StartDate })
                    .OrderByDescending(ay => ay.StartDate)
                    .ToListAsync(cancellationToken);

                for (int i = 0; i < academicYears.Count; i++)
                {
                    yearsSheet.Cell(i + 2, 1).Value = academicYears[i].YearId;
                    yearsSheet.Cell(i + 2, 2).Value = academicYears[i].YearValue;
                    yearsSheet.Cell(i + 2, 3).Value = academicYears[i].StartDate.ToString("yyyy-MM-dd");
                }

                // Auto-fit all reference sheets
                foreach (var sheet in workbook.Worksheets.Skip(1)) // Skip the main template sheet
                {
                    sheet.ColumnsUsed().AdjustToContents();

                    // Style headers
                    var headerRow = sheet.Row(1);
                    headerRow.Style.Font.Bold = true;
                    headerRow.Style.Fill.BackgroundColor = XLColor.LightGray;
                    headerRow.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error adding reference data sheets");
                // Continue without reference sheets if there's an error
            }
        }

        private void UpdateProgress(string progressKey, string message, int percentComplete, int currentBatch, int totalBatches)
        {
            try
            {
                _progressTracker[progressKey] = new ImportProgress
                {
                    CurrentStep = message,
                    PercentComplete = percentComplete,
                    Message = message,
                    LastUpdated = DateTime.Now,
                    CurrentBatch = currentBatch,
                    TotalBatches = totalBatches
                };
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, $"Error updating progress for key: {progressKey}");
            }
        }

        #endregion

        #region Helper Classes

        private class ValidationResult
        {
            public bool IsValid { get; set; }
            public string ErrorMessage { get; set; }
        }

        #endregion
    }

    #region DTOs and Models

    public class StudentImportDto
    {
        public int RowNumber { get; set; }

        // Required Personal Information
        public string FullName { get; set; }
        public string Email { get; set; }
        public string StudentId_Number { get; set; }
        public string NrcOrPassportNumber { get; set; }
        public DateTime DateOfBirth { get; set; }
        public string Gender { get; set; }
        public string Phone { get; set; }

        // Optional Personal Information
        public string MaritalStatus { get; set; }
        public string Nationality { get; set; }
        public string Religion { get; set; }
        public bool IsForeigner { get; set; }

        // Academic Information (Required)
        public int SchoolId { get; set; }
        public int? DepartmentId { get; set; } // Optional for backward compatibility
        public int ProgrammeId { get; set; }
        public int ProgrammeLevelId { get; set; }
        public int ModeOfStudyId { get; set; }
        public int AcademicYearId { get; set; }
        public int StudentCurrentYear { get; set; }
        public int CurrentSemester { get; set; }

        // Address Information (Optional)
        public string AddressLine1 { get; set; }
        public string AddressLine2 { get; set; }
        public string City { get; set; }
        public string State { get; set; }
        public string Country { get; set; }
        public string PostalCode { get; set; }

        // Next of Kin Information (Optional)
        public string NextOfKinName { get; set; }
        public string NextOfKinRelation { get; set; }
        public string NextOfKinPhone { get; set; }
        public string NextOfKinEmail { get; set; }
        public string NextOfKinAddress { get; set; }

        // Former School Information (Optional)
        public string FormerSchoolName { get; set; }
        public string FormerSchoolLevel { get; set; }
        public string FormerSchoolAddress { get; set; }
        public string? YearOfCompletion { get; set; }

        // Validation
        public List<string> ValidationErrors { get; set; } = new List<string>();
    }

    public class ImportPreviewResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public int TotalRows { get; set; }
        public List<StudentImportDto> ValidStudents { get; set; } = new List<StudentImportDto>();
        public List<StudentImportDto> InvalidStudents { get; set; } = new List<StudentImportDto>();
        public List<StudentValidationResult> ValidationResults { get; set; } = new List<StudentValidationResult>();
    }

    public class ImportProcessResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public int TotalProcessed { get; set; }
        public int SuccessfulImports { get; set; }
        public int FailedImports { get; set; }
        public int EmailsSent { get; set; }
        public TimeSpan ProcessingTime { get; set; }
        public List<ImportedStudentResult> ImportedStudents { get; set; } = new List<ImportedStudentResult>();
        public List<FailedImportRow> FailedRows { get; set; } = new List<FailedImportRow>();
        public ImportSummary ImportSummary { get; set; }
    }

    public class StudentValidationResult
    {
        public int RowNumber { get; set; }
        public bool IsValid { get; set; }
        public List<string> Errors { get; set; } = new List<string>();
        public List<string> Warnings { get; set; } = new List<string>();
    }

    public class ImportedStudentResult
    {
        public int RowNumber { get; set; }
        public string StudentId { get; set; }
        public string FullName { get; set; }
        public string Email { get; set; }
        public string ProgrammeName { get; set; }
        public string SchoolName { get; set; }
    }

    public class FailedImportRow
    {
        public int RowNumber { get; set; }
        public StudentImportDto StudentData { get; set; }
        public string ErrorMessage { get; set; }
    }

    public class ImportProgress
    {
        public string CurrentStep { get; set; }
        public int PercentComplete { get; set; }
        public string Message { get; set; }
        public DateTime LastUpdated { get; set; }
        public int CurrentBatch { get; set; }
        public int TotalBatches { get; set; }
    }

    public class ImportSummary
    {
        public int TotalRowsInFile { get; set; }
        public int ValidRowsForImport { get; set; }
        public int InvalidRowsSkipped { get; set; }
        public int UsersCreated { get; set; }
        public int EmailsSent { get; set; }
        public int ErrorsEncountered { get; set; }
    }

    #endregion
}