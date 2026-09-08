using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OnIT.Smtp.ConfigTool.Services;
using OnIT.Smtp.Core.Ipc;
using OnIT.Smtp.Core.RemoteApi;

namespace OnIT.Smtp.ConfigTool.ViewModels;

/// <summary>
/// Pairs with, pushes configuration to, and reads live status from a Part 2 Docker bridge over
/// its optional remote API -- an alternative to copying config.json + secret.key onto the
/// bridge's host by hand. Pairing (host, port, certificate fingerprint, pairing token) happens
/// once per bridge, using values the operator reads from the bridge's own boot log; everything
/// afterward is a couple of clicks.
/// </summary>
public partial class RemoteBridgeViewModel : ObservableObject
{
    private readonly RemoteBridgeStore _store = new();
    private readonly RemoteBridgeClient _client = new();

    [ObservableProperty] private string _newName = string.Empty;
    [ObservableProperty] private string _newHost = string.Empty;
    [ObservableProperty] private int _newPort = 8443;
    [ObservableProperty] private string _newFingerprint = string.Empty;
    [ObservableProperty] private string _newPairingToken = string.Empty;

    [ObservableProperty] private RemoteBridgeConnection? _selectedBridge;
    [ObservableProperty] private ServiceStatus? _liveStatus;
    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private bool _isBusy;

    public ObservableCollection<RemoteBridgeConnection> Bridges { get; } = new();

    public RemoteBridgeViewModel()
    {
        foreach (var bridge in _store.Load()) Bridges.Add(bridge);
    }

    partial void OnSelectedBridgeChanged(RemoteBridgeConnection? value)
    {
        LiveStatus = null;
        StatusMessage = string.Empty;
    }

    [RelayCommand]
    private void AddBridge()
    {
        if (string.IsNullOrWhiteSpace(NewHost) || string.IsNullOrWhiteSpace(NewFingerprint) || string.IsNullOrWhiteSpace(NewPairingToken))
        {
            StatusMessage = "Host, certificate fingerprint, and pairing token are all required -- read them from the bridge's log on first boot (`docker logs`).";
            return;
        }

        var bridge = new RemoteBridgeConnection
        {
            Name = NewName,
            Host = NewHost,
            Port = NewPort,
            CertificateFingerprint = NewFingerprint.Trim().ToUpperInvariant(),
            ProtectedPairingToken = ConfigurationContext.Instance.ProtectSecret(NewPairingToken)
        };

        Bridges.Add(bridge);
        _store.Save(Bridges.ToList());

        NewName = string.Empty;
        NewHost = string.Empty;
        NewPort = 8443;
        NewFingerprint = string.Empty;
        NewPairingToken = string.Empty;
        StatusMessage = "Bridge paired.";
    }

    [RelayCommand]
    private void RemoveSelected()
    {
        if (SelectedBridge is null) return;

        Bridges.Remove(SelectedBridge);
        _store.Save(Bridges.ToList());
        SelectedBridge = null;
        LiveStatus = null;
    }

    [RelayCommand]
    private async Task PushConfigAsync()
    {
        if (SelectedBridge is null) return;

        IsBusy = true;
        StatusMessage = "Pushing configuration...";
        try
        {
            var config = ConfigurationContext.Instance.Current;
            var request = new PushConfigRequest
            {
                SchemaVersion = config.SchemaVersion,
                EntraApp = new PushEntraApp
                {
                    TenantId = config.EntraApp.TenantId,
                    ApplicationId = config.EntraApp.ApplicationId,
                    DisplayName = config.EntraApp.DisplayName,
                    AuthMode = config.EntraApp.AuthMode,
                    ClientSecret = config.EntraApp.ProtectedClientSecret is null
                        ? null
                        : ConfigurationContext.Instance.UnprotectSecret(config.EntraApp.ProtectedClientSecret),
                    ClientSecretExpiresOn = config.EntraApp.ClientSecretExpiresOn,
                    AdminConsentGranted = config.EntraApp.AdminConsentGranted
                },
                AllowedSenders = config.AllowedSenders,
                IpAllowRules = config.IpAllowRules,
                SmtpListener = config.SmtpListener,
                Logging = config.Logging,
                Advanced = config.Advanced,
                SecretExpiryNotifications = config.SecretExpiryNotifications
            };

            var pairingToken = ConfigurationContext.Instance.UnprotectSecret(SelectedBridge.ProtectedPairingToken);
            var (success, message) = await _client.PushConfigAsync(SelectedBridge, pairingToken, request, CancellationToken.None);
            StatusMessage = message;

            if (success) await RefreshStatusAsync();
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task RefreshStatusAsync()
    {
        if (SelectedBridge is null) return;

        IsBusy = true;
        try
        {
            var pairingToken = ConfigurationContext.Instance.UnprotectSecret(SelectedBridge.ProtectedPairingToken);
            var (success, status, message) = await _client.GetStatusAsync(SelectedBridge, pairingToken, CancellationToken.None);
            LiveStatus = success ? status : null;
            StatusMessage = message;
        }
        finally
        {
            IsBusy = false;
        }
    }
}
