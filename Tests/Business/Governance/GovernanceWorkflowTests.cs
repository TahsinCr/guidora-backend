using Business.BusinessRules.Catalog;
using Business.Concrete;
using Core.CrossCuttingConcerns.Caching;
using Core.Utilities.Results;
using DataAccess.Abstract;
using Entities;
using Entities.DTOs;
using Tests.Core.TestDoubles;

namespace Tests.Business.Governance;

public sealed class GovernanceWorkflowTests
{
    [Fact]
    public async Task VerificationSummaryShowsPublicStateAndOnlyCurrentUsersVote()
    {
        var product = new Product { ProductId = 5, ProductStatusId = 1 };
        var manager = new ProductVerificationManager(
            new ProductDal([product]),
            new ProductStatusDal([new ProductStatus { Id = 1, Code = "published" }]),
            new VerificationTypeDal([
                new VerificationType { Id = 1, Code = "admin" },
                new VerificationType { Id = 2, Code = "community" }
            ]),
            new VerificationDal([
                new ProductVerification { ProductId = 5, VerificationTypeId = 1 },
                new ProductVerification { ProductId = 5, VerificationTypeId = 2, RevokedAt = DateTime.UtcNow }
            ]),
            new VerificationVoteDal([
                new ProductVerificationVote { ProductId = 5, UserId = 7, Approved = true },
                new ProductVerificationVote { ProductId = 5, UserId = 8, Approved = false }
            ]), new AuditDal(), new UnitOfWorkStub());

        var publicResult = await manager.GetSummaryAsync(5, null);
        var voterResult = await manager.GetSummaryAsync(5, 8);

        Assert.True(publicResult.Success);
        Assert.True(publicResult.Data.AdminVerified);
        Assert.False(publicResult.Data.CommunityVerified);
        Assert.True(publicResult.Data.CommunityRevoked);
        Assert.Equal(1, publicResult.Data.PositiveVotes);
        Assert.Equal(5, publicResult.Data.RequiredPositiveVotes);
        Assert.Null(publicResult.Data.CurrentUserHasVoted);
        Assert.Null(publicResult.Data.CurrentUserVote);
        Assert.True(voterResult.Data.CurrentUserHasVoted);
        Assert.False(voterResult.Data.CurrentUserVote);

        product.ProductStatusId = 2;
        Assert.IsAssignableFrom<INotFoundResult>(await manager.GetSummaryAsync(5, null));
    }

    [Fact]
    public async Task AcceptingPendingProposalAppliesSnapshotAndWritesAuditAtomically()
    {
        var product = new Product { ProductId = 5, Name = "Old", Slug = "old" };
        var proposal = new ProductEditProposal
        {
            Id = 12, ProductId = 5, Status = ProductEditProposalStatus.Pending,
            Name = "New", Slug = "new", ShortDescription = "short", Description = "details",
            WebsiteUrl = "https://new.test", LogoUrl = "https://new.test/logo"
        };
        var products = new ProductDal([product]);
        var productCategories = new ProductCategoryDal([
            new ProductCategory { ProductId = 5, CategoryId = 2 },
            new ProductCategory { ProductId = 5, CategoryId = 4 }
        ]);
        var productTags = new ProductTagDal([
            new ProductTag { ProductId = 5, TagId = 10 },
            new ProductTag { ProductId = 5, TagId = 22 }
        ]);
        var proposalDal = new ProposalDal([proposal]);
        var proposalCategories = new ProposalCategoryDal([
            new ProductEditProposalCategory { ProductEditProposalId = 12, CategoryId = 4 },
            new ProductEditProposalCategory { ProductEditProposalId = 12, CategoryId = 6 }
        ]);
        var proposalTags = new ProposalTagDal([
            new ProductEditProposalTag { ProductEditProposalId = 12, TagId = 22 },
            new ProductEditProposalTag { ProductEditProposalId = 12, TagId = 23 }
        ]);
        var categories = new CategoryDal([
            new Category { CategoryId = 4, IsActive = true },
            new Category { CategoryId = 6, IsActive = true }
        ]);
        var tags = new TagDal([
            new Tag { Id = 22, TagCategoryId = 3, IsActive = true },
            new Tag { Id = 23, TagCategoryId = 3, IsActive = true }
        ]);
        var tagCategories = new TagCategoryDal([new TagCategory { Id = 3, IsActive = true, AllowsMultipleSelection = true }]);
        var audit = new AuditDal();
        var unitOfWork = new UnitOfWorkStub();
        var manager = new ProductEditProposalManager(proposalDal, products, productCategories, productTags,
            proposalCategories, proposalTags, tags, tagCategories,
            new ProductBusinessRules(products, categories), new TagBusinessRules(), audit, unitOfWork);

        var result = await manager.AcceptAsync(12, 99, new ReviewNoteRequest { Note = "Reviewed" });

        Assert.True(result.Success);
        var cacheDirective = Assert.IsAssignableFrom<ICacheInvalidationDirective>(result);
        Assert.Contains(cacheDirective.CacheInvalidations, target => target.Profile == CacheProfiles.ProductDetail &&
            target.ArgumentIndex == 0 && Equals(target.ArgumentValue, product.ProductId));
        Assert.Equal("New", product.Name);
        Assert.Equal(ProductEditProposalStatus.Accepted, proposal.Status);
        Assert.Equal(99, proposal.ReviewedByUserId);
        Assert.Equal([4, 6], productCategories.GetAll().Select(item => item.CategoryId).Order());
        Assert.Equal(1, productCategories.DeleteCount);
        Assert.Equal([22, 23], productTags.GetAll().Select(item => item.TagId).Order());
        Assert.Equal(1, productTags.DeleteCount);
        Assert.Equal(2, audit.Entries.Count);
        var auditEntry = Assert.Single(audit.Entries, entry => entry.Action == "EditProposalAccepted");
        Assert.Equal("EditProposalAccepted", auditEntry.Action);
        Assert.Equal(99, auditEntry.ActorUserId);
        Assert.Contains(audit.Entries, entry => entry.Action == "Accepted");
        Assert.Equal(1, unitOfWork.SaveCount);
    }

    [Fact]
    public async Task RejectingProposalDoesNotChangeCanonicalProductAndCannotBeRepeated()
    {
        var product = new Product { ProductId = 5, Name = "Canonical" };
        var proposal = new ProductEditProposal
        {
            Id = 12, ProductId = 5, Status = ProductEditProposalStatus.Pending,
            Name = "Proposed", Slug = "proposed", WebsiteUrl = "https://proposed.example.test"
        };
        var audit = new AuditDal();
        var unitOfWork = new UnitOfWorkStub();
        var manager = CreateProposalManager(product, proposal, audit, unitOfWork);

        var first = await manager.RejectAsync(12, 99, new ReviewNoteRequest { Note = "Insufficient evidence" });
        var repeated = await manager.RejectAsync(12, 100, new ReviewNoteRequest());

        Assert.True(first.Success);
        Assert.False(repeated.Success);
        Assert.IsAssignableFrom<IConflictResult>(repeated);
        Assert.Equal("Canonical", product.Name);
        Assert.Equal(ProductEditProposalStatus.Rejected, proposal.Status);
        Assert.Equal(1, unitOfWork.SaveCount);
        Assert.Single(audit.Entries);
    }

    [Fact]
    public async Task ProposalAcceptanceWithMissingCategoryLeavesBothRecordsUnchanged()
    {
        var product = new Product { ProductId = 5, Name = "Canonical" };
        var proposal = new ProductEditProposal { Id = 12, ProductId = 5, Status = ProductEditProposalStatus.Pending };
        var audit = new AuditDal();
        var unitOfWork = new UnitOfWorkStub();
        var manager = CreateProposalManager(product, proposal, audit, unitOfWork);

        var result = await manager.AcceptAsync(12, 99, new ReviewNoteRequest());

        Assert.False(result.Success);
        Assert.Equal("Canonical", product.Name);
        Assert.Equal(ProductEditProposalStatus.Pending, proposal.Status);
        Assert.Empty(audit.Entries);
        Assert.Equal(0, unitOfWork.SaveCount);
    }

    [Fact]
    public async Task AdminVerificationCanBeRevokedAndReactivatedWithoutDuplicateRows()
    {
        var verification = new ProductVerification { Id = 8, ProductId = 5, VerificationTypeId = 1,
            VerifiedAt = DateTime.UtcNow.AddDays(-1), RevokedAt = DateTime.UtcNow.AddHours(-1) };
        var verificationDal = new VerificationDal([verification]);
        var audit = new AuditDal();
        var unitOfWork = new UnitOfWorkStub();
        var manager = new ProductVerificationManager(new ProductDal([new Product { ProductId = 5 }]),
            new ProductStatusDal(), new VerificationTypeDal([new VerificationType { Id = 1, Code = "admin" }]), verificationDal,
            new VerificationVoteDal(), audit, unitOfWork);

        var restored = await manager.VerifyAsAdminAsync(5, 99, new ReviewNoteRequest { Note = "Checked" });
        var revoked = await manager.RevokeAdminVerificationAsync(5, 99, new ReviewNoteRequest { Note = "Outdated" });

        Assert.True(restored.Success);
        Assert.True(revoked.Success);
        Assert.Single(verificationDal.GetAll());
        Assert.NotNull(verification.RevokedAt);
        Assert.Equal(99, verification.VerifiedByUserId);
        Assert.Equal(2, audit.Entries.Count);
        Assert.Equal(2, unitOfWork.SaveCount);
    }

    [Fact]
    public async Task DuplicateActiveAdminVerificationIsRejectedWithoutWriting()
    {
        var verificationDal = new VerificationDal([new ProductVerification
            { Id = 8, ProductId = 5, VerificationTypeId = 1, VerifiedAt = DateTime.UtcNow }]);
        var audit = new AuditDal();
        var unitOfWork = new UnitOfWorkStub();
        var manager = new ProductVerificationManager(new ProductDal([new Product { ProductId = 5 }]),
            new ProductStatusDal(), new VerificationTypeDal([new VerificationType { Id = 1, Code = "admin" }]), verificationDal,
            new VerificationVoteDal(), audit, unitOfWork);

        var result = await manager.VerifyAsAdminAsync(5, 99, new ReviewNoteRequest());

        Assert.False(result.Success);
        Assert.IsAssignableFrom<IConflictResult>(result);
        Assert.Single(verificationDal.GetAll());
        Assert.Empty(audit.Entries);
        Assert.Equal(0, unitOfWork.SaveCount);
    }

    [Fact]
    public async Task ReportsStartOpenAndCanBeResolvedOnlyOnce()
    {
        var reportDal = new ReportDal();
        var audit = new AuditDal();
        var unitOfWork = new UnitOfWorkStub();
        var manager = new ProductReportManager(reportDal,
            new ProductDal([new Product { ProductId = 5 }]),
            new ReportReasonDal([new ReportReason { Id = 2, Code = "inaccurate", IsActive = true }]),
            new ReportStatusDal([
                new ReportStatus { Id = 1, Code = "open" },
                new ReportStatus { Id = 2, Code = "resolved" },
                new ReportStatus { Id = 3, Code = "dismissed" }]), audit, unitOfWork);

        var submitted = await manager.SubmitAsync(5, 44,
            new ProductReportRequest { ReasonId = 2, Description = "Bad URL" });
        var resolved = await manager.ResolveAsync(70, 99, new ReviewNoteRequest { Note = "Corrected" });
        var repeated = await manager.DismissAsync(70, 99, new ReviewNoteRequest());

        Assert.True(submitted.Success);
        Assert.True(resolved.Success);
        Assert.False(repeated.Success);
        Assert.IsAssignableFrom<IConflictResult>(repeated);
        var report = Assert.Single(reportDal.GetAll());
        Assert.Equal(2, report.ReportStatusId);
        Assert.Equal(99, report.ResolvedByUserId);
        Assert.Equal(2, audit.Entries.Count);
        Assert.Equal(3, unitOfWork.SaveCount);
    }

    [Fact]
    public async Task ProposalReviewQueueReturnsOldestPendingItemsWithPageMetadata()
    {
        var proposalDal = new ProposalDal([
            new ProductEditProposal { Id = 1, Status = ProductEditProposalStatus.Pending,
                CreatedAt = DateTime.UtcNow.AddMinutes(-2) },
            new ProductEditProposal { Id = 2, Status = ProductEditProposalStatus.Pending,
                CreatedAt = DateTime.UtcNow.AddMinutes(-1) },
            new ProductEditProposal { Id = 3, Status = ProductEditProposalStatus.Accepted }
        ]);
        var products = new ProductDal();
        var manager = new ProductEditProposalManager(proposalDal, products, new ProductCategoryDal(),
            new ProductTagDal(), new ProposalCategoryDal(), new ProposalTagDal(), new TagDal(),
            new TagCategoryDal(), new ProductBusinessRules(products, new CategoryDal()), new TagBusinessRules(),
            new AuditDal(), new UnitOfWorkStub());

        var result = await manager.GetPendingAsync(new PageRequest { Page = 2, PageSize = 1 });

        Assert.True(result.Success);
        Assert.Equal(2, result.Data!.Items.Single().Id);
        Assert.Equal(2, result.Data.TotalCount);
        Assert.Equal(2, result.Data.Page);
    }

    [Fact]
    public async Task ReportReviewQueueContainsOnlyOpenReportsInStableOrder()
    {
        var reports = new ReportDal([
            new ProductReport { Id = 9, ReportStatusId = 1, CreatedAt = DateTime.UtcNow.AddMinutes(-1) },
            new ProductReport { Id = 8, ReportStatusId = 1, CreatedAt = DateTime.UtcNow.AddMinutes(-2) },
            new ProductReport { Id = 7, ReportStatusId = 2 }
        ]);
        var manager = new ProductReportManager(reports, new ProductDal(), new ReportReasonDal(),
            new ReportStatusDal([new ReportStatus { Id = 1, Code = "open" }]), new AuditDal(),
            new UnitOfWorkStub());

        var result = await manager.GetOpenAsync(new PageRequest { Page = 1, PageSize = 10 });

        Assert.True(result.Success);
        Assert.Equal([8, 9], result.Data!.Items.Select(report => report.Id));
        Assert.Equal(2, result.Data.TotalCount);
    }

    private static ProductEditProposalManager CreateProposalManager(Product product, ProductEditProposal proposal,
        AuditDal audit, UnitOfWorkStub unitOfWork)
    {
        var products = new ProductDal([product]);
        return new ProductEditProposalManager(new ProposalDal([proposal]), products, new ProductCategoryDal(),
            new ProductTagDal(), new ProposalCategoryDal(), new ProposalTagDal(), new TagDal(),
            new TagCategoryDal(), new ProductBusinessRules(products, new CategoryDal()), new TagBusinessRules(),
            audit, unitOfWork);
    }

    private sealed class ProductDal(IEnumerable<Product>? values = null) : EntityRepositoryStub<Product>(values), IProductDal
    {
        public Task<List<Product>> GetPublishedPageAsync(int statusId, int? categoryId, int skip, int take,
            CancellationToken cancellationToken = default) => Task.FromResult(categoryId.HasValue ? [] :
            GetAll(item => item.ProductStatusId == statusId).OrderBy(item => item.Name).Skip(skip).Take(take).ToList());
        public Task<int> CountPublishedAsync(int statusId, int? categoryId,
            CancellationToken cancellationToken = default) => Task.FromResult(categoryId.HasValue ? 0 :
            GetAll(item => item.ProductStatusId == statusId).Count);
    }
    private sealed class ProposalDal(IEnumerable<ProductEditProposal>? values = null) : EntityRepositoryStub<ProductEditProposal>(values), IProductEditProposalDal;
    private sealed class CategoryDal(IEnumerable<Category>? values = null) : EntityRepositoryStub<Category>(values), ICategoryDal;
    private sealed class TagDal(IEnumerable<Tag>? values = null) : EntityRepositoryStub<Tag>(values), ITagDal;
    private sealed class TagCategoryDal(IEnumerable<TagCategory>? values = null) : EntityRepositoryStub<TagCategory>(values), ITagCategoryDal;
    private sealed class ProposalCategoryDal(IEnumerable<ProductEditProposalCategory>? values = null) : EntityRepositoryStub<ProductEditProposalCategory>(values), IProductEditProposalCategoryDal;
    private sealed class ProposalTagDal(IEnumerable<ProductEditProposalTag>? values = null) : EntityRepositoryStub<ProductEditProposalTag>(values), IProductEditProposalTagDal;

    private sealed class ProductCategoryDal(IEnumerable<ProductCategory>? values = null)
        : EntityRepositoryStub<ProductCategory>(values), IProductCategoryDal
    {
        public override void PurgeRange(IEnumerable<ProductCategory> entities)
        {
            var removed = entities.ToArray();
            foreach (var entity in removed) Purge(entity);
        }
        public override void Purge(ProductCategory entity)
        {
            base.Purge(entity);
            Entities.Remove(entity);
        }
    }

    private sealed class ProductTagDal(IEnumerable<ProductTag>? values = null)
        : EntityRepositoryStub<ProductTag>(values), IProductTagDal
    {
        public Task<bool> HasOtherTagInSameCategoryForAssignedProductsAsync(int tagId, int tagCategoryId,
            CancellationToken cancellationToken = default) => Task.FromResult(false);

        public override void PurgeRange(IEnumerable<ProductTag> entities)
        {
            var removed = entities.ToArray();
            foreach (var entity in removed) Purge(entity);
        }
        public override void Purge(ProductTag entity)
        {
            base.Purge(entity);
            Entities.Remove(entity);
        }
    }

    private sealed class VerificationDal(IEnumerable<ProductVerification>? values = null) : EntityRepositoryStub<ProductVerification>(values), IProductVerificationDal;
    private sealed class VerificationVoteDal(IEnumerable<ProductVerificationVote>? values = null) : EntityRepositoryStub<ProductVerificationVote>(values), IProductVerificationVoteDal;
    private sealed class ProductStatusDal(IEnumerable<ProductStatus>? values = null) : EntityRepositoryStub<ProductStatus>(values), IProductStatusDal;
    private sealed class VerificationTypeDal(IEnumerable<VerificationType>? values = null) : EntityRepositoryStub<VerificationType>(values), IVerificationTypeDal;
    private sealed class ReportReasonDal(IEnumerable<ReportReason>? values = null) : EntityRepositoryStub<ReportReason>(values), IReportReasonDal;
    private sealed class ReportStatusDal(IEnumerable<ReportStatus>? values = null) : EntityRepositoryStub<ReportStatus>(values), IReportStatusDal;

    private sealed class ReportDal(IEnumerable<ProductReport>? values = null)
        : EntityRepositoryStub<ProductReport>(values), IProductReportDal
    {
        public override void Add(ProductReport entity)
        {
            entity.Id = 70;
            base.Add(entity);
        }
    }

    private sealed class AuditDal : IAuditLogDal
    {
        public List<AuditLog> Entries { get; } = [];
        public void Add(AuditLog auditLog) => Entries.Add(auditLog);
        public Task AddAsync(AuditLog auditLog, CancellationToken cancellationToken = default)
        {
            Add(auditLog);
            return Task.CompletedTask;
        }
        public IReadOnlyList<AuditLog> GetByEntity(string entityType, string entityId) =>
            Entries.Where(entry => entry.EntityType == entityType && entry.EntityId == entityId).ToArray();
        public Task<IReadOnlyList<AuditLog>> GetByEntityAsync(string entityType, string entityId,
            CancellationToken cancellationToken = default) => Task.FromResult(GetByEntity(entityType, entityId));
        public Task<AuditLog?> GetByIdAsync(long id, CancellationToken cancellationToken = default) =>
            Task.FromResult(Entries.SingleOrDefault(entry => entry.Id == id));
        public Task<bool> HasRevertAsync(long auditLogId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Entries.Any(entry => entry.RevertsAuditLogId == auditLogId));
    }
}
