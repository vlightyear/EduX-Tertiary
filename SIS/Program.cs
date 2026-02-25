using DocumentFormat.OpenXml.Office2016.Drawing.ChartDrawing;
using iTextSharp.text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Server.IIS;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.AspNetCore.Session;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NuGet.Packaging;
using SIS.Authorization;
using SIS.Data;
using SIS.Interfaces;
using SIS.Models;
using SIS.Models.Admin;
using SIS.Models.Configuration;
using SIS.Models.Zoom;
using SIS.Notifications;
using SIS.Repository;
using SIS.Services;
using SIS.Services.Accounting;
using SIS.Services.Documentation;
using SIS.Services.Emails;
using SIS.Services.FilePreview;
using SIS.Services.Payment;
using SIS.Services.PDF;
using SIS.Services.PhotoValidation;
using SIS.Services.Progression;
using SIS.Services.QuestionImport;
using SIS.Services.Registration;
using SIS.Services.Reports;
using SIS.Services.ResultImport;
using SIS.Services.StudentApplication;
using SIS.Services.StudentImport;
using SIS.Services.Transcripts;
using SIS.Services.Users;
using SIS.Services.Zoom;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Configure the database context
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    options.UseSqlServer(connectionString, sqlOptions =>
    {
        sqlOptions.CommandTimeout(36000);
        sqlOptions.EnableRetryOnFailure(
            maxRetryCount: 3,
            maxRetryDelay: TimeSpan.FromSeconds(5),
            errorNumbersToAdd: null);
    });

    // Configure for bulk operations
    options.EnableSensitiveDataLogging(false);
    options.EnableDetailedErrors(false);
});

// Add DbContextFactory for parallel operations (prevents "second operation started" errors)
// Using ServiceLifetime.Scoped to match the DbContext registration
builder.Services.AddDbContextFactory<ApplicationDbContext>(options =>
{
    options.UseSqlServer(connectionString, sqlOptions =>
    {
        sqlOptions.CommandTimeout(36000);
        sqlOptions.EnableRetryOnFailure(
            maxRetryCount: 3,
            maxRetryDelay: TimeSpan.FromSeconds(5),
            errorNumbersToAdd: null);
    });
    options.EnableSensitiveDataLogging(false);
    options.EnableDetailedErrors(false);
}, ServiceLifetime.Scoped);

// Register services
builder.Services.AddScoped<IApplicantService, ApplicantService>();
builder.Services.AddScoped<IPaymentService, PaymentService>();
builder.Services.AddScoped<IAccommodationAllocationService, AccommodationAllocationService>();
builder.Services.AddSingleton<EmailService>();
builder.Services.AddScoped<UserService>();

builder.Services.AddScoped<AcademicCalendarService>();
builder.Services.AddScoped<IPaymentReconciliationService, PaymentReconciliationService>();

builder.Services.AddScoped<IHtmlPdfService, HtmlPdfService>();

//Ting
builder.Services.Configure<TinggSettings>(
    builder.Configuration.GetSection("TinggSettings"));

builder.Services.AddScoped<SIS.Services.Payment.TinggAuthService>();
builder.Services.AddScoped<SIS.Services.Payment.TinggExpressCheckoutService>();

// Register Result Management Services
builder.Services.AddScoped<IResultIntegrityService, ResultIntegrityService>();
builder.Services.AddScoped<IAssessmentScoreService, AssessmentScoreService>();
builder.Services.AddScoped<ICourseResultCalculationService, CourseResultCalculationService>();
builder.Services.AddScoped<IResultAuditService, ResultAuditService>();
builder.Services.AddScoped<IResultImportService, ResultImportService>();
builder.Services.AddScoped<ITranscriptGenerationService, TranscriptGenerationService>();
builder.Services.AddHttpContextAccessor();

// Add Zoom configuration
builder.Services.Configure<ZoomOptions>(builder.Configuration.GetSection(ZoomOptions.SectionName));

// Add this line with your other service registrations
builder.Services.AddScoped<IProgrammeService, ProgrammeService>();
builder.Services.AddScoped<IPdfInvoiceService, PdfInvoiceService>();
builder.Services.AddScoped<ICalendarPdfService, CalendarPdfService>();
builder.Services.AddScoped<IPhotoValidationService, PhotoValidationService>();

// Institution Config Service
builder.Services.AddSingleton<IInstitutionConfigService, InstitutionConfigService>();

// Student Progression Service
builder.Services.AddScoped<IStudentProgressionService, StudentProgressionService>();

// Bridge for existing code - Fixed version
builder.Services.AddSingleton<IOptions<SIS.Models.Configuration.EmailSettings>>(provider =>
{
    var institutionConfig = provider.GetRequiredService<IInstitutionConfigService>();
    var emailSettings = institutionConfig.GetEmailSettings();
    return Options.Create(emailSettings);
});

builder.Services.AddScoped<IEmailService, EmailService>();

builder.Services.AddScoped<IStudentImportService, StudentImportService>();

builder.Services.AddScoped<IWorkflowService, WorkflowService>();
builder.Services.AddScoped<IWorkflowTemplateService, WorkflowTemplateService>();
builder.Services.AddScoped<IWorkflowStatisticsService, WorkflowStatisticsService>();
builder.Services.AddScoped<IWorkflowValidationService, WorkflowValidationService>();

builder.Services.AddScoped<IPaymentAllocationService, PaymentAllocationService>();

builder.Services.AddSingleton<IBackgroundEmailService, BackgroundEmailService>();
builder.Services.AddHostedService<BackgroundEmailService>(provider =>
    (BackgroundEmailService)provider.GetRequiredService<IBackgroundEmailService>());

// Configure Accounting System options
builder.Services.Configure<AccountingSystemOptions>(
    builder.Configuration.GetSection(AccountingSystemOptions.SectionName));

// Register HttpClient for accounting service
builder.Services.AddHttpClient<IAccountingService, AccountingService>((serviceProvider, client) =>
{
    var options = serviceProvider.GetRequiredService<IOptions<AccountingSystemOptions>>().Value;
    client.BaseAddress = new Uri(options.BaseUrl);
    client.DefaultRequestHeaders.Add("Eden-1-API-KEY", options.ApiKey);
    client.Timeout = TimeSpan.FromSeconds(30);
});

// Student Invoice
builder.Services.AddScoped<IStudentInvoiceService, StudentInvoiceService>();

// Register ZoomService
builder.Services.AddScoped<IZoomService, ZoomService>();
builder.Services.AddScoped<IZoomWebSdkService, ZoomWebSdkService>();

// File Preview Service
builder.Services.AddScoped<IFileService, FileService>();

// Register the CourseRegistrationService correctly
builder.Services.AddScoped<ICourseRegistrationService, CourseRegistrationService>();

// Register repositories
builder.Services.AddScoped<ICourseRepository, CourseRepository>();
builder.Services.AddScoped<IStudentRepository, StudentRepository>();
builder.Services.AddScoped<IAcademicRequestRepository, AcademicRequestRepository>();
builder.Services.AddScoped<ExamDocketService>();

// Senate Report Service
builder.Services.AddScoped<ISenateReportService, SenateReportService>();

// Register other services
builder.Services.AddScoped<IGradeService, GradeService>();
builder.Services.AddScoped<IAcademicRequestService, AcademicRequestService>();
builder.Services.AddScoped<IDocumentGenerationService, DocumentGenerationService>();
builder.Services.AddScoped<QuestionImportService>();

// Configure Identity (only call it once)
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.SignIn.RequireConfirmedAccount = false;
    options.User.RequireUniqueEmail = true;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

builder.Services.Configure<IdentityOptions>(options =>
{
    // Password settings
    // Lockout settings
    // User settings
    options.User.RequireUniqueEmail = true;
    options.User.AllowedUserNameCharacters = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-._@+'";
    options.User.RequireUniqueEmail = true;
});

// Register HttpClient
builder.Services.AddHttpClient();

builder.Services.AddControllersWithViews()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
        options.JsonSerializerOptions.DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
    });

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowSpecificOrigin", policy =>
    {
        policy.WithOrigins("https://localhost:7167")
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

// Add IHttpContextAccessor
builder.Services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();

// Add session services
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.IOTimeout = TimeSpan.FromMinutes(5);
});

// Configure request timeout
builder.Services.AddRequestTimeouts();

// Configure request size limits for large Excel files
builder.Services.Configure<IISServerOptions>(options =>
{
    options.MaxRequestBodySize = 50 * 1024 * 1024; // 50MB for large Excel files
});

// Configure Kestrel timeout
builder.Services.Configure<KestrelServerOptions>(options =>
{
    options.Limits.RequestHeadersTimeout = TimeSpan.FromMinutes(5);
    options.Limits.KeepAliveTimeout = TimeSpan.FromMinutes(10);
});

// Configure TempData for large data
builder.Services.Configure<CookieTempDataProviderOptions>(options =>
{
    options.Cookie.IsEssential = true;
});

Syncfusion.Licensing.SyncfusionLicenseProvider.RegisterLicense("Ngo9BigBOggjHTQxAR8/V1JFaF5cX2FCf1FpRmJGdld5fUVHYVZUTXxaS00DNHVRdkdmWH9edXRWQ2FfUkJyXUNWYEg=");

// Add Permission Service
builder.Services.AddScoped<IPermissionService, PermissionService>();

// Add Authorization with custom policy provider
builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
builder.Services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();

builder.Services.AddAuthorization();

var app = builder.Build();

var scopeFactory = app.Services.GetRequiredService<IServiceScopeFactory>();
StudentTools.Configure(scopeFactory);

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseCors("AllowSpecificOrigin");

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

// Middleware
app.UseAuthentication();
app.UseAuthorization();

// Add session middleware to the pipeline
app.UseSession();

app.UseRequestTimeouts();

// Define your routes
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Account}/{action=Login}/{id?}");

using (var scope = app.Services.CreateScope())
{
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

    var roles = new[] {
        "Admin",
        "Academic Officer",
        "Lecturer",
        "Tutor",
        "Student",
        "AccommodatedStudent",
        "AccommodationManager",
        "MaintenanceEngineer",
        "Staff",
        "Candidate",
        "Registrar",
        "Dean",
        "HOD",
        "Warden",
        "HostelManager",
        "ProgramCoordinator",
        "VC",
        "DVC",
        "Dev",
        "Finance",
        "Compliance",
        "ISO",
        "Assistant Registrar"
    };

    // Add roles if they don't exist
    foreach (var role in roles)
    {
        if (!await roleManager.RoleExistsAsync(role))
        {
            var newRole = new IdentityRole(role)
            {
                ConcurrencyStamp = Guid.NewGuid().ToString()
            };

            await roleManager.CreateAsync(newRole);
        }
    }

    // Add default admin user if it doesn't exist
    var defaultAdminEmail = "admin@ecampus.com";
    var adminUser = await userManager.FindByEmailAsync(defaultAdminEmail);

    if (adminUser == null)
    {
        // Create default admin user
        adminUser = new ApplicationUser
        {
            FullName = "Administrator",
            UserName = defaultAdminEmail,
            Email = defaultAdminEmail,
            EmailConfirmed = true
        };

        var createAdminResult = await userManager.CreateAsync(adminUser, "Admin@123");
        if (createAdminResult.Succeeded)
        {
            // Assign admin role
            await userManager.AddToRoleAsync(adminUser, "Admin");
        }
        else
        {
            // Handle errors if user creation fails
            foreach (var error in createAdminResult.Errors)
            {
                Console.WriteLine($"Error creating admin: {error.Description}");
            }
        }
    }

    // Create a virtual learning room if it doesn't exist
    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

    // Check if virtual room already exists
    var virtualRoomExists = await dbContext.LearningRooms
        .AnyAsync(r => r.Name.Contains("Virtual") || r.Name.Contains("Online"));

    if (!virtualRoomExists)
    {
        try
        {
            // Get any active building
            var building = await dbContext.Buildings
                .Where(b => b.IsActive)
                .FirstOrDefaultAsync();

            if (building != null)
            {
                // Create a virtual room in the existing building
                var newVirtualRoom = new LearningRoom
                {
                    Name = "Virtual/Online Room",
                    Description = "Used for online or virtual sessions with no physical location",
                    BuildingId = building.Id,
                    LearningCapacity = 10000,
                    ExamCapacity = 10000,
                    RoomType = "Virtual",
                    CreatedDate = DateTime.Now,
                    IsActive = true,
                    Area = 0
                };

                dbContext.LearningRooms.Add(newVirtualRoom);
                await dbContext.SaveChangesAsync();

                Console.WriteLine("Virtual learning room created successfully.");
            }
            else
            {
                Console.WriteLine("No active buildings found. Cannot create virtual learning room.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error creating virtual learning room: {ex.Message}");
        }
    }

    if (!await dbContext.AcademicEventTypes.AnyAsync())
    {
        var academicEventTypes = new List<AcademicEventType>
        {
            new AcademicEventType
            {
                Name = "Deferred Examination",
                DefaultColor = "#FF6B35",
                IconName = "schedule"
            },
            new AcademicEventType
            {
                Name = "Supplementary Examination",
                DefaultColor = "#FF6B35",
                IconName = "playlist_add"
            },
            new AcademicEventType
            {
                Name = "Make-up Examination",
                DefaultColor = "#FF6B35",
                IconName = "edit_calendar"
            },
            new AcademicEventType
            {
                Name = "Practical Examination",
                DefaultColor = "#DB4437",
                IconName = "science"
            },
            new AcademicEventType
            {
                Name = "Registration Period",
                DefaultColor = "#4285F4",
                IconName = "how_to_reg"
            },
            new AcademicEventType
            {
                Name = "Add/Drop Period",
                DefaultColor = "#4285F4",
                IconName = "playlist_add_check"
            },
            new AcademicEventType
            {
                Name = "Class Begin",
                DefaultColor = "#0F9D58",
                IconName = "school"
            },
            new AcademicEventType
            {
                Name = "Class End",
                DefaultColor = "#0F9D58",
                IconName = "school"
            },
            new AcademicEventType
            {
                Name = "Mid-term Examination",
                DefaultColor = "#DB4437",
                IconName = "rate_review"
            },
            new AcademicEventType
            {
                Name = "Final Examination",
                DefaultColor = "#DB4437",
                IconName = "edit_note"
            },
            new AcademicEventType
            {
                Name = "Continuous Assessment",
                DefaultColor = "#DB4437",
                IconName = "assignment"
            },
            new AcademicEventType
            {
                Name = "Fee Payment Deadline",
                DefaultColor = "#F4B400",
                IconName = "payments"
            },
            new AcademicEventType
            {
                Name = "Grade Submission Deadline",
                DefaultColor = "#F4B400",
                IconName = "timer"
            },
            new AcademicEventType
            {
                Name = "Application Deadline",
                DefaultColor = "#F4B400",
                IconName = "event_busy"
            },
            new AcademicEventType
            {
                Name = "Document Submission",
                DefaultColor = "#F4B400",
                IconName = "upload_file"
            },
            new AcademicEventType
            {
                Name = "Semester/Term Start",
                DefaultColor = "#4285F4",
                IconName = "start"
            },
            new AcademicEventType
            {
                Name = "Semester/Term End",
                DefaultColor = "#4285F4",
                IconName = "logout"
            },
            new AcademicEventType
            {
                Name = "Results Release",
                DefaultColor = "#0F9D58",
                IconName = "grading"
            },
            new AcademicEventType
            {
                Name = "Academic Appeal Period",
                DefaultColor = "#F4B400",
                IconName = "history_edu"
            },
            new AcademicEventType
            {
                Name = "Holiday",
                DefaultColor = "#9e9e9e",
                IconName = "today"
            },
            new AcademicEventType
            {
                Name = "Reading/Study Break",
                DefaultColor = "#9e9e9e",
                IconName = "menu_book"
            },
            new AcademicEventType
            {
                Name = "Semester Break",
                DefaultColor = "#9e9e9e",
                IconName = "free_breakfast"
            },
            new AcademicEventType
            {
                Name = "Orientation",
                DefaultColor = "#673AB7",
                IconName = "explore"
            },
            new AcademicEventType
            {
                Name = "Graduation/Convocation",
                DefaultColor = "#673AB7",
                IconName = "workspace_premium"
            },
            new AcademicEventType
            {
                Name = "Commencement",
                DefaultColor = "#673AB7",
                IconName = "celebration"
            },
            new AcademicEventType
            {
                Name = "Award Ceremony",
                DefaultColor = "#673AB7",
                IconName = "emoji_events"
            },
            new AcademicEventType
            {
                Name = "Faculty Meeting",
                DefaultColor = "#3F51B5",
                IconName = "groups"
            },
            new AcademicEventType
            {
                Name = "Department Meeting",
                DefaultColor = "#3F51B5",
                IconName = "groups"
            },
            new AcademicEventType
            {
                Name = "Board Meeting",
                DefaultColor = "#3F51B5",
                IconName = "meeting_room"
            },
            new AcademicEventType
            {
                Name = "Senate Meeting",
                DefaultColor = "#3F51B5",
                IconName = "meeting_room"
            },
            new AcademicEventType
            {
                Name = "Workshop",
                DefaultColor = "#009688",
                IconName = "build"
            },
            new AcademicEventType
            {
                Name = "Training",
                DefaultColor = "#009688",
                IconName = "engineering"
            },
            new AcademicEventType
            {
                Name = "Seminar",
                DefaultColor = "#009688",
                IconName = "groups"
            },
            new AcademicEventType
            {
                Name = "Conference",
                DefaultColor = "#009688",
                IconName = "connect_without_contact"
            },
            new AcademicEventType
            {
                Name = "Club/Society Event",
                DefaultColor = "#FF5722",
                IconName = "diversity_3"
            },
            new AcademicEventType
            {
                Name = "Career Fair",
                DefaultColor = "#FF5722",
                IconName = "work"
            },
            new AcademicEventType
            {
                Name = "Student Government",
                DefaultColor = "#FF5722",
                IconName = "gavel"
            },
            new AcademicEventType
            {
                Name = "Social Event",
                DefaultColor = "#FF5722",
                IconName = "theater_comedy"
            },
            new AcademicEventType
            {
                Name = "Research Symposium",
                DefaultColor = "#795548",
                IconName = "biotech"
            },
            new AcademicEventType
            {
                Name = "Thesis Defense",
                DefaultColor = "#795548",
                IconName = "psychology"
            },
            new AcademicEventType
            {
                Name = "Guest Lecture",
                DefaultColor = "#795548",
                IconName = "record_voice_over"
            },
            new AcademicEventType
            {
                Name = "Research Presentation",
                DefaultColor = "#795548",
                IconName = "science"
            },
            new AcademicEventType
            {
                Name = "Enrollment Period",
                DefaultColor = "#607D8B",
                IconName = "app_registration"
            },
            new AcademicEventType
            {
                Name = "System Maintenance",
                DefaultColor = "#607D8B",
                IconName = "settings"
            },
            new AcademicEventType
            {
                Name = "Campus Closure",
                DefaultColor = "#607D8B",
                IconName = "do_not_disturb_on"
            },
            new AcademicEventType
            {
                Name = "Health Campaign",
                DefaultColor = "#8BC34A",
                IconName = "health_and_safety"
            },
            new AcademicEventType
            {
                Name = "Wellness Event",
                DefaultColor = "#8BC34A",
                IconName = "spa"
            },
            new AcademicEventType
            {
                Name = "Field Trip",
                DefaultColor = "#00BCD4",
                IconName = "hiking"
            },
            new AcademicEventType
            {
                Name = "Internship Period",
                DefaultColor = "#00BCD4",
                IconName = "business_center"
            },
            new AcademicEventType
            {
                Name = "Study Abroad",
                DefaultColor = "#00BCD4",
                IconName = "flight"
            },
            new AcademicEventType
            {
                Name = "Open Day",
                DefaultColor = "#E91E63",
                IconName = "door_open"
            },
            new AcademicEventType
            {
                Name = "Campus Tour",
                DefaultColor = "#E91E63",
                IconName = "tour"
            },
            new AcademicEventType
            {
                Name = "Admission Interview",
                DefaultColor = "#E91E63",
                IconName = "record_voice_over"
            },
            new AcademicEventType
            {
                Name = "Dormitory Check-in",
                DefaultColor = "#2196F3",
                IconName = "house"
            },
            new AcademicEventType
            {
                Name = "Dormitory Check-out",
                DefaultColor = "#2196F3",
                IconName = "no_meeting_room"
            }
        };

        dbContext.AcademicEventTypes.AddRange(academicEventTypes);
        await dbContext.SaveChangesAsync();
        Console.WriteLine("Academic event types seeded successfully.");
    }
}

app.Run();