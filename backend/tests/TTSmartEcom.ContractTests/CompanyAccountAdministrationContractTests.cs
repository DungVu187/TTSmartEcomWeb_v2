using System.Reflection;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using TTSmartEcom.Api.Contracts.Users;
using TTSmartEcom.Api.Controllers.Users;
using TTSmartEcom.Domain.Security;

namespace TTSmartEcom.ContractTests;

public sealed class CompanyAccountAdministrationContractTests
{
    [Fact]
    public void Controller_UsesScopedControlPlaneRouteAndAuthentication()
    {
        RouteAttribute route = Assert.Single(typeof(CompanyAccountAdministrationController)
            .GetCustomAttributes<RouteAttribute>());

        Assert.Equal("control-plane/companies/{companyId:guid}/accounts", route.Template);
        Assert.Single(typeof(CompanyAccountAdministrationController).GetCustomAttributes<AuthorizeAttribute>());
    }

    [Theory]
    [InlineData(nameof(CompanyAccountAdministrationController.List), "GET", null)]
    [InlineData(nameof(CompanyAccountAdministrationController.ListRoles), "GET", "roles")]
    [InlineData(nameof(CompanyAccountAdministrationController.ListPermissions), "GET", "permissions")]
    [InlineData(nameof(CompanyAccountAdministrationController.CreateRole), "POST", "roles")]
    [InlineData(nameof(CompanyAccountAdministrationController.UpdateRole), "PUT", "roles/{roleId:guid}")]
    [InlineData(nameof(CompanyAccountAdministrationController.ListBranches), "GET", "{userId}/branches")]
    [InlineData(nameof(CompanyAccountAdministrationController.SaveBranch), "PUT", "{userId}/branches/{branchId:guid}")]
    [InlineData(nameof(CompanyAccountAdministrationController.RevokeBranch), "DELETE", "{userId}/branches/{branchId:guid}")]
    [InlineData(nameof(CompanyAccountAdministrationController.Upsert), "PUT", "{userId}/membership")]
    [InlineData(nameof(CompanyAccountAdministrationController.Revoke), "DELETE", "{userId}/membership")]
    [InlineData(nameof(CompanyAccountAdministrationController.SetStatus), "PUT", "{userId}/status")]
    public void Actions_UseExpectedMethodAndRoute(string methodName, string httpMethod, string? template)
    {
        MethodInfo method = typeof(CompanyAccountAdministrationController).GetMethod(methodName)!;
        HttpMethodAttribute route = Assert.Single(method.GetCustomAttributes<HttpMethodAttribute>());

        Assert.Equal(httpMethod, Assert.Single(route.HttpMethods));
        Assert.Equal(template, route.Template);
    }

    [Fact]
    public void UpsertRequest_PreservesUserTypeAndRoleIdPropertyNames()
    {
        using JsonDocument json = JsonDocument.Parse(JsonSerializer.Serialize(
            new CompanyMembershipUpsertRequest(2, Guid.NewGuid())));

        Assert.True(json.RootElement.TryGetProperty("userType", out _));
        Assert.True(json.RootElement.TryGetProperty("roleId", out _));
    }

    [Fact]
    public void AccountResponse_ExposesCurrentCompanyMembershipAndCompanyRoles()
    {
        Guid companyId = Guid.NewGuid();
        Guid roleId = Guid.NewGuid();
        CompanyAccountResponse response = CompanyAccountResponse.From(new CompanyAccountMembership(
            Guid.NewGuid(),
            companyId,
            Guid.NewGuid(),
            "Nhân viên",
            "employee@example.test",
            "0900000000",
            ControlPlaneAccountType.Company,
            ControlPlaneUserType.Member,
            1,
            [new CompanyRoleDefinition(
                roleId,
                companyId,
                "MEMBER",
                "Thành viên",
                ControlPlaneScopeType.Company,
                false,
                new HashSet<string>(["product.view"], StringComparer.Ordinal))]));

        Assert.Equal(companyId, response.CompanyId);
        Assert.Equal((byte)ControlPlaneUserType.Member, response.UserType);
        Assert.Equal(roleId, Assert.Single(response.Roles).RoleId);
        Assert.Equal((byte)ControlPlaneScopeType.Company, Assert.Single(response.Roles).ScopeType);
    }

    [Fact]
    public void PlatformSearchAndCompanyList_AreSeparateAuthenticatedRoutes()
    {
        RouteAttribute route = Assert.Single(typeof(PlatformAccessAdministrationController).GetCustomAttributes<RouteAttribute>());
        Assert.Equal("control-plane", route.Template);
        Assert.Single(typeof(PlatformAccessAdministrationController).GetCustomAttributes<AuthorizeAttribute>());
        Assert.Equal("companies", Assert.Single(typeof(PlatformAccessAdministrationController)
            .GetMethod(nameof(PlatformAccessAdministrationController.Companies))!.GetCustomAttributes<HttpGetAttribute>()).Template);
        Assert.Equal("users/search", Assert.Single(typeof(PlatformAccessAdministrationController)
            .GetMethod(nameof(PlatformAccessAdministrationController.SearchUsers))!.GetCustomAttributes<HttpGetAttribute>()).Template);
    }
}
