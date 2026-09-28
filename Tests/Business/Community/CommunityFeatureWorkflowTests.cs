using Business.BusinessRules.Content;
using Business.BusinessRules.Moderation;
using Business.Abstract;
using Business.Concrete;
using Business.ValidationRules.FluentValidation;
using Core.Entities.Concrete;
using Core.Pagination;
using Core.Utilities.Results;
using Entities;
using Entities.DTOs;
using Tests.Core.TestDoubles;

namespace Tests.Business.Community;

public sealed class CommunityFeatureWorkflowTests
{
    [Fact]
    public async Task FavoriteAddIsIdempotentAndWritesAuditOnlyForStateChange()
    {
        var favorites = new CommunityFavoriteDalStub();
        var audit = new CommunityAuditDalStub();
        var unitOfWork = new UnitOfWorkStub();
        var manager = new FavoriteManager(favorites,
            new CommunityProductDalStub([new Product { ProductId = 5, ProductStatusId = 1 }]),
            new CommunityProductStatusDalStub([new ProductStatus { Id = 1, Code = "published" }]),
            new CommunityUserDalStub([new User { Id = 7, Status = true }]), audit, unitOfWork);

        var first = await manager.AddAsync(5, 7);
        var repeated = await manager.AddAsync(5, 7);

        Assert.True(first.Success);
        Assert.True(repeated.Success);
        Assert.Single(favorites.Records);
        Assert.Single(audit.Entries);
        Assert.Equal(1, unitOfWork.SaveCount);
    }

    [Fact]
    public async Task FavoriteListUsesDatabasePageAndReportsTotalCount()
    {
        var favorites = new CommunityFavoriteDalStub([
            new Favorite { UserId = 7, ProductId = 1, CreatedAt = System.DateTime.UtcNow.AddDays(-2) },
            new Favorite { UserId = 7, ProductId = 2, CreatedAt = System.DateTime.UtcNow.AddDays(-1) },
            new Favorite { UserId = 7, ProductId = 3, CreatedAt = System.DateTime.UtcNow },
            new Favorite { UserId = 8, ProductId = 4, CreatedAt = System.DateTime.UtcNow }
        ]);
        var manager = new FavoriteManager(favorites, new CommunityProductDalStub(),
            new CommunityProductStatusDalStub(), new CommunityUserDalStub(), new CommunityAuditDalStub(),
            new UnitOfWorkStub());

        var result = await manager.GetForUserAsync(7, new PageRequest { Page = 2, PageSize = 1 });

        Assert.True(result.Success);
        Assert.Equal(3, result.Data.TotalCount);
        Assert.Equal(2, Assert.Single(result.Data.Items).ProductId);
    }

    [Fact]
    public async Task CommentDeleteChangesDomainStatusAndRejectsAnotherUsersComment()
    {
        var comment = new Comment { Id = 12, ProductId = 5, UserId = 7, CommentStatusId = 1, Content = "Useful" };
        var comments = new CommunityCommentDalStub([comment]);
        var audit = new CommunityAuditDalStub();
        var unitOfWork = new UnitOfWorkStub();
        var manager = new CommentManager(comments,
            new CommunityCommentStatusDalStub([
                new CommentStatus { Id = 1, Code = "active", IsVisible = true },
                new CommentStatus { Id = 2, Code = "hidden", IsVisible = false },
                new CommentStatus { Id = 3, Code = "deleted", IsVisible = false }]),
            new CommunityUserDalStub(), new CommunityProductDalStub(), new CommunityProductStatusDalStub(), audit,
            unitOfWork, new DeterministicContentModerationRule());

        var forbidden = await manager.DeleteAsync(12, 8);
        var deleted = await manager.DeleteAsync(12, 7);

        Assert.False(forbidden.Success);
        Assert.IsAssignableFrom<IForbiddenResult>(forbidden);
        Assert.True(deleted.Success);
        Assert.Equal(3, comment.CommentStatusId);
        Assert.Equal(0, comments.DeleteCount);
        Assert.Single(audit.Entries);
        Assert.Equal(1, unitOfWork.SaveCount);
    }

    [Fact]
    public async Task ModeratorCanBlockCommentAndRepeatedBlockIsIdempotent()
    {
        var comment = new Comment { Id = 13, ProductId = 5, UserId = 7, CommentStatusId = 1, Content = "Reported" };
        var comments = new CommunityCommentDalStub([comment]);
        var audit = new CommunityAuditDalStub();
        var unitOfWork = new UnitOfWorkStub();
        var manager = new CommentManager(comments,
            new CommunityCommentStatusDalStub([
                new CommentStatus { Id = 1, Code = "active", IsVisible = true },
                new CommentStatus { Id = 4, Code = "blocked", IsVisible = false }]),
            new CommunityUserDalStub(), new CommunityProductDalStub(), new CommunityProductStatusDalStub(), audit,
            unitOfWork, new DeterministicContentModerationRule());

        var blocked = await manager.BlockAsync(13, actorUserId: 99);
        var repeated = await manager.BlockAsync(13, actorUserId: 99);

        Assert.True(blocked.Success);
        Assert.True(repeated.Success);
        Assert.Equal(4, comment.CommentStatusId);
        Assert.Equal("Blocked", Assert.Single(audit.Entries).Action);
        Assert.Equal(1, unitOfWork.SaveCount);
    }

    [Fact]
    public async Task CommentReadIsPaginatedAndRejectsOversizedPages()
    {
        var comments = new CommunityCommentDalStub
        {
            VisibleRecords =
            [
                new Comment { Id = 1, ProductId = 5, CreatedAt = System.DateTime.UtcNow.AddMinutes(-2) },
                new Comment { Id = 2, ProductId = 5, CreatedAt = System.DateTime.UtcNow.AddMinutes(-1) },
                new Comment { Id = 3, ProductId = 5, CreatedAt = System.DateTime.UtcNow }
            ]
        };
        var manager = new CommentManager(comments, new CommunityCommentStatusDalStub(), new CommunityUserDalStub(),
            new CommunityProductDalStub(), new CommunityProductStatusDalStub(), new CommunityAuditDalStub(),
            new UnitOfWorkStub(), new DeterministicContentModerationRule());

        var page = await manager.GetVisibleByProductAsync(5, new PageRequest { Page = 2, PageSize = 1 });
        var invalid = await manager.GetVisibleByProductAsync(5, new PageRequest { Page = 1, PageSize = 101 });

        Assert.True(page.Success);
        Assert.Equal(3, page.Data.TotalCount);
        Assert.Equal(2, Assert.Single(page.Data.Items).Id);
        Assert.False(invalid.Success);
    }

    [Fact]
    public async Task CommentCreationUsesThePersistedIdInItsAuditRecord()
    {
        var comments = new CommunityCommentDalStub();
        var audit = new CommunityAuditDalStub();
        var unitOfWork = new UnitOfWorkStub();
        var manager = new CommentManager(comments,
            new CommunityCommentStatusDalStub([new CommentStatus { Id = 1, Code = "active", IsVisible = true }]),
            new CommunityUserDalStub([new User { Id = 7, Status = true }]),
            new CommunityProductDalStub([new Product { ProductId = 5, ProductStatusId = 2 }]),
            new CommunityProductStatusDalStub([new ProductStatus { Id = 2, Code = "published" }]), audit, unitOfWork,
            new DeterministicContentModerationRule());

        var result = await manager.CreateAsync(5, 7, new CommentRequest { Content = "  Helpful review  " });

        Assert.True(result.Success);
        Assert.Equal("Helpful review", Assert.Single(comments.GetAll()).Content);
        Assert.Equal("1", Assert.Single(audit.Entries).EntityId);
        Assert.Equal(2, unitOfWork.SaveCount);
    }

    [Fact]
    public async Task CommentCreationRejectsModeratedTextBeforePersistence()
    {
        var comments = new CommunityCommentDalStub();
        var audit = new CommunityAuditDalStub();
        var unitOfWork = new UnitOfWorkStub();
        var manager = new CommentManager(comments,
            new CommunityCommentStatusDalStub([new CommentStatus { Id = 1, Code = "active", IsVisible = true }]),
            new CommunityUserDalStub([new User { Id = 7, Status = true }]),
            new CommunityProductDalStub([new Product { ProductId = 5, ProductStatusId = 2 }]),
            new CommunityProductStatusDalStub([new ProductStatus { Id = 2, Code = "published" }]), audit, unitOfWork,
            new DeterministicContentModerationRule());

        var result = await manager.CreateAsync(5, 7, new CommentRequest { Content = "f.u.c.k" });

        Assert.False(result.Success);
        Assert.Empty(comments.GetAll());
        Assert.Empty(audit.Entries);
        Assert.Equal(0, unitOfWork.SaveCount);
    }

    [Fact]
    public async Task DocumentAndMediaMutationsAreBlockedForVerifiedProducts()
    {
        var products = new CommunityProductDalStub([new Product { ProductId = 5, ProductStatusId = 1 }]);
        var rules = new ProductResourceRules(products,
            new CommunityProductStatusDalStub([new ProductStatus { Id = 1, Code = "published" }]),
            new CommunityVerificationDalStub([new ProductVerification { ProductId = 5, RevokedAt = null }]),
            new DeterministicContentModerationRule());
        var documentDal = new CommunityDocumentDalStub();
        var mediaDal = new CommunityMediaDalStub();
        var audit = new CommunityAuditDalStub();
        var unitOfWork = new UnitOfWorkStub();
        var documents = new ProductDocumentManager(documentDal, rules, audit, unitOfWork);
        var media = new ProductMediaManager(mediaDal, rules, audit, unitOfWork);

        var documentResult = await documents.CreateAsync(5, 9,
            new ProductDocumentRequest { DocumentType = "README", Title = "Read me", Content = "# Guide" });
        var mediaResult = await media.CreateAsync(5, 9,
            new ProductMediaRequest { MediaType = "Screenshot", Url = "https://cdn.example.test/screen.png" });

        Assert.False(documentResult.Success);
        Assert.False(mediaResult.Success);
        Assert.Empty(documentDal.GetAll());
        Assert.Empty(mediaDal.GetAll());
        Assert.Empty(audit.Entries);
        Assert.Equal(0, unitOfWork.SaveCount);
    }

    [Fact]
    public async Task DocumentContentAndMediaMetadataArePersistedAndAudited()
    {
        var product = new Product { ProductId = 5, ProductStatusId = 1 };
        var rules = new ProductResourceRules(new CommunityProductDalStub([product]),
            new CommunityProductStatusDalStub([new ProductStatus { Id = 1, Code = "published" }]),
            new CommunityVerificationDalStub(), new DeterministicContentModerationRule());
        var documentsDal = new CommunityDocumentDalStub();
        var mediaDal = new CommunityMediaDalStub();
        var audit = new CommunityAuditDalStub();
        var unitOfWork = new UnitOfWorkStub();

        var documentResult = await new ProductDocumentManager(documentsDal, rules, audit, unitOfWork)
            .CreateAsync(5, 7, new ProductDocumentRequest
                { DocumentType = "README", Title = "Getting started", Content = "# Install" });
        var mediaResult = await new ProductMediaManager(mediaDal, rules, audit, unitOfWork)
            .CreateAsync(5, 7, new ProductMediaRequest
                { MediaType = "Screenshot", Url = "https://cdn.example.test/image.png", AltText = "App" });

        Assert.True(documentResult.Success);
        Assert.True(mediaResult.Success);
        Assert.Equal("# Install", Assert.Single(documentsDal.GetAll()).Content);
        Assert.Equal("https://cdn.example.test/image.png", Assert.Single(mediaDal.GetAll()).Url);
        Assert.Equal(2, audit.Entries.Count);
        Assert.Equal(4, unitOfWork.SaveCount);
        Assert.True(new ProductMediaRequestValidator().Validate(new ProductMediaRequest
            { MediaType = "Image", Url = "javascript:alert(1)" }).IsValid == false);
    }

    [Fact]
    public async Task LargeDocumentAuditKeepsHashesAndBoundsInlineDiff()
    {
        var product = new Product { ProductId = 5, ProductStatusId = 1 };
        var rules = new ProductResourceRules(new CommunityProductDalStub([product]),
            new CommunityProductStatusDalStub([new ProductStatus { Id = 1, Code = "published" }]),
            new CommunityVerificationDalStub(), new DeterministicContentModerationRule());
        var documents = new CommunityDocumentDalStub();
        var audit = new CommunityAuditDalStub();
        var manager = new ProductDocumentManager(documents, rules, audit, new UnitOfWorkStub());
        var original = new string('a', 40_000);
        var replacement = new string('b', 40_000);
        Assert.True((await manager.CreateAsync(5, 7, new ProductDocumentRequest
            { DocumentType = "README", Title = "Large document", Content = original })).Success);
        Assert.True((await manager.UpdateAsync(5, 0, 7, new ProductDocumentRequest
            { DocumentType = "README", Title = "Large document", Content = replacement })).Success);

        var entry = audit.Entries.Last();
        Assert.DoesNotContain(original, entry.OldValues);
        Assert.DoesNotContain(replacement, entry.NewValues);
        Assert.True(entry.OldValues.Length < 1_000);
        Assert.True(entry.NewValues.Length < 1_000);
        using var newValues = System.Text.Json.JsonDocument.Parse(entry.NewValues);
        Assert.True(newValues.RootElement.GetProperty("Content").GetProperty("diffOmitted").GetBoolean());
        Assert.Equal(40_000, newValues.RootElement.GetProperty("Content").GetProperty("utf8Bytes").GetInt32());
    }

    [Fact]
    public async Task ChangelogVersionsAreUniqueAndEntriesAreAppendOnly()
    {
        var changelogs = new CommunityChangelogDalStub([new ProductChangelog { Id = 3, ProductId = 5, Version = "1.0" }]);
        var rules = new ProductResourceRules(new CommunityProductDalStub([new Product { ProductId = 5, ProductStatusId = 1 }]),
            new CommunityProductStatusDalStub([new ProductStatus { Id = 1, Code = "published" }]),
            new CommunityVerificationDalStub(), new DeterministicContentModerationRule());
        var audit = new CommunityAuditDalStub();
        var manager = new ProductChangelogManager(changelogs, rules, audit, new UnitOfWorkStub());
        var duplicate = await manager.CreateAsync(5, 7, new ProductChangelogRequest
            { Version = "1.0", Title = "Release", Content = "Notes", ReleaseDate = System.DateTime.UtcNow });
        var added = await manager.CreateAsync(5, 7, new ProductChangelogRequest
            { Version = "1.1", Title = "Release", Content = "Notes", ReleaseDate = System.DateTime.UtcNow });

        Assert.IsAssignableFrom<IConflictResult>(duplicate);
        Assert.True(added.Success);
        Assert.Single(audit.Entries);
        Assert.DoesNotContain(typeof(IProductChangelogService).GetMethods(), method => method.Name is "UpdateAsync" or "DeleteAsync");
    }

    [Fact]
    public async Task ContributorCanCreditSelfButCannotAssignCreditToAnotherAccount()
    {
        var contributors = new CommunityContributorDalStub();
        var product = new Product { ProductId = 5, ProductStatusId = 1, CreatedByUserId = 7 };
        var rules = new ProductResourceRules(new CommunityProductDalStub([product]),
            new CommunityProductStatusDalStub([new ProductStatus { Id = 1, Code = "published" }]),
            new CommunityVerificationDalStub(), new DeterministicContentModerationRule());
        var manager = new ProductContributorManager(contributors, new CommunityProductDalStub([product]),
            new CommunityUserDalStub([new User { Id = 7, Status = true }, new User { Id = 8, Status = true }]),
            rules, new CommunityAuditDalStub(), new UnitOfWorkStub());

        var denied = await manager.AddAsync(5, 8, new ProductContributorRequest
            { UserId = 7, ContributionType = "Documentation" });
        var allowed = await manager.AddAsync(5, 8, new ProductContributorRequest
            { UserId = 8, ContributionType = "Documentation" });

        Assert.False(denied.Success);
        Assert.IsAssignableFrom<IForbiddenResult>(denied);
        Assert.True(allowed.Success);
        Assert.Equal(8, Assert.Single(contributors.GetAll()).UserId);
    }
}
