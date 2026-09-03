using System.Data;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using TTSmartEcom.Application.Abstractions.Products;
using TTSmartEcom.Domain.Products;

namespace TTSmartEcom.Infrastructure.SqlServer.Products;

public sealed class SqlProductBranchAssignmentRepository(ICompanyDbConnectionFactory factory)
    : IProductBranchAssignmentRepository
{
    public async Task<ProductBranchAssignmentQueryResult> ListForProductAsync(
        Guid companyId,
        string productPublicId,
        CancellationToken cancellationToken)
    {
        await using SqlConnection connection = factory.Create();
        await connection.OpenAsync(cancellationToken);
        await EnsureCompanyAsync(connection, null, companyId, cancellationToken);

        Guid? productId = await FindProductIdAsync(connection, null, productPublicId, cancellationToken);
        if (!productId.HasValue) return new(false, []);

        return new(true, await ReadAssignmentsAsync(connection, null, [productId.Value], cancellationToken));
    }

    public async Task<bool?> IsActiveAsync(
        Guid companyId,
        string productPublicId,
        Guid branchId,
        CancellationToken cancellationToken)
    {
        await using SqlConnection connection = factory.Create();
        await connection.OpenAsync(cancellationToken);
        await EnsureCompanyAsync(connection, null, companyId, cancellationToken);
        Guid? productId = await FindProductIdAsync(connection, null, productPublicId, cancellationToken);
        if (!productId.HasValue) return null;

        await using SqlCommand command = new("""
            SELECT IsActive
            FROM dbo.ProductBranchAssignments
            WHERE ProductId=@productId AND BranchId=@branchId;
            """, connection);
        command.Parameters.AddWithValue("@productId", productId.Value);
        command.Parameters.AddWithValue("@branchId", branchId);
        object? value = await command.ExecuteScalarAsync(cancellationToken);
        return value is not null && value is not DBNull && Convert.ToBoolean(value, System.Globalization.CultureInfo.InvariantCulture);
    }

    public async Task<ProductBranchAssignmentMutationResult> SetActiveAsync(
        Guid companyId,
        IReadOnlyCollection<string> productPublicIds,
        IReadOnlyCollection<Guid> branchIds,
        bool isActive,
        Guid? actorUserId,
        string actorName,
        CancellationToken cancellationToken)
    {
        await using SqlConnection connection = factory.Create();
        await connection.OpenAsync(cancellationToken);
        await using SqlTransaction transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken);
        try
        {
            await EnsureCompanyAsync(connection, transaction, companyId, cancellationToken);
            IReadOnlyDictionary<string, Guid> products = await FindProductIdsAsync(
                connection, transaction, productPublicIds, cancellationToken);
            string[] missing = productPublicIds.Where(id => !products.ContainsKey(id)).ToArray();
            if (missing.Length > 0)
            {
                await transaction.RollbackAsync(cancellationToken);
                return new(false, 0, [], missing);
            }

            long changed = 0;
            foreach ((string publicId, Guid productId) in products)
            {
                foreach (Guid branchId in branchIds)
                {
                    changed += await SetOneAsync(
                        connection, transaction, productId, branchId, isActive, actorUserId, cancellationToken);
                }
            }

            if (changed > 0)
            {
                await AppendAuditAsync(
                    connection,
                    transaction,
                    actorName,
                    isActive ? "assign_product_branches" : "revoke_product_branches",
                    productPublicIds,
                    branchIds,
                    changed,
                    cancellationToken);
            }

            IReadOnlyList<ProductBranchAssignment> result = await ReadAssignmentsAsync(
                connection, transaction, products.Values, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new(true, changed, result, []);
        }
        catch
        {
            if (transaction.Connection is not null) await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private static async Task<int> SetOneAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        Guid productId,
        Guid branchId,
        bool isActive,
        Guid? actorUserId,
        CancellationToken cancellationToken)
    {
        await using SqlCommand command = new(isActive ? """
            DECLARE @changed int=0;
            UPDATE dbo.ProductBranchAssignments WITH (UPDLOCK,HOLDLOCK)
            SET IsActive=1,AssignedAtUtc=SYSUTCDATETIME(),AssignedByUserId=@actor,
                RevokedAtUtc=NULL,RevokedByUserId=NULL
            WHERE ProductId=@product AND BranchId=@branch AND IsActive=0;
            IF @@ROWCOUNT=1 SET @changed=1;
            IF @changed=0 AND NOT EXISTS
                (SELECT 1 FROM dbo.ProductBranchAssignments WITH (UPDLOCK,HOLDLOCK)
                 WHERE ProductId=@product AND BranchId=@branch)
            BEGIN
                INSERT dbo.ProductBranchAssignments
                    (ProductBranchAssignmentId,ProductId,BranchId,IsActive,AssignedAtUtc,AssignedByUserId)
                VALUES (NEWID(),@product,@branch,1,SYSUTCDATETIME(),@actor);
                SET @changed=1;
            END;
            SELECT @changed;
            """ : """
            DECLARE @changed int=0;
            UPDATE dbo.ProductBranchAssignments WITH (UPDLOCK,HOLDLOCK)
            SET IsActive=0,RevokedAtUtc=SYSUTCDATETIME(),RevokedByUserId=@actor
            WHERE ProductId=@product AND BranchId=@branch AND IsActive=1;
            IF @@ROWCOUNT=1 SET @changed=1;
            SELECT @changed;
            """, connection, transaction);
        command.Parameters.AddWithValue("@product", productId);
        command.Parameters.AddWithValue("@branch", branchId);
        command.Parameters.AddWithValue("@actor", (object?)actorUserId ?? DBNull.Value);
        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken), System.Globalization.CultureInfo.InvariantCulture);
    }

    private static async Task AppendAuditAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        string actorName,
        string action,
        IReadOnlyCollection<string> productIds,
        IReadOnlyCollection<Guid> branchIds,
        long changedCount,
        CancellationToken cancellationToken)
    {
        string detail = JsonSerializer.Serialize(new
        {
            productIds = productIds.Order(StringComparer.Ordinal).ToArray(),
            branchIds = branchIds.Order().ToArray(),
            changedCount,
        });
        await using SqlCommand command = new("""
            INSERT dbo.ActivityLogs
                (ActivityLogId,PublicId,Action,ActorName,DetailsJson,CreatedAtUtc,Version)
            VALUES (NEWID(),@publicId,@action,@actor,@details,SYSUTCDATETIME(),0);
            """, connection, transaction);
        command.Parameters.AddWithValue("@publicId", SqlPublicIds.New());
        command.Parameters.AddWithValue("@action", action);
        command.Parameters.AddWithValue("@actor", actorName);
        command.Parameters.AddWithValue("@details", detail);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task EnsureCompanyAsync(
        SqlConnection connection,
        SqlTransaction? transaction,
        Guid companyId,
        CancellationToken cancellationToken)
    {
        await using SqlCommand command = new("""
            SELECT CompanyId
            FROM dbo.CompanyDatabaseInfo
            WHERE SingletonKey=1 AND DatabaseKind=N'CompanyShared';
            """, connection, transaction);
        object? value = await command.ExecuteScalarAsync(cancellationToken);
        if (value is not Guid configured || configured != companyId)
            throw new UnauthorizedAccessException("Company database assignment does not match the authenticated company scope.");
    }

    private static async Task<Guid?> FindProductIdAsync(
        SqlConnection connection,
        SqlTransaction? transaction,
        string productPublicId,
        CancellationToken cancellationToken)
    {
        await using SqlCommand command = new(
            "SELECT ProductId FROM dbo.Products WHERE PublicId=@id AND IsDeleted=0;",
            connection,
            transaction);
        command.Parameters.AddWithValue("@id", productPublicId);
        object? value = await command.ExecuteScalarAsync(cancellationToken);
        return value is Guid id ? id : null;
    }

    private static async Task<IReadOnlyDictionary<string, Guid>> FindProductIdsAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        IReadOnlyCollection<string> productPublicIds,
        CancellationToken cancellationToken)
    {
        string[] ids = productPublicIds.Distinct(StringComparer.Ordinal).ToArray();
        await using SqlCommand command = new() { Connection = connection, Transaction = transaction };
        command.CommandText = $"""
            SELECT PublicId,ProductId
            FROM dbo.Products WITH (UPDLOCK,HOLDLOCK)
            WHERE IsDeleted=0 AND PublicId IN ({string.Join(',', ids.Select((_, index) => "@product" + index))});
            """;
        for (int index = 0; index < ids.Length; index++)
            command.Parameters.AddWithValue("@product" + index, ids[index]);
        Dictionary<string, Guid> result = new(StringComparer.Ordinal);
        await using SqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken)) result[reader.GetString(0)] = reader.GetGuid(1);
        return result;
    }

    private static async Task<IReadOnlyList<ProductBranchAssignment>> ReadAssignmentsAsync(
        SqlConnection connection,
        SqlTransaction? transaction,
        IEnumerable<Guid> productIds,
        CancellationToken cancellationToken)
    {
        Guid[] ids = productIds.Distinct().ToArray();
        if (ids.Length == 0) return [];
        await using SqlCommand command = new() { Connection = connection, Transaction = transaction };
        command.CommandText = $"""
            SELECT p.PublicId,a.BranchId,a.IsActive,a.AssignedAtUtc,a.AssignedByUserId,
                   a.RevokedAtUtc,a.RevokedByUserId,a.RowVersion
            FROM dbo.ProductBranchAssignments a
            INNER JOIN dbo.Products p ON p.ProductId=a.ProductId
            WHERE a.ProductId IN ({string.Join(',', ids.Select((_, index) => "@product" + index))})
            ORDER BY p.PublicId,a.BranchId;
            """;
        for (int index = 0; index < ids.Length; index++) command.Parameters.AddWithValue("@product" + index, ids[index]);
        List<ProductBranchAssignment> result = [];
        await using SqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(new ProductBranchAssignment(
                reader.GetString(0),
                reader.GetGuid(1),
                reader.GetBoolean(2),
                new DateTimeOffset(reader.GetDateTime(3), TimeSpan.Zero),
                reader.IsDBNull(4) ? null : reader.GetGuid(4),
                reader.IsDBNull(5) ? null : new DateTimeOffset(reader.GetDateTime(5), TimeSpan.Zero),
                reader.IsDBNull(6) ? null : reader.GetGuid(6),
                Convert.ToBase64String((byte[])reader[7])));
        }
        return result;
    }
}
