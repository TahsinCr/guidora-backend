using Core.DataAccess.EntityFramework;
using DataAccess.Abstract;
using DataAccess.Concrete.Entityframework.Contexts;
using Entities;

namespace DataAccess.Concrete.Entityframework;

public sealed class EfEmailVerificationChallengeDal(GuidoraDbContext context)
    : EfEntityRepositoryBase<EmailVerificationChallenge, GuidoraDbContext>(context), IEmailVerificationChallengeDal;
