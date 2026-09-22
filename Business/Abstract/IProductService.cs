using System.Collections.Generic;
using Core.Utilities.Results;
using Entities;
using Entities.DTOs;

namespace Business.Abstract
{
    public interface IProductService
    {
        IDataResult<List<Product>> GetAll();
        public IDataResult<List<Product>> GetAllByCategoryId(int id);
        public IDataResult<Product> GetById(int id);
        IResult Create(CreateProductRequest request, int createdByUserId);
        IResult Update(int productId, UpdateProductRequest request);
        IResult Delete(int productId);

    }
}
