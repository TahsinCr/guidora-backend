using Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DataAccess.Concrete.Entityframework.Configurations
{
    public sealed class CategoryConfiguration : IEntityTypeConfiguration<Category>
    {
        public void Configure(EntityTypeBuilder<Category> builder)
        {
            builder.HasKey(category => category.CategoryId);
            builder.Property(category => category.CategoryName).HasMaxLength(150).IsRequired();
            builder.Property(category => category.Slug).HasMaxLength(180).IsRequired();
            builder.HasIndex(category => category.Slug).IsUnique();
        }
    }

    public sealed class ProductConfiguration : IEntityTypeConfiguration<Product>
    {
        public void Configure(EntityTypeBuilder<Product> builder)
        {
            builder.HasKey(product => product.ProductId);
            builder.Property(product => product.Name).HasMaxLength(200).IsRequired();
            builder.Property(product => product.Slug).HasMaxLength(220).IsRequired();
            builder.Property(product => product.ShortDescription).HasMaxLength(500).IsRequired();
            builder.Property(product => product.WebsiteUrl).HasMaxLength(2048).IsRequired();
            builder.Property(product => product.LogoUrl).HasMaxLength(2048).IsRequired();
            builder.HasIndex(product => product.Slug).IsUnique();
            builder.HasIndex(product => product.ProductStatusId);
            builder.HasIndex(product => product.CreatedByUserId);
            builder.HasOne<ProductStatus>().WithMany().HasForeignKey(product => product.ProductStatusId)
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<Core.Entities.Concrete.User>().WithMany().HasForeignKey(product => product.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }

    public sealed class ProductStatusConfiguration : IEntityTypeConfiguration<ProductStatus>
    {
        public void Configure(EntityTypeBuilder<ProductStatus> builder)
        {
            builder.HasKey(status => status.Id);
            builder.Property(status => status.Code).HasMaxLength(80).IsRequired();
            builder.Property(status => status.Name).HasMaxLength(120).IsRequired();
            builder.HasIndex(status => status.Code).IsUnique();
        }
    }

    public sealed class TagCategoryConfiguration : IEntityTypeConfiguration<TagCategory>
    {
        public void Configure(EntityTypeBuilder<TagCategory> builder)
        {
            builder.HasKey(category => category.Id);
            builder.Property(category => category.Name).HasMaxLength(120).IsRequired();
            builder.Property(category => category.Slug).HasMaxLength(140).IsRequired();
            builder.HasIndex(category => category.Slug).IsUnique();
        }
    }

    public sealed class TagConfiguration : IEntityTypeConfiguration<Tag>
    {
        public void Configure(EntityTypeBuilder<Tag> builder)
        {
            builder.HasKey(tag => tag.Id);
            builder.Property(tag => tag.Name).HasMaxLength(120).IsRequired();
            builder.Property(tag => tag.Slug).HasMaxLength(140).IsRequired();
            builder.HasIndex(tag => new { tag.TagCategoryId, tag.Slug }).IsUnique();
            builder.HasOne<TagCategory>().WithMany().HasForeignKey(tag => tag.TagCategoryId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }

    public sealed class ProductCategoryConfiguration : IEntityTypeConfiguration<ProductCategory>
    {
        public void Configure(EntityTypeBuilder<ProductCategory> builder)
        {
            builder.HasKey(item => item.Id);
            builder.HasIndex(item => new { item.ProductId, item.CategoryId }).IsUnique();
            builder.HasOne<Product>().WithMany().HasForeignKey(item => item.ProductId)
                .OnDelete(DeleteBehavior.Cascade);
            builder.HasOne<Category>().WithMany().HasForeignKey(item => item.CategoryId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }

    public sealed class ProductTagConfiguration : IEntityTypeConfiguration<ProductTag>
    {
        public void Configure(EntityTypeBuilder<ProductTag> builder)
        {
            builder.HasKey(item => item.Id);
            builder.HasIndex(item => new { item.ProductId, item.TagId }).IsUnique();
            builder.HasOne<Product>().WithMany().HasForeignKey(item => item.ProductId)
                .OnDelete(DeleteBehavior.Cascade);
            builder.HasOne<Tag>().WithMany().HasForeignKey(item => item.TagId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
