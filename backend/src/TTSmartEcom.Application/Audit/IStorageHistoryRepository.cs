using TTSmartEcom.Domain.Audit;

namespace TTSmartEcom.Application.Audit;

public interface IStorageHistoryRepository
{
    Task<StorageHistoryPage> QueryAsync(StorageHistoryQuery query, CancellationToken cancellationToken);
    Task<StorageHistoryPage> QueryProductAsync(string productId, int page, int limit, DateTimeOffset? startDate, DateTimeOffset? endDate, CancellationToken cancellationToken);
    Task<StorageHistoryFilterOptions> GetFilterOptionsAsync(CancellationToken cancellationToken);
    Task<long> UpdateOrderNameAsync(string orderId, string newOrderName, CancellationToken cancellationToken);
}
