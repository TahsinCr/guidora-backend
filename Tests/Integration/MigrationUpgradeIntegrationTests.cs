using DataAccess.Concrete.Entityframework.Contexts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace Tests.Integration;

public sealed class MigrationUpgradeIntegrationTests
{
    private const string PreSessionFamilyMigration = "20260927192431_CommunityVerificationReviewAndEmailEligibility";
    [RequiresEnvironmentFact("GUIDORA_TEST_CONNECTION_STRING")]
    public async Task PopulatedLegacySchemaUpgradesSessionFamiliesAndProductDescriptionsWithoutTruncation()
    {
        var baseConnectionString = Environment.GetEnvironmentVariable("GUIDORA_TEST_CONNECTION_STRING")!;
        var schema = $"migration_upgrade_{Guid.NewGuid():N}";
        var connectionString = new NpgsqlConnectionStringBuilder(baseConnectionString)
        {
            SearchPath = $"{schema},public"
        }.ConnectionString;

        await using (var connection = new NpgsqlConnection(baseConnectionString))
        {
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = "CREATE EXTENSION IF NOT EXISTS citext WITH SCHEMA public";
            await command.ExecuteNonQueryAsync();
            command.CommandText = $"CREATE SCHEMA \"{schema}\"";
            await command.ExecuteNonQueryAsync();
        }

        try
        {
            var options = new DbContextOptionsBuilder<GuidoraDbContext>()
                .UseNpgsql(connectionString)
                .Options;
            var userId = Random.Shared.Next(100_000, int.MaxValue);
            var productName = $"Migration fixture {Guid.NewGuid():N}";
            var productSlug = $"migration-fixture-{Guid.NewGuid():N}";
            var oversizedProductName = $"Oversized migration fixture {Guid.NewGuid():N}";
            var oversizedProductSlug = $"oversized-migration-fixture-{Guid.NewGuid():N}";
            var proposalId = Random.Shared.Next(100_000, int.MaxValue);
            var oversizedProposalId = Random.Shared.Next(100_000, int.MaxValue);
            var now = DateTime.UtcNow;
            var rootSessionId = Guid.NewGuid();
            var rotatedSessionId = Guid.NewGuid();
            var oversizedDescription = new string('x', 20_001);

            await using (var context = new GuidoraDbContext(options))
            {
                var migrator = context.GetService<IMigrator>();
                await migrator.MigrateAsync(PreSessionFamilyMigration);

                await context.Database.ExecuteSqlInterpolatedAsync($"""
                    INSERT INTO "Users" ("Id", "FirstName", "LastName", "Email", "PasswordHash", "PasswordSalt", "Status")
                    VALUES ({userId}, 'Migration', 'Upgrade', {$"migration-{Guid.NewGuid():N}@example.test"}, {new byte[] { 1 }}, {new byte[] { 2 }}, TRUE)
                    """);

                var preservedDescription = new string('d', 19_999);
                await context.Database.ExecuteSqlInterpolatedAsync($"""
                    INSERT INTO "Products" ("Name", "Slug", "ShortDescription", "Description", "WebsiteUrl", "LogoUrl", "ProductStatusId", "CreatedByUserId", "CreatedAt")
                    VALUES ({productName}, {productSlug}, 'Migration fixture', {preservedDescription}, 'https://migration-{Guid.NewGuid():N}.example.test', '', 1, {userId}, {now})
                    """);
                await context.Database.ExecuteSqlInterpolatedAsync($"""
                    INSERT INTO "Products" ("Name", "Slug", "ShortDescription", "Description", "WebsiteUrl", "LogoUrl", "ProductStatusId", "CreatedByUserId", "CreatedAt")
                    VALUES ({oversizedProductName}, {oversizedProductSlug}, 'Oversized fixture', {oversizedDescription}, 'https://oversized-migration-{Guid.NewGuid():N}.example.test', '', 1, {userId}, {now})
                    """);

                await context.Database.ExecuteSqlInterpolatedAsync($"""
                    INSERT INTO "ProductEditProposals" ("Id", "ProductId", "ProposedByUserId", "BaseProductVersion", "CreatedAt", "Description", "LogoUrl", "Name", "ReviewNote", "ShortDescription", "Slug", "Status", "WebsiteUrl")
                    SELECT {proposalId}, "ProductId", {userId}, 1, {now}, {preservedDescription}, '', 'Proposed migration product', '', 'Proposal fixture', 'proposed-migration-fixture', 'Pending', 'https://proposal-migration-{Guid.NewGuid():N}.example.test'
                    FROM "Products" WHERE "Name" = {productName}
                    """);
                await context.Database.ExecuteSqlInterpolatedAsync($"""
                    INSERT INTO "ProductEditProposals" ("Id", "ProductId", "ProposedByUserId", "BaseProductVersion", "CreatedAt", "Description", "LogoUrl", "Name", "ReviewNote", "ShortDescription", "Slug", "Status", "WebsiteUrl")
                    SELECT {oversizedProposalId}, "ProductId", {userId}, 1, {now}, {oversizedDescription}, '', 'Oversized proposed product', '', 'Oversized proposal fixture', 'oversized-proposed-migration-fixture', 'Pending', 'https://oversized-proposal-migration-{Guid.NewGuid():N}.example.test'
                    FROM "Products" WHERE "Name" = {oversizedProductName}
                    """);

                await context.Database.ExecuteSqlInterpolatedAsync($"""
                    INSERT INTO "UserSessions" ("Id", "UserId", "RefreshTokenHash", "CreatedAt", "ExpiresAt", "AbsoluteExpiresAt", "IsPersistent", "DeviceName")
                    VALUES ({rootSessionId}, {userId}, {new string('a', 64)}, {now}, {now.AddDays(1)}, {now.AddDays(30)}, TRUE, 'legacy root')
                    """);
                await context.Database.ExecuteSqlInterpolatedAsync($"""
                    INSERT INTO "UserSessions" ("Id", "UserId", "RefreshTokenHash", "CreatedAt", "ExpiresAt", "AbsoluteExpiresAt", "IsPersistent", "DeviceName")
                    VALUES ({rotatedSessionId}, {userId}, {new string('b', 64)}, {now}, {now.AddDays(1)}, {now.AddDays(30)}, TRUE, 'legacy rotated')
                    """);
                await context.Database.ExecuteSqlInterpolatedAsync($"""
                    UPDATE "UserSessions" SET "ReplacedBySessionId" = {rotatedSessionId}
                    WHERE "Id" = {rootSessionId}
                    """);

                var productLimitFailure = await Record.ExceptionAsync(() => migrator.MigrateAsync());
                Assert.Equal("22001", FindPostgresException(productLimitFailure)?.SqlState);
                await context.Database.ExecuteSqlInterpolatedAsync($"""
                    UPDATE "Products" SET "Description" = {new string('p', 20_000)}
                    WHERE "Name" = {oversizedProductName}
                    """);

                var proposalLimitFailure = await Record.ExceptionAsync(() => migrator.MigrateAsync());
                Assert.Equal("22001", FindPostgresException(proposalLimitFailure)?.SqlState);
                await context.Database.ExecuteSqlInterpolatedAsync($"""
                    UPDATE "ProductEditProposals" SET "Description" = {new string('q', 20_000)}
                    WHERE "Id" = {oversizedProposalId}
                    """);
                await migrator.MigrateAsync();

                var productDescription = await context.Database.SqlQuery<string>(
                    $"SELECT \"Description\" AS \"Value\" FROM \"Products\" WHERE \"Name\" = {productName}")
                    .SingleAsync();
                var proposalIdAfterUpgrade = await context.Database.SqlQuery<int>(
                    $"SELECT \"Id\" AS \"Value\" FROM \"ProductEditProposals\" WHERE \"Id\" = {proposalId}")
                    .SingleAsync();
                var proposalDescriptionLength = await context.Database.SqlQuery<int>(
                    $"SELECT char_length(\"Description\") AS \"Value\" FROM \"ProductEditProposals\" WHERE \"Id\" = {proposalId}")
                    .SingleAsync();
                var boundedProductLength = await context.Database.SqlQuery<int>(
                    $"SELECT char_length(\"Description\") AS \"Value\" FROM \"Products\" WHERE \"Name\" = {oversizedProductName}")
                    .SingleAsync();
                var boundedProposalLength = await context.Database.SqlQuery<int>(
                    $"SELECT char_length(\"Description\") AS \"Value\" FROM \"ProductEditProposals\" WHERE \"Id\" = {oversizedProposalId}")
                    .SingleAsync();
                var familyIds = await context.Database.SqlQuery<Guid>($"""
                    SELECT "SessionFamilyId" AS "Value" FROM "UserSessions"
                    WHERE "Id" IN ({rootSessionId}, {rotatedSessionId})
                    """).ToListAsync();

                Assert.Equal(preservedDescription, productDescription);
                Assert.Equal(proposalId, proposalIdAfterUpgrade);
                Assert.Equal(preservedDescription.Length, proposalDescriptionLength);
                Assert.Equal(20_000, boundedProductLength);
                Assert.Equal(20_000, boundedProposalLength);
                Assert.Equal(2, familyIds.Count);
                Assert.All(familyIds, familyId => Assert.Equal(rootSessionId, familyId));
            }
        }
        finally
        {
            await using var connection = new NpgsqlConnection(baseConnectionString);
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = $"DROP SCHEMA IF EXISTS \"{schema}\" CASCADE";
            await command.ExecuteNonQueryAsync();
        }
    }

    private static PostgresException? FindPostgresException(Exception? exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is PostgresException postgresException) return postgresException;
        }

        return null;
    }
}
