using Core.Entities;

namespace Core.Entities.Concrete
{
    public class UserStatus : IEntity
    {
        public int Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public bool AllowsAuthentication { get; set; }
        public bool AllowsContentVisibility { get; set; }
    }
}
