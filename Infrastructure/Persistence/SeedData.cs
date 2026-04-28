using SPRMS.API.Domain.Entities;
using SPRMS.API.Infrastructure.Persistence;
using SPRMS.Services.Domain;
using Microsoft.EntityFrameworkCore;
using SPRMS.Domain.Enums;

namespace SPRMS.Services;

/// <summary>
/// Database seeding utility for test data in development environment.
/// Creates sample users, roles, and test data.
/// </summary>
public static class SeedData
{
    /// <summary>
    /// Seed the database with initial test data.
    /// Call this during application startup in development.
    /// </summary>
    public static async Task SeedDatabaseAsync(WebApplication app)
    {
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var pwdSvc = scope.ServiceProvider.GetRequiredService<IPasswordService>();

            // Ensure database is created
            await db.Database.MigrateAsync();

            // Seed roles if they don't exist
            await SeedRolesAsync(db);

            // Seed test users
            await SeedUsersAsync(db, pwdSvc);

            // Seed funding sources first (required FK)
            await SeedFundingSourcesAsync(db);
            
            // Seed test scholarship programs  
            await SeedScholarshipProgramsAsync(db);
        }
    }


    /// <summary>
    /// Seed application roles (Admin, User, Finance, Evaluator).
    /// </summary>
    private static async Task SeedRolesAsync(AppDbContext db)
    {
        var roleNames = new[] { "Admin", "User", "Finance", "Evaluator" };

        foreach (var roleName in roleNames)
        {
            var roleExists = await db.Roles.AnyAsync(r => r.RoleName == roleName);
            if (!roleExists)
            {
                var description = roleName switch
                {
                    "Admin" => "System administrator with full access",
                    "User" => "Regular user (student/applicant)",
                    "Finance" => "Finance team member (payment processing)",
                    "Evaluator" => "Scholarship evaluator (can score applications)",
                    _ => ""
                };

                db.Roles.Add(new Role
                {
                    RoleName = roleName,
                    Description = description
                });
            }
        }

        await db.SaveChangesAsync();
    }

    /// <summary>
    /// Seed test user accounts.
    /// Creates 2 test accounts: Admin and Student.
    /// </summary>
    private static async Task SeedUsersAsync(AppDbContext db, IPasswordService pwdSvc)
    {
        // Test Admin User
        var adminCID = "11001000001";
        var adminExists = await db.Users.AnyAsync(u => u.CIDNumber == adminCID);

        if (!adminExists)
        {
            var (adminHash, adminSalt) = await pwdSvc.HashPasswordAsync("Admin@123");

                var adminUser = new User
                {
                    CIDNumber = adminCID,
                    FullName = "Dorji Penzang",
                    Email = "admin@rcsc.gov.bt",
                    Phone = "+975-2-326288",
                    PasswordHash = adminHash,
                    PasswordSalt = adminSalt,
                    NDISubjectID = Guid.NewGuid().ToString(), 
                    TwoFAEnabled = false,
                    FailedLoginCount = 0,
                    Status = Status.Active,
                    IsActive = true,
                    CreatedBy = "system",
                    CreatedOn = DateTime.UtcNow
                };

            db.Users.Add(adminUser);
            await db.SaveChangesAsync();

            // Assign Admin role
            var adminRole = await db.Roles.FirstOrDefaultAsync(r => r.RoleName == "Admin");
            if (adminRole != null)
            {
                db.UserRoles.Add(new UserRole
                {
                    UserID = adminUser.UserID,
                    RoleID = adminRole.RoleID,
                    AssignedBy = 0,
                    AssignedOn = DateTime.UtcNow
                });

                // Also assign Finance role
                var financeRole = await db.Roles.FirstOrDefaultAsync(r => r.RoleName == "Finance");
                if (financeRole != null)
                {
                    db.UserRoles.Add(new UserRole
                    {
                        UserID = adminUser.UserID,
                        RoleID = financeRole.RoleID,
                        AssignedBy = 0,
                        AssignedOn = DateTime.UtcNow
                    });
                }

                await db.SaveChangesAsync();
            }
        }

        // Test Student User
        var studentCID = "11002000001";
        var studentExists = await db.Users.AnyAsync(u => u.CIDNumber == studentCID);

        if (!studentExists)
        {
            var (studentHash, studentSalt) = await pwdSvc.HashPasswordAsync("Student@123");

            var studentUser = new User
            {
                CIDNumber = studentCID,
                FullName = "Tshering Dorji",
                Email = "tshering.student@example.bt",
                Phone = "+975-77-123456",
                PasswordHash = studentHash,
                PasswordSalt = studentSalt,
                NDISubjectID = Guid.NewGuid().ToString(), 
                TwoFAEnabled = false,
                FailedLoginCount = 0,
                Status = Status.Active,
                IsActive = true,
                CreatedBy = "system",
                CreatedOn = DateTime.UtcNow
            };

            db.Users.Add(studentUser);
            await db.SaveChangesAsync();

            // Assign User role
            var userRole = await db.Roles.FirstOrDefaultAsync(r => r.RoleName == "User");
            if (userRole != null)
            {
                db.UserRoles.Add(new UserRole
                {
                    UserID = studentUser.UserID,
                    RoleID = userRole.RoleID,
                    AssignedBy = 0,
                    AssignedOn = DateTime.UtcNow
                });

                await db.SaveChangesAsync();
            }

            // Create student profile
            var studentProfile = new StudentProfile
            {
                UserID = studentUser.UserID,
                CreatedOn = DateTime.UtcNow
            };

            db.StudentProfiles.Add(studentProfile);
            await db.SaveChangesAsync();
        }
    }

    /// <summary>
    /// Seed sample scholarship programs for testing.
    /// </summary>
    /// <summary>
    /// Seed sample funding sources first (required for ScholarshipPrograms FK)
    /// </summary>
    private static async Task SeedFundingSourcesAsync(AppDbContext db)
    {
        var sources = new[]
        {
            new FundingSource
            {
                SourceName = "RCSC General Scholarship Fund",
                Description = "Main funding pool for merit and need-based scholarships",
                IsActive = true
            },
            new FundingSource
            {
                SourceName = "Royal Scholarship Fund",
                Description = "HRH-funded scholarships for excellence",
                IsActive = true
            },
            new FundingSource
            {
                SourceName = "In-Service Training Fund",
                Description = "Funding for government employee advanced training",
                IsActive = true
            }
        };

        var existingSources = await db.FundingSources
            .IgnoreQueryFilters()
            .ToDictionaryAsync(x => x.SourceName, StringComparer.OrdinalIgnoreCase);

        var changed = false;
        foreach (var source in sources)
        {
            if (existingSources.TryGetValue(source.SourceName, out var existing))
            {
                if (!existing.IsActive || existing.Description != source.Description)
                {
                    existing.IsActive = true;
                    existing.Description = source.Description;
                    changed = true;
                }

                continue;
            }

            db.FundingSources.Add(source);
            changed = true;
        }

        if (changed)
        {
            await db.SaveChangesAsync();
        }
    }

    /// <summary>
    /// Seed sample scholarship programs (now with valid FundingSourceID).
    /// Uses FundingSourceID = 1 (RCSC General Fund).
    /// </summary>
    private static async Task SeedScholarshipProgramsAsync(AppDbContext db)
    {
        var fundingSourceIds = await db.FundingSources
            .IgnoreQueryFilters()
            .Where(x => x.IsActive)
            .ToDictionaryAsync(x => x.SourceName, x => x.FundingSourceID, StringComparer.OrdinalIgnoreCase);

        if (!fundingSourceIds.TryGetValue("RCSC General Scholarship Fund", out var generalFundId) ||
            !fundingSourceIds.TryGetValue("Royal Scholarship Fund", out var royalFundId))
        {
            throw new InvalidOperationException(
                "Required funding sources were not found before scholarship program seeding.");
        }

        var programs = new[]
        {
            new ScholarshipProgram
            {
                FundingSourceID = generalFundId,
                ProgramName = "Merit Excellence Scholarship 2024",
                Description = "For high-performing students with excellent academic records and demonstrated leadership",
                ApplicationDeadline = DateTime.UtcNow.AddMonths(2),
                StartDate = DateTime.UtcNow.AddMonths(3),
                EndDate = DateTime.UtcNow.AddMonths(15),
                MaxApplications = 50,
                AwardPerStudent = 50000,
                TotalAward = 2500000,
                MinGPA = 3.5m,
                TakenApplications = 0,
                Status = Status.Active
            },
            new ScholarshipProgram
            {
                FundingSourceID = generalFundId,
                ProgramName = "Financial Need Scholarship",
                Description = "For deserving students facing financial hardship",
                ApplicationDeadline = DateTime.UtcNow.AddMonths(2),
                StartDate = DateTime.UtcNow.AddMonths(3),
                EndDate = DateTime.UtcNow.AddMonths(12),
                MaxApplications = 100,
                AwardPerStudent = 30000,
                TotalAward = 3000000,
                MinGPA = 2.5m,
                TakenApplications = 0,
                Status = Status.Active
            },
            new ScholarshipProgram
            {
                FundingSourceID = royalFundId,
                ProgramName = "Science & Technology Excellence",
                Description = "For students excelling in STEM fields to pursue higher education",
                ApplicationDeadline = DateTime.UtcNow.AddMonths(1),
                StartDate = DateTime.UtcNow.AddMonths(2),
                EndDate = DateTime.UtcNow.AddMonths(18),
                MaxApplications = 30,
                AwardPerStudent = 75000,
                TotalAward = 2250000,
                MinGPA = 3.7m,
                TakenApplications = 0,
                Status = Status.Active
            },
            new ScholarshipProgram
            {
                FundingSourceID = generalFundId,
                ProgramName = "Disability Support Scholarship",
                Description = "For qualified individuals with disabilities pursuing education",
                ApplicationDeadline = DateTime.UtcNow.AddMonths(3),
                StartDate = DateTime.UtcNow.AddMonths(4),
                EndDate = DateTime.UtcNow.AddMonths(12),
                MaxApplications = 20,
                AwardPerStudent = 40000,
                TotalAward = 800000,
                MinGPA = 2.0m,
                TakenApplications = 0,
                Status = Status.Active
            }
        };

        var existingProgramNames = await db.ScholarshipPrograms
            .IgnoreQueryFilters()
            .Select(x => x.ProgramName)
            .ToListAsync();

        var existingNames = new HashSet<string>(existingProgramNames, StringComparer.OrdinalIgnoreCase);
        var newPrograms = programs
            .Where(x => !existingNames.Contains(x.ProgramName))
            .ToArray();

        if (newPrograms.Length > 0)
        {
            db.ScholarshipPrograms.AddRange(newPrograms);
            await db.SaveChangesAsync();
        }
    }

}
