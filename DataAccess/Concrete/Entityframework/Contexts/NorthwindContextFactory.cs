using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace DataAccess.Concrete.Entityframework.Contexts
{
    public sealed class NorthwindContextFactory : IDesignTimeDbContextFactory<NorthwindContext>
    {
        public NorthwindContext CreateDbContext(string[] args)
        {
            var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Guidora")
                ?? "Host=localhost;Port=5432;Database=guidora;Username=guidora;Password=change-me";

            var optionsBuilder = new DbContextOptionsBuilder<NorthwindContext>();
            optionsBuilder.UseNpgsql(connectionString);
            return new NorthwindContext(optionsBuilder.Options);
        }
    }
}
