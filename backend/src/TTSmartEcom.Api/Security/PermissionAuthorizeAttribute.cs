using Microsoft.AspNetCore.Authorization;

namespace TTSmartEcom.Api.Security;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class PermissionAuthorizeAttribute : AuthorizeAttribute
{
    public PermissionAuthorizeAttribute(string permission)
    {
        Permission = permission;
        Policy = $"permission:{permission}";
    }

    public string Permission { get; }
}
