using Core.DataAccess;
using Entities;
using System.Threading;
using System.Threading.Tasks;

namespace DataAccess.Abstract
{
    public interface ITagDal : IEntityRepository<Tag>
    {
        async Task<int> DeactivateByCategoryAsync(int tagCategoryId, CancellationToken cancellationToken = default)
        {
            var tags = await GetAllAsync(item => item.TagCategoryId == tagCategoryId && item.IsActive, cancellationToken);
            foreach (var tag in tags)
            {
                tag.IsActive = false;
                Update(tag);
            }
            return tags.Count;
        }
    }
}
