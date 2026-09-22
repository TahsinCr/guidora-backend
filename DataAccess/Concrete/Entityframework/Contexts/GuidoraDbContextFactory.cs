using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace DataAccess.Concrete.Entityframework.Contexts
{
    public sealed class GuidoraDbContextFactory : IDesignTimeDbContextFactory<GuidoraDbContext>
    {
        public GuidoraDbContext CreateDbContext(string[] args)
        {
            var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Guidora")
                ?? "Host=localhost;Port=5432;Database=guidora;Username=guidora;Password=change-me";

            var optionsBuilder = new DbContextOptionsBuilder<GuidoraDbContext>();
            optionsBuilder.UseNpgsql(connectionString);
            return new GuidoraDbContext(optionsBuilder.Options);
        }
    }
}
