using Core.Entities.Concrete;
using Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DataAccess.Concrete.Entityframework.Configurations
{
    public sealed class ProductDocumentConfiguration : IEntityTypeConfiguration<ProductDocument>
    {
        public void Configure(EntityTypeBuilder<ProductDocument> builder)
        {
            builder.HasKey(document => document.Id);
            builder.Property(document => document.DocumentType).HasMaxLength(80).IsRequired();
            builder.Property(document => document.Title).HasMaxLength(250).IsRequired();
            builder.HasIndex(document => new { document.ProductId, document.DocumentType });
            builder.HasOne<Product>().WithMany().HasForeignKey(document => document.ProductId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }

    public sealed class ProductMediaConfiguration : IEntityTypeConfiguration<ProductMedia>
    {
        public void Configure(EntityTypeBuilder<ProductMedia> builder)
        {
            builder.HasKey(media => media.Id);
            builder.Property(media => media.MediaType).HasMaxLength(50).IsRequired();
            builder.Property(media => media.Url).HasMaxLength(2048).IsRequired();
            builder.Property(media => media.AltText).HasMaxLength(500).IsRequired();
            builder.HasIndex(media => new { media.ProductId, media.DisplayOrder });
            builder.HasOne<Product>().WithMany().HasForeignKey(media => media.ProductId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }

    public sealed class ProductChangelogConfiguration : IEntityTypeConfiguration<ProductChangelog>
    {
        public void Configure(EntityTypeBuilder<ProductChangelog> builder)
        {
            builder.HasKey(changelog => changelog.Id);
            builder.Property(changelog => changelog.Version).HasMaxLength(80).IsRequired();
            builder.Property(changelog => changelog.Title).HasMaxLength(250).IsRequired();
            builder.HasIndex(changelog => new { changelog.ProductId, changelog.Version }).IsUnique();
            builder.HasOne<Product>().WithMany().HasForeignKey(changelog => changelog.ProductId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }

    public sealed class CommentStatusConfiguration : IEntityTypeConfiguration<CommentStatus>
    {
        public void Configure(EntityTypeBuilder<CommentStatus> builder)
        {
            builder.HasKey(status => status.Id);
            builder.Property(status => status.Code).HasMaxLength(80).IsRequired();
            builder.Property(status => status.Name).HasMaxLength(120).IsRequired();
            builder.HasIndex(status => status.Code).IsUnique();
            builder.HasData(
                new CommentStatus { Id = 1, Code = "active", Name = "Active", IsVisible = true },
                new CommentStatus { Id = 2, Code = "hidden", Name = "Hidden", IsVisible = false },
                new CommentStatus { Id = 3, Code = "deleted", Name = "Deleted", IsVisible = false },
                new CommentStatus { Id = 4, Code = "blocked", Name = "Blocked", IsVisible = false });
        }
    }

    public sealed class CommentConfiguration : IEntityTypeConfiguration<Comment>
    {
        public void Configure(EntityTypeBuilder<Comment> builder)
        {
            builder.HasKey(comment => comment.Id);
            builder.Property(comment => comment.Content).HasMaxLength(10000).IsRequired();
            builder.HasIndex(comment => new { comment.ProductId, comment.CreatedAt });
            builder.HasOne<Product>().WithMany().HasForeignKey(comment => comment.ProductId)
                .OnDelete(DeleteBehavior.Cascade);
            builder.HasOne<User>().WithMany().HasForeignKey(comment => comment.UserId)
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<CommentStatus>().WithMany().HasForeignKey(comment => comment.CommentStatusId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }

    public sealed class FavoriteConfiguration : IEntityTypeConfiguration<Favorite>
    {
        public void Configure(EntityTypeBuilder<Favorite> builder)
        {
            builder.HasKey(favorite => favorite.Id);
            builder.HasIndex(favorite => new { favorite.UserId, favorite.ProductId }).IsUnique();
            builder.HasIndex(favorite => new { favorite.UserId, favorite.CreatedAt });
            builder.HasOne<User>().WithMany().HasForeignKey(favorite => favorite.UserId)
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<Product>().WithMany().HasForeignKey(favorite => favorite.ProductId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
