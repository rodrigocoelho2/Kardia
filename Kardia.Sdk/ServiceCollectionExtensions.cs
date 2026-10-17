using Kardia.Library.Constants;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Kardia.Sdk;

/// <summary>
/// Where the WebApi is and the key to call it, bound from the "KardiaApi" section.
/// </summary>
public sealed class KardiaClientOptions
{
    /// <summary>The configuration section name.</summary>
    public const string SectionName = "KardiaApi";

    /// <summary>Gets or sets the WebApi base URL, e.g. http://webapi:8080/.</summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>Gets or sets the key sent in x-api-key.</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>Gets or sets the request timeout (a big Apple Health import can take a while).</summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(100);
}

/// <summary>
/// Registers <see cref="KardiaClient"/>.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the typed client with its base address and x-api-key header.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration holding the "KardiaApi" section.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddKardiaClient(this IServiceCollection services, IConfiguration configuration)
    {
        var options = configuration.GetSection(KardiaClientOptions.SectionName).Get<KardiaClientOptions>() ?? new KardiaClientOptions();
        if (!Uri.TryCreate(options.Url, UriKind.Absolute, out var baseAddress))
        {
            throw new InvalidOperationException("KardiaApi:Url must be an absolute URL (env KardiaApi__Url).");
        }

        if (string.IsNullOrWhiteSpace(options.ApiKey))
        {
            throw new InvalidOperationException("KardiaApi:ApiKey is required (env KardiaApi__ApiKey).");
        }

        // A trailing slash makes relative paths ("day/range") append to the base path instead of replacing it.
        var normalizedBase = baseAddress.AbsoluteUri.EndsWith('/') ? baseAddress : new Uri(baseAddress.AbsoluteUri + "/");

        services.AddHttpClient<KardiaClient>(client =>
        {
            client.BaseAddress = normalizedBase;
            client.Timeout = options.Timeout;
            client.DefaultRequestHeaders.Add(ApiHeaders.ApiKey, options.ApiKey);
        });

        return services;
    }
}
