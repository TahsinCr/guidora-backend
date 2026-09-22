using Core.Entities.Concrete;
using Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DataAccess.Concrete.Entityframework.Configurations
{
    public sealed class UserAuthenticationStateConfiguration : IEntityTypeConfiguration<UserAuthenticationState>
    {
        public void Configure(EntityTypeBuilder<UserAuthenticationState> builder)
        {
            builder.HasKey(state => state.Id);
            builder.HasIndex(state => state.UserId).IsUnique();
            builder.HasOne<User>().WithMany().HasForeignKey(state => state.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }

    public sealed class UserSessionConfiguration : IEntityTypeConfiguration<UserSession>
    {
        public void Configure(EntityTypeBuilder<UserSession> builder)
        {
            builder.HasKey(session => session.Id);
            builder.Property(session => session.RefreshTokenHash).HasMaxLength(128).IsRequired();
            builder.Property(session => session.DeviceName).HasMaxLength(250).IsRequired();
            builder.HasIndex(session => session.RefreshTokenHash).IsUnique();
            builder.HasIndex(session => new { session.UserId, session.RevokedAt, session.ExpiresAt });
            builder.HasOne<User>().WithMany().HasForeignKey(session => session.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
