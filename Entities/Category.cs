using Core.Entities;

namespace Entities
{
    public class Category : IEntity
    {
        public int CategoryId { get; set; }

        public string CategoryName { get; set; } = string.Empty;
    }
}
