using Core.Entities.Concrete;
using Entities;
using Microsoft.EntityFrameworkCore;


namespace DataAccess.Concrete.Entityframework.Contexts
{
    public class NorthwindContext : DbContext
    {
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseSqlServer(@"
                Server=(localdb)\mssqllocaldb;
                Database=Northwind;
                Trusted_Connection=true;
            ");
        }

        public DbSet<Product> Products { get; set; }
        public DbSet<Category> Categories { get; set; }
        public DbSet<OperationClaim> OperationClaims { get; set; }
        public DbSet<User> Users { get; set; }
        public DbSet<UserOperationClaim> UserOperationClaims { get; set; }
        public DbSet<UserProfile> UserProfiles { get; set; }
        public DbSet<Role> Roles { get; set; }
        public DbSet<UserRole> UserRoles { get; set; }
        public DbSet<RoleOperationClaim> RoleOperationClaims { get; set; }
        public DbSet<ProductStatus> ProductStatuses { get; set; }
        public DbSet<TagCategory> TagCategories { get; set; }
        public DbSet<Tag> Tags { get; set; }
        public DbSet<ProductCategory> ProductCategories { get; set; }
        public DbSet<ProductTag> ProductTags { get; set; }
        public DbSet<ProductDocument> ProductDocuments { get; set; }
        public DbSet<ProductMedia> ProductMedia { get; set; }
        public DbSet<ProductChangelog> ProductChangelogs { get; set; }
        public DbSet<CommentStatus> CommentStatuses { get; set; }
        public DbSet<Comment> Comments { get; set; }
        public DbSet<Favorite> Favorites { get; set; }
        public DbSet<ReportReason> ReportReasons { get; set; }
        public DbSet<ReportStatus> ReportStatuses { get; set; }
        public DbSet<ProductReport> ProductReports { get; set; }
        public DbSet<ProductContributor> ProductContributors { get; set; }
        public DbSet<VerificationType> VerificationTypes { get; set; }
        public DbSet<ProductVerification> ProductVerifications { get; set; }
        public DbSet<ProductEditProposal> ProductEditProposals { get; set; }
        public DbSet<ProductEditProposalTag> ProductEditProposalTags { get; set; }
        public DbSet<ProductEditProposalCategory> ProductEditProposalCategories { get; set; }
        public DbSet<AuditLog> AuditLogs { get; set; }


    }
}
