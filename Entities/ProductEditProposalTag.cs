using Core.Entities;

namespace Entities
{
    public class ProductEditProposalTag : IEntity
    {
        public int Id { get; set; }
        public int ProductEditProposalId { get; set; }
        public int TagId { get; set; }
    }
}
