using Core.Entities;

namespace Entities
{
    public class ProductEditProposalCategory : IEntity
    {
        public int Id { get; set; }
        public int ProductEditProposalId { get; set; }
        public int CategoryId { get; set; }
    }
}
