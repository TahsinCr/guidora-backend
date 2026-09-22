using Core.Entities.Concrete;
using Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DataAccess.Concrete.Entityframework.Configurations
{
    public sealed class ReportReasonConfiguration : IEntityTypeConfiguration<ReportReason>
    {
        public void Configure(EntityTypeBuilder<ReportReason> builder)
        {
            builder.HasKey(reason => reason.Id);
            builder.Property(reason => reason.Code).HasMaxLength(80).IsRequired();
            builder.Property(reason => reason.Name).HasMaxLength(150).IsRequired();
            builder.HasIndex(reason => reason.Code).IsUnique();
            builder.HasData(
                new ReportReason { Id = 1, Code = "spam", Name = "Spam", IsActive = true },
                new ReportReason { Id = 2, Code = "inappropriate", Name = "Inappropriate content", IsActive = true },
                new ReportReason { Id = 3, Code = "inaccurate", Name = "Inaccurate information", IsActive = true });
        }
    }

    public sealed class ReportStatusConfiguration : IEntityTypeConfiguration<ReportStatus>
    {
        public void Configure(EntityTypeBuilder<ReportStatus> builder)
        {
            builder.HasKey(status => status.Id);
            builder.Property(status => status.Code).HasMaxLength(80).IsRequired();
            builder.Property(status => status.Name).HasMaxLength(120).IsRequired();
            builder.HasIndex(status => status.Code).IsUnique();
            builder.HasData(
                new ReportStatus { Id = 1, Code = "open", Name = "Open" },
                new ReportStatus { Id = 2, Code = "resolved", Name = "Resolved" },
                new ReportStatus { Id = 3, Code = "dismissed", Name = "Dismissed" });
        }
    }

    public sealed class ProductReportConfiguration : IEntityTypeConfiguration<ProductReport>
    {
        public void Configure(EntityTypeBuilder<ProductReport> builder)
        {
            builder.HasKey(report => report.Id);
            builder.Property(report => report.Description).HasMaxLength(4000).IsRequired();
            builder.HasIndex(report => new { report.ProductId, report.ReportedByUserId, report.ReportStatusId });
            builder.HasOne<Product>().WithMany().HasForeignKey(report => report.ProductId)
                .OnDelete(DeleteBehavior.Cascade);
            builder.HasOne<User>().WithMany().HasForeignKey(report => report.ReportedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<ReportReason>().WithMany().HasForeignKey(report => report.ReportReasonId)
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<ReportStatus>().WithMany().HasForeignKey(report => report.ReportStatusId)
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<User>().WithMany().HasForeignKey(report => report.ResolvedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }

    public sealed class ProductContributorConfiguration : IEntityTypeConfiguration<ProductContributor>
    {
        public void Configure(EntityTypeBuilder<ProductContributor> builder)
        {
            builder.HasKey(contributor => contributor.Id);
            builder.Property(contributor => contributor.ContributionType).HasMaxLength(100).IsRequired();
            builder.HasIndex(contributor => new { contributor.ProductId, contributor.UserId }).IsUnique();
            builder.HasOne<Product>().WithMany().HasForeignKey(contributor => contributor.ProductId)
                .OnDelete(DeleteBehavior.Cascade);
            builder.HasOne<User>().WithMany().HasForeignKey(contributor => contributor.UserId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }

    public sealed class VerificationTypeConfiguration : IEntityTypeConfiguration<VerificationType>
    {
        public void Configure(EntityTypeBuilder<VerificationType> builder)
        {
            builder.HasKey(type => type.Id);
            builder.Property(type => type.Code).HasMaxLength(80).IsRequired();
            builder.Property(type => type.Name).HasMaxLength(120).IsRequired();
            builder.HasIndex(type => type.Code).IsUnique();
            builder.HasData(
                new VerificationType { Id = 1, Code = "admin", Name = "Admin verified" },
                new VerificationType { Id = 2, Code = "community", Name = "Community verified" },
                new VerificationType { Id = 3, Code = "bot", Name = "Bot verified" });
        }
    }

    public sealed class ProductVerificationConfiguration : IEntityTypeConfiguration<ProductVerification>
    {
        public void Configure(EntityTypeBuilder<ProductVerification> builder)
        {
            builder.HasKey(verification => verification.Id);
            builder.Property(verification => verification.Note).HasMaxLength(4000).IsRequired();
            builder.HasIndex(verification => new { verification.ProductId, verification.VerificationTypeId })
                .IsUnique();
            builder.HasOne<Product>().WithMany().HasForeignKey(verification => verification.ProductId)
                .OnDelete(DeleteBehavior.Cascade);
            builder.HasOne<VerificationType>().WithMany().HasForeignKey(verification => verification.VerificationTypeId)
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<User>().WithMany().HasForeignKey(verification => verification.VerifiedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }

    public sealed class ProductEditProposalConfiguration : IEntityTypeConfiguration<ProductEditProposal>
    {
        public void Configure(EntityTypeBuilder<ProductEditProposal> builder)
        {
            builder.HasKey(proposal => proposal.Id);
            builder.Property(proposal => proposal.Status).HasConversion<string>().HasMaxLength(80).IsRequired();
            builder.Property(proposal => proposal.Name).HasMaxLength(200).IsRequired();
            builder.Property(proposal => proposal.Slug).HasMaxLength(220).IsRequired();
            builder.Property(proposal => proposal.WebsiteUrl).HasMaxLength(2048).IsRequired();
            builder.Property(proposal => proposal.LogoUrl).HasMaxLength(2048).IsRequired();
            builder.Property(proposal => proposal.ReviewNote).HasMaxLength(4000).IsRequired();
            builder.HasIndex(proposal => new { proposal.ProductId, proposal.Status });
            builder.HasOne<Product>().WithMany().HasForeignKey(proposal => proposal.ProductId)
                .OnDelete(DeleteBehavior.Cascade);
            builder.HasOne<User>().WithMany().HasForeignKey(proposal => proposal.ProposedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<User>().WithMany().HasForeignKey(proposal => proposal.ReviewedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }

    public sealed class ProductEditProposalTagConfiguration : IEntityTypeConfiguration<ProductEditProposalTag>
    {
        public void Configure(EntityTypeBuilder<ProductEditProposalTag> builder)
        {
            builder.HasKey(item => item.Id);
            builder.HasIndex(item => new { item.ProductEditProposalId, item.TagId }).IsUnique();
            builder.HasOne<ProductEditProposal>().WithMany().HasForeignKey(item => item.ProductEditProposalId)
                .OnDelete(DeleteBehavior.Cascade);
            builder.HasOne<Tag>().WithMany().HasForeignKey(item => item.TagId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }

    public sealed class ProductEditProposalCategoryConfiguration : IEntityTypeConfiguration<ProductEditProposalCategory>
    {
        public void Configure(EntityTypeBuilder<ProductEditProposalCategory> builder)
        {
            builder.HasKey(item => item.Id);
            builder.HasIndex(item => new { item.ProductEditProposalId, item.CategoryId }).IsUnique();
            builder.HasOne<ProductEditProposal>().WithMany().HasForeignKey(item => item.ProductEditProposalId)
                .OnDelete(DeleteBehavior.Cascade);
            builder.HasOne<Category>().WithMany().HasForeignKey(item => item.CategoryId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }

    public sealed class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
    {
        public void Configure(EntityTypeBuilder<AuditLog> builder)
        {
            builder.HasKey(log => log.Id);
            builder.Property(log => log.EntityType).HasMaxLength(150).IsRequired();
            builder.Property(log => log.EntityId).HasMaxLength(100).IsRequired();
            builder.Property(log => log.Action).HasMaxLength(100).IsRequired();
            builder.HasIndex(log => new { log.EntityType, log.EntityId, log.CreatedAt });
            builder.HasOne<User>().WithMany().HasForeignKey(log => log.ActorUserId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
