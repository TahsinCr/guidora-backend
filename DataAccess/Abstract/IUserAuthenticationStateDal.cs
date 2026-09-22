using Core.DataAccess;
using Entities;

namespace DataAccess.Abstract
{
    public interface IUserAuthenticationStateDal : IEntityRepository<UserAuthenticationState>
    {
    }
}
