using Autofac;
using Business.Abstract;
using Business.DependencyResolvers.Autofac;
using DataAccess.Concrete.Entityframework.Contexts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Tests.Business.Security;

public sealed class ServiceRegistrationTests
{
    [Fact]
    public void VerificationAndEmailServicesResolveWithTheirDataAccessDependencies()
    {
        var builder = new ContainerBuilder();
        builder.RegisterModule<AutofacBusinessModule>();
        builder.RegisterInstance(new ConfigurationBuilder().AddInMemoryCollection([]).Build())
            .As<IConfiguration>();
        var options = new DbContextOptionsBuilder<GuidoraDbContext>()
            .UseNpgsql("Host=localhost;Database=registration_check;Username=guidora;Password=unused")
            .Options;
        builder.Register(ctx => new GuidoraDbContext(options)).InstancePerLifetimeScope();

        using var container = builder.Build();
        using var scope = container.BeginLifetimeScope();
        Assert.NotNull(scope.Resolve<ICommunityVerificationService>());
        Assert.NotNull(scope.Resolve<IProductVerificationService>());
        Assert.NotNull(scope.Resolve<IEmailVerificationService>());
    }
}
