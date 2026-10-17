using System.Reflection;

namespace Kardia.UnitTest.Sdk;

/// <summary>
/// Kardia.Sdk keeps its own copies of the WebApi's DTOs (the Website never references the WebApi). These tests make sure
/// every copy has the same properties, with the same types, as the class the WebApi sends.
/// </summary>
public sealed class SdkContractTests
{
    private static readonly Assembly WebApi = typeof(Kardia.WebApi.Features.KardiaOptions).Assembly;
    private static readonly Assembly Sdk = typeof(Kardia.Sdk.KardiaClient).Assembly;

    public static TheoryData<string> Contracts()
    {
        var data = new TheoryData<string>();
        foreach (var type in Sdk.GetTypes().Where(static t => t.IsPublic && t.Namespace == "Kardia.Sdk.Models").OrderBy(static t => t.Name))
        {
            data.Add(type.Name);
        }

        return data;
    }

    [Fact]
    public void EveryDtoOfTheWebApiHasASdkCopy()
    {
        var sdkNames = Sdk.GetTypes().Where(static t => t.Namespace == "Kardia.Sdk.Models").Select(static t => t.Name).ToHashSet();
        var missing = WebApi.GetTypes()
            .Where(static t => t.IsPublic && t.Namespace?.EndsWith(".Models.DTOs", StringComparison.Ordinal) == true)
            .Select(static t => t.Name)
            .Where(name => !sdkNames.Contains(name))
            .ToList();

        Assert.Empty(missing);
    }

    [Theory]
    [MemberData(nameof(Contracts))]
    public void TheSdkCopyMatchesTheWebApi(string name)
    {
        var sdk = Sdk.GetTypes().Single(t => t.Namespace == "Kardia.Sdk.Models" && t.Name == name);
        var api = WebApi.GetTypes().Single(t => t.IsPublic && t.Name == name && t.Namespace?.EndsWith(".Models.DTOs", StringComparison.Ordinal) == true);

        var expected = api.GetProperties().Select(Describe).OrderBy(static p => p).ToList();
        var actual = sdk.GetProperties().Select(Describe).OrderBy(static p => p).ToList();

        Assert.Equal(string.Join(", ", expected), string.Join(", ", actual));
    }

    private static string Describe(PropertyInfo property)
    {
        var type = property.PropertyType;
        var typeName = type.IsGenericType
            ? $"{type.Name}<{string.Join(",", type.GetGenericArguments().Select(static a => a.Name))}>"
            : type.Name;
        return $"{property.Name}:{typeName}";
    }
}
