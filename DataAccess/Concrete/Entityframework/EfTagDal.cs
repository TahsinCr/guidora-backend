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
    public class EfTagDal : EfEntityRepositoryBase<Tag, GuidoraDbContext>, ITagDal
    {
        public EfTagDal(GuidoraDbContext context) : base(context) { }

        public Task<int> DeactivateByCategoryAsync(int tagCategoryId, CancellationToken cancellationToken = default) =>
            Context.Tags.Where(tag => tag.TagCategoryId == tagCategoryId && tag.IsActive)
                .ExecuteUpdateAsync(setters => setters.SetProperty(tag => tag.IsActive, false), cancellationToken);
    }
}
