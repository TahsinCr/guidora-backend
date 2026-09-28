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
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using DataAccess.Concrete.Entityframework.Contexts;
using Business.Abstract;
using System.Security.Claims;
using StackExchange.Redis;
using Business.Security;
using DataAccess.Abstract;
using Core.DataAccess;
using System.Threading.RateLimiting;
using WebAPI.HealthChecks;
using WebAPI.Logging;
using WebAPI.Configuration;
using WebAPI.Services;

namespace WebAPI
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            const string apiCorsPolicy = "_apiCorsPolicy";
            var allowOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];

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

            builder.Services.ConfigureOptions<ForwardedHeadersOptionsSetup>();

            builder.Services.AddRateLimiter(options =>
            {
                options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
                AddEmailIpAggregateLimit(options, builder.Configuration);
                AddIpRateLimitPolicy(options, builder.Configuration, "auth-login", "RateLimiting:Login",
                    permitLimit: 10, windowSeconds: 60);
                AddIpRateLimitPolicy(options, builder.Configuration, "auth-register", "RateLimiting:Register",
                    permitLimit: 5, windowSeconds: 3600);
                AddIpRateLimitPolicy(options, builder.Configuration, "auth-refresh", "RateLimiting:Refresh",
                    permitLimit: 30, windowSeconds: 60);
                AddUserRateLimitPolicy(options, builder.Configuration, "email-verification-request",
                    "RateLimiting:EmailVerificationRequest", permitLimit: 3, windowSeconds: 3600);
                AddUserRateLimitPolicy(options, builder.Configuration, "email-verification-confirm",
                    "RateLimiting:EmailVerificationConfirm", permitLimit: 10, windowSeconds: 600);
            });

            // Add services to the container.
            builder.Services.AddControllers();
            builder.Services.AddHostedService<ExpiredSessionCleanupService>();
            builder.Services.AddDbContext<GuidoraDbContext>(options =>
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
            builder.Services.AddHealthChecks()
                .AddCheck<PostgreSqlHealthCheck>("postgresql", tags: ["ready"])
                .AddCheck<RedisHealthCheck>("redis", tags: ["ready"]);

            builder.Host.UseServiceProviderFactory(new AutofacServiceProviderFactory())
                .ConfigureContainer<ContainerBuilder>(autofacBuilder => 
                {
                    autofacBuilder.RegisterModule(new AutofacBusinessModule());
                });


            var tokenOptions = builder.Configuration.GetSection("TokenOptions").Get<TokenOptions>()
                ?? throw new InvalidOperationException("TokenOptions configuration is missing.");
            ValidateStartupConfiguration(builder.Configuration, tokenOptions);

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
                        OnTokenValidated = async context =>
                        {
                            var userIdValue = context.Principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                            var sessionId = context.Principal?.FindFirst("sid")?.Value;
                            var authorizationVersionValue = context.Principal?.FindFirst("av")?.Value;

                            if (!int.TryParse(userIdValue, out var userId) ||
                                !int.TryParse(authorizationVersionValue, out var authorizationVersion) ||
                                string.IsNullOrWhiteSpace(sessionId))
                            {
                                context.Fail("Access token security claims are invalid.");
                                return;
                            }

                            var validationService = context.HttpContext.RequestServices
                                .GetRequiredService<ITokenValidationService>();
                            var validationResult = await validationService.ValidateAsync(userId, sessionId,
                                authorizationVersion, context.HttpContext.RequestAborted);
                            if (!validationResult.Success)
                            {
                                context.Fail(validationResult.Message);
                            }

                        }
                    };
                });

            builder.Services.AddDependencyResolvers(
                new CoreModule()
            );


            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();

            var app = builder.Build();
            Core.Utilities.IoC.ServiceTool.SetProvider(app.Services);

            if (builder.Configuration.GetValue<bool>("Database:ApplyMigrationsOnStartup"))
            {
                await ApplyDatabaseMigrationsAsync(app.Services);
            }

            if (builder.Configuration.GetValue<bool>("Authorization:SyncOperationClaimsOnStartup"))
            {
                using var scope = app.Services.CreateScope();
                SecurityOperationTool.SyncOperationClaims(
                    scope.ServiceProvider.GetRequiredService<IOperationClaimDal>(),
                    scope.ServiceProvider.GetRequiredService<IUnitOfWork>());
            }

            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }

            app.UseForwardedHeaders();
            app.UseGuidoraHttpLogging();
            app.ConfigureCustomExceptionMiddleware();

            app.UseRouting();

            app.UseCors(apiCorsPolicy);

            app.UseHttpsRedirection();

            app.UseAuthentication();

            app.UseRateLimiter();

            app.UseAuthorization();
            
            app.MapControllers();
            app.MapHealthChecks("/health/live", new HealthCheckOptions
            {
                Predicate = _ => false
            });
            app.MapHealthChecks("/health/ready", new HealthCheckOptions
            {
                Predicate = check => check.Tags.Contains("ready")
            });
            app.MapHealthChecks("/health", new HealthCheckOptions
            {
                Predicate = check => check.Tags.Contains("ready")
            });

            app.Run();
        }

        private static async Task ApplyDatabaseMigrationsAsync(IServiceProvider services)
        {
            using IServiceScope scope = services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<DataAccess.Concrete.Entityframework.Contexts.GuidoraDbContext>();
            var migrations = context.Database.GetMigrations();

            if (!migrations.Any())
            {
                throw new InvalidOperationException(
                    "Database migration startup is enabled, but no EF Core migrations are available.");
            }

            await context.Database.MigrateAsync();
        }

        private static void AddIpRateLimitPolicy(RateLimiterOptions options, IConfiguration configuration,
            string policyName, string configurationPath, int permitLimit, int windowSeconds)
        {
            var configuredPermitLimit = configuration.GetValue<int?>($"{configurationPath}:PermitLimit")
                                        ?? permitLimit;
            var configuredWindowSeconds = configuration.GetValue<int?>($"{configurationPath}:WindowSeconds")
                                          ?? windowSeconds;

            if (configuredPermitLimit <= 0 || configuredWindowSeconds <= 0)
            {
                throw new InvalidOperationException($"Rate limit settings for '{policyName}' must be positive.");
            }

            options.AddPolicy(policyName, context => RateLimitPartition.GetFixedWindowLimiter(
                RateLimitPartitionKey.FromAddress(context.Connection.RemoteIpAddress),
                _ => new FixedWindowRateLimiterOptions
                {
                    AutoReplenishment = true,
                    PermitLimit = configuredPermitLimit,
                    QueueLimit = 0,
                    Window = TimeSpan.FromSeconds(configuredWindowSeconds)
                }));
        }

        private static void AddUserRateLimitPolicy(RateLimiterOptions options, IConfiguration configuration,
            string policyName, string configurationPath, int permitLimit, int windowSeconds)
        {
            var configuredPermitLimit = configuration.GetValue<int?>($"{configurationPath}:PermitLimit")
                                        ?? permitLimit;
            var configuredWindowSeconds = configuration.GetValue<int?>($"{configurationPath}:WindowSeconds")
                                          ?? windowSeconds;
            if (configuredPermitLimit <= 0 || configuredWindowSeconds <= 0)
                throw new InvalidOperationException($"Rate limit settings for '{policyName}' must be positive.");

            options.AddPolicy(policyName, context => RateLimitPartition.GetFixedWindowLimiter(
                context.User.FindFirstValue(ClaimTypes.NameIdentifier)
                    ?? RateLimitPartitionKey.FromAddress(context.Connection.RemoteIpAddress),
                _ => new FixedWindowRateLimiterOptions
                {
                    AutoReplenishment = true,
                    PermitLimit = configuredPermitLimit,
                    QueueLimit = 0,
                    Window = TimeSpan.FromSeconds(configuredWindowSeconds)
                }));
        }

        private static void AddEmailIpAggregateLimit(RateLimiterOptions options, IConfiguration configuration)
        {
            var permitLimit = configuration.GetValue<int?>("RateLimiting:EmailVerificationIp:PermitLimit") ?? 120;
            var windowSeconds = configuration.GetValue<int?>("RateLimiting:EmailVerificationIp:WindowSeconds") ?? 3600;
            if (permitLimit <= 0 || windowSeconds <= 0)
                throw new InvalidOperationException("Email verification IP aggregate rate limit must be positive.");

            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
            {
                if (context.User.Identity?.IsAuthenticated != true ||
                    !context.Request.Path.StartsWithSegments("/api/auth/email-verification"))
                    return RateLimitPartition.GetNoLimiter("other-routes");

                return RateLimitPartition.GetFixedWindowLimiter(
                    "email-ip:" + RateLimitPartitionKey.FromAddress(context.Connection.RemoteIpAddress),
                    _ => new FixedWindowRateLimiterOptions
                    {
                        AutoReplenishment = true,
                        PermitLimit = permitLimit,
                        QueueLimit = 0,
                        Window = TimeSpan.FromSeconds(windowSeconds)
                    });
            });
        }

        private static void ValidateStartupConfiguration(IConfiguration configuration, TokenOptions tokenOptions)
        {
            if (string.IsNullOrWhiteSpace(tokenOptions.Issuer) ||
                string.IsNullOrWhiteSpace(tokenOptions.Audience) ||
                System.Text.Encoding.UTF8.GetByteCount(tokenOptions.SecurityKey ?? string.Empty) < 32 ||
                tokenOptions.AccessTokenExpirationMinutes <= 0 ||
                tokenOptions.RefreshTokenExpirationDays <= 0 ||
                tokenOptions.RememberMeExpirationDays <= 0 ||
                tokenOptions.SessionAbsoluteExpirationDays <= 0 ||
                tokenOptions.ClockSkewSeconds < 0)
                throw new InvalidOperationException("TokenOptions must include issuer, audience, a signing key of at least 32 bytes, and valid lifetimes.");

            if (System.Text.Encoding.UTF8.GetByteCount(
                    configuration["CommunityVerification:SignalHashKey"] ?? string.Empty) < 32)
                throw new InvalidOperationException("CommunityVerification:SignalHashKey must be at least 32 bytes.");

            if (!configuration.GetValue<bool>("EmailVerification:Enabled")) return;
            if (string.IsNullOrWhiteSpace(configuration["EmailVerification:Smtp:Host"]) ||
                string.IsNullOrWhiteSpace(configuration["EmailVerification:Smtp:From"]) ||
                string.IsNullOrWhiteSpace(configuration["EmailVerification:Smtp:Username"]) ||
                string.IsNullOrWhiteSpace(configuration["EmailVerification:Smtp:Password"]) ||
                configuration.GetValue<int>("EmailVerification:Smtp:Port") is < 1 or > 65535)
                throw new InvalidOperationException("Email verification is enabled but SMTP configuration is incomplete.");
            try
            {
                _ = new System.Net.Mail.MailAddress(configuration["EmailVerification:Smtp:From"]!);
            }
            catch (FormatException exception)
            {
                throw new InvalidOperationException("Email verification SMTP sender address is invalid.", exception);
            }
        }
    }
}
