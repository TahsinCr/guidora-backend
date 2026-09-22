using System.Collections.Generic;
using Core.Business;
using Core.Utilities.Results;
using Entities;
using Entities.DTOs;

namespace Business.Abstract
{
    public interface IProductService : IEntityManagerRepository<Product>
    {
        public IDataResult<List<Product>> GetAllByCategoryId(int id);
        public IDataResult<Product> GetById(int id);
        IResult Create(CreateProductRequest request, int createdByUserId);
        IResult Update(int productId, UpdateProductRequest request);
        IResult Delete(int productId);

    }
}
