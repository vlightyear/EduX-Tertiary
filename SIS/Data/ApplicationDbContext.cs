using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using SIS.Enums;
using SIS.Models;
using SIS.Models.Accounts;
using SIS.Models.Admin;
using SIS.Models.Appeals;
using SIS.Models.Applications;
using SIS.Models.Assessments;
using SIS.Models.Compliance;
using SIS.Models.Courses;
using SIS.Models.Fees;
using SIS.Models.Identity;
using SIS.Models.Lecturer;
using SIS.Models.Payments;
using SIS.Models.Registration;
using SIS.Models.Reports;
using SIS.Models.Results;
using SIS.Models.StudentAccommodation;
using SIS.Models.StudentApplication;
using SIS.Models.StudentResults;
using SIS.Models.StudyPermits;
using SIS.Models.TimeTabling;
using SIS.Models.Zoom;
using SIS.Services.Reports;
using System.Reflection.Emit;

namespace SIS.Data
{
    public class ApplicationUser : IdentityUser
    {
        public required string FullName { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public bool HasChangedInitialPassword { get; set; } = false;
    }

    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
        {
        }

        // DbSets for Admin models
        public DbSet<School> Schools { get; set; }
        public DbSet<Department> Departments { get; set; }
        public DbSet<Programme> Programmes { get; set; }
        public DbSet<Course> Courses { get; set; }
        public DbSet<ProgrammeCourse> ProgrammeCourses { get; set; }
        public DbSet<ApplicationPeriod> ApplicationPeriods { get; set; }

        // DBSets for Registration models
        public DbSet<Subject> Subjects { get; set; }
        public DbSet<Grade> Grades { get; set; }
        public DbSet<AcademicYear> AcademicYears { get; set; }
        public DbSet<ModeOfStudy> ModesOfStudy { get; set; }
        public DbSet<Reg_Requirements> Reg_Requirements { get; set; }
        public DbSet<CourseGrades> CourseGrades { get; set; }
        public DbSet<StudentCourse> StudentCourses { get; set; }
        public DbSet<AcademicRequest> AcademicRequests { get; set; }
        public DbSet<AcademicRequestDocument> AcademicRequestDocuments { get; set; }

        // DBSets for Fees models
        public DbSet<OtherFees> OtherFees { get; set; }
        public DbSet<RegistrationFees> RegistrationFees { get; set; }
        public DbSet<ApplicationFees> ApplicationFees { get; set; }
        public DbSet<CourseFees> CourseFees { get; set; }
         public DbSet<Quotation> Quotations { get; set; }
          public DbSet<QuotationItem> QuotationItems { get; set; }


        // DBset for Payment models
        public DbSet<MomoPayment> MomoPayments { get; set; }
        public DbSet<OnlinePayments> OnlinePayments { get; set; }

        public DbSet<PaymentsDetails> PaymentsDetails { get; set; }
        public DbSet<AccountType> AccountTypes { get; set; }
        public DbSet<CreditTransactions> CreditTransactions { get; set; }
        public DbSet<DebitTransactions> DebitTransactions { get; set; }
        public DbSet<FinancialStatement> FinancialStatements { get; set; }

        // DBset for Student Application and Admission models
        public DbSet<Student> Students { get; set; }
        public DbSet<StudentIdSequence> StudentIdSequences { get; set; }
        public DbSet<Applicant> Applicants { get; set; }
        public DbSet<ApplicantSubject> ApplicantSubjects { get; set; }
        public DbSet<StudentGceSubjects> StudentGceSubjects { get; set; }

        public DbSet<StudNextOfKin> studNextOfKins { get; set; }
        public DbSet<StudentAddress> StudentAddresses { get; set; }
        public DbSet<StudFormerSchool> StudFormerSchools { get; set; }


        // DbSets for facilities like buildings and learning rooms
        public DbSet<Building> Buildings { get; set; }
        public DbSet<LearningRoom> LearningRooms { get; set; }
        public DbSet<CoursePrerequisite> CoursePrerequisites { get; set; }

        public DbSet<CourseLecturer> CourseLecturer { get; set; }

        public DbSet<CourseAssessment> CourseAssessment { get; set; }

        public DbSet<FeeType> FeeTypes { get; set; }
        public DbSet<FeeConfiguration> FeeConfigurations { get; set; }

        public DbSet<ProgramLevel> ProgramLevels { get; set; }
        public DbSet<ProgressionRule> ProgressionRules { get; set; }
        public DbSet<Assessment> Assessments { get; set; }

        public DbSet<StudentCourseRegistration> StudentCourseRegistrations { get; set; }
        public DbSet<StudentExaminableCourse> StudentExaminableCourses { get; set; }
        public DbSet<GradeConfiguration> GradeConfigurations { get; set; }
        public DbSet<StudentAcademicPerformanceArchive> StudentAcademicPerformanceArchives { get; set; }
        public DbSet<ApplicationPayment> ApplicationPayments { get; set; }


        public DbSet<TimeSlotConfiguration> TimeSlotConfigurations { get; set; }
        public DbSet<WorkingDayConfiguration> WorkingDayConfigurations { get; set; }

        public DbSet<Timetable> Timetables { get; set; }
        public DbSet<ScheduleTracking> ScheduleTrackings { get; set; }
        public DbSet<CourseContent> CourseContents { get; set; }

        // Assessments 
        public DbSet<QuestionGroup> QuestionGroups { get; set; }
        public DbSet<Question> Questions { get; set; }
        public DbSet<QuestionOption> QuestionOptions { get; set; }
        public DbSet<AssessmentConfiguration> AssessmentConfigurations { get; set; }
        public DbSet<AssessmentQuestionGroup> AssessmentQuestionGroups { get; set; }
        public DbSet<StudentAttempt> StudentAttempts { get; set; }
        public DbSet<StudentResponse> StudentResponses { get; set; }
        public DbSet<StudentDisqualification> StudentDisqualifications { get; set; }


        // Student Accomodation 
        public DbSet<Campus> Campuses { get; set; }
        public DbSet<Hostel> Hostels { get; set; }
        public DbSet<Room> Rooms { get; set; }
        public DbSet<BedSpace> BedSpaces { get; set; }
        public DbSet<ResourceType> ResourceTypes { get; set; }
        public DbSet<RoomResource> RoomResources { get; set; }
        public DbSet<AccomodationConfiguration> AccomodationConfigurations { get; set; }


        // Accommodation models
        public DbSet<AccommodationPeriod> AccommodationPeriods { get; set; }
        public DbSet<AccommodationApplication> AccommodationApplications { get; set; }
        public DbSet<Allocation> Allocations { get; set; }
        public DbSet<CheckInOut> CheckInOuts { get; set; }
        public DbSet<MaintenanceRequest> MaintenanceRequests { get; set; }
        public DbSet<AccomodationConfiguration> accomodationConfigurations { get; set; }
        public DbSet<Notification> Notifications { get; set; }


        public DbSet<ZoomMeeting> ZoomMeetings { get; set; }
        public DbSet<MeetingAttendance> MeetingsAttendance { get; set; }


        public DbSet<Chapter> Chapters { get; set; }

        public DbSet<ChapterProgress> ChapterProgress { get; set; }


        public DbSet<AcademicEventType> AcademicEventTypes { get; set; }
        public DbSet<AcademicCalendarEvent> AcademicCalendarEvents { get; set; }
        //public DbSet<EmailSettings> EmailSettings { get; set; }


        public DbSet<StudentInvoice> StudentInvoices { get; set; }
        public DbSet<StudentInvoiceItem> StudentInvoiceItems { get; set; }


        public DbSet<StudentCarryoverCourse> StudentCarryoverCourses { get; set; }

        public DbSet<ChapterRating> ChapterRatings { get; set; }

        // DbSet for Senate Report Caches
        public DbSet<SenateReportCache> SenateReportCaches { get; set; }


        public DbSet<StudentAssessmentScore> StudentAssessmentScores { get; set; }
        public DbSet<StudentCourseResult> StudentCourseResults { get; set; }
        public DbSet<ResultAuditLog> ResultAuditLogs { get; set; }

        //Dockets
        public DbSet<StudentData> StudentDockets { get; set; } = null!;

        //Reports
        public DbSet<Models.ViewModels.VwBiSchoolBillingMonthly> VwBiSchoolBillingMonthly { get; set; } = null!;

        //Study Permits
        public DbSet<StudyPermit> StudyPermits { get; set; }
        public DbSet<StudyPermitConfig> StudyPermitConfigs { get; set; }
        public DbSet<StudyPermitNotificationLog> StudyPermitNotificationLogs { get; set; }

        // Workflow entities
        public DbSet<WorkflowTemplate> WorkflowTemplates { get; set; }
        public DbSet<WorkflowStage> WorkflowStages { get; set; }
        public DbSet<WorkflowInstance> WorkflowInstances { get; set; }
        public DbSet<WorkflowApproval> WorkflowApprovals { get; set; }
        public DbSet<WorkflowNotification> WorkflowNotifications { get; set; }
        public DbSet<ResultSubmissionBatch> ResultSubmissionBatches { get; set; }
        public DbSet<StudentResultView> StudentResultViews => Set<StudentResultView>();

        // Result Appeals
        public DbSet<ResultAppeal> ResultAppeals { get; set; }
        public DbSet<AppealTypeConfig> AppealTypeConfigs { get; set; }
        public DbSet<AppealStatusHistory> AppealStatusHistories { get; set; }

        //Payment Allocations
        public DbSet<PaymentAllocation> PaymentAllocations { get; set; }

        //Permissions
        public DbSet<Permission> Permissions { get; set; }
        public DbSet<RolePermission> RolePermissions { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.Entity<StudentInvoiceItem>()
                .ToTable(tb => tb.UseSqlOutputClause(false));

            // ===================================================================
            // PERFORMANCE INDEXES FOR SENATE REPORT QUERIES
            // ===================================================================

            // Configure Permission
            builder.Entity<Permission>(entity =>
            {
                entity.HasKey(p => p.Id);
                entity.HasIndex(p => p.Name).IsUnique();
                entity.Property(p => p.Name).IsRequired();
            });

            // Configure RolePermission
            builder.Entity<RolePermission>(entity =>
            {
                entity.HasKey(rp => rp.Id);

                entity.HasOne(rp => rp.Role)
                    .WithMany()
                    .HasForeignKey(rp => rp.RoleId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(rp => rp.Permission)
                    .WithMany(p => p.RolePermissions)
                    .HasForeignKey(rp => rp.PermissionId)
                    .OnDelete(DeleteBehavior.Cascade);

                // Unique constraint: one permission per role
                entity.HasIndex(rp => new { rp.RoleId, rp.PermissionId }).IsUnique();
            });

            // School indexes for role-based access
            builder.Entity<School>(entity =>
            {
                entity.HasIndex(s => s.DeanId)
                    .HasDatabaseName("IX_Schools_DeanId");

                entity.HasIndex(s => s.AssistantDeanId)
                    .HasDatabaseName("IX_Schools_AssistantDeanId");

                // Composite index for Dean access checks
                entity.HasIndex(s => new { s.DeanId, s.AssistantDeanId })
                    .HasDatabaseName("IX_Schools_Dean_AssistantDean");
            });

            // Department indexes for role-based access and hierarchy queries
            builder.Entity<Department>(entity =>
            {
                entity.HasIndex(d => d.HODId)
                    .HasDatabaseName("IX_Departments_HODId");

                entity.HasIndex(d => d.SchoolId)
                    .HasDatabaseName("IX_Departments_SchoolId");

                // Composite index for HOD access checks
                entity.HasIndex(d => new { d.SchoolId, d.HODId })
                    .HasDatabaseName("IX_Departments_SchoolId_HODId");
            });

            // Programme indexes for hierarchy and filtering
            builder.Entity<Programme>(entity =>
            {
                // Note: IX_Programmes_DepartmentId is auto-created by EF Core for the FK relationship

                entity.HasIndex(p => p.ModeOfStudyId)
                    .HasDatabaseName("IX_Programmes_ModeOfStudyId");

                entity.HasIndex(p => p.ProgrammeLevelId)
                    .HasDatabaseName("IX_Programmes_ProgrammeLevelId");

                // Composite index for common filtering
                entity.HasIndex(p => new { p.DepartmentId, p.ModeOfStudyId })
                    .HasDatabaseName("IX_Programmes_Department_ModeOfStudy");

                entity.HasIndex(p => new { p.DepartmentId, p.ProgrammeLevelId })
                    .HasDatabaseName("IX_Programmes_Department_Level");
            });

            // StudentExaminableCourse indexes - CRITICAL for performance
            builder.Entity<StudentExaminableCourse>(entity =>
            {
                // Primary composite index for report queries
                entity.HasIndex(sec => new { sec.CourseId, sec.AcademicYearId, sec.Semester })
                    .HasDatabaseName("IX_StudentExaminableCourses_Course_Year_Semester");

                // Index for student-based queries
                entity.HasIndex(sec => new { sec.StudentId, sec.AcademicYearId, sec.Semester })
                    .HasDatabaseName("IX_StudentExaminableCourses_Student_Year_Semester");

                // Full composite for unique lookups
                entity.HasIndex(sec => new { sec.StudentId, sec.CourseId, sec.AcademicYearId, sec.Semester })
                    .HasDatabaseName("IX_StudentExaminableCourses_Full_Composite");

                // Index for filtering by student
                entity.HasIndex(sec => sec.StudentId)
                    .HasDatabaseName("IX_StudentExaminableCourses_StudentId");

                // Index for filtering by course
                entity.HasIndex(sec => sec.CourseId)
                    .HasDatabaseName("IX_StudentExaminableCourses_CourseId");

                // Index for status filtering
                entity.HasIndex(sec => sec.Status)
                    .HasDatabaseName("IX_StudentExaminableCourses_Status");

                // Index for academic year filtering
                entity.HasIndex(sec => sec.AcademicYearId)
                    .HasDatabaseName("IX_StudentExaminableCourses_AcademicYearId");
            });

            // StudentAssessmentScore indexes - CRITICAL for grading calculations
            builder.Entity<StudentAssessmentScore>(entity =>
            {
                entity.ToTable("StudentAssessmentScores");

                // Primary composite index for score lookups
                entity.HasIndex(e => new { e.CourseId, e.AcademicYearId, e.Semester, e.StudentId })
                    .HasDatabaseName("IX_StudentAssessmentScores_Course_Year_Semester_Student");

                // Index for batch-based queries
                entity.HasIndex(e => e.rsbId)
                    .HasDatabaseName("IX_StudentAssessmentScores_RsbId");

                // Index for active scores
                entity.HasIndex(e => new { e.IsActive, e.CourseId, e.AcademicYearId })
                    .HasDatabaseName("IX_StudentAssessmentScores_Active_Course_Year");

                // Existing indexes
                entity.HasIndex(e => new { e.StudentId, e.CourseId, e.AcademicYearId })
                    .HasDatabaseName("IX_StudentAssessmentScores_Student_Course_Year");

                entity.HasIndex(e => e.ScoreHash)
                    .HasDatabaseName("IX_StudentAssessmentScores_Hash");

                entity.HasIndex(e => e.IsActive)
                    .HasDatabaseName("IX_StudentAssessmentScores_IsActive");

                entity.HasIndex(e => new { e.CourseId, e.AssessmentId })
                    .HasDatabaseName("IX_StudentAssessmentScores_Course_Assessment");

                // Unique constraint to prevent duplicate scores
                entity.HasIndex(e => new { e.StudentId, e.CourseId, e.AssessmentId, e.AcademicYearId, e.Semester })
                    .IsUnique()
                    .HasFilter("[IsActive] = 1")
                    .HasDatabaseName("UX_StudentAssessmentScores_Unique");

                // Decimal precision
                entity.Property(e => e.Score).HasPrecision(5, 2);
                entity.Property(e => e.MaxScore).HasPrecision(5, 2);
                entity.Property(e => e.WeightPercentage).HasPrecision(5, 2);

                // Default values
                entity.Property(e => e.RecordedAt).HasDefaultValueSql("GETUTCDATE()");
                entity.Property(e => e.IsActive).HasDefaultValue(true);
                entity.Property(e => e.MaxScore).HasDefaultValue(100);

                // Relationships with cascade behavior
                entity.HasOne(e => e.Student)
                    .WithMany()
                    .HasForeignKey(e => e.StudentId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.Course)
                    .WithMany()
                    .HasForeignKey(e => e.CourseId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.AcademicYear)
                    .WithMany()
                    .HasForeignKey(e => e.AcademicYearId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.Assessment)
                    .WithMany()
                    .HasForeignKey(e => e.AssessmentId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.RecordedByUser)
                    .WithMany()
                    .HasForeignKey(e => e.RecordedBy)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.ModifiedByUser)
                    .WithMany()
                    .HasForeignKey(e => e.ModifiedBy)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // ResultSubmissionBatch indexes - for workflow and publishing
            builder.Entity<ResultSubmissionBatch>(entity =>
            {
                entity.HasIndex(rsb => new { rsb.CourseId, rsb.AcademicYearId, rsb.Semester })
                    .HasDatabaseName("IX_ResultSubmissionBatches_Course_Year_Semester");

                entity.HasIndex(rsb => rsb.ApprovalStatus)
                    .HasDatabaseName("IX_ResultSubmissionBatches_ApprovalStatus");

                entity.HasIndex(rsb => new { rsb.ApprovalStatus, rsb.AcademicYearId })
                    .HasDatabaseName("IX_ResultSubmissionBatches_Status_Year");

                // For batch publishing queries
                entity.HasIndex(rsb => new { rsb.CourseId, rsb.ApprovalStatus })
                    .HasDatabaseName("IX_ResultSubmissionBatches_Course_Status");
            });

            // GradeConfiguration index - frequently accessed for grading
            builder.Entity<GradeConfiguration>(entity =>
            {
                entity.HasIndex(g => g.IsActive)
                    .HasDatabaseName("IX_GradeConfigurations_IsActive");

                entity.HasIndex(g => new { g.IsActive, g.MinScore })
                    .HasDatabaseName("IX_GradeConfigurations_Active_MinScore");
            });

            // Course indexes for hierarchical queries
            builder.Entity<Course>(entity =>
            {
                entity.HasIndex(c => c.ProgrammeID)
                    .HasDatabaseName("IX_Courses_ProgrammeID");

                entity.HasIndex(c => new { c.ProgrammeID, c.YearTaken, c.SemesterTaken })
                    .HasDatabaseName("IX_Courses_Programme_Year_Semester");
            });

            // Student indexes for report filtering
            builder.Entity<Student>(entity =>
            {
                entity.HasIndex(s => s.ProgrammeId)
                    .HasDatabaseName("IX_Students_ProgrammeId");

                entity.HasIndex(s => new { s.ProgrammeId, s.StudentCurrentYear })
                    .HasDatabaseName("IX_Students_Programme_CurrentYear");

                entity.HasIndex(s => s.ModeOfStudyId)
                    .HasDatabaseName("IX_Students_ModeOfStudyId");

                entity.HasIndex(s => new { s.ProgrammeId, s.ModeOfStudyId, s.StudentCurrentYear })
                    .HasDatabaseName("IX_Students_Programme_Mode_Year");

                entity.HasIndex(s => s.SchoolId)
                    .HasDatabaseName("IX_Students_SchoolId");

                entity.HasIndex(s => s.AcademicYearId)
                    .HasDatabaseName("IX_Students_AcademicYearId");
            });

            // ===================================================================
            // EXISTING CONFIGURATIONS (unchanged from original)
            // ===================================================================

            // Configure relationships for Department and School
            builder.Entity<Department>()
                .HasOne(d => d.School)
                .WithMany(s => s.Departments)
                .HasForeignKey(d => d.SchoolId)
                .OnDelete(DeleteBehavior.Restrict);


            builder.Entity<OnlinePayments>()
                    .Property(p => p.Amount)
                    .HasPrecision(18, 2);

            // Configure relationships for Programme and Department
            builder.Entity<Programme>()
                .HasOne(p => p.Department)
                .WithMany(d => d.Programmes)
                .HasForeignKey(p => p.DepartmentId)
                .OnDelete(DeleteBehavior.Restrict);

            // Configure relationships for Programme and Coordinator (ApplicationUser)
            builder.Entity<Programme>()
                .HasOne<ApplicationUser>(p => p.Coordinator)
                .WithMany()
                .HasForeignKey(p => p.CoordinatorId)
                .OnDelete(DeleteBehavior.Restrict);

            // Configure relationships for Programme and ModeOfStudy
            builder.Entity<Programme>()
                .HasOne(p => p.ModeOfStudy)
                .WithMany()
                .HasForeignKey(p => p.ModeOfStudyId)
                .OnDelete(DeleteBehavior.Restrict);

            // Configure relationships for Programme and ProgrammeLevel
            builder.Entity<Programme>()
                .HasOne(p => p.ProgrammeLevel)
                .WithMany()
                .HasForeignKey(p => p.ProgrammeLevelId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<ProgrammeCourse>()
                .HasOne(pc => pc.Programme)
                .WithMany(p => p.ProgrammeCourses)
                .HasForeignKey(pc => pc.ProgrammeId)
                .OnDelete(DeleteBehavior.Cascade);

            // Configure relationships for AcademicRequests with the necessary delete behaviors
            builder.Entity<AcademicRequest>()
                .HasOne(ar => ar.Student)
                .WithMany(s => s.AcademicRequests)
                .HasForeignKey(ar => ar.StudentId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<AcademicRequest>()
                .HasOne(ar => ar.Programme)
                .WithMany(p => p.AcademicRequests)
                .HasForeignKey(ar => ar.ProgrammeId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<AcademicRequest>()
                .HasOne(ar => ar.School)
                .WithMany(s => s.AcademicRequests)
                .HasForeignKey(ar => ar.SchoolId)
                .OnDelete(DeleteBehavior.Restrict);

            // Configure FinancialStatements relationships with Students and AcademicYears
            builder.Entity<FinancialStatement>()
                .HasOne(fs => fs.Student)
                .WithMany(s => s.FinancialStatements)
                .HasForeignKey(fs => fs.StudentId)
                .OnDelete(DeleteBehavior.NoAction);

            builder.Entity<FinancialStatement>()
                .HasOne(fs => fs.AcademicYear)
                .WithMany(ay => ay.FinancialStatements)
                .HasForeignKey(fs => fs.AcademicYearId)
                .OnDelete(DeleteBehavior.NoAction);

            builder.Entity<ApplicantSubject>()
                .HasOne(a => a.Applicant)
                .WithMany(a => a.SubjectGrades)
                .HasForeignKey(a => a.ApplicantId)
                .HasPrincipalKey(a => a.ApplicantId);

            // Configure relationships for Student and Subject/Grade (Owned Entities)
            builder.Entity<Student>(e =>
            {
                e.OwnsMany(a => a.SubjectGrades, sub =>
                {
                    sub.ToTable("StudentGceSubjects");
                    sub.WithOwner().HasForeignKey("StudentId");
                    sub.HasOne(s => s.Subject).WithMany().HasForeignKey("SubjectId");
                    sub.HasOne(s => s.Grade).WithMany().HasForeignKey("GradeId");
                });
            });

            // Configure relationships for Student and Course/Grade (Owned Entities)
            builder.Entity<Student>(e =>
            {
                e.OwnsMany(a => a.CourseGrades, sub =>
                {
                    sub.ToTable("CourseGrades");
                    sub.WithOwner().HasForeignKey("StudentId");
                    sub.HasOne(s => s.Course).WithMany().HasForeignKey("CourseId");
                    sub.HasOne(s => s.Grade).WithMany().HasForeignKey("GradeId");
                });
            });


            // Configure many-to-many relationships for Course and Prerequisites
            builder.Entity<CoursePrerequisite>()
                .HasKey(cp => new { cp.CourseId, cp.PrerequisiteCourseId });

            builder.Entity<CoursePrerequisite>()
                .HasOne(cp => cp.PrerequisiteCourse)
                .WithMany(c => c.Prerequisites)
                .HasForeignKey(cp => cp.PrerequisiteCourseId)
                .OnDelete(DeleteBehavior.Restrict);

            // Configure FeeConfiguration relationships
            builder.Entity<FeeConfiguration>()
                .HasOne(fc => fc.FeeType)
                .WithMany()
                .HasForeignKey(fc => fc.FeeTypeId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<FeeConfiguration>()
                .HasOne(fc => fc.AcademicYear)
                .WithMany()
                .HasForeignKey(fc => fc.AcademicYearId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<FeeConfiguration>()
                .HasOne(fc => fc.School)
                .WithMany()
                .HasForeignKey(fc => fc.SchoolId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<FeeConfiguration>()
                .HasOne(fc => fc.Programme)
                .WithMany()
                .HasForeignKey(fc => fc.ProgrammeId)
                .OnDelete(DeleteBehavior.Cascade);

            // Configure Assessment precision
            builder.Entity<Assessment>()
                .Property(a => a.PassMark)
                .HasPrecision(4, 1);

            builder.Entity<ApplicationPayment>()
            .Property(p => p.Amount)
            .HasColumnType("decimal(18,2)");


            // Configure Timetable relationships
            builder.Entity<Timetable>(entity =>
            {
                entity.HasOne(t => t.Course)
                    .WithMany()
                    .HasForeignKey(t => t.CourseId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(t => t.LearningRoom)
                    .WithMany()
                    .HasForeignKey(t => t.LearningRoomId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(t => t.TimeSlotConfig)
                    .WithMany()
                    .HasForeignKey(t => t.TimeSlotConfigId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(t => t.AcademicYear)
                    .WithMany()
                    .HasForeignKey(t => t.AcademicYearId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.Property(t => t.Status)
                    .IsRequired()
                    .HasMaxLength(20)
                    .HasDefaultValue("Draft");

                entity.Property(t => t.SpecialInstructions)
                    .HasMaxLength(500);

                entity.HasIndex(t => new { t.AcademicYearId, t.ModeOfStudyId, t.Date, t.PeriodNumber });
                entity.HasIndex(t => t.Status);
            });

            // Configure ScheduleTracking relationships
            builder.Entity<ScheduleTracking>(entity =>
            {
                entity.HasOne(st => st.TimeSlotConfig)
                    .WithMany()
                    .HasForeignKey(st => st.TimeSlotConfigId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(st => st.OccupiedByCourse)
                    .WithMany()
                    .HasForeignKey(st => st.OccupiedByCourseId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.Property(st => st.EntityId)
                    .IsRequired()
                    .HasMaxLength(50);

                entity.Property(st => st.EntityType)
                    .IsRequired()
                    .HasMaxLength(20);

                entity.HasIndex(st => new { st.EntityId, st.EntityType, st.Date, st.PeriodNumber })
                    .IsUnique();
            });

            // Assessments configurations
            builder.Entity<QuestionGroup>()
                .HasOne(qg => qg.Course)
                .WithMany()
                .HasForeignKey(qg => qg.CourseId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<Question>()
                .HasOne(q => q.QuestionGroup)
                .WithMany(qg => qg.Questions)
                .HasForeignKey(q => q.QuestionGroupId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<QuestionOption>()
                .HasOne(qo => qo.Question)
                .WithMany(q => q.Options)
                .HasForeignKey(qo => qo.QuestionId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<AssessmentQuestionGroup>()
                .HasOne(aqg => aqg.AssessmentConfiguration)
                .WithMany(ac => ac.QuestionGroups)
                .HasForeignKey(aqg => aqg.AssessmentConfigurationId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<AssessmentQuestionGroup>()
                .HasOne(aqg => aqg.QuestionGroup)
                .WithMany()
                .HasForeignKey(aqg => aqg.QuestionGroupId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<StudentAttempt>()
                .HasOne(sa => sa.AssessmentConfiguration)
                .WithMany()
                .HasForeignKey(sa => sa.AssessmentConfigurationId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<StudentResponse>()
                .HasOne(sr => sr.StudentAttempt)
                .WithMany(sa => sa.Responses)
                .HasForeignKey(sr => sr.StudentAttemptId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<StudentResponse>()
                .HasOne(sr => sr.Question)
                .WithMany()
                .HasForeignKey(sr => sr.QuestionId)
                .OnDelete(DeleteBehavior.Restrict);

            // Accommodation model configurations
            builder.Entity<Hostel>()
                .HasMany(h => h.Rooms)
                .WithOne(r => r.Hostel)
                .HasForeignKey(r => r.HostelId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<Room>()
                .HasMany(r => r.BedSpaces)
                .WithOne(b => b.Room)
                .HasForeignKey(b => b.RoomId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<Room>()
                .HasMany(r => r.Resources)
                .WithOne(rr => rr.Room)
                .HasForeignKey(rr => rr.RoomId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<Campus>()
                .HasMany(c => c.Hostels)
                .WithOne(h => h.Campus)
                .HasForeignKey(h => h.CampusId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<AccommodationApplication>()
                .HasOne(a => a.Student)
                .WithMany()
                .HasForeignKey(a => a.StudentId)
                .OnDelete(DeleteBehavior.NoAction);

            builder.Entity<Allocation>()
                .HasOne(a => a.Bed)
                .WithMany()
                .HasForeignKey(a => a.BedId)
                .OnDelete(DeleteBehavior.NoAction);

            builder.Entity<Allocation>()
                .HasOne(a => a.AllocatedBy)
                .WithMany()
                .HasForeignKey(a => a.AllocatedById)
                .OnDelete(DeleteBehavior.NoAction);

            builder.Entity<AccommodationApplication>()
                .HasOne(a => a.Allocation)
                .WithOne(a => a.Application)
                .HasForeignKey<Allocation>(a => a.ApplicationId);

            builder.Entity<Allocation>()
                .HasOne(a => a.CheckInOut)
                .WithOne(c => c.Allocation)
                .HasForeignKey<CheckInOut>(c => c.AllocationId);

            builder.Entity<CheckInOut>()
                .HasOne(c => c.CheckInStaff)
                .WithMany()
                .HasForeignKey(c => c.CheckInStaffId)
                .OnDelete(DeleteBehavior.NoAction);

            builder.Entity<CheckInOut>()
                .HasOne(c => c.CheckOutStaff)
                .WithMany()
                .HasForeignKey(c => c.CheckOutStaffId)
                .OnDelete(DeleteBehavior.NoAction);

            builder.Entity<AccommodationApplication>()
                .HasIndex(a => a.StudentId);

            builder.Entity<AccommodationApplication>()
                .HasIndex(a => a.PeriodId);

            builder.Entity<Allocation>()
                .HasIndex(a => a.BedId);

            // ===================================================================
            // ACCOMMODATION PERIOD CONFIGURATION - UPDATED FOR TypeOfPayment
            // ===================================================================
            builder.Entity<AccommodationPeriod>(entity =>
            {
                // Relationships - REMOVED AcademicYear relationship
                entity.HasOne(a => a.School)
                    .WithMany()
                    .HasForeignKey(a => a.SchoolId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(a => a.Programme)
                    .WithMany()
                    .HasForeignKey(a => a.ProgrammeId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(a => a.ModeOfStudy)
                    .WithMany()
                    .HasForeignKey(a => a.ModeOfStudyId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(a => a.ProgramLevel)
                    .WithMany()
                    .HasForeignKey(a => a.ProgramLevelId)
                    .OnDelete(DeleteBehavior.Restrict);

                // TypeOfPayment configuration
                entity.Property(a => a.TypeOfPayment)
                    .HasMaxLength(50)
                    .HasDefaultValue("Semester");

                entity.Property(a => a.TypeOfPaymentAmount)
                    .HasPrecision(18, 2)
                    .HasDefaultValue(0);

                // Indexes
                entity.HasIndex(a => a.Status)
                    .HasDatabaseName("IX_AccommodationPeriods_Status");

                entity.HasIndex(a => a.TypeOfPayment)
                    .HasDatabaseName("IX_AccommodationPeriods_TypeOfPayment");

                entity.HasIndex(a => new { a.Status, a.ApplicationStartDate, a.ApplicationEndDate })
                    .HasDatabaseName("IX_AccommodationPeriods_Status_Dates");
            });

            // AccommodationApplication configuration - add NumberOfDays
            builder.Entity<AccommodationApplication>(entity =>
            {
                entity.Property(a => a.NumberOfDays)
                    .IsRequired(false);

                entity.HasIndex(a => new { a.StudentId, a.Status })
                    .HasDatabaseName("IX_AccommodationApplications_Student_Status");
            });

            builder.Entity<MaintenanceRequest>()
                .HasOne(m => m.Room)
                .WithMany(r => r.MaintenanceRequests)
                .HasForeignKey(m => m.RoomId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<MeetingAttendance>()
                .HasOne(m => m.Meeting)
                .WithMany()
                .HasForeignKey(m => m.ZoomMeetingId)
                .OnDelete(DeleteBehavior.NoAction);

            builder.Entity<CourseContent>()
                .HasOne(cc => cc.Chapter)
                .WithMany(ch => ch.Contents)
                .HasForeignKey(cc => cc.ChapterId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<ChapterProgress>()
                .HasIndex(cp => new { cp.StudentId, cp.ChapterId, cp.CourseId })
                .IsUnique();

            builder.Entity<ChapterProgress>()
                .HasOne(cp => cp.Student)
                .WithMany(s => s.ChapterProgresses)
                .HasForeignKey(cp => cp.StudentId)
                .OnDelete(DeleteBehavior.Cascade);

            // StudentInvoice configurations
            builder.Entity<StudentInvoice>(entity =>
            {
                entity.Property(e => e.TotalAmount).HasPrecision(18, 2);
                entity.Property(e => e.InvoiceReference).HasMaxLength(50).IsRequired();
                entity.Property(e => e.Status).HasMaxLength(20).IsRequired();

                entity.HasOne(si => si.Student)
                    .WithMany()
                    .HasForeignKey(si => si.StudentId)
                    .OnDelete(DeleteBehavior.NoAction);

                entity.HasOne(si => si.AcademicYear)
                    .WithMany()
                    .HasForeignKey(si => si.AcademicYearId)
                    .OnDelete(DeleteBehavior.NoAction);

                entity.HasIndex(e => new { e.StudentId, e.AcademicYearId, e.Semester })
                    .HasDatabaseName("IX_StudentInvoice_Student_AcademicYear_Semester");

                entity.HasIndex(e => e.InvoiceReference)
                    .IsUnique()
                    .HasDatabaseName("IX_StudentInvoice_InvoiceReference");
            });

            builder.Entity<StudentInvoiceItem>(entity =>
            {
                entity.Property(e => e.Amount).HasPrecision(18, 2);
                entity.Property(e => e.FeeTypeName).HasMaxLength(100).IsRequired();
                entity.Property(e => e.Description).HasMaxLength(500);

                entity.HasOne(sii => sii.StudentInvoice)
                    .WithMany(si => si.InvoiceItems)
                    .HasForeignKey(sii => sii.StudentInvoiceId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(sii => sii.FeeConfiguration)
                    .WithMany()
                    .HasForeignKey(sii => sii.FeeConfigurationId)
                    .OnDelete(DeleteBehavior.NoAction);
            });

            // ProgressionRule configuration
            builder.Entity<ProgressionRule>(entity =>
            {
                entity.HasOne(pr => pr.School)
                      .WithMany()
                      .HasForeignKey(pr => pr.SchoolId)
                      .OnDelete(DeleteBehavior.SetNull);

                entity.HasIndex(pr => new { pr.SchoolId, pr.PercentFailedOfCourseLoad, pr.IsActive })
                      .HasDatabaseName("IX_ProgressionRule_SchoolId_MaxFailed_IsActive");
            });

            // StudentCarryoverCourse configuration
            builder.Entity<StudentCarryoverCourse>(entity =>
            {
                entity.HasOne(scc => scc.Student)
                      .WithMany()
                      .HasForeignKey(scc => scc.StudentId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(scc => scc.Course)
                      .WithMany()
                      .HasForeignKey(scc => scc.CourseId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(scc => scc.OriginalAcademicYear)
                      .WithMany()
                      .HasForeignKey(scc => scc.OriginalAcademicYearId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasIndex(scc => new { scc.StudentId, scc.CourseId, scc.IsActive })
                      .HasDatabaseName("IX_StudentCarryover_Student_Course_Active")
                      .HasFilter("[IsActive] = 1");

                entity.HasIndex(scc => new { scc.StudentId, scc.IsActive })
                      .HasDatabaseName("IX_StudentCarryover_Student_Active");
            });

            // ChapterRating configuration
            builder.Entity<ChapterRating>(entity =>
            {
                entity.HasKey(e => e.Id);

                entity.HasIndex(e => new { e.StudentId, e.ChapterId })
                      .IsUnique()
                      .HasDatabaseName("IX_ChapterRating_Student_Chapter_Unique");

                entity.HasIndex(e => e.ChapterId)
                      .HasDatabaseName("IX_ChapterRating_ChapterId");

                entity.HasIndex(e => e.CourseId)
                      .HasDatabaseName("IX_ChapterRating_CourseId");

                entity.Property(e => e.CreatedAt)
                      .HasDefaultValueSql("GETDATE()");

                entity.HasOne(e => e.Student)
                      .WithMany()
                      .HasForeignKey(e => e.StudentId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.Chapter)
                      .WithMany()
                      .HasForeignKey(e => e.ChapterId)
                      .OnDelete(DeleteBehavior.NoAction);

                entity.HasOne(e => e.Course)
                      .WithMany()
                      .HasForeignKey(e => e.CourseId)
                      .OnDelete(DeleteBehavior.NoAction);

                entity.Property(e => e.ReviewText)
                      .IsRequired(false);
            });

            builder.Entity<Programme>()
               .HasOne(p => p.AssociatedNQProgramme)
               .WithMany(p => p.AssociatedRegularProgrammes)
               .HasForeignKey(p => p.AssociatedNQProgrammeId)
               .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<Programme>()
                .HasIndex(p => p.IsNonQuota)
                .HasDatabaseName("IX_Programmes_IsNonQuota");

            builder.Entity<Programme>()
                .HasIndex(p => new { p.DepartmentId, p.IsNonQuota })
                .HasDatabaseName("IX_Programmes_Department_IsNonQuota");

            // Senate Report Cache configuration
            builder.Entity<SenateReportCache>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.ReportKey).IsRequired().HasMaxLength(500);
                entity.HasIndex(e => e.ReportKey).IsUnique();
                entity.HasIndex(e => e.ExpiresAt);
                entity.Property(e => e.ReportData).HasColumnType("nvarchar(max)");
            });

            // Configure StudentCourseResult
            builder.Entity<StudentCourseResult>(entity =>
            {
                entity.ToTable("StudentCourseResults");

                entity.HasIndex(e => new { e.StudentId, e.CourseId, e.AcademicYearId })
                    .HasDatabaseName("IX_StudentCourseResults_Student_Course_Year");

                entity.HasIndex(e => e.ResultHash)
                    .HasDatabaseName("IX_StudentCourseResults_Hash");

                entity.HasIndex(e => e.Status)
                    .HasDatabaseName("IX_StudentCourseResults_Status");

                entity.HasIndex(e => new { e.AcademicYearId, e.Status })
                    .HasDatabaseName("IX_StudentCourseResults_Year_Status");

                entity.HasIndex(e => e.GradeLetter)
                    .HasDatabaseName("IX_StudentCourseResults_Grade");

                entity.HasIndex(e => e.IsPassed)
                    .HasDatabaseName("IX_StudentCourseResults_IsPassed");

                entity.HasIndex(e => new { e.StudentId, e.CourseId, e.AcademicYearId, e.Semester, e.AttemptNumber })
                    .IsUnique()
                    .HasDatabaseName("UX_StudentCourseResults_Unique");

                entity.Property(e => e.WeightedTotal).HasPrecision(5, 2);
                entity.Property(e => e.NormalizedTotal).HasPrecision(5, 2);
                entity.Property(e => e.GradePoints).HasPrecision(3, 2);
                entity.Property(e => e.PassMark).HasPrecision(5, 2);
                entity.Property(e => e.TotalWeightPercentage).HasPrecision(5, 2);

                entity.Property(e => e.CalculatedAt).HasDefaultValueSql("GETUTCDATE()");
                entity.Property(e => e.Status).HasDefaultValue(Status.Draft);
                entity.Property(e => e.IsCarryover).HasDefaultValue(false);
                entity.Property(e => e.AttemptNumber).HasDefaultValue(1);

                entity.HasOne(e => e.Student)
                    .WithMany()
                    .HasForeignKey(e => e.StudentId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.Course)
                    .WithMany()
                    .HasForeignKey(e => e.CourseId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.AcademicYear)
                    .WithMany()
                    .HasForeignKey(e => e.AcademicYearId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.PublishedByUser)
                    .WithMany()
                    .HasForeignKey(e => e.PublishedBy)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.CalculatedByUser)
                    .WithMany()
                    .HasForeignKey(e => e.CalculatedBy)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.Ignore(e => e.AssessmentScores);
            });

            // Configure ResultAuditLog
            builder.Entity<ResultAuditLog>(entity =>
            {
                entity.ToTable("ResultAuditLogs");

                entity.HasIndex(e => new { e.EntityType, e.EntityId })
                    .HasDatabaseName("IX_ResultAuditLogs_Entity");

                entity.HasIndex(e => new { e.StudentId, e.CourseId })
                    .HasDatabaseName("IX_ResultAuditLogs_Student_Course");

                entity.HasIndex(e => e.ChangedAt)
                    .HasDatabaseName("IX_ResultAuditLogs_ChangedAt");

                entity.HasIndex(e => e.ChangedBy)
                    .HasDatabaseName("IX_ResultAuditLogs_ChangedBy");

                entity.HasIndex(e => e.BatchId)
                    .HasDatabaseName("IX_ResultAuditLogs_BatchId");

                entity.HasIndex(e => e.ActionType)
                    .HasDatabaseName("IX_ResultAuditLogs_ActionType");

                entity.HasIndex(e => new { e.AcademicYearId, e.ActionType, e.ChangedAt })
                    .HasDatabaseName("IX_ResultAuditLogs_Year_Action_Date");

                entity.Property(e => e.ChangedAt).HasDefaultValueSql("GETUTCDATE()");
                entity.Property(e => e.IsBatchOperation).HasDefaultValue(false);

                entity.HasOne(e => e.Student)
                    .WithMany()
                    .HasForeignKey(e => e.StudentId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.Course)
                    .WithMany()
                    .HasForeignKey(e => e.CourseId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.AcademicYear)
                    .WithMany()
                    .HasForeignKey(e => e.AcademicYearId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.ChangedByUser)
                    .WithMany()
                    .HasForeignKey(e => e.ChangedBy)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // StudentDisqualification configuration
            builder.Entity<StudentDisqualification>(entity =>
            {
                entity.HasOne(sd => sd.Student)
                    .WithMany()
                    .HasForeignKey(sd => sd.StudentId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(sd => sd.Course)
                    .WithMany()
                    .HasForeignKey(sd => sd.CourseId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(sd => sd.AcademicYear)
                    .WithMany()
                    .HasForeignKey(sd => sd.AcademicYearId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasIndex(sd => new { sd.StudentId, sd.CourseId, sd.AcademicYearId, sd.Semester })
                    .HasDatabaseName("IX_StudentDisqualifications_Student_Course_Year_Semester");

                entity.HasIndex(sd => sd.Status)
                    .HasDatabaseName("IX_StudentDisqualifications_Status");

                entity.HasIndex(sd => sd.StudentId)
                    .HasDatabaseName("IX_StudentDisqualifications_StudentId");
            });

            // ===================================================================
            // RESULT APPEALS CONFIGURATION
            // ===================================================================

            // ResultAppeal configuration
            builder.Entity<ResultAppeal>(entity =>
            {
                entity.HasOne(ra => ra.Student)
                    .WithMany(s => s.ResultAppeals)
                    .HasForeignKey(ra => ra.StudentId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(ra => ra.Course)
                    .WithMany()
                    .HasForeignKey(ra => ra.CourseId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(ra => ra.AcademicYear)
                    .WithMany()
                    .HasForeignKey(ra => ra.AcademicYearId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(ra => ra.Responder)
                    .WithMany()
                    .HasForeignKey(ra => ra.ResponseBy)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(ra => ra.DecisionMaker)
                    .WithMany()
                    .HasForeignKey(ra => ra.DecisionBy)
                    .OnDelete(DeleteBehavior.Restrict);

                // Performance indexes
                entity.HasIndex(ra => new { ra.StudentId, ra.CourseId, ra.AcademicYearId, ra.Semester })
                    .HasDatabaseName("IX_ResultAppeals_Student_Course_Year_Semester");

                entity.HasIndex(ra => ra.Status)
                    .HasDatabaseName("IX_ResultAppeals_Status");

                entity.HasIndex(ra => ra.StudentId)
                    .HasDatabaseName("IX_ResultAppeals_StudentId");

                entity.HasIndex(ra => ra.AppealType)
                    .HasDatabaseName("IX_ResultAppeals_AppealType");

                entity.HasIndex(ra => new { ra.AcademicYearId, ra.Status })
                    .HasDatabaseName("IX_ResultAppeals_Year_Status");

                entity.HasIndex(ra => new { ra.AppealType, ra.FeePaid })
                    .HasDatabaseName("IX_ResultAppeals_Type_FeePaid");

                // Decimal precision for fee and marks
                entity.Property(ra => ra.AppealFee).HasPrecision(18, 2);
                entity.Property(ra => ra.OriginalMark).HasPrecision(5, 2);
                entity.Property(ra => ra.RevisedMark).HasPrecision(5, 2);
            });

            // AppealStatusHistory configuration
            builder.Entity<AppealStatusHistory>(entity =>
            {
                entity.HasOne(ash => ash.Appeal)
                    .WithMany()
                    .HasForeignKey(ash => ash.AppealId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(ash => ash.ChangedByUser)
                    .WithMany()
                    .HasForeignKey(ash => ash.ChangedBy)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasIndex(ash => ash.AppealId)
                    .HasDatabaseName("IX_AppealStatusHistories_AppealId");

                entity.HasIndex(ash => ash.ChangedAt)
                    .HasDatabaseName("IX_AppealStatusHistories_ChangedAt");
            });

            // AppealTypeConfig configuration with seed data
            builder.Entity<AppealTypeConfig>(entity =>
            {
                entity.HasIndex(atc => atc.TypeCode)
                    .IsUnique()
                    .HasDatabaseName("IX_AppealTypeConfigs_TypeCode");

                entity.HasIndex(atc => atc.IsActive)
                    .HasDatabaseName("IX_AppealTypeConfigs_IsActive");

                entity.Property(atc => atc.Fee).HasPrecision(18, 2);

                // Seed data for appeal types
                entity.HasData(
                    new AppealTypeConfig
                    {
                        Id = 1,
                        TypeCode = "Remark",
                        TypeName = "Remark (Re-marking of script)",
                        Description = "Request for complete re-marking of examination script by a different examiner",
                        Fee = 500.00m,
                        RequiresFee = true,
                        IsActive = true,
                        DisplayOrder = 1
                    },
                    new AppealTypeConfig
                    {
                        Id = 2,
                        TypeCode = "Review",
                        TypeName = "Review (Review of marking)",
                        Description = "Request to review the marking for any errors or omissions",
                        Fee = 0,
                        RequiresFee = false,
                        IsActive = true,
                        DisplayOrder = 2
                    },
                    new AppealTypeConfig
                    {
                        Id = 3,
                        TypeCode = "Recalculation",
                        TypeName = "Recalculation (Check totals)",
                        Description = "Request to verify the totaling and calculation of marks",
                        Fee = 0,
                        RequiresFee = false,
                        IsActive = true,
                        DisplayOrder = 3
                    },
                    new AppealTypeConfig
                    {
                        Id = 4,
                        TypeCode = "MissingMarks",
                        TypeName = "Missing Marks",
                        Description = "Appeal for marks that were not recorded or are missing from the system",
                        Fee = 0,
                        RequiresFee = false,
                        IsActive = true,
                        DisplayOrder = 4
                    },
                    new AppealTypeConfig
                    {
                        Id = 5,
                        TypeCode = "GradeDispute",
                        TypeName = "Grade Dispute",
                        Description = "Dispute regarding the final grade assigned",
                        Fee = 0,
                        RequiresFee = false,
                        IsActive = true,
                        DisplayOrder = 5
                    },
                    new AppealTypeConfig
                    {
                        Id = 6,
                        TypeCode = "Other",
                        TypeName = "Other",
                        Description = "Other appeal types not covered by the above categories",
                        Fee = 0,
                        RequiresFee = false,
                        IsActive = true,
                        DisplayOrder = 6
                    }
                );
            });

            // ===================================================================
            // END OF RESULT APPEALS CONFIGURATION
            // ===================================================================

            builder.Entity<StudentData>()
                .HasNoKey() // Views don't have primary keys
                .ToView("vwStudentCourses"); // Map it to the actual view

            builder.Entity<Models.ViewModels.VwBiSchoolBillingMonthly>(entity =>
            {
                entity.HasNoKey();
                entity.ToView("VW_BI_SchoolBilling_Monthly");
            });

            builder.Entity<WorkflowApproval>()
                .HasOne(a => a.WorkflowInstance)
                .WithMany(i => i.Approvals)
                .HasForeignKey(a => a.WorkflowInstanceId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<WorkflowNotification>()
                .HasOne(n => n.WorkflowInstance)
                .WithMany()
                .HasForeignKey(n => n.WorkflowInstanceId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<StudentResultView>()
                .HasNoKey()
                .ToView("VW_StudentResults");

            // Configure PaymentAllocation relationships
            builder.Entity<PaymentAllocation>()
                .HasOne(pa => pa.OnlinePayment)
                .WithMany(op => op.PaymentAllocations)
                .HasForeignKey(pa => pa.OnlinePaymentId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<PaymentAllocation>()
                .HasOne(pa => pa.StudentInvoice)
                .WithMany(si => si.PaymentAllocations)
                .HasForeignKey(pa => pa.StudentInvoiceId)
                .OnDelete(DeleteBehavior.Restrict);

            // Create index for performance
            builder.Entity<PaymentAllocation>()
                .HasIndex(pa => new { pa.OnlinePaymentId, pa.AllocationSequence });

            builder.Entity<PaymentAllocation>()
                .HasIndex(pa => pa.StudentInvoiceId);

            // Seed default permissions
            //SeedPermissions(builder);
            //SeedRolePermissions(builder);
        }

        private void SeedPermissions(ModelBuilder modelBuilder)
        {
            var permissions = new List<Permission>
            {
                // =========================
                // STUDENTS (Registry)
                // =========================
                new Permission { Id = 1, Name = "Students.View", Description = "View student records", Category = "Students" },
                new Permission { Id = 2, Name = "Students.Create", Description = "Create student records", Category = "Students" },
                new Permission { Id = 3, Name = "Students.Edit", Description = "Edit student records", Category = "Students" },
                new Permission { Id = 4, Name = "Students.Delete", Description = "Delete student records", Category = "Students" },
                new Permission { Id = 5, Name = "Students.Suspend", Description = "Suspend or deactivate students", Category = "Students" },
                new Permission { Id = 6, Name = "Students.Graduate", Description = "Graduate students", Category = "Students" },

                // =========================
                // APPLICATIONS & ADMISSIONS
                // =========================
                new Permission { Id = 7, Name = "Applications.View", Description = "View applications", Category = "Admissions" },
                new Permission { Id = 8, Name = "Applications.Review", Description = "Review applications", Category = "Admissions" },
                new Permission { Id = 9, Name = "Applications.Approve", Description = "Approve applications", Category = "Admissions" },
                new Permission { Id = 10, Name = "Applications.Reject", Description = "Reject applications", Category = "Admissions" },

                // =========================
                // PROGRAMMES & COURSES
                // =========================
                new Permission { Id = 11, Name = "Programmes.View", Description = "View programmes", Category = "Academics" },
                new Permission { Id = 12, Name = "Programmes.Create", Description = "Create programmes", Category = "Academics" },
                new Permission { Id = 13, Name = "Programmes.Edit", Description = "Edit programmes", Category = "Academics" },
                new Permission { Id = 14, Name = "Programmes.Archive", Description = "Archive programmes", Category = "Academics" },

                new Permission { Id = 15, Name = "Courses.View", Description = "View courses", Category = "Academics" },
                new Permission { Id = 16, Name = "Courses.Create", Description = "Create courses", Category = "Academics" },
                new Permission { Id = 17, Name = "Courses.Edit", Description = "Edit courses", Category = "Academics" },
                new Permission { Id = 18, Name = "Courses.AssignLecturer", Description = "Assign lecturers to courses", Category = "Academics" },

                // =========================
                // REGISTRATION & ENROLMENT
                // =========================
                new Permission { Id = 19, Name = "Registration.View", Description = "View registrations", Category = "Registration" },
                new Permission { Id = 20, Name = "Registration.Approve", Description = "Approve registrations", Category = "Registration" },
                new Permission { Id = 21, Name = "Registration.Override", Description = "Override registration rules", Category = "Registration" },

                // =========================
                // ASSESSMENTS & RESULTS
                // =========================
                new Permission { Id = 22, Name = "Assessments.Create", Description = "Create assessments", Category = "Assessments" },
                new Permission { Id = 23, Name = "Assessments.Edit", Description = "Edit assessments", Category = "Assessments" },
                new Permission { Id = 24, Name = "Assessments.Publish", Description = "Publish assessments", Category = "Assessments" },

                new Permission { Id = 25, Name = "Results.Enter", Description = "Enter student results", Category = "Results" },
                new Permission { Id = 26, Name = "Results.Edit", Description = "Edit student results", Category = "Results" },
                new Permission { Id = 27, Name = "Results.Approve", Description = "Approve results", Category = "Results" },
                new Permission { Id = 28, Name = "Results.Publish", Description = "Publish results", Category = "Results" },

                // =========================
                // SENATE & ACADEMIC BOARDS
                // =========================
                new Permission { Id = 29, Name = "Senate.View", Description = "View senate matters", Category = "Governance" },
                new Permission { Id = 30, Name = "Senate.ApproveResults", Description = "Approve results at senate", Category = "Governance" },
                new Permission { Id = 31, Name = "Senate.ApproveGraduation", Description = "Approve graduation lists", Category = "Governance" },

                // =========================
                // FINANCE
                // =========================
                new Permission { Id = 32, Name = "Invoices.View", Description = "View invoices", Category = "Finance" },
                new Permission { Id = 33, Name = "Invoices.Create", Description = "Create invoices", Category = "Finance" },
                new Permission { Id = 34, Name = "Invoices.Edit", Description = "Edit invoices", Category = "Finance" },
                new Permission { Id = 35, Name = "Invoices.Cancel", Description = "Cancel invoices", Category = "Finance" },

                new Permission { Id = 36, Name = "Payments.View", Description = "View payments", Category = "Finance" },
                new Permission { Id = 37, Name = "Payments.Record", Description = "Record payments", Category = "Finance" },
                new Permission { Id = 38, Name = "Payments.Approve", Description = "Approve payments", Category = "Finance" },
                new Permission { Id = 39, Name = "Payments.Reverse", Description = "Reverse payments", Category = "Finance" },

                // =========================
                // REPORTS
                // =========================
                new Permission { Id = 40, Name = "Reports.View", Description = "View reports", Category = "Reports" },
                new Permission { Id = 41, Name = "Reports.Financial", Description = "View financial reports", Category = "Reports" },
                new Permission { Id = 42, Name = "Reports.Academic", Description = "View academic reports", Category = "Reports" },
                new Permission { Id = 43, Name = "Reports.Export", Description = "Export reports", Category = "Reports" },

                // =========================
                // USERS, ROLES & SYSTEM
                // =========================
                new Permission { Id = 44, Name = "Users.View", Description = "View system users", Category = "Administration" },
                new Permission { Id = 45, Name = "Users.Create", Description = "Create system users", Category = "Administration" },
                new Permission { Id = 46, Name = "Users.Edit", Description = "Edit system users", Category = "Administration" },
                new Permission { Id = 47, Name = "Users.Disable", Description = "Disable system users", Category = "Administration" },

                new Permission { Id = 48, Name = "Roles.Manage", Description = "Manage roles and permissions", Category = "Administration" },
                new Permission { Id = 49, Name = "System.Settings", Description = "Manage system settings", Category = "Administration" },
                new Permission { Id = 50, Name = "AuditLogs.View", Description = "View audit logs", Category = "Administration" }
            };

            modelBuilder.Entity<Permission>().HasData(permissions);
        }

        private void SeedRolePermissions(ModelBuilder modelBuilder)
        {
            var rolePermissions = new List<RolePermission>();

            // =========================
            // STUDENT
            // =========================
            rolePermissions.AddRange(Map("f056dbd8-c037-46ed-8b65-72a5c414b30e",
                19, // Registration.View
                32, // Invoices.View
                36  // Payments.View
            ));

            // =========================
            // LECTURER
            // =========================
            rolePermissions.AddRange(Map("fe241f62-0eed-424f-93b2-4984a4179349",
                11, // Programmes.View
                15, // Courses.View
                22, // Assessments.Create
                23, // Assessments.Edit
                25  // Results.Enter
            ));

            // =========================
            // HOD
            // =========================
            rolePermissions.AddRange(Map("397eb810-1b85-4af9-8bf8-75e34fdcc6ff",
                // Lecturer permissions
                11, 15, 22, 23, 25,

                // Additional
                18, // Courses.AssignLecturer
                26, // Results.Edit
                40, // Reports.View
                42  // Reports.Academic
            ));

            // =========================
            // DEAN
            // =========================
            rolePermissions.AddRange(Map("127c2a8a-0be0-404e-ba2c-767368708946",
                // HOD permissions
                11, 15, 18, 22, 23, 25, 26,

                // Additional
                27, // Results.Approve
                13, // Programmes.Edit
                42  // Reports.Academic
            ));

            // =========================
            // SENATE
            // =========================
            rolePermissions.AddRange(Map("3c061125-830d-4dd6-bc60-cb01fe712f58",
                29, // Senate.View
                30, // Senate.ApproveResults
                31  // Senate.ApproveGraduation
            ));

            // =========================
            // REGISTRY
            // =========================
            rolePermissions.AddRange(Map("3f085bea-5ba0-422f-9138-3ee926e40c20",
                1, 2, 3, 4, 5, 6,        // Students.*
                7, 8, 9, 10,            // Applications.*
                19, 20, 21              // Registration.*
            ));

            // =========================
            // FINANCE
            // =========================
            rolePermissions.AddRange(Map("319e07d4-4f94-4b5d-8be0-3828d51ecad2",
                32, 33, 34, 35,         // Invoices.*
                36, 37, 38, 39,         // Payments.*
                41                      // Reports.Financial
            ));

            // =========================
            // SYSTEM ADMIN
            // =========================
            rolePermissions.AddRange(Map("55f04e59-988a-4f18-8482-dec5b7e6241c",
                44, 45, 46, 47,         // Users.*
                48,                     // Roles.Manage
                49,                     // System.Settings
                50                      // AuditLogs.View
            ));

            modelBuilder.Entity<RolePermission>().HasData(rolePermissions);
        }

        private static IEnumerable<RolePermission> Map(string roleId, params int[] permissionIds)
        {
            return permissionIds.Select(pid => new RolePermission
            {
                RoleId = roleId,
                PermissionId = pid
            });
        }

    }
}