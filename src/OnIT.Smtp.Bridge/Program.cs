using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OnIT.Smtp.Bridge;
using OnIT.Smtp.Core.Configuration;
using OnIT.Smtp.Core.Logging;
using OnIT.Smtp.Core.Mail;
using OnIT.Smtp.Core.RemoteApi;
using OnIT.Smtp.Core.Runtime;
using Serilog;

ConfigPaths.EnsureDirectoriesExist();

var configStore = new ConfigStore();
var initialConfig = configStore.Load();

var liveLogSink = new LiveLogSink();
var serilogLogger = LoggingSetup.CreateLogger(initialConfig.Logging, liveLogSink, includeConsole: true);
Log.Logger = serilogLogger;

try
{
    Log.Information("Starting OnIT-SMTP bridge.");

    if (initialConfig.RemoteApi.Enabled)
    {
        // Kestrel + minimal APIs only stand up when the remote config/status API is turned
        // on (off by default) -- see RemoteApiEndpoints for what it exposes and how it's
        // authenticated.
        var builder = WebApplication.CreateBuilder(args);
        builder.Host.UseSerilog();

        var (certificate, certFingerprint, certFreshlyGenerated) =
            RemoteApiCertificateProvider.LoadOrCreate(ConfigPaths.RemoteApiCertificatePath);
        Log.Information(
            certFreshlyGenerated
                ? "Remote API certificate generated. SHA-256 fingerprint (pin this in the config tool): {Fingerprint}"
                : "Remote API certificate loaded. SHA-256 fingerprint: {Fingerprint}",
            certFingerprint);

        var (pairingToken, tokenFreshlyGenerated) = RemoteApiTokenProvider.LoadOrCreate(ConfigPaths.RemoteApiTokenPath);
        if (tokenFreshlyGenerated)
        {
            Log.Information(
                "Remote API pairing token generated -- shown once, copy it into the config tool now: {Token}",
                pairingToken);
        }

        builder.WebHost.ConfigureKestrel(options =>
        {
            options.ListenAnyIP(initialConfig.RemoteApi.Port, listenOptions => listenOptions.UseHttps(certificate));
        });

        RegisterSharedServices(builder.Services);

        // The minimal-API JSON body binder uses its own JsonSerializerOptions, separate from
        // ConfigStore's -- without this, PushConfigRequest's enum fields (e.g. AuthMode) fail
        // to bind from their string values ("ClientSecret") since numeric is the default.
        builder.Services.ConfigureHttpJsonOptions(options =>
            options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

        var app = builder.Build();
        var relayWorker = app.Services.GetRequiredService<BridgeRelayWorker>();
        RemoteApiEndpoints.Map(app, pairingToken, configStore, app.Services.GetRequiredService<ISecretProtector>(), relayWorker);

        Log.Information("Remote API listening on port {Port}.", initialConfig.RemoteApi.Port);
        await app.RunAsync();
    }
    else
    {
        var host = Host.CreateDefaultBuilder(args)
            .UseSerilog()
            .ConfigureServices(RegisterSharedServices)
            .Build();

        await host.RunAsync();
    }
}
catch (Exception ex)
{
    Log.Fatal(ex, "OnIT-SMTP bridge terminated unexpectedly.");
}
finally
{
    Log.CloseAndFlush();
}

void RegisterSharedServices(IServiceCollection services)
{
    services.AddSingleton(liveLogSink);
    services.AddSingleton(configStore);
    services.AddSingleton<ISecretProtector, PortableSecretProtector>();
    services.AddSingleton<GraphCredentialFactory>();
    services.AddSingleton<GraphMailService>();
    services.AddSingleton<BridgeRelayWorker>();
    services.AddHostedService(sp => sp.GetRequiredService<BridgeRelayWorker>());
    services.AddHostedService<SecretExpiryWorker>();
}
