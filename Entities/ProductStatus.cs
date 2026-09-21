using Core.Entities;

namespace Entities
{
    public class ProductStatus : IEntity
    {
        public int Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public bool IsVisible { get; set; }
    }
}
