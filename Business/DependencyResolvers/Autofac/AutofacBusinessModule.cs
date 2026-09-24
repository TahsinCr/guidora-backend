using Autofac;
using Autofac.Extras.DynamicProxy;
using Business.Abstract;
using Business.Concrete;
using Business.BusinessRules.Moderation;
using Castle.DynamicProxy;
using Core.Utilities.Interceptors.Autofac;
using Core.Utilities.Security.JWT;
using DataAccess.Abstract;
using DataAccess.Concrete.Entityframework;
using Core.DataAccess;

namespace Business.DependencyResolvers.Autofac
{
    public class AutofacBusinessModule: Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            builder.RegisterType<GuidoraUnitOfWork>().As<IUnitOfWork>().InstancePerLifetimeScope();
            builder.RegisterType<ProductManager>().As<IProductService>();
            builder.RegisterType<EfProductDal>().As<IProductDal>();

            builder.RegisterType<CategoryManager>().As<ICategoryService>();
            builder.RegisterType<EfCategoryDal>().As<ICategoryDal>();

            builder.RegisterType<UserManager>().As<IUserService>();
            builder.RegisterType<EfUserDal>().As<IUserDal>();

            builder.RegisterType<OperationClaimManager>().As<IOperationClaimService>();
            builder.RegisterType<EfOperationClaimDal>().As<IOperationClaimDal>();

            builder.RegisterType<EfUserProfileDal>().As<IUserProfileDal>();
            builder.RegisterType<EfRoleDal>().As<IRoleDal>();
            builder.RegisterType<EfUserRoleDal>().As<IUserRoleDal>();
            builder.RegisterType<EfRoleOperationClaimDal>().As<IRoleOperationClaimDal>();
            builder.RegisterType<EfProductStatusDal>().As<IProductStatusDal>();
            builder.RegisterType<EfTagCategoryDal>().As<ITagCategoryDal>();
            builder.RegisterType<EfTagDal>().As<ITagDal>();
            builder.RegisterType<EfProductCategoryDal>().As<IProductCategoryDal>();
            builder.RegisterType<EfProductTagDal>().As<IProductTagDal>();
            builder.RegisterType<EfProductDocumentDal>().As<IProductDocumentDal>();
            builder.RegisterType<EfProductMediaDal>().As<IProductMediaDal>();
            builder.RegisterType<EfProductChangelogDal>().As<IProductChangelogDal>();
            builder.RegisterType<EfCommentStatusDal>().As<ICommentStatusDal>();
            builder.RegisterType<EfCommentDal>().As<ICommentDal>();
            builder.RegisterType<EfFavoriteDal>().As<IFavoriteDal>();
            builder.RegisterType<EfReportReasonDal>().As<IReportReasonDal>();
            builder.RegisterType<EfReportStatusDal>().As<IReportStatusDal>();
            builder.RegisterType<EfProductReportDal>().As<IProductReportDal>();
            builder.RegisterType<EfProductContributorDal>().As<IProductContributorDal>();
            builder.RegisterType<EfVerificationTypeDal>().As<IVerificationTypeDal>();
            builder.RegisterType<EfProductVerificationDal>().As<IProductVerificationDal>();
            builder.RegisterType<EfProductEditProposalDal>().As<IProductEditProposalDal>();
            builder.RegisterType<EfProductEditProposalTagDal>().As<IProductEditProposalTagDal>();
            builder.RegisterType<EfProductEditProposalCategoryDal>().As<IProductEditProposalCategoryDal>();
            builder.RegisterType<EfAuditLogDal>().As<IAuditLogDal>();
            builder.RegisterType<EfUserAuthenticationStateDal>().As<IUserAuthenticationStateDal>();
            builder.RegisterType<EfUserSessionDal>().As<IUserSessionDal>();

            builder.RegisterType<AuthManager>().As<IAuthService>();
            builder.RegisterType<UserSessionInvalidationManager>().As<IUserSessionInvalidationService>();
            builder.RegisterType<TokenValidationManager>().As<ITokenValidationService>();
            builder.RegisterType<UserSecurityManager>().As<IUserSecurityService>();
            builder.RegisterType<JwtHelper>().As<ITokenHelper>();

            var assembly = System.Reflection.Assembly.GetExecutingAssembly();

            builder.RegisterAssemblyTypes(assembly).AsImplementedInterfaces()
                .EnableInterfaceInterceptors(new ProxyGenerationOptions()
                {
                    Selector = new AspectInterceptorSelector()
                }).InstancePerLifetimeScope();

            builder.RegisterType<DeterministicContentModerationRule>()
                .As<IContentModerationRule>()
                .SingleInstance();
            builder.RegisterBuildCallback(scope => scope.Resolve<IContentModerationRule>());

        }
    }
}
