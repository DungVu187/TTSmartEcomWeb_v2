using System.Security.Claims;
using TTSmartEcom.Application.Abstractions.Authentication;
using TTSmartEcom.Application.Abstractions.Security;
using TTSmartEcom.Domain.Security;

namespace TTSmartEcom.Api.Middleware;

public sealed partial class CurrentUserContextMiddleware(
    RequestDelegate next,
    IControlPlaneIdentityReader controlPlaneReader,
    IUserIdentityReader legacyIdentityReader,
    ILogger<CurrentUserContextMiddleware> logger)
{
    public const string ContextItemKey = "CurrentUserContext";

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            string? userIdStr = context.User.FindFirstValue("userId")
                ?? context.User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (string.IsNullOrWhiteSpace(userIdStr))
            {
                context.User = new ClaimsPrincipal(new ClaimsIdentity());
            }
            else
            {
                ICurrentUserContext? resolvedContext = null;

                // 1. Try Control Plane if userId is a valid GUID
                if (Guid.TryParse(userIdStr, out Guid userIdGuid))
                {
                    resolvedContext = await controlPlaneReader.FindContextByIdAsync(userIdGuid, context.RequestAborted);
                }

                // 2. Fallback to legacy operational reader for customer accounts
                if (resolvedContext is null)
                {
                    UserIdentitySnapshot? legacySnapshot = await legacyIdentityReader.FindByIdAsync(userIdStr, context.RequestAborted);
                    if (legacySnapshot is not null && !IsIssuedBeforePasswordChange(context.User, legacySnapshot))
                    {
                        resolvedContext = CreateContextFromLegacySnapshot(legacySnapshot);
                        context.Items[LegacyPrincipalMiddleware.IdentityItemKey] = legacySnapshot;
                    }
                }
                else
                {
                    // Map Control Plane context to legacy UserIdentitySnapshot for backward compatibility
                    context.Items[LegacyPrincipalMiddleware.IdentityItemKey] = new UserIdentitySnapshot(
                        Id: resolvedContext.UserId.ToString()!,
                        Email: resolvedContext.Email,
                        Phone: resolvedContext.Phone ?? string.Empty,
                        Name: resolvedContext.DisplayName,
                        Role: ToLegacyCompatibilityRole(resolvedContext),
                        Functions: [],
                        Permissions: PermissionsForActiveScope(resolvedContext),
                        PasswordChangedAt: null,
                        StationIds: []);
                }

                if (resolvedContext is null)
                {
                    LogStaleIdentity(logger);
                    context.User = new ClaimsPrincipal(new ClaimsIdentity());
                }
                else
                {
                    // 3. Resolve active Company/Branch from headers if provided
                    Guid? activeCompanyId = resolvedContext.ActiveCompanyId;
                    Guid? activeBranchId = resolvedContext.ActiveBranchId;

                    if (context.Request.Headers.TryGetValue("X-Company-Id", out var companyHeaderVal))
                    {
                        if (!Guid.TryParse(companyHeaderVal.ToString(), out Guid reqCompanyId))
                        {
                            await WriteInvalidScopeResponseAsync(context, "CompanyId không hợp lệ");
                            return;
                        }

                        if (!resolvedContext.CanAccessCompany(reqCompanyId))
                        {
                            context.Response.StatusCode = StatusCodes.Status403Forbidden;
                            context.Response.ContentType = "application/json";
                            await context.Response.WriteAsync("{\"message\":\"Truy cập bị từ chối: không thuộc phạm vi công ty được yêu cầu\"}", context.RequestAborted);
                            return;
                        }

                        activeCompanyId = reqCompanyId;
                        if (!context.Request.Headers.ContainsKey("X-Branch-Id"))
                        {
                            activeBranchId = null;
                        }
                        else if (activeBranchId.HasValue && !resolvedContext.BranchMemberships.Any(branch =>
                                branch.BranchId == activeBranchId.Value && branch.CompanyId == reqCompanyId))
                        {
                            // A default primary branch can belong to another company. A
                            // company-only selection must never carry that branch into
                            // the new request scope.
                            activeBranchId = null;
                        }
                    }

                    if (context.Request.Headers.TryGetValue("X-Branch-Id", out var branchHeaderVal))
                    {
                        if (!Guid.TryParse(branchHeaderVal.ToString(), out Guid reqBranchId))
                        {
                            await WriteInvalidScopeResponseAsync(context, "BranchId không hợp lệ");
                            return;
                        }

                        if (!resolvedContext.CanAccessBranch(reqBranchId))
                        {
                            context.Response.StatusCode = StatusCodes.Status403Forbidden;
                            context.Response.ContentType = "application/json";
                            await context.Response.WriteAsync("{\"message\":\"Truy cập bị từ chối: không thuộc phạm vi chi nhánh được yêu cầu\"}", context.RequestAborted);
                            return;
                        }

                        BranchMembershipContext? branch = resolvedContext.BranchMemberships
                            .FirstOrDefault(item => item.BranchId == reqBranchId);
                        if (branch is not null)
                        {
                            if (activeCompanyId.HasValue && activeCompanyId.Value != branch.CompanyId)
                            {
                                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                                context.Response.ContentType = "application/json";
                                await context.Response.WriteAsync("{\"message\":\"Truy cập bị từ chối: chi nhánh không thuộc công ty được yêu cầu\"}", context.RequestAborted);
                                return;
                            }

                            activeCompanyId = branch.CompanyId;
                        }

                        activeBranchId = reqBranchId;
                    }

                    if (activeCompanyId.HasValue && activeBranchId.HasValue)
                    {
                        BranchMembershipContext? activeBranch = resolvedContext.BranchMemberships
                            .FirstOrDefault(item => item.BranchId == activeBranchId.Value);
                        if (activeBranch is not null && activeBranch.CompanyId != activeCompanyId.Value)
                        {
                            context.Response.StatusCode = StatusCodes.Status403Forbidden;
                            context.Response.ContentType = "application/json";
                            await context.Response.WriteAsync("{\"message\":\"Truy cập bị từ chối: company và branch không cùng phạm vi\"}", context.RequestAborted);
                            return;
                        }
                    }

                    if (activeCompanyId != resolvedContext.ActiveCompanyId || activeBranchId != resolvedContext.ActiveBranchId)
                    {
                        resolvedContext = new CurrentUserContext(
                            resolvedContext.UserId,
                            resolvedContext.IsAuthenticated,
                            resolvedContext.IsPlatformSuperAdmin,
                            resolvedContext.DisplayName,
                            resolvedContext.Email,
                            resolvedContext.Phone,
                            resolvedContext.CompanyMemberships,
                            activeCompanyId,
                            resolvedContext.BranchMemberships,
                            activeBranchId,
                            resolvedContext.Roles,
                            resolvedContext.Permissions,
                            resolvedContext.IsControlPlaneIdentity,
                            inferActiveBranch: !context.Request.Headers.ContainsKey("X-Company-Id")
                                || context.Request.Headers.ContainsKey("X-Branch-Id"));
                    }

                    if (resolvedContext.IsControlPlaneIdentity)
                    {
                        context.Items[LegacyPrincipalMiddleware.IdentityItemKey] = new UserIdentitySnapshot(
                            Id: resolvedContext.UserId.ToString()!,
                            Email: resolvedContext.Email,
                            Phone: resolvedContext.Phone ?? string.Empty,
                            Name: resolvedContext.DisplayName,
                            Role: ToLegacyCompatibilityRole(resolvedContext),
                            Functions: [],
                            Permissions: PermissionsForActiveScope(resolvedContext),
                            PasswordChangedAt: null,
                            StationIds: []);
                    }

                    context.Items[ContextItemKey] = resolvedContext;
                }
            }
        }

        await next(context);
    }

    private static CurrentUserContext CreateContextFromLegacySnapshot(UserIdentitySnapshot legacy)
    {
        bool isSuperAdmin = legacy.Role == SystemRoles.SuperAdmin;
        return new CurrentUserContext(
            userId: Guid.TryParse(legacy.Id, out Guid parsedId) ? parsedId : null,
            isAuthenticated: true,
            isPlatformSuperAdmin: isSuperAdmin,
            displayName: legacy.Name,
            email: legacy.Email,
            phone: legacy.Phone,
            companyMemberships: [],
            activeCompanyId: null,
            branchMemberships: [],
            activeBranchId: null,
            roles: [legacy.Role],
            permissions: new HashSet<string>(legacy.Permissions, StringComparer.Ordinal));
    }

    private static string ToLegacyCompatibilityRole(ICurrentUserContext context) =>
        context.IsPlatformSuperAdmin ? SystemRoles.SuperAdmin : SystemRoles.Staff;

    private static IReadOnlyList<string> PermissionsForActiveScope(ICurrentUserContext context)
    {
        if (context.IsPlatformSuperAdmin)
        {
            return context.Permissions.ToList();
        }

        if (context.ActiveBranchId.HasValue)
        {
            BranchMembershipContext? branch = context.BranchMemberships
                .FirstOrDefault(item => item.BranchId == context.ActiveBranchId.Value);
            if (branch is null)
            {
                return [];
            }

            IEnumerable<string> companyPermissions = context.CompanyMemberships
                .Where(item => item.CompanyId == branch.CompanyId)
                .SelectMany(item => item.Permissions);
            return branch.Permissions.Concat(companyPermissions).Distinct(StringComparer.Ordinal).ToArray();
        }

        if (context.ActiveCompanyId.HasValue)
        {
            return context.CompanyMemberships
                .Where(item => item.CompanyId == context.ActiveCompanyId.Value)
                .SelectMany(item => item.Permissions)
                .Distinct(StringComparer.Ordinal)
                .ToArray();
        }

        return [];
    }

    private static async Task WriteInvalidScopeResponseAsync(HttpContext context, string message)
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsync($"{{\"message\":\"{message}\"}}", context.RequestAborted);
    }

    private static bool IsIssuedBeforePasswordChange(ClaimsPrincipal principal, UserIdentitySnapshot identity)
    {
        Claim? issuedAt = principal.FindFirst("iat");
        return identity.PasswordChangedAt.HasValue
            && issuedAt is not null
            && long.TryParse(issuedAt.Value, out long seconds)
            && DateTimeOffset.FromUnixTimeSeconds(seconds) < identity.PasswordChangedAt.Value;
    }

    [LoggerMessage(EventId = 1004, Level = LogLevel.Information, Message = "Rejected stale or missing control-plane identity")]
    private static partial void LogStaleIdentity(ILogger logger);
}
