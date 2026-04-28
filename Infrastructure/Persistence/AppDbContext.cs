using Microsoft.EntityFrameworkCore;
using SPRMS.API.Domain.Entities;
using SPRMS.Common;

namespace SPRMS.API.Infrastructure.Persistence
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public AppDbContext(DbContextOptions<AppDbContext> options, ICurrentUser currentUser, AuditInterceptor auditInterceptor) : base(options)
        {
            // Add interceptors if needed
        }

        public DbSet<ScholarshipApplication> ScholarshipApplications { get; set; }
        public DbSet<StudentProfile> StudentProfiles { get; set; }
        public DbSet<ApplicationDocument> ApplicationDocuments { get; set; }
        public DbSet<ScholarshipProgram> ScholarshipPrograms { get; set; }
        public DbSet<User> Users { get; set; }
        public DbSet<Role> Roles { get; set; }
        public DbSet<SystemConfiguration> SystemConfigurations { get; set; }
        public DbSet<ProgressReport> ProgressReports { get; set; }
        public DbSet<Notification> Notifications { get; set; }
        public DbSet<UserRole> UserRoles { get; set; }
        public DbSet<Scholarship> Scholarships { get; set; }
        public DbSet<RefundLedger> RefundLedgers { get; set; }
        public DbSet<FundingSource> FundingSources { get; set; }
        public DbSet<PaymentRequest> PaymentRequests { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ScholarshipApplication>()
                .ToTable("ScholarshipApplications")
                .HasKey(x => x.ApplicationID);

            modelBuilder.Entity<StudentProfile>()
                .ToTable("StudentProfiles")
                .HasKey(x => x.StudentProfileID);

            modelBuilder.Entity<StudentProfile>()
                .HasOne(x => x.Application)
                .WithMany()
                .HasForeignKey(x => x.ApplicationID);

            modelBuilder.Entity<ApplicationDocument>()
                .ToTable("ApplicationDocuments")
                .HasKey(x => x.DocumentID);

            modelBuilder.Entity<ApplicationDocument>()
                .HasOne(x => x.Application)
                .WithMany()
                .HasForeignKey(x => x.ApplicationID);
        }
    }
}