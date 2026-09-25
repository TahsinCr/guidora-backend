using Core.DataAccess.EntityFramework;
using DataAccess.Abstract;
using DataAccess.Concrete.Entityframework.Contexts;
using Entities;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace DataAccess.Concrete.Entityframework
{
    public class EfProductTagDal : EfEntityPurgeRepositoryBase<ProductTag, GuidoraDbContext>, IProductTagDal
    {
        public EfProductTagDal(GuidoraDbContext context) : base(context) { }

        public Task<bool> HasOtherTagInSameCategoryForAssignedProductsAsync(int tagId, int tagCategoryId,
            CancellationToken cancellationToken = default) => Context.ProductTags.AsNoTracking()
            .Where(assignment => assignment.TagId == tagId)
            .AnyAsync(assignment => Context.ProductTags.AsNoTracking().Any(otherAssignment =>
                otherAssignment.ProductId == assignment.ProductId && otherAssignment.TagId != tagId &&
                Context.Tags.Any(tag => tag.Id == otherAssignment.TagId && tag.TagCategoryId == tagCategoryId)),
                cancellationToken);

        public Task<bool> HasProductsWithMultipleTagsInCategoryAsync(int tagCategoryId,
            CancellationToken cancellationToken = default) => Context.ProductTags.AsNoTracking()
            .Where(assignment => Context.Tags.Any(tag => tag.Id == assignment.TagId && tag.TagCategoryId == tagCategoryId))
            .GroupBy(assignment => assignment.ProductId)
            .AnyAsync(group => group.Select(assignment => assignment.TagId).Distinct().Count() > 1,
                cancellationToken);
    }
}
