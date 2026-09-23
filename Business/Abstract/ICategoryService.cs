using Core.Utilities.Results;
using Entities;
using Entities.DTOs;
using System.Collections.Generic;

namespace Business.Abstract
{
    public interface ICategoryService
    {
        IDataResult<List<Category>> GetAll();
        IDataResult<Category> GetById(int id);
        IResult Create(CategoryRequest request);
        IResult Update(int categoryId, CategoryRequest request);
        IResult Deactivate(int categoryId);
    }
}
