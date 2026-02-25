using Microsoft.EntityFrameworkCore;
using SIS.Data;
using System.IO;
using SIS.DTOs.StudentApplication;
using SIS.Models.StudentApplication;
using SIS.Models;
using Microsoft.AspNetCore.Mvc;
using SIS.Models.Registration;
using System;
using SIS.Enums;

namespace SIS.Services.StudentApplication
{
    public class ApplicantService : IApplicantService
    {
        private readonly ApplicationDbContext _context;

        public ApplicantService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<List<DropdownOptionDto>> GetSubjectsAsync()
        {
            return await _context.Subjects
                .Select(s => new DropdownOptionDto { Id = s.SubjectId, Name = s.SubjectName + "/" + s.SubjectCode })
                .ToListAsync();
        }

        public async Task<List<DropdownOptionDto>> GetGradesAsync()
        {
            return await _context.Grades
                .Select(g => new DropdownOptionDto { Id = g.GradeId, Name = g.GradeValue })
                .ToListAsync();
        }

        public async Task<List<DropdownOptionDto>> GetSchoolsAsync()
        {
            return await _context.Schools
                .Select(s => new DropdownOptionDto { Id = s.Id, Name = s.Name })
                .ToListAsync();
        }

        public async Task<List<DropdownOptionDto>> GetProgrammesAsync(int schoolId)
        {
            return await _context.Programmes
                .Where(p => p.Department.SchoolId == schoolId)
                .Select(p => new DropdownOptionDto { Id = p.Id, Name = p.Name })
                .ToListAsync();
        }

        public async Task<List<DropdownOptionDto>> GetModesOfStudyAsync()
        {
            return await _context.ModesOfStudy
                .Select(m => new DropdownOptionDto { Id = m.ModeId, Name = m.ModeName })
                .ToListAsync();
        }

        public async Task<List<DropdownOptionDto>> GetAcademicYearsAsync()
        {
            return await _context.AcademicYears
                .Where(a => a.IsActive != false)
                .Select(a => new DropdownOptionDto
                {
                    Id = a.YearId,
                    Name = $"{a.YearValue}/{a.SemesterId} ({a.StartDate.ToString("dd/MM/yyyy")})"
                })
                .ToListAsync();
        }

        public async Task<Applicant> GetApplicantByNrcOrPassportAsync(string nrcOrPassport)
        {
            if (!string.IsNullOrEmpty(nrcOrPassport))
            {
                return await _context.Applicants.FirstOrDefaultAsync(a => a.NrcOrPassport == nrcOrPassport);
            }
            return null;
        }

        public string GenerateReferenceNumber()
        {
            var datePart = DateTime.Now.ToString("yyyyMMdd");
            var randomPart = new Random().Next(100000, 999999).ToString();

            return $"APP{datePart}{randomPart}";
        }

        // Generate the student ID
        public async Task<string> GenerateStudentIdAsync(int academicYearId)
        {
            string studentId;
            bool isUnique = false;
            int maxAttempts = 100;
            int attempts = 0;

            // Get the academic year to extract the year value
            var academicYear = await _context.AcademicYears
                .FirstOrDefaultAsync(a => a.YearId == academicYearId);

            if (academicYear == null)
            {
                throw new InvalidOperationException($"Academic year with ID {academicYearId} not found.");
            }

            // Extract the first 4 characters from YearValue (e.g., "2025" from "2025/2026")
            string yearPrefix;
            if (!string.IsNullOrEmpty(academicYear.YearValue) && academicYear.YearValue.Length >= 4)
            {
                yearPrefix = academicYear.YearValue.Substring(0, 4);
            }
            else
            {
                // Fallback to current year if YearValue is invalid
                yearPrefix = DateTime.Now.Year.ToString();
                Console.WriteLine($"⚠️ Invalid YearValue '{academicYear.YearValue}', using current year {yearPrefix}");
            }

            do
            {
                attempts++;

                var idSequence = await _context.StudentIdSequences
                    .FirstOrDefaultAsync(s => s.AcademicYearId == academicYearId);

                if (idSequence == null)
                {
                    idSequence = new StudentIdSequence
                    {
                        AcademicYearId = academicYearId,
                        LastGeneratedId = 100001
                    };
                    _context.StudentIdSequences.Add(idSequence);
                    await _context.SaveChangesAsync();
                }
                else
                {
                    idSequence.LastGeneratedId++;
                    _context.StudentIdSequences.Update(idSequence);
                    await _context.SaveChangesAsync();
                }

                // Format: [YearPrefix][SequenceNumber]
                // e.g., 2025100001, 2025100002, etc.
                studentId = $"{yearPrefix}{idSequence.LastGeneratedId}";

                isUnique = !await _context.Students
                    .AnyAsync(s => s.StudentId_Number == studentId);

                if (!isUnique)
                {
                    Console.WriteLine($"⚠️ Student ID {studentId} already exists. Moving to next number...");
                }

            } while (!isUnique && attempts < maxAttempts);

            if (!isUnique)
            {
                throw new InvalidOperationException(
                    $"Failed to generate unique Student ID after checking {maxAttempts} sequential numbers. " +
                    $"Last attempted ID: {studentId}. Please contact system administrator.");
            }

            Console.WriteLine($"✅ Generated unique Student ID: {studentId} (Academic Year: {academicYear.YearValue})");
            return studentId;
        }

        public Task<List<Applicant>> GetApplicationsByUserIdAsync(string userId)
        {
            var applications = _context.Applicants
                .Where(a => a.CreatedBy.Equals(userId)).ToListAsync();

            if (applications != null)
            {
                return applications;
            }
            else
            {
                return null;
            }
        }

        public async Task<List<DropdownOptionDto>> GetProgrammeLevels()
        {
            return await _context.ProgramLevels
                .Where(pl => pl.IsActive != false)
                .Select(pl => new DropdownOptionDto { Id = pl.Id, Name = pl.Name })
                .ToListAsync();
        }

        public async Task<List<DropdownOptionDto>> GetProgrammesAsync(int schoolId, int programmeLevelId)
        {
            return await _context.Programmes
                .Where(p => p.Department.SchoolId == schoolId && p.ProgrammeLevelId == programmeLevelId)
                .Select(p => new DropdownOptionDto { Id = p.Id, Name = p.Name })
                .ToListAsync();
        }
    }
}