using System.Reflection;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using TTSmartEcom.Api.Contracts.Products;
using TTSmartEcom.Api.Controllers.Products;
using TTSmartEcom.Api.Security;

namespace TTSmartEcom.ContractTests;

public sealed class ProductBranchDistributionContractTests
{
    [Theory]
    [InlineData(nameof(ProductBranchDistributionController.ListActiveBranches), "GET", "branches")]
    [InlineData(nameof(ProductBranchDistributionController.Status), "POST", "status")]
    [InlineData(nameof(ProductBranchDistributionController.List), "GET", "{productId}/branches")]
    [InlineData(nameof(ProductBranchDistributionController.IsActive), "GET", "{productId}/branches/{branchId:guid}")]
    [InlineData(nameof(ProductBranchDistributionController.Assign), "POST", "assign")]
    [InlineData(nameof(ProductBranchDistributionController.Revoke), "POST", "revoke")]
    public void DistributionRoutes_UseExpectedContract(string methodName, string httpMethod, string template)
    {
        MethodInfo method = typeof(ProductBranchDistributionController).GetMethod(methodName)!;
        HttpMethodAttribute route = Assert.Single(method.GetCustomAttributes<HttpMethodAttribute>());
        PermissionAuthorizeAttribute permission = Assert.Single(method.GetCustomAttributes<PermissionAuthorizeAttribute>());

        Assert.Equal(httpMethod, Assert.Single(route.HttpMethods));
        Assert.Equal(template, route.Template);
        Assert.Equal("product.edit", permission.Permission);
    }

    [Fact]
    public void DistributionRequest_PreservesJsonPropertyNames()
    {
        Guid branchId = Guid.NewGuid();
        string json = JsonSerializer.Serialize(new ProductBranchDistributionRequest(
            ["507f191e810c19729de860ea"], [branchId]));
        using JsonDocument document = JsonDocument.Parse(json);

        Assert.True(document.RootElement.TryGetProperty("productIds", out _));
        Assert.True(document.RootElement.TryGetProperty("branchIds", out _));
    }
}
