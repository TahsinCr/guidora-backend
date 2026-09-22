using System;
using System.Linq;
using System.Threading.Tasks;
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
using DataAccess.Concrete.Entityframework.Contexts;
using Business.Abstract;
using System.Security.Claims;
using StackExchange.Redis;
using Core.CrossCuttingConcerns.Messaging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

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
                        configurePolicy.WithOrigins(allowOrigins)
                            .AllowAnyHeader()
                            .AllowAnyMethod()
                            .AllowCredentials();
                    }
                );
            });

            // Add services to the container.
            builder.Services.AddControllers();
            builder.Services.AddDbContext<NorthwindContext>(options =>
                options.UseNpgsql(builder.Configuration.GetConnectionString("Guidora")));
            builder.Services.AddStackExchangeRedisCache(options =>
            {
                options.Configuration = builder.Configuration.GetSection("Redis")["Configuration"];
                options.InstanceName = "guidora:";
            });
            builder.Services.AddSingleton<IConnectionMultiplexer>(_ =>
            {
                string configuration = builder.Configuration.GetSection("Redis")["Configuration"]
                    ?? throw new InvalidOperationException("Redis configuration is missing.");
                var options = ConfigurationOptions.Parse(configuration);
                options.AbortOnConnectFail = false;
                options.ClientName = "guidora-api";
                return ConnectionMultiplexer.Connect(options);
            });
            builder.Services.Configure<RabbitMqOptions>(builder.Configuration.GetSection("RabbitMq"));
            builder.Services.AddSingleton<IConnection>(serviceProvider =>
            {
                var options = serviceProvider.GetRequiredService<IOptions<RabbitMqOptions>>().Value;
                var factory = new ConnectionFactory
                {
                    HostName = options.HostName,
                    Port = options.Port,
                    UserName = options.UserName,
                    Password = options.Password,
                    VirtualHost = options.VirtualHost,
                    ClientProvidedName = options.ClientProvidedName
                };
                return factory.CreateConnectionAsync().GetAwaiter().GetResult();
            });
            builder.Services.AddSingleton<IEventPublisher, RabbitMqEventPublisher>();

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
                        IssuerSigningKey = SecurityKeyHelper.CreateSecurityKey(tokenOptions.SecurityKey),
                        ClockSkew = TimeSpan.FromSeconds(tokenOptions.ClockSkewSeconds)
                    };
                    options.Events = new JwtBearerEvents
                    {
                        OnTokenValidated = context =>
                        {
                            var userIdValue = context.Principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                            var sessionId = context.Principal?.FindFirst("sid")?.Value;
                            var authorizationVersionValue = context.Principal?.FindFirst("av")?.Value;

                            if (!int.TryParse(userIdValue, out var userId) ||
                                !int.TryParse(authorizationVersionValue, out var authorizationVersion) ||
                                string.IsNullOrWhiteSpace(sessionId))
                            {
                                context.Fail("Access token security claims are invalid.");
                                return Task.CompletedTask;
                            }

                            var validationService = context.HttpContext.RequestServices
                                .GetRequiredService<ITokenValidationService>();
                            var validationResult = validationService.Validate(userId, sessionId, authorizationVersion);
                            if (!validationResult.Success)
                            {
                                context.Fail(validationResult.Message);
                            }

                            return Task.CompletedTask;
                        }
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
            app.MapGet("/health", () => "ok");

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
