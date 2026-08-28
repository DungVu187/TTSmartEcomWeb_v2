using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System.Reflection;
using TTSmartEcom.Api.Middleware;
using TTSmartEcom.Application.Abstractions.Security;
using TTSmartEcom.Domain.Security;

namespace TTSmartEcom.Api.Security;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
public sealed class ScopeAuthorizeAttribute : Attribute, IAsyncActionFilter
{
    public string? Permission { get; set; }
    public string? CompanyRouteParam { get; set; }
    public string? BranchRouteParam { get; set; }

    public ScopeAuthorizeAttribute(string? permission = null)
    {
        Permission = permission;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        ICurrentUserContext? userContext = context.HttpContext.Items[CurrentUserContextMiddleware.ContextItemKey] as ICurrentUserContext;
        if (userContext is null || !userContext.IsAuthenticated)
        {
            context.Result = new UnauthorizedObjectResult(new { message = "Access denied, no token provided" });
            return;
        }

        if (userContext.IsPlatformSuperAdmin)
        {
            return;
        }

        IAccessScopeService scopeService = context.HttpContext.RequestServices.GetRequiredService<IAccessScopeService>();

        Guid? companyId = null;
        Guid? branchId = null;

        // Resolve company scope from route, query, action argument or DTO property.
        if (!string.IsNullOrWhiteSpace(CompanyRouteParam))
        {
            if (!TryResolveGuid(context, CompanyRouteParam, out companyId))
            {
                context.Result = BadScope("CompanyId không hợp lệ");
                return;
            }
        }

        // Resolve branch scope from route, query, action argument or DTO property.
        if (!string.IsNullOrWhiteSpace(BranchRouteParam))
        {
            if (!TryResolveGuid(context, BranchRouteParam, out branchId))
            {
                context.Result = BadScope("BranchId không hợp lệ");
                return;
            }
        }

        if (!scopeService.IsInScope(userContext, companyId, branchId))
        {
            context.Result = BadScope("không có quyền truy cập phạm vi được yêu cầu");
            return;
        }

        if (!string.IsNullOrWhiteSpace(Permission))
        {
            bool allowed = branchId.HasValue
                ? scopeService.HasBranchPermission(userContext, branchId.Value, Permission)
                : companyId.HasValue
                    ? scopeService.HasCompanyPermission(userContext, companyId.Value, Permission)
                    : scopeService.HasActiveScopePermission(userContext, Permission);
            if (!allowed)
            {
                context.Result = BadScope($"thiếu permission: {Permission}");
                return;
            }
        }

        await next();
    }

    private static ObjectResult BadScope(string message) => new(new { message = $"Access denied: {message}" })
    {
        StatusCode = StatusCodes.Status403Forbidden,
    };

    private static bool TryResolveGuid(ActionExecutingContext context, string name, out Guid? result)
    {
        result = null;
        context.ActionArguments.TryGetValue(name, out object? actionArgument);
        object? value = context.RouteData.Values[name]
            ?? context.HttpContext.Request.Query[name].FirstOrDefault()
            ?? actionArgument;

        if (value is null)
        {
            value = context.ActionArguments.Values
                .Select(argument => argument?.GetType().GetProperty(name,
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase)?.GetValue(argument))
                .FirstOrDefault(candidate => candidate is not null);
        }

        if (value is Guid guid && guid != Guid.Empty)
        {
            result = guid;
            return true;
        }

        if (Guid.TryParse(value?.ToString(), out guid) && guid != Guid.Empty)
        {
            result = guid;
            return true;
        }

        return false;
    }
}
