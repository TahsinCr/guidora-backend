using System;
using System.Linq;
using Autofac;
using Autofac.Extensions.DependencyInjection;
using Business.DependencyResolvers.Autofac;
using Core.DependencyResolvers;
using Core.Extensions;
using Core.Utilities.Security.Encyption;
using Core.Utilities.Security.JWT;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;

namespace WebAPI
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            string apiCorsPolicy = "_apiCorsPolicy";
            string[] allowOrigins = ["http://localhost:4200"];

            builder.Services.AddCors(options =>
            {
                options.AddPolicy(apiCorsPolicy, configurePolicy =>
                    {
                        configurePolicy.WithOrigins(allowOrigins).AllowAnyHeader();
                    }
                );
            });

            // Add services to the container.
            builder.Services.AddControllers();
            builder.Services.AddScoped<DataAccess.Concrete.Entityframework.Contexts.NorthwindContext>();

            //builder.Services.AddSingleton<IProductDal,EfProductDal>();
            //builder.Services.AddSingleton<IProductService,ProductManager>();
            builder.Host.UseServiceProviderFactory(new AutofacServiceProviderFactory())
                .ConfigureContainer<ContainerBuilder>(autofacBuilder => 
                {
                    autofacBuilder.RegisterModule(new AutofacBusinessModule());
                });


            // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle

            var tokenOptions = builder.Configuration.GetSection("TokenOptions").Get<TokenOptions>()
                ?? throw new InvalidOperationException("TokenOptions configuration is missing.");

            builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(options =>
                {
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidateAudience = true,
                        ValidateLifetime = true,
                        ValidIssuer = tokenOptions.Issuer,
                        ValidAudience = tokenOptions.Audience,
                        ValidateIssuerSigningKey = true,
                        IssuerSigningKey = SecurityKeyHelper.CreateSecurityKey(tokenOptions.SecurityKey)
                    };
                });

            builder.Services.AddDependencyResolvers(
                new CoreModule()
            );


            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();

            var app = builder.Build();

            if (builder.Configuration.GetValue<bool>("Database:ApplyMigrationsOnStartup"))
            {
                await ApplyDatabaseMigrationsAsync(app.Services);
            }

            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }

            app.ConfigureCustomExceptionMiddleware();

            app.UseCors(apiCorsPolicy);

            app.UseHttpsRedirection();

            app.UseAuthentication();

            app.UseAuthorization();
            
            app.MapControllers();

            app.Run();
        }

        private static async Task ApplyDatabaseMigrationsAsync(IServiceProvider services)
        {
            using IServiceScope scope = services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<DataAccess.Concrete.Entityframework.Contexts.NorthwindContext>();
            var migrations = await context.Database.GetMigrationsAsync();

            if (!migrations.Any())
            {
                throw new InvalidOperationException(
                    "Database migration startup is enabled, but no EF Core migrations are available.");
            }

            await context.Database.MigrateAsync();
        }
    }
}
