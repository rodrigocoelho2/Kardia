using Kardia.Library.Services;
using Kardia.WebApi.Extensions.Time;
using Kardia.WebApi.Features.Days.Services;
using Kardia.WebApi.Features.Push.Models.Entities;
using Kardia.WebApi.Features.Push.Repositories;
using Kardia.WebApi.Features.Reports.Services;
using Kardia.WebApi.Features.Settings.Models.Entities;
using Kardia.WebApi.Features.Settings.Repositories;
using Microsoft.Extensions.Options;

namespace Kardia.WebApi.Features.Push.Services;

/// <summary>
/// Every five minutes: for every account with a device that turned notifications on, works out what's due
/// (<see cref="NotificationPlanner"/>), claims each so it goes out once (even across restarts), and sends it.
/// </summary>
/// <param name="pushRepository">Devices and the notifications already sent.</param>
/// <param name="pushSender">Sends notifications.</param>
/// <param name="settingRepository">What each account wants.</param>
/// <param name="dayService">Today.</param>
/// <param name="reportService">The weekly and monthly assessments.</param>
/// <param name="clock">The app's clock.</param>
/// <param name="options">The options (push on or off).</param>
/// <param name="logger">The logger.</param>
internal sealed class NotificationWorker(
    IPushRepository pushRepository,
    IPushSender pushSender,
    ISettingRepository settingRepository,
    IDayService dayService,
    IReportService reportService,
    IAppClock clock,
    IOptions<KardiaOptions> options,
    ILogger<NotificationWorker> logger) : BackgroundService
{
    /// <summary>How often the worker looks.</summary>
    public static readonly TimeSpan Interval = TimeSpan.FromMinutes(5);

    /// <summary>Sends what's due now to every account with notifications on.</summary>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>How many notifications went out.</returns>
    public async Task<int> RunOnceAsync(CancellationToken cancellationToken = default)
    {
        var sent = 0;
        foreach (var account in (await pushRepository.ListAsync(null)).GroupBy(static device => device.UserUniqueID))
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                sent += await NotifyAsync(account.Key, [.. account], cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogWarning(exception, "Notifications for an account failed");
            }
        }

        return sent;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.PushEnabled)
        {
            return;
        }

        using var timer = new PeriodicTimer(Interval);
        do
        {
            try
            {
                await RunOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "The notification worker failed; it tries again in five minutes");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false));
    }

    private async Task<int> NotifyAsync(Guid userId, List<PushSubscriptionRow> devices, CancellationToken cancellationToken)
    {
        var now = clock.Now;
        var setting = await settingRepository.GetAsync(userId) ?? UserSetting.Defaults;
        var account = userId.ToString("N");
        var due = new List<Notification>();

        if (setting.NotifyMorning || setting.NotifyBedtime || setting.NotifyHealth || setting.NotifyStrain)
        {
            if (Value(await dayService.GetAsync(account, null)) is { } day)
            {
                due.AddRange(NotificationPlanner.Daily(now, day, setting));
            }
        }

        var sent = 0;

        // Assessments: claimed first, so the report is only worked out once.
        foreach (var (period, date, key) in NotificationPlanner.ReportsDue(now, setting))
        {
            if (await pushRepository.ClaimAsync(userId, key)
                && Value(await reportService.GetAsync(account, period, date)) is { } report
                && NotificationPlanner.Report(report) is { } message)
            {
                await pushSender.SendAsync(devices, message, cancellationToken);
                sent++;
            }
        }

        foreach (var notification in due)
        {
            if (!await pushRepository.ClaimAsync(userId, notification.Key))
            {
                continue;
            }

            await pushSender.SendAsync(devices, notification.Message, cancellationToken);
            sent++;
        }

        if (sent > 0)
        {
            logger.LogInformation("Sent {Count} notification(s) to an account with {Devices} device(s)", sent, devices.Count);
        }

        return sent;
    }

    private static T? Value<T>(ServiceApiResponse<T> response)
        where T : class => response.IsSuccess ? response.Result : null;
}
