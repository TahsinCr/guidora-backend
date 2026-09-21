using Core.Entities;

namespace Entities
{
    public class ProductTag : IEntity
    {
        public int Id { get; set; }
        public int ProductId { get; set; }
        public int TagId { get; set; }
    }
}
