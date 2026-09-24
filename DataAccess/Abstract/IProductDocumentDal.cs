using Core.DataAccess;
using Entities;

namespace DataAccess.Abstract
{
    public interface IProductDocumentDal : IEntityRepository<ProductDocument>, IEntityPurgeRepository<ProductDocument>
    {
    }
}
