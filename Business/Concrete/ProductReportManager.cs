using System;
using Business.Abstract;
using Business.BusinessAspect.Autofac;
using Business.Utilities;
using Business.ValidationRules.FluentValidation;
using Core.Aspects.Autofac.Validation;
using Core.Aspects.Transaction;
using Core.DataAccess;
using Core.Utilities.Results;
using DataAccess.Abstract;
using Entities;
using Entities.DTOs;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace Business.Concrete;

public sealed class ProductReportManager : IProductReportService
{
    private readonly IProductReportDal _reportDal;
    private readonly IProductDal _productDal;
    private readonly IReportReasonDal _reasonDal;
    private readonly IReportStatusDal _statusDal;
    private readonly IAuditLogDal _auditLogDal;
    private readonly IUnitOfWork _unitOfWork;

    public ProductReportManager(IProductReportDal reportDal, IProductDal productDal,
        IReportReasonDal reasonDal, IReportStatusDal statusDal, IAuditLogDal auditLogDal,
        IUnitOfWork unitOfWork)
    {
        _reportDal = reportDal;
        _productDal = productDal;
        _reasonDal = reasonDal;
        _statusDal = statusDal;
        _auditLogDal = auditLogDal;
        _unitOfWork = unitOfWork;
    }

    [ValidationAspect(typeof(ProductReportRequestValidator))]
    [TransactionScopeAspect]
    public async Task<IResult> SubmitAsync(int productId, int reporterUserId, ProductReportRequest request,
        CancellationToken cancellationToken = default)
    {
        if (await _productDal.GetAsync(product => product.ProductId == productId, cancellationToken) is null)
            return new NotFoundResult("Product was not found.");
        var reason = await _reasonDal.GetAsync(item => item.Id == request.ReasonId && item.IsActive, cancellationToken);
        if (reason is null) return new ErrorResult("Report reason is unavailable.");
        var openStatus = await _statusDal.GetAsync(status => status.Code == "open", cancellationToken);
        if (openStatus is null) return new ErrorResult("Open report status is not configured.");

        var report = new ProductReport
        {
            ProductId = productId,
            ReportedByUserId = reporterUserId,
            ReportReasonId = reason.Id,
            ReportStatusId = openStatus.Id,
            Description = request.Description,
            CreatedAt = DateTime.UtcNow
        };
        await _reportDal.AddAsync(report, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await _auditLogDal.AddAsync(AuditLogFactory.Create(reporterUserId, nameof(ProductReport), report.Id.ToString(), "Submitted",
            new { }, new
            {
                report.ProductId, report.ReportedByUserId, report.ReportReasonId,
                report.ReportStatusId, report.Description, report.CreatedAt,
                report.ResolvedAt, report.ResolvedByUserId
            }), cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return new SuccessResult("Product report submitted.");
    }

    [SecuredOperationAspect("ProductReport.Review")]
    [ValidationAspect(typeof(ReviewNoteRequestValidator))]
    [TransactionScopeAspect]
    public Task<IResult> ResolveAsync(int reportId, int reviewerUserId, ReviewNoteRequest request,
        CancellationToken cancellationToken = default) =>
        ChangeStatusAsync(reportId, reviewerUserId, "resolved", request.Note, cancellationToken);

    [SecuredOperationAspect("ProductReport.Review")]
    [ValidationAspect(typeof(ReviewNoteRequestValidator))]
    [TransactionScopeAspect]
    public Task<IResult> DismissAsync(int reportId, int reviewerUserId, ReviewNoteRequest request,
        CancellationToken cancellationToken = default) =>
        ChangeStatusAsync(reportId, reviewerUserId, "dismissed", request.Note, cancellationToken);

    [SecuredOperationAspect("ProductReport.Review")]
    [ValidationAspect(typeof(PageRequestValidator))]
    public async Task<IDataResult<PagedResult<ProductReport>>> GetOpenAsync(PageRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request is null || request.Page < 1 || request.PageSize is < 1 or > 100)
            return new ErrorDataResult<PagedResult<ProductReport>>("Invalid moderation page request.");
        var offset = ((long)request.Page - 1) * request.PageSize;
        if (offset > int.MaxValue)
            return new ErrorDataResult<PagedResult<ProductReport>>("Requested page is out of range.");
        var openStatus = await _statusDal.GetAsync(status => status.Code == "open", cancellationToken);
        if (openStatus is null)
            return new ErrorDataResult<PagedResult<ProductReport>>("Open report status is not configured.");
        var items = await _reportDal.GetByStatusPageAsync(openStatus.Id, (int)offset, request.PageSize, cancellationToken);
        var totalCount = await _reportDal.CountByStatusAsync(openStatus.Id, cancellationToken);
        return new SuccessDataResult<PagedResult<ProductReport>>(
            new PagedResult<ProductReport>(items, request.Page, request.PageSize, totalCount));
    }

    private async Task<IResult> ChangeStatusAsync(int reportId, int reviewerUserId, string statusCode,
        string reviewNote, CancellationToken cancellationToken)
    {
        var report = await _reportDal.GetAsync(item => item.Id == reportId, cancellationToken);
        if (report is null) return new NotFoundResult("Product report was not found.");
        var currentStatus = await _statusDal.GetAsync(item => item.Id == report.ReportStatusId, cancellationToken);
        if (currentStatus?.Code != "open") return new ErrorResult("Only open reports can be reviewed.");
        var targetStatus = await _statusDal.GetAsync(item => item.Code == statusCode, cancellationToken);
        if (targetStatus is null) return new ErrorResult("Target report status is not configured.");

        var oldValues = new
        {
            Status = currentStatus.Code,
            report.ResolvedAt,
            report.ResolvedByUserId
        };
        report.ReportStatusId = targetStatus.Id;
        report.ResolvedAt = DateTime.UtcNow;
        report.ResolvedByUserId = reviewerUserId;
        _reportDal.Update(report);
        await _auditLogDal.AddAsync(AuditLogFactory.Create(reviewerUserId, nameof(ProductReport), reportId.ToString(),
            statusCode == "resolved" ? "Resolved" : "Dismissed", oldValues,
            new
            {
                Status = targetStatus.Code, report.ResolvedAt, report.ResolvedByUserId,
                ReviewNote = reviewNote
            }), cancellationToken);
        try
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return new ConflictResult("This Product report was reviewed by another moderator. Reload and try again.");
        }
        return new SuccessResult($"Product report {statusCode}.");
    }
}
