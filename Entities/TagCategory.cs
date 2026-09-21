using Core.Entities;

namespace Entities
{
    public class TagCategory : IEntity
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;
        public bool AllowsMultipleSelection { get; set; }
        public bool IsActive { get; set; }
    }
}
