using Core.Entities;

namespace Entities
{
    public class Product : IEntity
    {
        public int ProductId { get; set; }

        public int CategoryId { get; set; }

        public string ProductName { get; set; } = string.Empty;

        public short UnitsInStock { get; set; }

        public decimal UnitPrice { get; set; }

    }
}
