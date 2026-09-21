using Core.Entities;

namespace Core.Entities.Concrete
{
    public class RoleOperationClaim : IEntity
    {
        public int Id { get; set; }
        public int RoleId { get; set; }
        public int OperationClaimId { get; set; }
    }
}
