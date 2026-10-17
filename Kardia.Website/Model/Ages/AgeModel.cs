using Kardia.Sdk;
using Kardia.Sdk.Models;

namespace Kardia.Website.Model.Ages;

/// <summary>
/// The biological age screen.
/// </summary>
public sealed class AgePageModel : AppPageModel
{
    /// <summary>Gets or sets the biological age.</summary>
    public AgeDTO Age { get; set; } = new();
}

/// <summary>
/// Loads the biological age screen.
/// </summary>
/// <param name="apiClient">The API client.</param>
public sealed class AgeModel(KardiaClient apiClient) : WebsiteBaseModel(apiClient)
{
    /// <summary>
    /// Builds the screen.
    /// </summary>
    /// <param name="page">The page with the shell filled in.</param>
    /// <param name="userId">The account.</param>
    /// <returns>The page.</returns>
    public async Task<AgePageModel> PageAsync(AgePageModel page, string userId)
    {
        var ageCall = CallAsync(() => ApiClient.AgeGetAsync(userId));
        var syncCall = CallAsync(() => ApiClient.SyncKeyGetAsync(userId));
        var age = await ageCall;
        page.LastSyncAt = (await syncCall).Data?.LastUsedAt;
        if (!age.IsValid || age.Data is null)
        {
            page.Unavailable = age.ValidationErrors.FirstOrDefault()?.Message ?? ApiUnavailableMessage;
            return page;
        }

        page.Age = age.Data;
        return page;
    }
}
