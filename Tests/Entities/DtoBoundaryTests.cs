using Entities;
using Entities.DTOs;

namespace Tests.Entities;

public sealed class DtoBoundaryTests
{
    [Fact]
    public void CategoryWriteDtoDoesNotExposePersistenceOrLifecycleFields()
    {
        var propertyNames = typeof(CategoryRequest).GetProperties().Select(property => property.Name).ToHashSet();

        Assert.Equal(new HashSet<string> { "CategoryName", "Slug", "Description" }, propertyNames);
        Assert.DoesNotContain(nameof(Category.CategoryId), propertyNames);
        Assert.DoesNotContain(nameof(Category.IsActive), propertyNames);
        Assert.DoesNotContain(nameof(Category.CreatedAt), propertyNames);
    }
}
