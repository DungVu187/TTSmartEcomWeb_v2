using Microsoft.Data.SqlClient;

namespace TTSmartEcom.Infrastructure.SqlServer.Files;

public sealed class SqlFileMetadataRepository(ISqlConnectionFactory factory)
{
    public async Task RecordAsync(string storageKey,string fileName,string? mimeType,long length,string sha256,string sourceUrl,CancellationToken ct)
    {
        await using SqlConnection c=factory.Create();await c.OpenAsync(ct);await using SqlCommand q=new("MERGE dbo.Files WITH(HOLDLOCK) AS t USING(SELECT @key AS StorageKey) s ON t.StorageKey=s.StorageKey WHEN MATCHED THEN UPDATE SET FileName=@name,MimeType=@mime,ByteLength=@length,Sha256=@sha,SourceUrl=@url,Version=t.Version+1 WHEN NOT MATCHED THEN INSERT(FileId,PublicId,StorageKey,FileName,MimeType,ByteLength,Sha256,SourceUrl,Version) VALUES(NEWID(),@id,@key,@name,@mime,@length,@sha,@url,0);",c);q.Parameters.AddWithValue("@id",SqlPublicIds.New());q.Parameters.AddWithValue("@key",storageKey);q.Parameters.AddWithValue("@name",fileName);q.Parameters.AddWithValue("@mime",(object?)mimeType??DBNull.Value);q.Parameters.AddWithValue("@length",length);q.Parameters.AddWithValue("@sha",sha256);q.Parameters.AddWithValue("@url",sourceUrl);await q.ExecuteNonQueryAsync(ct);
    }
    public async Task MarkDeletedAsync(string storageKey,CancellationToken ct){await using SqlConnection c=factory.Create();await c.OpenAsync(ct);await using SqlCommand q=new("DELETE FROM dbo.Files WHERE StorageKey=@key;",c);q.Parameters.AddWithValue("@key",storageKey);await q.ExecuteNonQueryAsync(ct);}
}
