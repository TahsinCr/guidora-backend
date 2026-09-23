using System.Collections.Generic;
using Core.Utilities.Results;
using Entities;
using Entities.DTOs;
using System.Threading;
using System.Threading.Tasks;

namespace Business.Abstract
{
    public interface IProductService
    {
        IDataResult<List<Product>> GetAll();
        public IDataResult<List<Product>> GetAllByCategoryId(int id);
        public IDataResult<Product> GetById(int id);
        Task<IResult> CreateAsync(CreateProductRequest request, int createdByUserId,
            CancellationToken cancellationToken = default);
        Task<IResult> UpdateAsync(int productId, UpdateProductRequest request, int updatedByUserId,
            CancellationToken cancellationToken = default);
        Task<IResult> DeleteAsync(int productId, int deletedByUserId,
            CancellationToken cancellationToken = default);

    }
}
