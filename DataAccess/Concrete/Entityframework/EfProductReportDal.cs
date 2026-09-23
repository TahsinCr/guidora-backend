using Core.DataAccess.EntityFramework;
using DataAccess.Abstract;
using DataAccess.Concrete.Entityframework.Contexts;
using Entities;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace DataAccess.Concrete.Entityframework
{
    public class EfProductReportDal : EfEntityRepositoryBase<ProductReport, GuidoraDbContext>, IProductReportDal
    {
        public EfProductReportDal(GuidoraDbContext context) : base(context) { }

        public Task<List<ProductReport>> GetByStatusPageAsync(int statusId, int skip, int take,
            CancellationToken cancellationToken = default) => Context.ProductReports.AsNoTracking()
            .Where(report => report.ReportStatusId == statusId)
            .OrderBy(report => report.CreatedAt).ThenBy(report => report.Id)
            .Skip(skip).Take(take).ToListAsync(cancellationToken);

        public Task<int> CountByStatusAsync(int statusId, CancellationToken cancellationToken = default) =>
            Context.ProductReports.AsNoTracking()
                .CountAsync(report => report.ReportStatusId == statusId, cancellationToken);
    }
}
