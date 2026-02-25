using Microsoft.Extensions.Options;
using SIS.Models.Accounting;
using SIS.Models.Configuration;
using SIS.Models.Fees;
using SIS.Services.Accounting;
using SIS.Data;
using Microsoft.EntityFrameworkCore;
using System.Text;
using System.Text.Json;

public class AccountingService : IAccountingService
{
    private readonly HttpClient _httpClient;
    private readonly AccountingSystemOptions _options;
    private readonly ILogger<AccountingService> _logger;
    private readonly ApplicationDbContext _context;

    public AccountingService(
        HttpClient httpClient,
        IOptions<AccountingSystemOptions> options,
        ILogger<AccountingService> logger,
        ApplicationDbContext context)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
        _context = context;
    }

    public async Task<AccountingApiResponse> PostRegistrationFeeAsync(decimal amount, string studentReference)
    {
        try
        {
            var reference = $"REG-{studentReference}";

            var request = new RegistrationFeeRequest
            {
                CCode = _options.DefaultCCode,
                Reference = reference,
                Description = _options.RegistrationFee.Description,
                Items = new List<RegistrationFeeItem>
                {
                    new RegistrationFeeItem
                    {
                        Amount = amount,
                        Reference = reference,
                        Description = _options.RegistrationFee.Description,
                        CreditNCode = _options.RegistrationFee.CreditNCode,
                        DebitNCode = _options.RegistrationFee.DebitNCode
                    }
                }
            };

            var jsonContent = JsonSerializer.Serialize(request);
            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            _logger.LogInformation("Posting registration fee for student: {StudentReference}, Amount: {Amount}",
                studentReference, amount);

            var response = await _httpClient.PostAsync("/api/eden/post-registration-fee", content);
            var responseContent = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Successfully posted registration fee for student: {StudentReference}",
                    studentReference);

                return new AccountingApiResponse
                {
                    Success = true,
                    Message = "Registration fee posted successfully",
                    TransactionId = reference
                };

            }
            else
            {
                _logger.LogError("Failed to post registration fee. Status: {StatusCode}, Response: {Response}",
                    response.StatusCode, responseContent);

                return new AccountingApiResponse
                {
                    Success = false,
                    Message = $"API call failed: {response.StatusCode} - {responseContent}"
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception occurred while posting registration fee for student: {StudentReference}",
                studentReference);

            return new AccountingApiResponse
            {
                Success = false,
                Message = $"An error occurred: {ex.Message}"
            };
        }
    }

    public async Task<AccountingApiResponse> CreateCustomerAsync(string studentId, string fullName, string address, string email, string phone)
    {
        try
        {
            var request = new CreateCustomerRequest
            {
                CCode = studentId,
                CName = fullName,
                CAddress = address,
                CEmail = email,
                CPhone = phone,
                CTexRegistrationNumber = _options.CustomerDefaults.TaxRegistrationNumber,
                CreditScore = _options.CustomerDefaults.CreditScore,
                CBalance = _options.CustomerDefaults.CBalance,
                CBalanceC = _options.CustomerDefaults.CBalanceC,
                BankDetails = new List<BankDetail>
            {
                new BankDetail
                {
                    BankName = _options.CustomerDefaults.DefaultBankDetails.BankName,
                    BranchName = _options.CustomerDefaults.DefaultBankDetails.BranchName,
                    BankId = _options.CustomerDefaults.DefaultBankDetails.BankId,
                    AccountNumber = _options.CustomerDefaults.DefaultBankDetails.AccountNumber,
                    SortCode = _options.CustomerDefaults.DefaultBankDetails.SortCode,
                    SwiftCode = _options.CustomerDefaults.DefaultBankDetails.SwiftCode,
                    AccountName = fullName // Use student's name for account name
                }
            }
            };

            var jsonContent = JsonSerializer.Serialize(request, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });
            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            _logger.LogInformation("Creating customer in accounting system for student: {StudentId}", studentId);

            var response = await _httpClient.PostAsync("/api/eden/create", content);
            var responseContent = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Successfully created customer for student: {StudentId}", studentId);

                return new AccountingApiResponse
                {
                    Success = true,
                    Message = "Customer created successfully",
                    TransactionId = studentId
                };
            }
            else
            {
                _logger.LogError("Failed to create customer. Status: {StatusCode}, Response: {Response}",
                    response.StatusCode, responseContent);

                return new AccountingApiResponse
                {
                    Success = false,
                    Message = $"API call failed: {response.StatusCode} - {responseContent}"
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception occurred while creating customer for student: {StudentId}", studentId);

            return new AccountingApiResponse
            {
                Success = false,
                Message = $"An error occurred: {ex.Message}"
            };
        }
    }

    public async Task<AccountingApiResponse> PostStudentInvoiceAsync(string studentId, string studentName, string address, string email, string phone, decimal totalAmount, List<FeeConfiguration> fees)
    {
        try
        {
            var invoiceReference = $"{_options.InvoiceDefaults.InvoicePrefix}-{DateTime.Now:yyyyMMdd}-{studentId}";

            // Get programme NCodes for the student
            var invoiceItems = new List<InvoiceItem>();

            foreach (var fee in fees)
            {
                // Get specific N-Codes for this fee type
                var feeNCodes = await GetStudentFeeConfigurationNCodesAsync(studentId, fee.FeeTypeId);

                invoiceItems.Add(new InvoiceItem
                {
                    Amount = fee.Amount,
                    Reference = invoiceReference,
                    Description = fee.FeeType?.Name ?? "Student Fee",
                    // Use fee-specific NCodes if available, otherwise use defaults
                    CreditNCode = feeNCodes?.CreditNCode ?? _options.InvoiceDefaults.CreditNCode,
                    DebitNCode = feeNCodes?.DebitNCode ?? _options.InvoiceDefaults.DebitNCode
                });
            }

            var request = new PostInvoiceRequest
            {
                CCode = studentId,
                Reference = invoiceReference,
                Description = $"Student Fees Invoice for {studentName}",
                Names = studentName,
                Address = address,
                Email = email,
                Phone = phone,
                Items = invoiceItems
            };

            var jsonContent = JsonSerializer.Serialize(request, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });
            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            _logger.LogInformation("Posting student invoice to accounting system for student: {StudentId}", studentId);

            var response = await _httpClient.PostAsync("/api/eden/post-invoice", content);
            var responseContent = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Successfully posted student invoice for student: {StudentId}", studentId);

                return new AccountingApiResponse
                {
                    Success = true,
                    Message = "Student invoice posted successfully",
                    TransactionId = invoiceReference
                };
            }
            else
            {
                _logger.LogError("Failed to post student invoice. Status: {StatusCode}, Response: {Response}",
                    response.StatusCode, responseContent);

                return new AccountingApiResponse
                {
                    Success = false,
                    Message = $"API call failed: {response.StatusCode} - {responseContent}"
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception occurred while posting student invoice for student: {StudentId}", studentId);

            return new AccountingApiResponse
            {
                Success = false,
                Message = $"An error occurred: {ex.Message}"
            };
        }
    }

    public async Task<AccountingApiResponse> PostPaymentAsync(string studentId, decimal paymentAmount)
    {
        try
        {
            var request = new PostPaymentRequest
            {
                CCode = studentId,
                CreditNCode = _options.PaymentDefaults.CreditNCode,
                DebitNCode = _options.PaymentDefaults.DebitNCode,
                PaymentAmount = paymentAmount
            };

            var jsonContent = JsonSerializer.Serialize(request, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });
            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            _logger.LogInformation("Posting payment to accounting system for student: {StudentId}, Amount: {Amount}",
                studentId, paymentAmount);

            var response = await _httpClient.PostAsync("/api/eden/post-payment", content);
            var responseContent = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Successfully posted payment for student: {StudentId}, Amount: {Amount}",
                    studentId, paymentAmount);

                return new AccountingApiResponse
                {
                    Success = true,
                    Message = "Payment posted successfully",
                    TransactionId = $"PAY-{DateTime.Now:yyyyMMddHHmmss}-{studentId}"
                };
            }
            else
            {
                _logger.LogError("Failed to post payment. Status: {StatusCode}, Response: {Response}",
                    response.StatusCode, responseContent);

                return new AccountingApiResponse
                {
                    Success = false,
                    Message = $"API call failed: {response.StatusCode} - {responseContent}"
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception occurred while posting payment for student: {StudentId}", studentId);

            return new AccountingApiResponse
            {
                Success = false,
                Message = $"An error occurred: {ex.Message}"
            };
        }
    }

    private async Task<ProgrammeNCodesDto?> GetStudentFeeConfigurationNCodesAsync(string studentId, int feeTypeId, int? academicYearId = null)
    {
        try
        {
            // Get student information
            var student = await _context.Students
                .Include(s => s.Programme)
                    .ThenInclude(p => p.Department)
                    .ThenInclude(d => d.School)
                .Include(s => s.Programme)
                    .ThenInclude(p => p.ProgrammeLevel)
                .Include(s => s.ModeOfStudy)
                .FirstOrDefaultAsync(s => s.StudentId_Number == studentId);

            if (student == null)
            {
                _logger.LogWarning("Student not found: {StudentId}", studentId);
                return null;
            }

            // Build query for fee configuration with N-Codes
            var query = _context.FeeConfigurations
                .Where(fc => fc.FeeTypeId == feeTypeId &&
                            !string.IsNullOrWhiteSpace(fc.CreditNCode) &&
                            !string.IsNullOrWhiteSpace(fc.DebitNCode));

            // Add academic year filter if provided
            if (academicYearId.HasValue)
            {
                query = query.Where(fc => fc.AcademicYearId == academicYearId);
            }

            // Try to find the most specific match first
            var feeConfig = await query
                .Where(fc =>
                    // Exact match for all criteria
                    fc.SchoolId == student.Programme.Department.SchoolId &&
                    fc.ProgrammeId == student.ProgrammeId &&
                    fc.ModeOfStudyId == student.ModeOfStudyId &&
                    fc.YearOfStudy == student.StudentCurrentYear &&
                    fc.ProgramLevelId == student.Programme.ProgrammeLevelId)
                .FirstOrDefaultAsync();

            // If no exact match, try progressively less specific matches
            if (feeConfig == null)
            {
                feeConfig = await query
                    .Where(fc =>
                        fc.SchoolId == student.Programme.Department.SchoolId &&
                        fc.ProgrammeId == student.ProgrammeId &&
                        fc.ModeOfStudyId == student.ModeOfStudyId &&
                        fc.YearOfStudy == student.StudentCurrentYear)
                    .FirstOrDefaultAsync();
            }

            if (feeConfig == null)
            {
                feeConfig = await query
                    .Where(fc =>
                        fc.SchoolId == student.Programme.Department.SchoolId &&
                        fc.ProgrammeId == student.ProgrammeId)
                    .FirstOrDefaultAsync();
            }

            if (feeConfig == null)
            {
                feeConfig = await query
                    .Where(fc => fc.SchoolId == student.Programme.Department.SchoolId)
                    .FirstOrDefaultAsync();
            }

            if (feeConfig == null)
            {
                feeConfig = await query
                    .Where(fc => fc.AppliesUniversally == true)
                    .FirstOrDefaultAsync();
            }

            if (feeConfig != null)
            {
                _logger.LogInformation("Found fee configuration N-Codes for student: {StudentId}, FeeType: {FeeTypeId}, Credit: {CreditNCode}, Debit: {DebitNCode}",
                    studentId, feeTypeId, feeConfig.CreditNCode, feeConfig.DebitNCode);

                return new ProgrammeNCodesDto
                {
                    CreditNCode = feeConfig.CreditNCode,
                    DebitNCode = feeConfig.DebitNCode
                };
            }

            _logger.LogInformation("No fee configuration N-Codes found for student: {StudentId}, FeeType: {FeeTypeId}, using defaults", studentId, feeTypeId);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error retrieving fee configuration N-Codes for student: {StudentId}, FeeType: {FeeTypeId}, using defaults", studentId, feeTypeId);
            return null;
        }
    }
}

// DTO class to hold programme NCodes
public class ProgrammeNCodesDto
{
    public string CreditNCode { get; set; } = string.Empty;
    public string DebitNCode { get; set; } = string.Empty;
}