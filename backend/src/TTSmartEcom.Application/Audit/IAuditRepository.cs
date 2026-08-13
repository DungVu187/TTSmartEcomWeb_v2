using TTSmartEcom.Domain.Audit;

namespace TTSmartEcom.Application.Audit;

public interface IAuditRepository
{
    Task<ActivityLogPage> QueryAsync(ActivityLogQuery query, CancellationToken cancellationToken);
}
