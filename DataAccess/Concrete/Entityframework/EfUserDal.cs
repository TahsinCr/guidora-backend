using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Core.DataAccess.EntityFramework;
using Core.Entities;
using Core.Entities.Concrete;
using DataAccess.Abstract;
using DataAccess.Concrete.Entityframework.Contexts;
using Microsoft.EntityFrameworkCore;

namespace DataAccess.Concrete.Entityframework
{
    public class EfUserDal : EfEntityRepositoryBase<User, GuidoraDbContext>, IUserDal
    {
        public EfUserDal(GuidoraDbContext context)
            : base(context)
        {
        }

        public List<OperationClaim> GetClaims(User user)
        {
            var directClaims = from operationClaim in Context.OperationClaims
                join userOperationClaim in Context.UserOperationClaims
                    on operationClaim.Id equals userOperationClaim.OperationClaimId
                where userOperationClaim.UserId == user.Id
                select new OperationClaim { Id = operationClaim.Id, Name = operationClaim.Name };

            var roleClaims = from operationClaim in Context.OperationClaims
                join roleOperationClaim in Context.RoleOperationClaims
                    on operationClaim.Id equals roleOperationClaim.OperationClaimId
                join role in Context.Roles
                    on roleOperationClaim.RoleId equals role.Id
                join userRole in Context.UserRoles
                    on role.Id equals userRole.RoleId
                where userRole.UserId == user.Id && role.IsActive
                select new OperationClaim { Id = operationClaim.Id, Name = operationClaim.Name };

            return directClaims.Concat(roleClaims).Distinct().ToList();
        }

        public void AddClaim(User user, OperationClaim operationClaim)
        {
            var addedEntity = Context.Entry(new UserOperationClaim
            {
                UserId = user.Id,
                OperationClaimId = operationClaim.Id
            });
            addedEntity.State = EntityState.Added;
            Context.SaveChanges();
        }
    }
}
