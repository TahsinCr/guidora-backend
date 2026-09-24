using Core.Utilities.Results;
using System.Threading;
using System.Threading.Tasks;

namespace Business.Abstract;

public interface IAuditRevertService
{
    Task<IResult> RevertAsync(long auditLogId, int actorUserId, CancellationToken cancellationToken = default);
}
