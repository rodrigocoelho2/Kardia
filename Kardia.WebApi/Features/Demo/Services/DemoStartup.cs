using Kardia.Library.Enums;
using Kardia.WebApi.Features.Profiles.Models.Entities;
using Kardia.WebApi.Features.Profiles.Repositories;
using Kardia.WebApi.Features.Users.Repositories;
using Kardia.WebApi.Features.Users.Services;
using Microsoft.Extensions.Options;

namespace Kardia.WebApi.Features.Demo.Services;

/// <summary>
/// In demo mode, creates the "demo" account (a 30-year-old, 180 cm) with its sample data when the app starts,
/// then refreshes the last two days every 15 minutes so the demo follows the clock.
/// </summary>
/// <param name="userRepository">Accounts.</param>
/// <param name="profileRepository">Profiles.</param>
/// <param name="demoSeeder">Sample data.</param>
/// <param name="options">Module options (the demo password).</param>
/// <param name="logger">Logger.</param>
internal sealed class DemoStartup(
    IUserRepository userRepository,
    IProfileRepository profileRepository,
    IDemoSeeder demoSeeder,
    IOptions<KardiaOptions> options,
    ILogger<DemoStartup> logger) : BackgroundService
{
    private static readonly TimeSpan RefreshEvery = TimeSpan.FromMinutes(15);

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await CreateAsync();
        using var timer = new PeriodicTimer(RefreshEvery);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                if (await userRepository.GetByNameAsync(UserService.DemoUserName) is { } user)
                    await demoSeeder.RefreshAsync(user.UserUniqueID);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogWarning(exception, "Demo mode: could not refresh the sample data.");
            }
        }
    }

    private async Task CreateAsync()
    {
        if (await userRepository.GetByNameAsync(UserService.DemoUserName) is not null)
        {
            return;
        }

        var user = await userRepository.CreateAsync(UserService.DemoUserName, PasswordHasher.Hash(options.Value.DemoPassword), Guid.NewGuid().ToString("N"));
        if (user is null)
        {
            return;
        }

        await profileRepository.SaveAsync(user.UserUniqueID, new UserProfile
        {
            BirthDate = new DateTime(1996, 5, 14),
            Sex = (byte)BiologicalSex.Male,
            HeightCm = 180,
            WeightKg = 79,
        });
        await demoSeeder.SeedAsync(user.UserUniqueID);
        if (logger.IsEnabled(LogLevel.Information))
            logger.LogInformation("Demo mode: account \"{UserName}\" is ready with sample data.", UserService.DemoUserName);
    }
}
