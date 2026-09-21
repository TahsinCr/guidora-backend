using Core.Entities;

namespace Entities
{
    public class Tag : IEntity
    {
        public int Id { get; set; }
        public int TagCategoryId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;
        public bool IsExclusive { get; set; }
        public bool IsActive { get; set; }
    }
}
