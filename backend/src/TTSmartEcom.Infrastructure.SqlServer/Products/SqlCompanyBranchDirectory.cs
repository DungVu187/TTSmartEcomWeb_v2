using Microsoft.Data.SqlClient;
using TTSmartEcom.Application.Abstractions.Products;

namespace TTSmartEcom.Infrastructure.SqlServer.Products;

public sealed class SqlCompanyBranchDirectory(IControlDbConnectionFactory factory) : ICompanyBranchDirectory
{
    public async Task<IReadOnlyDictionary<Guid, BranchCompanyReference>> FindBranchesAsync(
        IReadOnlyCollection<Guid> branchIds,
        CancellationToken cancellationToken)
    {
        Guid[] ids = branchIds.Where(static id => id != Guid.Empty).Distinct().ToArray();
        if (ids.Length == 0) return new Dictionary<Guid, BranchCompanyReference>();

        await using SqlConnection connection = factory.Create();
        await connection.OpenAsync(cancellationToken);
        await using SqlCommand command = connection.CreateCommand();
        command.CommandText = $"""
            SELECT BranchId,CompanyId,BranchCode,Status,IsDeleted
            FROM dbo.Branches
            WHERE BranchId IN ({string.Join(',', ids.Select((_, index) => "@branch" + index))});
            """;
        for (int index = 0; index < ids.Length; index++)
            command.Parameters.AddWithValue("@branch" + index, ids[index]);

        Dictionary<Guid, BranchCompanyReference> result = [];
        await using SqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            Guid branchId = reader.GetGuid(0);
            result[branchId] = new BranchCompanyReference(
                branchId,
                reader.GetGuid(1),
                reader.GetString(2),
                reader.GetByte(3) == 1 && !reader.GetBoolean(4));
        }

        return result;
    }
}
