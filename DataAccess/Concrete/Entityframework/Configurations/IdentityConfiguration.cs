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
            builder.Property(user => user.Email).HasMaxLength(320).IsRequired();
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
        }
    }

    public sealed class UserProfileConfiguration : IEntityTypeConfiguration<UserProfile>
    {
        public void Configure(EntityTypeBuilder<UserProfile> builder)
        {
            builder.HasKey(profile => profile.Id);
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
        }
    }
}
