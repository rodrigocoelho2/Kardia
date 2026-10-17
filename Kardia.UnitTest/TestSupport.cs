using Kardia.Library.Enums;
using Kardia.WebApi.Extensions.Data;
using Kardia.WebApi.Extensions.Globalization;
using Kardia.WebApi.Extensions.Time;
using Kardia.WebApi.Features;
using Kardia.WebApi.Features.Ages.Services;
using Kardia.WebApi.Features.Days.Services;
using Kardia.WebApi.Features.Demo.Services;
using Kardia.WebApi.Features.Journals.Repositories;
using Kardia.WebApi.Features.Journals.Services;
using Kardia.WebApi.Features.Plans.Services;
using Kardia.WebApi.Features.Push.Repositories;
using Kardia.WebApi.Features.Reports.Services;
using Kardia.WebApi.Features.Settings.Repositories;
using Kardia.WebApi.Features.Settings.Services;
using Kardia.WebApi.Features.Profiles.Repositories;
using Kardia.WebApi.Features.Profiles.Services;
using Kardia.WebApi.Features.Syncs.Repositories;
using Kardia.WebApi.Features.Syncs.Services;
using Kardia.WebApi.Features.Users.Models.DTOs;
using Kardia.WebApi.Features.Users.Repositories;
using Kardia.WebApi.Features.Users.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Kardia.UnitTest;

/// <summary>Shared helpers: a fixed clock and the whole WebApi wired on the in-memory repositories.</summary>
internal static class TestSupport
{
    /// <summary>Friday 16 October 2026, 18:40 in Lisbon (17:40 UTC): the day the demo data is built around.</summary>
    public static readonly DateTimeOffset Now = new(2026, 10, 16, 17, 40, 0, TimeSpan.Zero);

    public static DateOnly Today => new(2026, 10, 16);

    public static IOptions<KardiaOptions> Options(bool demo = false, Action<KardiaOptions>? configure = null)
    {
        var options = new KardiaOptions { ApiKey = "test-key", TimeZone = "Europe/Lisbon", DemoMode = demo };
        configure?.Invoke(options);
        return Microsoft.Extensions.Options.Options.Create(options);
    }
}

/// <summary>A clock stopped at <see cref="TestSupport.Now"/>.</summary>
internal sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    public DateTimeOffset Value { get; set; } = now;

    public override DateTimeOffset GetUtcNow() => Value;
}

/// <summary>
/// The services of Kardia.WebApi on one in-memory store, as demo mode wires them.
/// </summary>
internal sealed class TestApp
{
    public TestApp(bool demo = false)
    {
        Time = new FixedTimeProvider(TestSupport.Now);
        var options = TestSupport.Options(demo);
        Clock = new AppClock(Time, options);
        var globalization = new GlobalizationHelper();
        Store = new MemoryStore();
        Users = new MemoryUserRepository(Store, Time);
        Profiles = new MemoryProfileRepository(Store);
        Health = new MemoryHealthDataRepository(Store);
        SyncKeys = new MemorySyncKeyRepository(Store, Time);
        Journal = new MemoryJournalRepository(Store);
        Settings = new MemorySettingRepository(Store);
        Push = new MemoryPushRepository(Store);
        Secrets = new MemorySecretRepository(Store);
        DayLoads = new DayLoadService(Health, Profiles, Clock);
        Scores = new DayScoreReader(Health);
        Seeder = demo ? new DemoSeeder(Health, SyncKeys, Journal, DayLoads, Clock) : new NoDemoSeeder();
        UserService = new UserService(Users, Profiles, Seeder, globalization, Clock, options);
        ProfileService = new ProfileService(Profiles, Users, globalization, Clock);
        DayService = new DayService(Users, Profiles, Health, SyncKeys, Journal, Scores, Settings, globalization, Clock);
        SettingService = new SettingService(Settings, Users, globalization);
        PlanService = new PlanService(Users, Profiles, Settings, Scores, globalization, Clock);
        ReportService = new ReportService(Users, Profiles, Scores, globalization, Clock);
        AgeService = new AgeService(Users, Profiles, Scores, globalization, Clock);
        JournalService = new JournalService(Journal, Users, Profiles, Scores, globalization, Clock);
        SyncService = new SyncService(SyncKeys, Health, Users, DayLoads, globalization, Clock, NullLogger<SyncService>.Instance);
    }

    public FixedTimeProvider Time { get; }

    public IAppClock Clock { get; }

    public MemoryStore Store { get; }

    public MemoryUserRepository Users { get; }

    public MemoryProfileRepository Profiles { get; }

    public MemoryHealthDataRepository Health { get; }

    public MemorySyncKeyRepository SyncKeys { get; }

    public MemoryJournalRepository Journal { get; }

    public MemorySettingRepository Settings { get; }

    public MemoryPushRepository Push { get; }

    public MemorySecretRepository Secrets { get; }

    public SettingService SettingService { get; }

    public PlanService PlanService { get; }

    public ReportService ReportService { get; }

    public DayLoadService DayLoads { get; }

    public DayScoreReader Scores { get; }

    public IDemoSeeder Seeder { get; }

    public UserService UserService { get; }

    public ProfileService ProfileService { get; }

    public DayService DayService { get; }

    public AgeService AgeService { get; }

    public JournalService JournalService { get; }

    public SyncService SyncService { get; }

    /// <summary>Creates an account (seeded with the demo data when the app runs in demo mode) and returns its id.</summary>
    public async Task<string> CreateUserAsync(string name = "rita", DateOnly? birthDate = null, BiologicalSex sex = BiologicalSex.Female)
    {
        var created = await UserService.CreateAsync(new UserCreateRequest
        {
            UserName = name,
            Password = "recovery-2026",
            BirthDate = birthDate ?? new DateOnly(1996, 5, 14),
            Sex = sex,
        });
        Assert.True(created.IsSuccess, created.Problems?.Title);
        return created.Result!.Id;
    }
}
