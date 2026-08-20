using System.Text;
using Microsoft.AspNetCore.DataProtection;

namespace TTSmartEcom.Infrastructure.SqlServer.Integrations;

#pragma warning disable CA1725

public interface ISqlLocalSecretStore
{
    Task<string> PutAsync(string value, CancellationToken cancellationToken);
    Task<string?> GetAsync(string? reference, CancellationToken cancellationToken);
}

public sealed class SqlLocalSecretStore(IDataProtectionProvider provider) : ISqlLocalSecretStore
{
    private readonly IDataProtector protector=provider.CreateProtector("TTSmartEcom.SqlServer.LocalSecrets.v1");
    private static readonly string Root=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"TTSmartEcom","secrets");
    public async Task<string> PutAsync(string value,CancellationToken ct){Directory.CreateDirectory(Root);string key="sql-"+Guid.NewGuid().ToString("N");string path=Path.Combine(Root,key+".secret");await File.WriteAllTextAsync(path,protector.Protect(value),Encoding.UTF8,ct);return key;}
    public async Task<string?> GetAsync(string? reference,CancellationToken ct){if(string.IsNullOrWhiteSpace(reference)||reference.Any(x=>!(char.IsLetterOrDigit(x)||x=='-')))return null;string path=Path.Combine(Root,reference+".secret");if(!File.Exists(path))return null;try{return protector.Unprotect(await File.ReadAllTextAsync(path,ct));}catch(Exception){return null;}}
}
