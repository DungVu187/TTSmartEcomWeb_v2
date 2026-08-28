using System.Globalization;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using TTSmartEcom.Application.Audit;
using TTSmartEcom.Domain.Audit;

namespace TTSmartEcom.Infrastructure.SqlServer.Audit;

#pragma warning disable CA1725

public sealed class SqlStorageHistoryRepository(ISqlConnectionFactory factory) : IStorageHistoryRepository, IStorageHistoryWriter
{
    private const int MaximumExportRows = 10_000;

    public async Task AppendAsync(StorageHistoryWriteEntry entry, CancellationToken ct)
    {
        await using SqlConnection connection = factory.Create();
        await connection.OpenAsync(ct);
        await using SqlTransaction transaction = (SqlTransaction)await connection.BeginTransactionAsync(ct);
        try
        {
            Guid operationId = Guid.NewGuid();
            DateTimeOffset transactionDate = entry.TransactionDate ?? DateTimeOffset.UtcNow;
            string details = JsonSerializer.Serialize(new
            {
                productName = entry.ProductName,
                userName = entry.UserName,
                orderId = entry.OrderId,
                orderName = entry.OrderName,
                note = entry.Note,
                isAIScan = entry.IsAiScan,
                source = entry.Source,
                quantityBefore = entry.QuantityBefore,
                quantityAfter = entry.QuantityAfter,
            });
            await using (SqlCommand command = new("INSERT dbo.StockOperations(StockOperationId,PublicId,OperationType,SourceReference,OccurredAtUtc,TransactionDateUtc,DetailsJson,Version) VALUES(@id,@public,@type,@ref,SYSUTCDATETIME(),@transactionDate,@details,0);", connection, transaction))
            {
                command.Parameters.AddWithValue("@id", operationId);
                command.Parameters.AddWithValue("@public", SqlPublicIds.New());
                command.Parameters.AddWithValue("@type", (object?)entry.Source ?? DBNull.Value);
                command.Parameters.AddWithValue("@ref", (object?)entry.OrderId ?? DBNull.Value);
                command.Parameters.AddWithValue("@transactionDate", transactionDate.UtcDateTime);
                command.Parameters.AddWithValue("@details", details);
                await command.ExecuteNonQueryAsync(ct);
            }
            await using (SqlCommand command = new("INSERT dbo.StockMovementLines(StockMovementLineId,PublicId,StockOperationId,ProductId,SourceProductId,Quantity,DetailsJson,SortOrder,Version) VALUES(NEWID(),@public,@operation,(SELECT ProductId FROM dbo.Products WHERE PublicId=@product),@product,@quantity,@details,0,0);", connection, transaction))
            {
                command.Parameters.AddWithValue("@public", SqlPublicIds.New());
                command.Parameters.AddWithValue("@operation", operationId);
                command.Parameters.AddWithValue("@product", entry.ProductId);
                command.Parameters.AddWithValue("@quantity", entry.Quantity);
                command.Parameters.AddWithValue("@details", details);
                await command.ExecuteNonQueryAsync(ct);
            }
            await transaction.CommitAsync(ct);
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            throw;
        }
    }

    public Task<StorageHistoryPage> QueryAsync(StorageHistoryQuery query, CancellationToken ct) => QueryCoreAsync(query, null, ct);

    public Task<StorageHistoryPage> QueryProductAsync(string productId, int page, int limit, DateTimeOffset? startDate, DateTimeOffset? endDate, CancellationToken ct) =>
        QueryCoreAsync(new StorageHistoryQuery(page, limit, startDate, endDate, null, null, null, null, false), productId, ct);

    public async Task<StorageHistoryFilterOptions> GetFilterOptionsAsync(CancellationToken ct)
    {
        await using SqlConnection connection = factory.Create();
        await connection.OpenAsync(ct);
        return new StorageHistoryFilterOptions(true, await DistinctAsync(connection, "userName", ct), await DistinctAsync(connection, "orderName", ct));
    }

    public async Task<long> UpdateOrderNameAsync(string orderId, string newOrderName, CancellationToken ct)
    {
        await using SqlConnection connection = factory.Create();
        await connection.OpenAsync(ct);
        await using SqlCommand command = new("UPDATE dbo.StockOperations SET DetailsJson=JSON_MODIFY(DetailsJson,'$.orderName',@name) WHERE SourceReference=@id;", connection);
        command.Parameters.AddWithValue("@name", newOrderName);
        command.Parameters.AddWithValue("@id", orderId);
        return await command.ExecuteNonQueryAsync(ct);
    }

    public async Task UpdateTransactionDateAsync(string orderId, DateTimeOffset transactionDate, CancellationToken ct)
    {
        await using SqlConnection connection = factory.Create();
        await connection.OpenAsync(ct);
        await using SqlCommand command = new("UPDATE dbo.StockOperations SET TransactionDateUtc=@date WHERE SourceReference=@id;", connection);
        command.Parameters.AddWithValue("@date", transactionDate.UtcDateTime);
        command.Parameters.AddWithValue("@id", orderId);
        await command.ExecuteNonQueryAsync(ct);
    }

    private async Task<StorageHistoryPage> QueryCoreAsync(StorageHistoryQuery query, string? productId, CancellationToken ct)
    {
        await using SqlConnection connection = factory.Create();
        await connection.OpenAsync(ct);
        List<string> filters = [];
        await using SqlCommand count = new() { Connection = connection };
        AddFilters(filters, count, query, productId);
        count.CommandText = $"SELECT COUNT(*) FROM dbo.StockMovementLines l JOIN dbo.StockOperations o ON o.StockOperationId=l.StockOperationId{Where(filters)};";
        long total = Convert.ToInt64(await count.ExecuteScalarAsync(ct), CultureInfo.InvariantCulture);
        int take = query.ExportAll ? Math.Min(MaximumExportRows, (int)Math.Min(total, MaximumExportRows)) : query.Limit;
        int skip = query.ExportAll ? 0 : checked((query.Page - 1) * query.Limit);
        await using SqlCommand command = new() { Connection = connection };
        AddFilters(filters = [], command, query, productId);
        command.CommandText = $"SELECT l.PublicId,l.SourceProductId,l.Quantity,l.DetailsJson,o.TransactionDateUtc,o.OccurredAtUtc FROM dbo.StockMovementLines l JOIN dbo.StockOperations o ON o.StockOperationId=l.StockOperationId{Where(filters)} ORDER BY COALESCE(o.TransactionDateUtc,o.OccurredAtUtc) DESC,o.OccurredAtUtc DESC OFFSET @skip ROWS FETCH NEXT @take ROWS ONLY;";
        command.Parameters.AddWithValue("@skip", skip);
        command.Parameters.AddWithValue("@take", take);
        List<StorageHistoryEntry> values = [];
        await using SqlDataReader reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            using JsonDocument document = JsonDocument.Parse(reader.IsDBNull(3) ? "{}" : reader.GetString(3));
            JsonElement root = document.RootElement;
            DateTimeOffset? transactionDate = reader.IsDBNull(4) ? null : new DateTimeOffset(reader.GetDateTime(4), TimeSpan.Zero);
            DateTimeOffset? occurredAt = reader.IsDBNull(5) ? null : new DateTimeOffset(reader.GetDateTime(5), TimeSpan.Zero);
            values.Add(new StorageHistoryEntry(reader.GetString(0), reader.IsDBNull(1) ? null : reader.GetString(1), Text(root, "productName"), reader.IsDBNull(2) ? 0 : (double)reader.GetDecimal(2), Text(root, "userName"), Text(root, "orderId"), Text(root, "orderName"), Text(root, "note"), Bool(root, "isAIScan"), Text(root, "source"), transactionDate ?? occurredAt, Double(root, "quantityBefore"), Double(root, "quantityAfter"), occurredAt, occurredAt));
        }
        long responseLimit = query.ExportAll ? Math.Min(total, MaximumExportRows) : query.Limit;
        int pages = query.ExportAll ? (total > 0 ? 1 : 0) : (int)Math.Ceiling(total / (double)query.Limit);
        return new StorageHistoryPage(true, query.Page, responseLimit, total, pages, values);
    }

    private static void AddFilters(List<string> filters, SqlCommand command, StorageHistoryQuery query, string? productId)
    {
        if (productId is not null) { filters.Add("l.SourceProductId=@product"); command.Parameters.AddWithValue("@product", productId); }
        if (query.StartDate.HasValue) { filters.Add("(o.TransactionDateUtc>=@start OR (o.TransactionDateUtc IS NULL AND o.OccurredAtUtc>=@start))"); command.Parameters.AddWithValue("@start", query.StartDate.Value.UtcDateTime); }
        if (query.EndDate.HasValue) { filters.Add("(o.TransactionDateUtc<=@end OR (o.TransactionDateUtc IS NULL AND o.OccurredAtUtc<=@end))"); command.Parameters.AddWithValue("@end", query.EndDate.Value.UtcDateTime); }
        if (!string.IsNullOrWhiteSpace(query.OrderName)) { filters.Add("o.DetailsJson LIKE @order"); command.Parameters.AddWithValue("@order", "%" + query.OrderName + "%"); }
        if (!string.IsNullOrWhiteSpace(query.UserName)) { filters.Add("o.DetailsJson LIKE @user"); command.Parameters.AddWithValue("@user", "%" + query.UserName + "%"); }
        if (query.Direction == "import") filters.Add("(l.Quantity>0 OR o.OperationType=N'import_quantity_adjustment')");
        else if (query.Direction == "export") filters.Add("l.Quantity<0");
        if (!string.IsNullOrWhiteSpace(query.NoteType)) { filters.Add("o.DetailsJson LIKE @note"); command.Parameters.AddWithValue("@note", "%" + query.NoteType + "%"); }
    }

    private static string Where(List<string> filters) => filters.Count == 0 ? string.Empty : " WHERE " + string.Join(" AND ", filters);
    private static async Task<string[]> DistinctAsync(SqlConnection connection, string property, CancellationToken ct)
    {
        await using SqlCommand command = new($"SELECT DISTINCT JSON_VALUE(DetailsJson,'$.{property}') FROM dbo.StockOperations WHERE JSON_VALUE(DetailsJson,'$.{property}') IS NOT NULL AND JSON_VALUE(DetailsJson,'$.{property}')<>N'' ORDER BY JSON_VALUE(DetailsJson,'$.{property}');", connection);
        List<string> result = [];
        await using SqlDataReader reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct)) result.Add(reader.GetString(0));
        return result.ToArray();
    }

    private static string? Text(JsonElement root, string property) => root.TryGetProperty(property, out JsonElement value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
    private static bool Bool(JsonElement root, string property) => root.TryGetProperty(property, out JsonElement value) && value.ValueKind == JsonValueKind.True;
    private static double? Double(JsonElement root, string property) => root.TryGetProperty(property, out JsonElement value) && value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out double result) ? result : null;
}
