using Core.DataAccess;
using Entities;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace DataAccess.Abstract
{
    public interface IProductReportDal : IEntityRepository<ProductReport>
    {
        async Task<List<ProductReport>> GetByStatusPageAsync(int statusId, int skip, int take,
            CancellationToken cancellationToken = default) => (await GetAllAsync(
                report => report.ReportStatusId == statusId, cancellationToken))
            .OrderBy(report => report.CreatedAt).ThenBy(report => report.Id)
            .Skip(skip).Take(take).ToList();
        Task<int> CountByStatusAsync(int statusId, CancellationToken cancellationToken = default) =>
            CountAsync(report => report.ReportStatusId == statusId, cancellationToken);
    }
}
