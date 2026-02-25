using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SIS.Data;
using SIS.Models.Fees;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SIS.Controllers
{
    [Route("Admin/[action]")]
    [Authorize(Roles = "Admin")]
    public class QuotationsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public QuotationsController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> GetProgrammesBySchool2(int schoolId)
        {
            var programmes = await _context.Programmes
                .Include(p => p.Department)
                .Where(p => p.Department.SchoolId == schoolId)
                .OrderBy(p => p.Name)
                .Select(p => new { p.Id, p.Name })
                .ToListAsync();
            return Json(programmes);
        }

        // GET: Admin/Quotations
        [HttpGet]
        public async Task<IActionResult> Quotations()
        {
            try
            {
                // Get active academic years for filter
                ViewBag.AcademicYears = await _context.AcademicYears
                    .AsNoTracking()
                    .Where(a => a.IsActive)
                    .OrderByDescending(a => a.YearId)
                    .Select(a => new SelectListItem
                    {
                        Value = a.YearId.ToString(),
                        Text = a.YearValue
                    })
                    .ToListAsync();

                ViewBag.Schools = await _context.Schools.AsNoTracking().OrderBy(s => s.Name).ToListAsync();
                ViewBag.Programmes = await _context.Programmes.AsNoTracking().ToListAsync();

                // Get fees from OtherFees
                var otherFees = await _context.OtherFees
                    .AsNoTracking()
                    .Include(o => o.School)
                    .Where(o => o.IsActive)
                    .Select(o => new
                    {
                        Id = o.Id,
                        FeeName = o.FeeName,
                        Amount = o.Amount,
                        SchoolName = o.School != null ? o.School.Name : "All Schools",
                        SchoolId = o.SchoolId,
                        ProgrammeId = o.ProgrammeId,
                        ModeId = o.ModeOfStudyId,
                        LevelId = o.ProgramLevelId,
                        YearOfStudy = (int?)null,
                        Semester = o.Semester,
                        AppliesOnlyToForeignStudents = o.AppliesOnlyToForeignStudents,
                        AppliesOnlyToLocalStudents = false,
                        Source = "OtherFees"
                    })
                    .ToListAsync();

                // Get fees from FeeConfiguration with all filter fields
                var bestFeeIds = await _context.FeeConfigurations
                    .Where(f => f.Amount > 0)
                    .GroupBy(f => new
                    {
                        f.AcademicYearId,
                        f.SchoolId,
                        f.ProgrammeId,
                        f.ModeOfStudyId,
                        f.ProgramLevelId,
                        f.YearOfStudy,
                        f.Semester,
                        f.FeeTypeId,
                        f.AppliesOnlyToForeignStudents,
                        f.AppliesOnlyToLocalStudents,
                        f.AppliesUniversally,
                        f.AppliesOnlyToAccommodated
                    })
                    .Select(g => g
                        .OrderByDescending(x => x.Amount)
                        .Select(x => x.Id)
                        .First())
                    .ToListAsync();

                var configuredFees = await _context.FeeConfigurations
                    .Where(f => bestFeeIds.Contains(f.Id))
                    .Include(f => f.FeeType)
                    .Include(f => f.School)
                    .Select(f => new
                    {
                        Id = f.Id,
                        FeeName = f.FeeType != null ? f.FeeType.Name : "Unknown Fee",
                        Amount = f.Amount,
                        SchoolName = f.School != null ? f.School.Name : "All Schools",
                        SchoolId = f.SchoolId,
                        ProgrammeId = f.ProgrammeId,
                        ModeId = f.ModeOfStudyId,
                        LevelId = f.ProgramLevelId,
                        YearOfStudy = f.YearOfStudy,
                        Semester = f.Semester,
                        AppliesOnlyToForeignStudents = f.AppliesOnlyToForeignStudents,
                        AppliesOnlyToLocalStudents = f.AppliesOnlyToLocalStudents,
                        Source = "FeeConfiguration"
                    })
                    .ToListAsync();


                // Combine both fee sources
                var allFees = otherFees.Concat(configuredFees)
                    .GroupBy(f => new
                    {
                        f.FeeName,
                        f.Amount,
                        f.SchoolId,
                        f.ProgrammeId,
                        f.ModeId,
                        f.LevelId,
                        f.YearOfStudy,
                        f.Semester,
                        f.AppliesOnlyToForeignStudents,
                        f.AppliesOnlyToLocalStudents
                    })
                    .Select(g => g.First()) // Take the first occurrence of each duplicate group
                    .OrderBy(f => f.Source)
                    .ThenBy(f => f.FeeName)
                    .ToList();

                ViewBag.AllFees = allFees;

                Console.WriteLine($"Loaded {otherFees.Count} Other Fees and {configuredFees.Count} Fee Configuration entries");

                return View("~/Views/Admin/Quotations.cshtml");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading quotations page: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                TempData["ErrorMessage"] = "An error occurred while loading the quotations page.";
                return View("~/Views/Admin/Quotations.cshtml");
            }
        }

        // POST: Admin/ValidateStudentsForQuotation
        [HttpPost]
        public async Task<IActionResult> ValidateStudentsForQuotation([FromBody] QuotationValidateStudentsRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.StudentIds))
                {
                    return Json(new
                    {
                        validStudents = new List<object>(),
                        invalidStudents = new List<object>(),
                        newStudents = new List<object>(),
                        totalValid = 0,
                        totalInvalid = 0,
                        totalNew = 0
                    });
                }

                var studentIdArray = request.StudentIds
                    .Split(',')
                    .Select(s => s.Trim())
                    .Where(s => !string.IsNullOrEmpty(s))
                    .Distinct()
                    .ToList();

                var validStudents = await _context.Students
                    .Include(s => s.Programme)
                        .ThenInclude(p => p.Department)
                        .ThenInclude(d => d.School)
                    .Where(s => studentIdArray.Contains(s.StudentId_Number))
                    .Select(s => new
                    {
                        studentId_Number = s.StudentId_Number,
                        fullName = s.FullName,
                        programmeName = s.Programme != null ? s.Programme.Name : "N/A",
                        schoolName = s.Programme != null && s.Programme.Department != null && s.Programme.Department.School != null
                            ? s.Programme.Department.School.Name : "N/A",
                        email = s.Email,
                        phoneNumber = s.Phone
                    })
                    .ToListAsync();

                var foundIds = validStudents.Select(s => s.studentId_Number).ToList();
                var notFoundIds = studentIdArray.Except(foundIds).ToList();

                // Return not found IDs as "new students" (pending admission)
                var newStudents = notFoundIds.Select(id => new
                {
                    studentId_Number = id,
                    status = "NEW STUDENT (Not yet in system)"
                }).ToList();

                return Json(new
                {
                    validStudents = validStudents,
                    invalidStudents = new List<object>(), // Keep for backward compatibility
                    newStudents = newStudents,
                    totalValid = validStudents.Count,
                    totalInvalid = 0,
                    totalNew = newStudents.Count
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error validating students: {ex.Message}");
                return Json(new
                {
                    error = true,
                    message = "An error occurred while validating students."
                });
            }
        }

        // POST: Admin/GenerateQuotations
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> GenerateQuotations(GenerateQuotationsRequest request)
        {
            try
            {
                if (request.SelectedFeeIds == null || !request.SelectedFeeIds.Any())
                {
                    TempData["ErrorMessage"] = "Please select at least one fee.";
                    return RedirectToAction(nameof(Quotations));
                }

                if (!request.AcademicYearId.HasValue)
                {
                    TempData["ErrorMessage"] = "Please select an academic year.";
                    return RedirectToAction(nameof(Quotations));
                }

                // Allow empty student IDs for general quotations
                var studentIdArray = new List<string>();
                if (!string.IsNullOrEmpty(request.StudentIds))
                {
                    studentIdArray = request.StudentIds
                        .Split(',')
                        .Select(s => s.Trim())
                        .Where(s => !string.IsNullOrEmpty(s))
                        .Distinct()
                        .ToList();
                }

                // If no student IDs provided, create a general quotation
                if (!studentIdArray.Any())
                {
                    studentIdArray.Add("GENERAL"); // Use GENERAL as placeholder for non-student quotations
                }

                // Get existing students (skip for GENERAL)
                var existingStudents = new List<dynamic>();
                var newStudentIds = new List<string>();

                if (studentIdArray.Contains("GENERAL"))
                {
                    newStudentIds.Add("GENERAL");
                }
                else
                {
                    var existingStudentsList = await _context.Students
                        .Where(s => studentIdArray.Contains(s.StudentId_Number))
                        .ToListAsync();

                    existingStudents = existingStudentsList.Cast<dynamic>().ToList();
                    var existingStudentIds = existingStudentsList.Select(s => s.StudentId_Number).ToList();
                    newStudentIds = studentIdArray.Except(existingStudentIds).ToList();
                }

                // Parse fee IDs with source information (format: "source-id")
                var feeSelections = request.SelectedFeeIds
                    .Select(feeString =>
                    {
                        var parts = feeString.Split('-');
                        if (parts.Length == 2 && int.TryParse(parts[1], out int id))
                        {
                            return new { Source = parts[0], Id = id };
                        }
                        return null;
                    })
                    .Where(f => f != null)
                    .ToList();

                // Get fees from both sources
                var otherFeeIds = feeSelections.Where(f => f.Source == "OtherFees").Select(f => f.Id).ToList();
                var configFeeIds = feeSelections.Where(f => f.Source == "FeeConfiguration").Select(f => f.Id).ToList();

                // Create lists to hold fee information
                var allSelectedFees = new List<SelectedFeeDto>();

                // Get fees from OtherFees
                if (otherFeeIds.Any())
                {
                    var otherFees = await _context.OtherFees
                        .Where(f => otherFeeIds.Contains(f.Id))
                        .ToListAsync();

                    foreach (var fee in otherFees)
                    {
                        allSelectedFees.Add(new SelectedFeeDto
                        {
                            Id = fee.Id,
                            FeeName = fee.FeeName,
                            Amount = fee.Amount,
                            AppliesOnlyToForeignStudents = fee.AppliesOnlyToForeignStudents,
                            Source = "OtherFees"
                        });
                    }
                }

                // Get fees from FeeConfiguration
                if (configFeeIds.Any())
                {
                    var configFees = await _context.FeeConfigurations
                        .Include(f => f.FeeType)
                        .Where(f => configFeeIds.Contains(f.Id))
                        .ToListAsync();

                    foreach (var fee in configFees)
                    {
                        allSelectedFees.Add(new SelectedFeeDto
                        {
                            Id = fee.Id,
                            FeeName = fee.FeeType?.Name ?? "Unknown Fee",
                            Amount = fee.Amount,
                            AppliesOnlyToForeignStudents = fee.AppliesOnlyToForeignStudents,
                            Source = "FeeConfiguration"
                        });
                    }
                }

                if (!allSelectedFees.Any())
                {
                    TempData["ErrorMessage"] = "Selected fees not found.";
                    return RedirectToAction(nameof(Quotations));
                }

                // Get current user's name
                var currentUserName = User.Identity?.Name ?? "System";

                // Generate batch reference
                var lastBatchNumber = await _context.Quotations
                    .Where(q => q.BatchReference != null)
                    .OrderByDescending(q => q.Id)
                    .Select(q => q.BatchReference)
                    .FirstOrDefaultAsync();

                int nextBatchNumber = 1;
                if (!string.IsNullOrEmpty(lastBatchNumber))
                {
                    var parts = lastBatchNumber.Split('-');
                    if (parts.Length > 1 && int.TryParse(parts[1], out int currentNumber))
                    {
                        nextBatchNumber = currentNumber + 1;
                    }
                }

                string batchReference = $"QBatch-{nextBatchNumber:D6}";
                var createdQuotations = new List<Quotation>();
                var skippedStudents = new List<string>();

                // Process existing students
                foreach (var student in existingStudents)
                {
                    await CreateQuotationForStudent(
                        student.StudentId_Number,
                        student.FullName,
                        request,
                        allSelectedFees,
                        batchReference,
                        currentUserName,
                        createdQuotations,
                        skippedStudents
                    );
                }

                // Process new students (not yet in system) or general quotations
                foreach (var newStudentId in newStudentIds)
                {
                    string studentName;
                    if (newStudentId == "GENERAL")
                    {
                        studentName = "General Quotation - No Student Assigned";
                    }
                    else
                    {
                        studentName = $"[Pending Admission - {newStudentId}]";
                    }

                    await CreateQuotationForStudent(
                        newStudentId,
                        studentName,
                        request,
                        allSelectedFees,
                        batchReference,
                        currentUserName,
                        createdQuotations,
                        skippedStudents
                    );
                }

                if (createdQuotations.Any())
                {
                    // Get the last created quotation with full details for printing
                    var lastQuotation = createdQuotations.Last();

                    // Reload quotation with all related data
                    var quotationForPrint = await _context.Quotations
                        .Include(q => q.AcademicYear)
                        .Include(q => q.Items)
                        .FirstOrDefaultAsync(q => q.Id == lastQuotation.Id);

                    if (quotationForPrint != null)
                    {
                        // Prepare quotation data for printing
                        var quotationData = new
                        {
                            quotationReference = quotationForPrint.QuotationReference,
                            batchReference = quotationForPrint.BatchReference,
                            studentName = quotationForPrint.StudentName,
                            studentId = quotationForPrint.StudentId,
                            academicYear = quotationForPrint.AcademicYear?.YearValue ?? "N/A",
                            semester = quotationForPrint.Semester,
                            quotationDescription = quotationForPrint.QuotationDescription ?? "Standard Quotation",
                            isNewStudent = quotationForPrint.StudentName?.Contains("[Pending Admission") ?? false,
                            createdDate = quotationForPrint.CreatedDate,
                            validUntil = quotationForPrint.ValidUntil,
                            items = quotationForPrint.Items.Select(item => new
                            {
                                feeTypeName = item.FeeTypeName,
                                description = item.Description,
                                amount = item.Amount
                            }).ToList()
                        };

                        TempData["LastQuotationData"] = Newtonsoft.Json.JsonConvert.SerializeObject(quotationData);
                    }

                    var generalCount = createdQuotations.Count(q => q.StudentId == "GENERAL");
                    var existingCount = createdQuotations.Count(q => q.StudentId != "GENERAL" && !q.StudentName.Contains("[Pending Admission"));
                    var newCount = createdQuotations.Count(q => q.StudentName.Contains("[Pending Admission"));

                    string message = $"Successfully generated {createdQuotations.Count} quotation(s) with batch reference: {batchReference}";
                    if (generalCount > 0)
                    {
                        message += $" ({generalCount} general quotation{(generalCount > 1 ? "s" : "")})";
                    }
                    if (newCount > 0)
                    {
                        message += $" ({newCount} for pending admission)";
                    }

                    TempData["SuccessMessage"] = message;

                    if (skippedStudents.Any())
                    {
                        TempData["WarningMessage"] = $"Skipped {skippedStudents.Count} student(s) with existing quotations: {string.Join(", ", skippedStudents)}";
                    }
                }
                else
                {
                    TempData["ErrorMessage"] = "No quotations were generated. All students may have existing quotations.";
                }

                return RedirectToAction(nameof(Quotations));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error generating quotations: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                TempData["ErrorMessage"] = "An error occurred while generating quotations.";
                return RedirectToAction(nameof(Quotations));
            }
        }

        /*public async Task<IActionResult> GenerateQuotations(GenerateQuotationsRequest request)
        {
            try
            {
                if (request.SelectedFeeIds == null || !request.SelectedFeeIds.Any())
                {
                    TempData["ErrorMessage"] = "Please select at least one fee.";
                    return RedirectToAction(nameof(Quotations));
                }

                if (!request.AcademicYearId.HasValue)
                {
                    TempData["ErrorMessage"] = "Please select an academic year.";
                    return RedirectToAction(nameof(Quotations));
                }

                // Allow empty student IDs for general quotations
                var studentIdArray = new List<string>();
                if (!string.IsNullOrEmpty(request.StudentIds))
                {
                    studentIdArray = request.StudentIds
                        .Split(',')
                        .Select(s => s.Trim())
                        .Where(s => !string.IsNullOrEmpty(s))
                        .Distinct()
                        .ToList();
                }

                // If no student IDs provided, create a general quotation
                if (!studentIdArray.Any())
                {
                    studentIdArray.Add("GENERAL"); // Use GENERAL as placeholder for non-student quotations
                }

                // Get existing students (skip for GENERAL)
                var existingStudents = new List<dynamic>();
                var newStudentIds = new List<string>();

                if (studentIdArray.Contains("GENERAL"))
                {
                    newStudentIds.Add("GENERAL");
                }
                else
                {
                    var existingStudentsList = await _context.Students
                        .Where(s => studentIdArray.Contains(s.StudentId_Number))
                        .ToListAsync();

                    existingStudents = existingStudentsList.Cast<dynamic>().ToList();
                    var existingStudentIds = existingStudentsList.Select(s => s.StudentId_Number).ToList();
                    newStudentIds = studentIdArray.Except(existingStudentIds).ToList();
                }

                // Parse fee IDs with source information (format: "source-id")
                var feeSelections = request.SelectedFeeIds
                    .Select(feeString => 
                    {
                        var parts = feeString.Split('-');
                        if (parts.Length == 2 && int.TryParse(parts[1], out int id))
                        {
                            return new { Source = parts[0], Id = id };
                        }
                        return null;
                    })
                    .Where(f => f != null)
                    .ToList();

                // Get fees from both sources
                var otherFeeIds = feeSelections.Where(f => f.Source == "OtherFees").Select(f => f.Id).ToList();
                var configFeeIds = feeSelections.Where(f => f.Source == "FeeConfiguration").Select(f => f.Id).ToList();

                // Create lists to hold fee information
                var allSelectedFees = new List<SelectedFeeDto>();

                // Get fees from OtherFees
                if (otherFeeIds.Any())
                {
                    var otherFees = await _context.OtherFees
                        .Where(f => otherFeeIds.Contains(f.Id))
                        .ToListAsync();

                    foreach (var fee in otherFees)
                    {
                        allSelectedFees.Add(new SelectedFeeDto
                        {
                            Id = fee.Id,
                            FeeName = fee.FeeName,
                            Amount = fee.Amount,
                            AppliesOnlyToForeignStudents = fee.AppliesOnlyToForeignStudents,
                            Source = "OtherFees"
                        });
                    }
                }

                // Get fees from FeeConfiguration
                if (configFeeIds.Any())
                {
                    var configFees = await _context.FeeConfigurations
                        .Include(f => f.FeeType)
                        .Where(f => configFeeIds.Contains(f.Id))
                        .ToListAsync();

                    foreach (var fee in configFees)
                    {
                        allSelectedFees.Add(new SelectedFeeDto
                        {
                            Id = fee.Id,
                            FeeName = fee.FeeType?.Name ?? "Unknown Fee",
                            Amount = fee.Amount,
                            AppliesOnlyToForeignStudents = fee.AppliesOnlyToForeignStudents,
                            Source = "FeeConfiguration"
                        });
                    }
                }

                if (!allSelectedFees.Any())
                {
                    TempData["ErrorMessage"] = "Selected fees not found.";
                    return RedirectToAction(nameof(Quotations));
                }

                // Get current user's name
                var currentUserName = User.Identity?.Name ?? "System";

                // Generate batch reference
                var lastBatchNumber = await _context.Quotations
                    .Where(q => q.BatchReference != null)
                    .OrderByDescending(q => q.Id)
                    .Select(q => q.BatchReference)
                    .FirstOrDefaultAsync();

                int nextBatchNumber = 1;
                if (!string.IsNullOrEmpty(lastBatchNumber))
                {
                    var parts = lastBatchNumber.Split('-');
                    if (parts.Length > 1 && int.TryParse(parts[1], out int currentNumber))
                    {
                        nextBatchNumber = currentNumber + 1;
                    }
                }

                string batchReference = $"QBatch-{nextBatchNumber:D6}";
                var createdQuotations = new List<Quotation>();
                var skippedStudents = new List<string>();

                // Process existing students
                foreach (var student in existingStudents)
                {
                    await CreateQuotationForStudent(
                        student.StudentId_Number, 
                        student.FullName,
                        request, 
                        allSelectedFees, 
                        batchReference, 
                        currentUserName, 
                        createdQuotations, 
                        skippedStudents
                    );
                }

                // Process new students (not yet in system) or general quotations
                foreach (var newStudentId in newStudentIds)
                {
                    string studentName;
                    if (newStudentId == "GENERAL")
                    {
                        studentName = "General Quotation - No Student Assigned";
                    }
                    else
                    {
                        studentName = $"[Pending Admission - {newStudentId}]";
                    }

                    await CreateQuotationForStudent(
                        newStudentId, 
                        studentName,
                        request, 
                        allSelectedFees, 
                        batchReference, 
                        currentUserName, 
                        createdQuotations, 
                        skippedStudents
                    );
                }

                if (createdQuotations.Any())
                {
                    var generalCount = createdQuotations.Count(q => q.StudentId == "GENERAL");
                    var existingCount = createdQuotations.Count(q => q.StudentId != "GENERAL" && !q.StudentName.Contains("[Pending Admission"));
                    var newCount = createdQuotations.Count(q => q.StudentName.Contains("[Pending Admission"));
                    
                    string message = $"Successfully generated {createdQuotations.Count} quotation(s) with batch reference: {batchReference}";
                    if (generalCount > 0)
                    {
                        message += $" ({generalCount} general quotation{(generalCount > 1 ? "s" : "")})";
                    }
                    if (newCount > 0)
                    {
                        message += $" ({newCount} for pending admission)";
                    }
                    
                    TempData["SuccessMessage"] = message;
                    
                    if (skippedStudents.Any())
                    {
                        TempData["WarningMessage"] = $"Skipped {skippedStudents.Count} student(s) with existing quotations: {string.Join(", ", skippedStudents)}";
                    }
                }
                else
                {
                    TempData["ErrorMessage"] = "No quotations were generated. All students may have existing quotations.";
                }

                return RedirectToAction(nameof(Quotations));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error generating quotations: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                TempData["ErrorMessage"] = "An error occurred while generating quotations.";
                return RedirectToAction(nameof(Quotations));
            }
        }*/

        private async Task CreateQuotationForStudent(
            string studentId,
            string studentName,
            GenerateQuotationsRequest request,
            List<SelectedFeeDto> allSelectedFees,
            string batchReference,
            string currentUserName,
            List<Quotation> createdQuotations,
            List<string> skippedStudents)
        {
            // Check for duplicates if not allowed
            if (!request.AllowDuplicates)
            {
                var existingQuotation = await _context.Quotations
                    .AnyAsync(q => q.StudentId == studentId &&
                                  q.AcademicYearId == request.AcademicYearId.Value &&
                                  q.Semester == request.Semester &&
                                  !q.IsDeleted);

                if (existingQuotation)
                {
                    skippedStudents.Add(studentId);
                    return;
                }
            }

            // Generate quotation reference
            var lastQuotationNumber = await _context.Quotations
                .OrderByDescending(q => q.Id)
                .Select(q => q.QuotationReference)
                .FirstOrDefaultAsync();

            int nextQuotationNumber = 1;
            if (!string.IsNullOrEmpty(lastQuotationNumber))
            {
                var parts = lastQuotationNumber.Split('-');
                if (parts.Length > 1 && int.TryParse(parts[1], out int currentNumber))
                {
                    nextQuotationNumber = currentNumber + 1 + createdQuotations.Count;
                }
            }
            else
            {
                nextQuotationNumber += createdQuotations.Count;
            }

            string quotationReference = $"QUOT-{nextQuotationNumber:D6}";

            // Calculate total amount - include ALL selected fees (no filtering by student type)
            // User is responsible for selecting appropriate fees
            decimal totalAmount = 0;
            foreach (var fee in allSelectedFees)
            {
                totalAmount += fee.Amount;
            }

            // Create quotation with user information
            var quotation = new Quotation
            {
                QuotationReference = quotationReference,
                BatchReference = batchReference,
                StudentId = studentId,
                StudentName = studentName,
                AcademicYearId = request.AcademicYearId.Value,
                Semester = request.Semester,
                TotalAmount = totalAmount,
                Status = QuotationStatus.Pending,
                QuotationDescription = request.QuotationDescription ?? "Standard Quotation",
                ValidUntil = DateTime.Now.AddDays(30),
                CreatedDate = DateTime.Now,
                CreatedBy = currentUserName,
                UpdatedBy = currentUserName,
                UpdatedDate = DateTime.Now,
                IsDeleted = false
            };

            _context.Quotations.Add(quotation);
            await _context.SaveChangesAsync();

            // Create quotation items for ALL selected fees
            foreach (var fee in allSelectedFees)
            {
                var feeTmp = await _context.FeeConfigurations.AsNoTracking().Include(f => f.FeeType).FirstOrDefaultAsync(f => f.Id == fee.Id);

                /*if(feeTmp == null)
                {
                    feeTmp = await _context.OtherFees.AsNoTracking().Include(f => f.FeeType).FirstOrDefaultAsync(f => f.Id == fee.Id);
                }*/
                var quotationItem = new QuotationItem
                {
                    QuotationId = quotation.Id,
                    FeeTypeId = feeTmp.FeeType.Id,
                    FeeTypeName = feeTmp.FeeType.Name,
                    Description = $"{fee.FeeName} ({fee.Source})",
                    Amount = fee.Amount,
                    CreatedDate = DateTime.Now
                };

                _context.QuotationItems.Add(quotationItem);
            }

            await _context.SaveChangesAsync();
            createdQuotations.Add(quotation);
        }

        // POST: Admin/GetQuotations
        [HttpPost]
        public async Task<IActionResult> GetQuotations([FromBody] GetQuotationsRequest request)
        {
            try
            {
                var query = _context.Quotations
                    .Include(q => q.AcademicYear)
                    .AsQueryable();

                // Apply filters
                if(request != null)
                {
                    if (request.AcademicYearId.HasValue)
                    {
                        query = query.Where(q => q.AcademicYearId == request.AcademicYearId.Value);
                    }

                    if (request.Semester.HasValue)
                    {
                        query = query.Where(q => q.Semester == request.Semester.Value);
                    }

                    if (!string.IsNullOrEmpty(request.Status))
                    {
                        if (Enum.TryParse<QuotationStatus>(request.Status, out var status))
                        {
                            query = query.Where(q => q.Status == status);
                        }
                    }

                    if (!string.IsNullOrEmpty(request.SearchTerm))
                    {
                        query = query.Where(q => q.StudentId.Contains(request.SearchTerm) ||
                                                q.QuotationReference.Contains(request.SearchTerm) ||
                                                q.StudentName.Contains(request.SearchTerm));
                    }

                    if (!request.IncludeDeleted)
                    {
                        query = query.Where(q => !q.IsDeleted);
                    }
                }
                else
                {
                    request = new();
                    request.Page = 1;
                    request.PageSize = 20;
                }

                var totalRecords = await query.CountAsync();
                List<Quotation> quotationData = [];

                if(request != null)
                {
                    quotationData = await query
                    .OrderByDescending(q => q.CreatedDate)
                    .Skip((request.Page - 1) * request.PageSize)
                    .Take(request.PageSize)
                    .ToListAsync();
                }
                else
                {
                    quotationData = await query
                    .OrderByDescending(q => q.CreatedDate)
                    .ToListAsync();
                }

                var quotations = quotationData.Select((q, index) => new
                {
                    id = q.Id,
                    rowNumber = (request.Page - 1) * request.PageSize + index + 1,
                    quotationReference = q.QuotationReference,
                    batchReference = q.BatchReference,
                    studentId = q.StudentId,
                    studentName = q.StudentName ?? $"{q.StudentId} (Not Found)",
                    isNewStudent = q.StudentName?.Contains("[Pending Admission") ?? false,
                    academicYear = q.AcademicYear?.YearValue ?? "N/A",
                    semester = q.Semester.HasValue ? q.Semester.Value.ToString() : null,
                    totalAmount = q.TotalAmount,
                    status = q.Status.ToString(),
                    validUntil = q.ValidUntil,
                    createdDate = q.CreatedDate,
                    createdBy = q.CreatedBy ?? "System",
                    isDeleted = q.IsDeleted
                }).ToList();

                return Json(new
                {
                    quotations = quotations,
                    totalRecords = totalRecords,
                    totalPages = (int)Math.Ceiling((double)totalRecords / request.PageSize),
                    currentPage = request.Page
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading quotations: {ex.Message}");
                return Json(new { error = true, message = "An error occurred while loading quotations." });
            }
        }

        // GET: Admin/GetQuotationDetails
        [HttpGet]
        public async Task<IActionResult> GetQuotationDetails(int id)
        {
            try
            {
                var quotation = await _context.Quotations
                    .Include(q => q.AcademicYear)
                    .Include(q => q.Items)
                    .FirstOrDefaultAsync(q => q.Id == id);

                if (quotation == null)
                {
                    return NotFound();
                }

                // Get student details if available
                var student = await _context.Students
                    .Where(s => s.StudentId_Number == quotation.StudentId)
                    .Select(s => new 
                    { 
                        s.FullName,
                        s.Email,
                        s.Phone
                    })
                    .FirstOrDefaultAsync();

                var studentName = quotation.StudentName ?? student?.FullName ?? $"{quotation.StudentId} (Student Not Found)";
                var isNewStudent = quotation.StudentName?.Contains("[Pending Admission") ?? false;

                var result = new
                {
                    id = quotation.Id,
                    quotationReference = quotation.QuotationReference,
                    batchReference = quotation.BatchReference,
                    studentId = quotation.StudentId,
                    studentName = studentName,
                    studentEmail = student?.Email ?? (isNewStudent ? "Pending Admission" : "N/A"),
                    studentPhone = student?.Phone ?? (isNewStudent ? "Pending Admission" : "N/A"),
                    isNewStudent = isNewStudent,
                    academicYear = quotation.AcademicYear?.YearValue ?? "N/A",
                    semester = quotation.Semester?.ToString() ?? "N/A",
                    totalAmount = quotation.TotalAmount,
                    status = quotation.Status.ToString(),
                    quotationDescription = quotation.QuotationDescription ?? "Standard Quotation",
                    validUntil = quotation.ValidUntil,
                    createdDate = quotation.CreatedDate,
                    createdBy = quotation.CreatedBy ?? "System",
                    updatedDate = quotation.UpdatedDate,
                    updatedBy = quotation.UpdatedBy,
                    items = quotation.Items.Select(i => new
                    {
                        feeTypeName = i.FeeTypeName,
                        description = i.Description,
                        amount = i.Amount
                    }).ToList()
                };

                return Json(result);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading quotation details: {ex.Message}");
                return Json(new { error = true, message = "An error occurred while loading quotation details." });
            }
        }

        // POST: Admin/CancelQuotation
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CancelQuotation(int id)
        {
            try
            {
                var quotation = await _context.Quotations.FindAsync(id);

                if (quotation == null)
                {
                    TempData["ErrorMessage"] = "Quotation not found.";
                    return RedirectToAction(nameof(Quotations));
                }

                quotation.IsDeleted = true;
                quotation.UpdatedDate = DateTime.Now;
                quotation.UpdatedBy = User.Identity?.Name ?? "System";

                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "Quotation deleted successfully.";
                return RedirectToAction(nameof(Quotations));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error deleting quotation: {ex.Message}");
                TempData["ErrorMessage"] = "An error occurred while deleting the quotation.";
                return RedirectToAction(nameof(Quotations));
            }
        }

        // POST: Admin/RestoreQuotation
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RestoreQuotation(int id)
        {
            try
            {
                var quotation = await _context.Quotations.FindAsync(id);

                if (quotation == null)
                {
                    TempData["ErrorMessage"] = "Quotation not found.";
                    return RedirectToAction(nameof(Quotations));
                }

                quotation.IsDeleted = false;
                quotation.UpdatedDate = DateTime.Now;
                quotation.UpdatedBy = User.Identity?.Name ?? "System";

                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "Quotation restored successfully.";
                return RedirectToAction(nameof(Quotations));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error restoring quotation: {ex.Message}");
                TempData["ErrorMessage"] = "An error occurred while restoring the quotation.";
                return RedirectToAction(nameof(Quotations));
            }
        }

        // Helper methods for filters
        [HttpGet]
        public async Task<IActionResult> GetSchools()
        {
            var schools = await _context.Schools
                .OrderBy(s => s.Name)
                .Select(s => new { s.Id, s.Name })
                .ToListAsync();
            return Json(schools);
        }

        [HttpGet]
        public async Task<IActionResult> GetProgrammes()
        {
            var programmes = await _context.Programmes
                .OrderBy(p => p.Name)
                .Select(p => new { p.Id, p.Name })
                .ToListAsync();
            return Json(programmes);
        }

        [HttpGet]
        public async Task<IActionResult> GetModesOfStudy()
        {
            var modes = await _context.ModesOfStudy
                .OrderBy(m => m.ModeName)
                .Select(m => new { m.ModeId, m.ModeName })
                .ToListAsync();
            return Json(modes);
        }

        [HttpGet]
        public async Task<IActionResult> GetProgramLevels()
        {
            var levels = await _context.ProgramLevels
                .Where(p => p.IsActive)
                .OrderBy(p => p.Rank)
                .Select(p => new { p.Id, p.Name })
                .ToListAsync();
            return Json(levels);
        }
    }

    // Request Models for Quotations
    public class QuotationValidateStudentsRequest
    {
        public string StudentIds { get; set; }
    }

    public class GenerateQuotationsRequest
    {
        public List<string> SelectedFeeIds { get; set; }
        public int? AcademicYearId { get; set; }
        public int? Semester { get; set; }
        public string StudentIds { get; set; }
        public string QuotationDescription { get; set; }
        public bool AllowDuplicates { get; set; }
    }

    public class GetQuotationsRequest
    {
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 10;
        public int? AcademicYearId { get; set; }
        public int? Semester { get; set; }
        public string Status { get; set; }
        public string SearchTerm { get; set; }
        public bool IncludeDeleted { get; set; }
    }

    // DTO for selected fees
    public class SelectedFeeDto
    {
        public int Id { get; set; }
        public string FeeName { get; set; }
        public decimal Amount { get; set; }
        public bool AppliesOnlyToForeignStudents { get; set; }
        public bool AppliesOnlyToLocalStudents { get; set; }
        public string Source { get; set; }
    }
}