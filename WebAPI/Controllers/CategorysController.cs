using Business.Abstract;
using Entities.DTOs;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using WebAPI.Utilities;

namespace WebAPI.Controllers
{
    [Route("api/categories")]
    [ApiController]
    public class CategorysController : ControllerBase
    {
        private ICategoryService _categoryService;

        public CategorysController(ICategoryService categoryService)
        {
            _categoryService = categoryService;
        }

        [HttpPost]
        public async Task<IActionResult> Add(CategoryRequest request, CancellationToken cancellationToken)
        {
            if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var actorId))
                return Unauthorized();
            var result = await _categoryService.CreateAsync(request, actorId, cancellationToken);
            return this.ToHttpResult(result);
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
        {
            if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var actorId))
                return Unauthorized();
            var result = await _categoryService.DeactivateAsync(id, actorId, cancellationToken);
            return this.ToHttpResult(result);
        }

        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id, CancellationToken cancellationToken)
        {
            var result = await _categoryService.GetByIdAsync(id, cancellationToken);
            return this.ToHttpResult(result);
        }

        [HttpGet]
        public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
        {
            var result = await _categoryService.GetAllAsync(cancellationToken);
            return this.ToHttpResult(result);
        }

        [HttpGet("management")]
        public async Task<IActionResult> GetAllForManagement(CancellationToken cancellationToken)
        {
            var result = await _categoryService.GetAllForManagementAsync(cancellationToken);
            return this.ToHttpResult(result);
        }

        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, CategoryRequest request, CancellationToken cancellationToken)
        {
            if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var actorId))
                return Unauthorized();
            var result = await _categoryService.UpdateAsync(id, request, actorId, cancellationToken);
            return this.ToHttpResult(result);
        }
    }
}
