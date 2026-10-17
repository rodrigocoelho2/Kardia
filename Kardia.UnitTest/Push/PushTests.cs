using System.Buffers.Binary;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Kardia.Library.Enums;
using Kardia.Library.Services;
using Kardia.WebApi.Extensions.Globalization;
using Kardia.WebApi.Features.Days.Models.DTOs;
using Kardia.WebApi.Features.Push.Models.DTOs;
using Kardia.WebApi.Features.Push.Services;
using Kardia.WebApi.Features.Reports.Models.DTOs;
using Kardia.WebApi.Features.Settings.Models.Entities;
using Microsoft.Extensions.Logging.Abstractions;

namespace Kardia.UnitTest.Push;

/// <summary>Web Push (the encryption a browser opens, the VAPID token, the devices) and which notifications are due.</summary>
public sealed class PushTests
{
    private const string Endpoint = "https://web.push.apple.com/QOxdevice";

    [Fact]
    public void Encrypt_CanBeOpenedByTheBrowser()
    {
        var (browser, p256dh, auth) = Browser();
        var payload = Encoding.UTF8.GetBytes("{\"title\":\"Recovery 72%\"}");

        var body = WebPushCrypto.Encrypt(payload, p256dh, auth);

        Assert.Equal(payload, Decrypt(body, browser, WebPushCrypto.FromBase64Url(auth)));
        Assert.Equal(4096u, BinaryPrimitives.ReadUInt32BigEndian(body.AsSpan(16, 4)));
    }

    [Fact]
    public void VapidToken_IsSignedForThePushServiceOrigin()
    {
        var (publicKey, privateKey) = WebPushCrypto.CreateVapidKeys();
        var header = WebPushCrypto.VapidAuthorization(new Uri(Endpoint), "https://kardia.example.com", publicKey, privateKey, DateTimeOffset.UtcNow.AddHours(12));

        var token = header["vapid t=".Length..header.IndexOf(',', StringComparison.Ordinal)].Split('.');
        var claims = JsonDocument.Parse(WebPushCrypto.FromBase64Url(token[1])).RootElement;
        Assert.Equal("https://web.push.apple.com", claims.GetProperty("aud").GetString());
        var point = WebPushCrypto.FromBase64Url(publicKey);
        using var key = ECDsa.Create(new ECParameters { Curve = ECCurve.NamedCurves.nistP256, Q = new ECPoint { X = point[1..33], Y = point[33..65] } });
        Assert.True(key.VerifyData(Encoding.ASCII.GetBytes($"{token[0]}.{token[1]}"), WebPushCrypto.FromBase64Url(token[2]), HashAlgorithmName.SHA256));
    }

    [Theory]
    [InlineData("https://web.push.apple.com/QOx", true)]
    [InlineData("https://fcm.googleapis.com/fcm/send/x", true)]
    [InlineData("http://fcm.googleapis.com/fcm/send/x", false)]
    [InlineData("https://evil.example/fcm.googleapis.com", false)]
    [InlineData("https://192.168.1.10/push", false)]
    public void OnlyThePushServices_AreAccepted(string endpoint, bool expected) =>
        Assert.Equal(expected, PushSender.IsPushServiceEndpoint(endpoint));

    [Fact]
    public async Task Subscribe_Test_AndAGoneDevice()
    {
        var app = new TestApp();
        var user = await app.CreateUserAsync();
        var handler = new RecordingHandler(HttpStatusCode.Created);
        var service = Service(app, handler);
        var (browser, p256dh, auth) = Browser();

        var refused = await service.SubscribeAsync(new PushSubscribeRequest { UserId = user, Endpoint = "https://nas.local/push", P256dh = p256dh, Auth = auth });
        Assert.Equal(ServiceApiResponseStatus.ValidationProblems, refused.Status);
        Assert.Equal(ServiceApiResponseStatus.ValidationProblems, (await service.TestAsync(user)).Status);

        var state = (await service.SubscribeAsync(new PushSubscribeRequest { UserId = user, Endpoint = Endpoint, P256dh = p256dh, Auth = auth, Device = "iPhone · Kardia app" })).Result!;
        Assert.Equal(PushDeviceDTO.KeyOf(Endpoint), Assert.Single(state.Devices).Key);
        Assert.Equal(65, WebPushCrypto.FromBase64Url(state.PublicKey).Length);
        Assert.Equal(state.PublicKey, (await service.GetAsync(user)).Result!.PublicKey);

        var result = (await service.TestAsync(user)).Result!;
        Assert.Equal((1, 0), (result.Reached, result.Failed));
        var request = Assert.Single(handler.Requests);
        var payload = JsonDocument.Parse(Decrypt(request.Body, browser, WebPushCrypto.FromBase64Url(auth))).RootElement;
        Assert.Equal("Kardia notifications are on", payload.GetProperty("title").GetString());
        Assert.StartsWith("vapid t=", request.Authorization);

        var gone = Service(app, new RecordingHandler(HttpStatusCode.Gone));
        await gone.TestAsync(user);
        Assert.Empty((await gone.GetAsync(user)).Result!.Devices);
    }

    [Fact]
    public void Payload_IsCutToFitOneRecord()
    {
        var payload = PushSender.PayloadOf(new PushMessage("Title", new string('x', 10_000), "/", "t"));

        Assert.True(payload.Length <= WebPushCrypto.MaxPayloadBytes);
        Assert.Equal('…', JsonDocument.Parse(payload).RootElement.GetProperty("body").GetString()![^1]);
    }

    [Fact]
    public void Morning_GoesOutOnceTheNightIsIn_AndOnlyInTheMorning()
    {
        var day = Day();

        var morning = NotificationPlanner.Daily(new DateTime(2026, 10, 16, 7, 30, 0), day, UserSetting.Defaults);
        var afternoon = NotificationPlanner.Daily(new DateTime(2026, 10, 16, 15, 0, 0), day, UserSetting.Defaults);
        day.Sleep.HasNight = false;
        var noNight = NotificationPlanner.Daily(new DateTime(2026, 10, 16, 7, 30, 0), day, UserSetting.Defaults);

        var sent = Assert.Single(morning);
        Assert.Equal("morning:2026-10-16", sent.Key);
        Assert.Equal("Recovery 72% · Ready to push", sent.Message.Title);
        Assert.Contains("HRV 58 ms", sent.Message.Body);
        Assert.Empty(afternoon);
        Assert.Empty(noNight);
    }

    [Fact]
    public void Bedtime_GoesOutTheLeadTimeBefore()
    {
        var day = Day();
        var setting = new UserSetting { NotifyMorning = false, BedtimeLeadMinutes = 30 };

        Assert.Empty(NotificationPlanner.Daily(new DateTime(2026, 10, 16, 22, 20, 0), day, setting));
        var due = Assert.Single(NotificationPlanner.Daily(new DateTime(2026, 10, 16, 22, 35, 0), day, setting));
        Assert.Equal("bedtime:2026-10-16", due.Key);
        Assert.Equal("Bedtime in 30 min", due.Message.Title);
        Assert.Contains("23:05", due.Message.Body);
        Assert.Contains("Perform", due.Message.Body);
        Assert.Empty(NotificationPlanner.Daily(new DateTime(2026, 10, 17, 0, 10, 0), day, setting));
    }

    [Fact]
    public void HealthAndStrain_FollowTheChoices()
    {
        var day = Day();
        day.Health.Signs =
        [
            new HealthSignDTO { Key = "hrv", Name = "Heart rate variability", Value = 31, Unit = "ms", Low = 45, High = 70, Typical = 58, Status = HealthSignStatus.FarOutside },
            new HealthSignDTO { Key = "resting", Name = "Resting heart rate", Value = 61, Unit = "bpm", Low = 48, High = 55, Typical = 52, Status = HealthSignStatus.Outside },
        ];
        day.Strain.Score = 13;
        var now = new DateTime(2026, 10, 16, 16, 0, 0);

        var defaults = NotificationPlanner.Daily(now, day, UserSetting.Defaults);
        var withStrain = NotificationPlanner.Daily(now, day, new UserSetting { NotifyStrain = true });

        Assert.Equal("Health Monitor: 2 signs outside your range", Assert.Single(defaults).Message.Title);
        Assert.Contains(withStrain, static n => n.Key == "strain:2026-10-16" && n.Message.Title == "Strain target reached");
    }

    [Fact]
    public void Reports_AreDueOnSundayEvening_AndOnTheFirst()
    {
        Assert.Empty(NotificationPlanner.ReportsDue(new DateTime(2026, 10, 18, 18, 0, 0), UserSetting.Defaults));
        var week = Assert.Single(NotificationPlanner.ReportsDue(new DateTime(2026, 10, 18, 19, 5, 0), UserSetting.Defaults));
        Assert.Equal((ReportPeriod.Week, "week:2026-10-12"), (week.Period, week.Key));
        var month = Assert.Single(NotificationPlanner.ReportsDue(new DateTime(2026, 11, 1, 10, 0, 0), UserSetting.Defaults));
        Assert.Equal((ReportPeriod.Month, new DateOnly(2026, 10, 31), "month:2026-10"), (month.Period, month.Date, month.Key));
        Assert.Empty(NotificationPlanner.ReportsDue(new DateTime(2026, 11, 1, 10, 0, 0), new UserSetting { NotifyReports = false }));

        var message = NotificationPlanner.Report(new ReportDTO { Period = ReportPeriod.Week, From = new DateOnly(2026, 10, 12), DaysWithData = 7, Score = 78, Assessment = "Strong", Highlights = ["One.", "Two.", "Three."] })!;
        Assert.Equal("Your week: Strong (78)", message.Title);
        Assert.Equal("One. Two.", message.Body);
        Assert.Equal("/report?period=week&date=2026-10-12", message.Url);
        Assert.Null(NotificationPlanner.Report(new ReportDTO()));
    }

    [Fact]
    public async Task TheWorker_SendsEachNotificationOnce()
    {
        var app = new TestApp(demo: true);
        var user = await app.CreateUserAsync();
        app.Time.Value = new DateTimeOffset(2026, 10, 16, 6, 30, 0, TimeSpan.Zero);   // 07:30 in Lisbon
        var handler = new RecordingHandler(HttpStatusCode.Created);
        var (_, p256dh, auth) = Browser();
        await Service(app, handler).SubscribeAsync(new PushSubscribeRequest { UserId = user, Endpoint = Endpoint, P256dh = p256dh, Auth = auth });
        var worker = new NotificationWorker(app.Push, Sender(app, handler), app.Settings, app.DayService, app.ReportService, app.Clock, TestSupport.Options(), NullLogger<NotificationWorker>.Instance);

        var first = await worker.RunOnceAsync();
        var second = await worker.RunOnceAsync();

        Assert.True(first >= 1);
        Assert.Equal(0, second);
        Assert.Equal(first, handler.Requests.Count);
    }

    private static DayDTO Day() => new()
    {
        Date = new DateOnly(2026, 10, 16),
        IsToday = true,
        Recovery = new RecoveryDTO { Score = 72, Zone = RecoveryZone.High, Hrv = 58, RestingHeartRate = 51, TargetLow = 12.1, TargetHigh = 14.1 },
        Sleep = new SleepDTO { HasNight = true, AsleepMinutes = 452, Performance = 94, GoalPercent = 85, TonightGoalMinutes = 410, Bedtime = new DateTime(2026, 10, 16, 23, 5, 0), WakeAt = new DateTime(2026, 10, 17, 7, 0, 0) },
        Strain = new StrainDTO { Score = 4.2 },
    };

    private static PushService Service(TestApp app, RecordingHandler handler) =>
        new(app.Push, Sender(app, handler), app.Users, TestSupport.Options(), new GlobalizationHelper());

    private static PushSender Sender(TestApp app, RecordingHandler handler) =>
        new(app.Push, app.Secrets, TestSupport.Options(), app.Time, NullLogger<PushSender>.Instance) { Http = new HttpClient(handler) };

    private static (ECDiffieHellman Key, string P256dh, string Auth) Browser()
    {
        var key = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);
        var q = key.ExportParameters(false).Q;
        return (key, WebPushCrypto.Base64Url([0x04, .. q.X!, .. q.Y!]), WebPushCrypto.Base64Url(RandomNumberGenerator.GetBytes(16)));
    }

    private static byte[] Decrypt(byte[] body, ECDiffieHellman browser, byte[] authSecret)
    {
        var salt = body[..16];
        var idLength = body[20];
        var serverPublic = body[21..(21 + idLength)];
        var ciphertext = body[(21 + idLength)..^16];
        var tag = body[^16..];
        using var server = ECDiffieHellman.Create(new ECParameters { Curve = ECCurve.NamedCurves.nistP256, Q = new ECPoint { X = serverPublic[1..33], Y = serverPublic[33..65] } });
        var q = browser.ExportParameters(false).Q;
        byte[] browserPublic = [0x04, .. q.X!, .. q.Y!];
        var shared = browser.DeriveRawSecretAgreement(server.PublicKey);
        byte[] info = [.. Encoding.ASCII.GetBytes("WebPush: info\0"), .. browserPublic, .. serverPublic];
        var ikm = HKDF.DeriveKey(HashAlgorithmName.SHA256, shared, 32, authSecret, info);
        var key = HKDF.DeriveKey(HashAlgorithmName.SHA256, ikm, 16, salt, Encoding.ASCII.GetBytes("Content-Encoding: aes128gcm\0"));
        var nonce = HKDF.DeriveKey(HashAlgorithmName.SHA256, ikm, 12, salt, Encoding.ASCII.GetBytes("Content-Encoding: nonce\0"));
        var plaintext = new byte[ciphertext.Length];
        using (var aes = new AesGcm(key, 16))
        {
            aes.Decrypt(nonce, ciphertext, tag, plaintext);
        }

        return plaintext[..^1];
    }

    private sealed class RecordingHandler(HttpStatusCode status) : HttpMessageHandler
    {
        public List<(byte[] Body, string Authorization)> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add((await request.Content!.ReadAsByteArrayAsync(cancellationToken),
                request.Headers.TryGetValues("Authorization", out var authorization) ? authorization.First() : string.Empty));
            return new HttpResponseMessage(status);
        }
    }
}
