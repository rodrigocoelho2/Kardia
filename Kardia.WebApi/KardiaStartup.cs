using System.Text.Json.Serialization;
using Kardia.WebApi.Extensions.Authentication;
using Kardia.WebApi.Extensions.Data;
using Kardia.WebApi.Extensions.Globalization;
using Kardia.WebApi.Extensions.Mvc;
using Kardia.WebApi.Extensions.Time;
using Kardia.WebApi.Features;
using Kardia.WebApi.Features.Ages.Services;
using Kardia.WebApi.Features.AiCoach.Services;
using Kardia.WebApi.Features.Days.Services;
using Kardia.WebApi.Features.Demo.Services;
using Kardia.WebApi.Features.Journals.Repositories;
using Kardia.WebApi.Features.Journals.Services;
using Kardia.WebApi.Features.Profiles.Repositories;
using Kardia.WebApi.Features.Plans.Services;
using Kardia.WebApi.Features.Profiles.Services;
using Kardia.WebApi.Features.Push.Repositories;
using Kardia.WebApi.Features.Push.Services;
using Kardia.WebApi.Features.Reports.Services;
using Kardia.WebApi.Features.Settings.Repositories;
using Kardia.WebApi.Features.Settings.Services;
using Kardia.WebApi.Features.Syncs.Repositories;
using Kardia.WebApi.Features.Syncs.Services;
using Kardia.WebApi.Features.Users.Repositories;
using Kardia.WebApi.Features.Users.Services;
using Microsoft.AspNetCore.Authentication;

namespace Kardia.WebApi;

/// <summary>
/// Service registration and request pipeline of the WebApi.
/// </summary>
internal static class KardiaStartup
{
    /// <summary>
    /// Registers every service. All services and repositories are singletons; each repository is registered before its
    /// service. In demo mode the Memory* repositories replace the SQL Server ones.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration.</param>
    /// <param name="environment">The hosting environment.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddKardia(this IServiceCollection services, IConfiguration configuration, IWebHostEnvironment environment)
    {
        services.AddOptions<KardiaOptions>()
            .Bind(configuration.GetSection(KardiaOptions.SectionName))
            .Validate(static options => !string.IsNullOrWhiteSpace(options.ApiKey), "Kardia:ApiKey is required (env Kardia__ApiKey).")
            .ValidateOnStart();
        var demoMode = configuration.GetSection(KardiaOptions.SectionName).GetValue<bool>(nameof(KardiaOptions.DemoMode));

        services.AddControllers()
            .ConfigureApplicationPartManager(manager => manager.FeatureProviders.Add(new InternalControllerFeatureProvider()))
            .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

        services.AddAuthentication(ApiKeyAuthenticationHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, ApiKeyAuthenticationHandler>(ApiKeyAuthenticationHandler.SchemeName, null);
        services.AddAuthorization();

        if (environment.IsDevelopment())
        {
            services.AddEndpointsApiExplorer();
            services.AddSwaggerGen(options => options.EnableAnnotations());
        }

        // Infrastructure
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IAppClock, AppClock>();
        services.AddSingleton<IGlobalizationHelper, GlobalizationHelper>();

        // Repositories: SQL Server, or memory in demo mode.
        if (demoMode)
        {
            services.AddSingleton<MemoryStore>();
            services.AddSingleton<IUserRepository, MemoryUserRepository>();
            services.AddSingleton<IProfileRepository, MemoryProfileRepository>();
            services.AddSingleton<ISyncKeyRepository, MemorySyncKeyRepository>();
            services.AddSingleton<IHealthDataRepository, MemoryHealthDataRepository>();
            services.AddSingleton<IJournalRepository, MemoryJournalRepository>();
            services.AddSingleton<ISettingRepository, MemorySettingRepository>();
            services.AddSingleton<IPushRepository, MemoryPushRepository>();
            services.AddSingleton<ISecretRepository, MemorySecretRepository>();
        }
        else
        {
            services.AddSingleton<ISqlConnectionFactory, SqlConnectionFactory>();
            services.AddSingleton<IUserRepository, UserRepository>();
            services.AddSingleton<IProfileRepository, ProfileRepository>();
            services.AddSingleton<ISyncKeyRepository, SyncKeyRepository>();
            services.AddSingleton<IHealthDataRepository, HealthDataRepository>();
            services.AddSingleton<IJournalRepository, JournalRepository>();
            services.AddSingleton<ISettingRepository, SettingRepository>();
            services.AddSingleton<IPushRepository, PushRepository>();
            services.AddSingleton<ISecretRepository, SecretRepository>();
        }

        // Days (scores)
        services.AddSingleton<IDayScoreReader, DayScoreReader>();
        services.AddSingleton<IDayLoadService, DayLoadService>();
        services.AddSingleton<IDayService, DayService>();

        // Demo
        if (demoMode)
        {
            services.AddSingleton<IDemoSeeder, DemoSeeder>();
            services.AddHostedService<DemoStartup>();
        }
        else
        {
            services.AddSingleton<IDemoSeeder, NoDemoSeeder>();
        }

        // Users, Profiles
        services.AddSingleton<IUserService, UserService>();
        services.AddSingleton<IProfileService, ProfileService>();

        // Syncs
        services.AddSingleton<ISyncService, SyncService>();

        // Ages, Journals
        services.AddSingleton<IAgeService, AgeService>();
        services.AddSingleton<IJournalService, JournalService>();

        // Settings, Plans, Reports
        services.AddSingleton<ISettingService, SettingService>();
        services.AddSingleton<IPlanService, PlanService>();
        services.AddSingleton<IReportService, ReportService>();

        // AI coach
        services.AddSingleton<IAiModelClient, AiModelClient>();
        services.AddSingleton<IAiCoachService, AiCoachService>();

        // Push notifications
        services.AddSingleton<IPushSender, PushSender>();
        services.AddSingleton<IPushService, PushService>();
        services.AddHostedService<NotificationWorker>();

        return services;
    }

    /// <summary>
    /// Configures the request pipeline.
    /// </summary>
    /// <param name="app">The application.</param>
    /// <returns>The application.</returns>
    public static WebApplication UseKardia(this WebApplication app)
    {
        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        app.UseAuthentication();
        app.UseAuthorization();
        app.MapControllers();

        return app;
    }
}
