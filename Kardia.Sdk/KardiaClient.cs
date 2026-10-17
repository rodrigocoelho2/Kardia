using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Kardia.Library.Constants;
using Kardia.Library.Enums;
using Kardia.Library.Validation;
using Kardia.Sdk.Models;

namespace Kardia.Sdk;

/// <summary>
/// Typed client of Kardia.WebApi. Method names follow the API operation ids ({Feature}{Operation}Async).
/// Register with <see cref="ServiceCollectionExtensions.AddKardiaClient"/>, which sets the base address and the x-api-key header.
/// </summary>
/// <param name="httpClient">The configured HttpClient.</param>
public sealed class KardiaClient(HttpClient httpClient)
{
    private static readonly JsonSerializerOptions SerializerOptions = CreateSerializerOptions();

    // ----- User -----

    /// <summary>How many accounts exist, and the demo account in demo mode.</summary>
    /// <returns>The status.</returns>
    public Task<UserStatusDTO> UserStatusAsync() => GetAsync<UserStatusDTO>("user/status");

    /// <summary>Lists every account.</summary>
    /// <returns>The accounts.</returns>
    public Task<List<UserDTO>> UserListAsync() => GetAsync<List<UserDTO>>("user/list");

    /// <summary>Gets one account.</summary>
    /// <param name="identifier">The account id.</param>
    /// <returns>The account.</returns>
    public Task<UserDTO> UserGetAsync(string identifier) => GetAsync<UserDTO>($"user?identifier={Uri.EscapeDataString(identifier)}");

    /// <summary>Creates an account (the first one is an admin).</summary>
    /// <param name="item">User name, password, date of birth and sex.</param>
    /// <returns>The account.</returns>
    public Task<UserDTO> UserCreateAsync(UserCreateRequest item) => PostAsync<UserDTO>("user", item);

    /// <summary>Checks a user name and password.</summary>
    /// <param name="item">User name and password.</param>
    /// <returns>The account.</returns>
    public Task<UserDTO> UserSignInAsync(UserSignInRequest item) => PostAsync<UserDTO>("user/signin", item);

    /// <summary>Changes a password.</summary>
    /// <param name="item">The account, current and new password.</param>
    /// <returns>The account.</returns>
    public Task<UserDTO> UserPasswordAsync(UserPasswordRequest item) => PostAsync<UserDTO>("user/password", item);

    /// <summary>Deletes an account and its data.</summary>
    /// <param name="identifier">The account id.</param>
    /// <returns>A task that completes on success.</returns>
    public Task UserDeleteAsync(string identifier) => DeleteAsync($"user?identifier={Uri.EscapeDataString(identifier)}");

    // ----- Profile -----

    /// <summary>Gets the profile.</summary>
    /// <param name="userId">The account.</param>
    /// <returns>The profile.</returns>
    public Task<ProfileDTO> ProfileGetAsync(string userId) => GetAsync<ProfileDTO>($"profile?userId={Uri.EscapeDataString(userId)}");

    /// <summary>Saves the profile.</summary>
    /// <param name="item">The profile.</param>
    /// <returns>The saved profile.</returns>
    public Task<ProfileDTO> ProfileSaveAsync(ProfileSaveRequest item) => PostAsync<ProfileDTO>("profile", item);

    // ----- Day -----

    /// <summary>Gets one day: recovery, sleep, strain, heart rate, the last two weeks and the coach.</summary>
    /// <param name="userId">The account.</param>
    /// <param name="date">The day (today when null).</param>
    /// <returns>The day.</returns>
    public Task<DayDTO> DayGetAsync(string userId, DateOnly? date = null) =>
        GetAsync<DayDTO>($"day?userId={Uri.EscapeDataString(userId)}{(date is { } day ? "&date=" + Iso(day) : string.Empty)}");

    /// <summary>Gets a range of days in short.</summary>
    /// <param name="userId">The account.</param>
    /// <param name="from">First day.</param>
    /// <param name="to">Last day.</param>
    /// <returns>One summary per day.</returns>
    public Task<List<DaySummaryDTO>> DayRangeAsync(string userId, DateOnly from, DateOnly to) =>
        GetAsync<List<DaySummaryDTO>>($"day/range?userId={Uri.EscapeDataString(userId)}&from={Iso(from)}&to={Iso(to)}");

    /// <summary>Gets the Health Monitor over the last 30 or 180 days.</summary>
    /// <param name="userId">The account.</param>
    /// <param name="days">30 or 180.</param>
    /// <returns>One series per sign.</returns>
    public Task<HealthReportDTO> DayHealthReportAsync(string userId, int days) =>
        GetAsync<HealthReportDTO>($"day/health?userId={Uri.EscapeDataString(userId)}&days={days}");

    // ----- Setting -----

    /// <summary>Gets the goals and notification choices.</summary>
    /// <param name="userId">The account.</param>
    /// <returns>The settings.</returns>
    public Task<SettingDTO> SettingGetAsync(string userId) => GetAsync<SettingDTO>($"setting?userId={Uri.EscapeDataString(userId)}");

    /// <summary>Saves the goals and notification choices.</summary>
    /// <param name="item">The settings.</param>
    /// <returns>The saved settings.</returns>
    public Task<SettingDTO> SettingSaveAsync(SettingDTO item) => PostAsync<SettingDTO>("setting", item);

    // ----- Plan -----

    /// <summary>Gets this week's plan.</summary>
    /// <param name="userId">The account.</param>
    /// <returns>The plan.</returns>
    public Task<PlanDTO> PlanGetAsync(string userId) => GetAsync<PlanDTO>($"plan?userId={Uri.EscapeDataString(userId)}");

    // ----- Report -----

    /// <summary>Gets the weekly or monthly assessment around a day.</summary>
    /// <param name="userId">The account.</param>
    /// <param name="period">Week or month.</param>
    /// <param name="date">A day inside it (today when null).</param>
    /// <returns>The report.</returns>
    public Task<ReportDTO> ReportGetAsync(string userId, ReportPeriod period, DateOnly? date = null) =>
        GetAsync<ReportDTO>($"report?userId={Uri.EscapeDataString(userId)}&period={period}{(date is { } day ? "&date=" + Iso(day) : string.Empty)}");

    // ----- AI coach -----

    /// <summary>Gets whether the AI coach is set up, whether it's working, and the last report of a topic and day.</summary>
    /// <param name="userId">The account.</param>
    /// <param name="topic">What the report looks at.</param>
    /// <param name="date">The day (today when null).</param>
    /// <returns>The state.</returns>
    public Task<AiCoachDTO> AiCoachGetAsync(string userId, AiCoachTopic topic = AiCoachTopic.Overview, DateOnly? date = null) =>
        GetAsync<AiCoachDTO>($"aicoach?userId={Uri.EscapeDataString(userId)}&topic={topic}{(date is { } day ? "&date=" + Iso(day) : string.Empty)}");

    /// <summary>Starts an analysis.</summary>
    /// <param name="userId">The account.</param>
    /// <param name="topic">What the report looks at.</param>
    /// <param name="date">The day (today when null).</param>
    /// <returns>The state.</returns>
    public Task<AiCoachDTO> AiCoachAnalyseAsync(string userId, AiCoachTopic topic = AiCoachTopic.Overview, DateOnly? date = null) =>
        PostAsync<AiCoachDTO>($"aicoach/analyse?userId={Uri.EscapeDataString(userId)}&topic={topic}{(date is { } day ? "&date=" + Iso(day) : string.Empty)}", null);

    // ----- Push -----

    /// <summary>Gets whether notifications are on, the key to subscribe with and the devices.</summary>
    /// <param name="userId">The account.</param>
    /// <returns>The state.</returns>
    public Task<PushDTO> PushGetAsync(string userId) => GetAsync<PushDTO>($"push?userId={Uri.EscapeDataString(userId)}");

    /// <summary>Turns notifications on for a browser.</summary>
    /// <param name="item">The subscription.</param>
    /// <returns>The state.</returns>
    public Task<PushDTO> PushSubscribeAsync(PushSubscribeRequest item) => PostAsync<PushDTO>("push/subscribe", item);

    /// <summary>Turns notifications off for a device.</summary>
    /// <param name="item">The device.</param>
    /// <returns>The state.</returns>
    public Task<PushDTO> PushUnsubscribeAsync(PushUnsubscribeRequest item) => PostAsync<PushDTO>("push/unsubscribe", item);

    /// <summary>Sends a test notification.</summary>
    /// <param name="userId">The account.</param>
    /// <returns>How many devices it reached.</returns>
    public Task<PushTestResultDTO> PushTestAsync(string userId) => PostAsync<PushTestResultDTO>($"push/test?userId={Uri.EscapeDataString(userId)}", null);

    // ----- Age -----

    /// <summary>Gets the biological age.</summary>
    /// <param name="userId">The account.</param>
    /// <returns>The age.</returns>
    public Task<AgeDTO> AgeGetAsync(string userId) => GetAsync<AgeDTO>($"age?userId={Uri.EscapeDataString(userId)}");

    // ----- Journal -----

    /// <summary>Gets a day's journal.</summary>
    /// <param name="userId">The account.</param>
    /// <param name="date">The day (today when null).</param>
    /// <returns>The journal.</returns>
    public Task<JournalDayDTO> JournalGetAsync(string userId, DateOnly? date = null) =>
        GetAsync<JournalDayDTO>($"journal?userId={Uri.EscapeDataString(userId)}{(date is { } day ? "&date=" + Iso(day) : string.Empty)}");

    /// <summary>Marks or unmarks a habit.</summary>
    /// <param name="item">The habit, day and state.</param>
    /// <returns>The day's journal.</returns>
    public Task<JournalDayDTO> JournalSetAsync(JournalSetRequest item) => PostAsync<JournalDayDTO>("journal", item);

    // ----- Sync -----

    /// <summary>Gets the Apple Health connection (hint only).</summary>
    /// <param name="userId">The account.</param>
    /// <returns>The connection.</returns>
    public Task<SyncKeyDTO> SyncKeyGetAsync(string userId) => GetAsync<SyncKeyDTO>($"sync/key?userId={Uri.EscapeDataString(userId)}");

    /// <summary>Makes a new sync key (shown once).</summary>
    /// <param name="userId">The account.</param>
    /// <returns>The key.</returns>
    public Task<SyncKeyCreatedDTO> SyncKeyCreateAsync(string userId) => PostAsync<SyncKeyCreatedDTO>($"sync/key?userId={Uri.EscapeDataString(userId)}", null);

    /// <summary>Revokes the sync key.</summary>
    /// <param name="userId">The account.</param>
    /// <returns>A task that completes on success.</returns>
    public Task SyncKeyRevokeAsync(string userId) => DeleteAsync($"sync/key?userId={Uri.EscapeDataString(userId)}");

    /// <summary>Forwards an Apple Health export as it arrived.</summary>
    /// <param name="key">The personal key that came with it (header or body).</param>
    /// <param name="body">The raw JSON body.</param>
    /// <param name="cancellationToken">Cancels the forward.</param>
    /// <returns>What was imported.</returns>
    public async Task<SyncIngestResultDTO> SyncIngestAsync(string? key, Stream body, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "sync/ingest") { Content = new StreamContent(body) };
        if (body.CanSeek)
        {
            request.Content.Headers.ContentLength = body.Length - body.Position;
        }

        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        if (!string.IsNullOrWhiteSpace(key))
        {
            request.Headers.TryAddWithoutValidation(ApiHeaders.SyncKey, key);
        }

        using var response = await httpClient.SendAsync(request, cancellationToken);
        return await ReadAsync<SyncIngestResultDTO>(response);
    }

    // ----- Health -----

    /// <summary>Checks the WebApi and its database.</summary>
    /// <returns>The health.</returns>
    public Task<HealthDTO> HealthGetAsync() => GetAsync<HealthDTO>("health");

    private static string Iso(DateOnly date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private async Task<T> GetAsync<T>(string path)
    {
        using var response = await httpClient.GetAsync(path);
        return await ReadAsync<T>(response);
    }

    private async Task<T> PostAsync<T>(string path, object? body)
    {
        using var response = await httpClient.PostAsJsonAsync(path, body, SerializerOptions);
        return await ReadAsync<T>(response);
    }

    private async Task DeleteAsync(string path)
    {
        using var response = await httpClient.DeleteAsync(path);
        await EnsureSuccessAsync(response);
    }

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        await EnsureSuccessAsync(response);
        return (await response.Content.ReadFromJsonAsync<T>(SerializerOptions))!;
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        ApiValidationProblemDetails? problems = null;
        try
        {
            problems = await response.Content.ReadFromJsonAsync<ApiValidationProblemDetails>(SerializerOptions);
        }
        catch (Exception exception) when (exception is JsonException or NotSupportedException)
        {
            // Not a problem body (an empty 401, an HTML error page).
        }

        throw new KardiaApiException(response.StatusCode, problems);
    }

    private static JsonSerializerOptions CreateSerializerOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
