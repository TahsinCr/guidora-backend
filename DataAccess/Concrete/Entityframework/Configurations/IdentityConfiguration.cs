using Core.Entities.Concrete;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DataAccess.Concrete.Entityframework.Configurations
{
    public sealed class UserConfiguration : IEntityTypeConfiguration<User>
    {
        public void Configure(EntityTypeBuilder<User> builder)
        {
            builder.HasKey(user => user.Id);
            builder.Property(user => user.Email).HasColumnType("citext").HasMaxLength(320).IsRequired();
            builder.HasIndex(user => user.Email).IsUnique();
            builder.Property(user => user.FirstName).HasMaxLength(100).IsRequired();
            builder.Property(user => user.LastName).HasMaxLength(100).IsRequired();
            builder.Property(user => user.PasswordHash).IsRequired();
            builder.Property(user => user.PasswordSalt).IsRequired();
        }
    }

    public sealed class OperationClaimConfiguration : IEntityTypeConfiguration<OperationClaim>
    {
        public void Configure(EntityTypeBuilder<OperationClaim> builder)
        {
            builder.HasKey(claim => claim.Id);
            builder.Property(claim => claim.Name).HasMaxLength(150).IsRequired();
            builder.HasIndex(claim => claim.Name).IsUnique();
            builder.HasData(
                new OperationClaim { Id = 1, Name = "Product.Create" },
                new OperationClaim { Id = 2, Name = "Product.Update" },
                new OperationClaim { Id = 3, Name = "Product.Delete" },
                new OperationClaim { Id = 4, Name = "Category.Manage" },
                new OperationClaim { Id = 5, Name = "Product.Verify" },
                new OperationClaim { Id = 6, Name = "Product.Proposal.Review" });
        }
    }

    public sealed class RoleConfiguration : IEntityTypeConfiguration<Role>
    {
        public void Configure(EntityTypeBuilder<Role> builder)
        {
            builder.HasKey(role => role.Id);
            builder.Property(role => role.Name).HasMaxLength(100).IsRequired();
            builder.Property(role => role.Code).HasMaxLength(100).IsRequired();
            builder.HasIndex(role => role.Code).IsUnique();
            builder.HasData(
                new Role { Id = 1, Name = "User", Code = "user", IsActive = true },
                new Role { Id = 2, Name = "Moderator", Code = "moderator", IsActive = true },
                new Role { Id = 3, Name = "Admin", Code = "admin", IsActive = true });
        }
    }

    public sealed class UserProfileConfiguration : IEntityTypeConfiguration<UserProfile>
    {
        public void Configure(EntityTypeBuilder<UserProfile> builder)
        {
            builder.HasKey(profile => profile.Id);
            builder.Property(profile => profile.DisplayName).HasMaxLength(100).IsRequired();
            builder.Property(profile => profile.Name).HasMaxLength(200).IsRequired();
            builder.Property(profile => profile.Description).HasMaxLength(4000).IsRequired();
            builder.Property(profile => profile.AvatarUrl).HasMaxLength(2048).IsRequired();
            builder.Property(profile => profile.PhoneNumber).HasMaxLength(32).IsRequired();
            builder.Property(profile => profile.WebsiteUrl).HasMaxLength(2048).IsRequired();
            builder.Property(profile => profile.Country).HasMaxLength(100).IsRequired();
            builder.Property(profile => profile.City).HasMaxLength(100).IsRequired();
            builder.HasIndex(profile => profile.UserId).IsUnique();
            builder.HasOne<User>().WithMany().HasForeignKey(profile => profile.UserId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }

    public sealed class UserOperationClaimConfiguration : IEntityTypeConfiguration<UserOperationClaim>
    {
        public void Configure(EntityTypeBuilder<UserOperationClaim> builder)
        {
            builder.HasKey(item => item.Id);
            builder.HasIndex(item => new { item.UserId, item.OperationClaimId }).IsUnique();
            builder.HasOne<User>().WithMany().HasForeignKey(item => item.UserId)
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<OperationClaim>().WithMany().HasForeignKey(item => item.OperationClaimId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }

    public sealed class UserRoleConfiguration : IEntityTypeConfiguration<UserRole>
    {
        public void Configure(EntityTypeBuilder<UserRole> builder)
        {
            builder.HasKey(item => item.Id);
            builder.HasIndex(item => new { item.UserId, item.RoleId }).IsUnique();
            builder.HasOne<User>().WithMany().HasForeignKey(item => item.UserId)
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<Role>().WithMany().HasForeignKey(item => item.RoleId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }

    public sealed class RoleOperationClaimConfiguration : IEntityTypeConfiguration<RoleOperationClaim>
    {
        public void Configure(EntityTypeBuilder<RoleOperationClaim> builder)
        {
            builder.HasKey(item => item.Id);
            builder.HasIndex(item => new { item.RoleId, item.OperationClaimId }).IsUnique();
            builder.HasOne<Role>().WithMany().HasForeignKey(item => item.RoleId)
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<OperationClaim>().WithMany().HasForeignKey(item => item.OperationClaimId)
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasData(
                new RoleOperationClaim { Id = 1, RoleId = 1, OperationClaimId = 1 },
                new RoleOperationClaim { Id = 2, RoleId = 2, OperationClaimId = 2 },
                new RoleOperationClaim { Id = 3, RoleId = 2, OperationClaimId = 5 },
                new RoleOperationClaim { Id = 4, RoleId = 2, OperationClaimId = 6 },
                new RoleOperationClaim { Id = 5, RoleId = 3, OperationClaimId = 1 },
                new RoleOperationClaim { Id = 6, RoleId = 3, OperationClaimId = 2 },
                new RoleOperationClaim { Id = 7, RoleId = 3, OperationClaimId = 3 },
                new RoleOperationClaim { Id = 8, RoleId = 3, OperationClaimId = 4 },
                new RoleOperationClaim { Id = 9, RoleId = 3, OperationClaimId = 5 },
                new RoleOperationClaim { Id = 10, RoleId = 3, OperationClaimId = 6 });
        }
    }
}
