using System.Diagnostics;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging.Abstractions;
using OnIT.Smtp.ConfigTool.Services;
using OnIT.Smtp.Core.Configuration;
using OnIT.Smtp.Core.Entra;

namespace OnIT.Smtp.ConfigTool.ViewModels;

public partial class EntraAppViewModel : ObservableObject
{
    [ObservableProperty] private string _tenantId = string.Empty;
    [ObservableProperty] private string _displayName = "OnIT-SMTP Bridge";
    [ObservableProperty] private bool _useDeviceCodeSignIn;
    [ObservableProperty] private string _signInClientIdOverride = string.Empty;

    /// <summary>
    /// When checked (the default), app creation first tries to grant Mail.Send admin consent
    /// directly via the Graph API -- a nice shortcut when it works, but it needs the
    /// signed-in account to hold a role Graph accepts for app-role assignment, which can fail
    /// unpredictably depending on tenant policy. When unchecked, that attempt is skipped
    /// entirely and only the browser consent link is prepared: the simpler, more reliable
    /// path where a Global/Application Administrator just opens it and clicks Accept.
    /// </summary>
    [ObservableProperty] private bool _attemptAutomaticConsent = true;

    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private bool _isBusy;

    partial void OnSignInClientIdOverrideChanged(string value)
    {
        EntraSessionService.Instance.ClientIdOverride = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    public EntraAppSettings Settings => ConfigurationContext.Instance.Current.EntraApp;

    public bool IsConfigured => Settings.IsConfigured;
    public string ApplicationId => Settings.ApplicationId;
    public bool AdminConsentGranted => Settings.AdminConsentGranted;
    public string? AdminConsentUrl => Settings.AdminConsentUrl;
    public bool CanOpenConsentUrl => !string.IsNullOrWhiteSpace(AdminConsentUrl);

    public EntraAppViewModel()
    {
        if (Settings.IsConfigured) TenantId = Settings.TenantId;
    }

    [RelayCommand]
    private async Task CreateAppAsync()
    {
        if (string.IsNullOrWhiteSpace(TenantId))
        {
            StatusMessage = "Enter your tenant ID or domain (e.g. contoso.onmicrosoft.com) first.";
            return;
        }

        IsBusy = true;
        StatusMessage = "Signing in...";

        try
        {
            var client = EntraSessionService.Instance.EnsureClient(TenantId, UseDeviceCodeSignIn,
                message => Application.Current.Dispatcher.Invoke(() => StatusMessage = message));

            StatusMessage = "Creating the app registration...";
            var manager = new EntraAppManager(client, NullLogger<EntraAppManager>.Instance);
            var result = await manager.CreateAppAsync(TenantId, DisplayName, AttemptAutomaticConsent);

            var config = ConfigurationContext.Instance.Current;
            config.EntraApp.TenantId = result.TenantId;
            config.EntraApp.ApplicationId = result.ApplicationId;
            config.EntraApp.ApplicationObjectId = result.ApplicationObjectId;
            config.EntraApp.ServicePrincipalObjectId = result.ServicePrincipalObjectId;
            config.EntraApp.DisplayName = result.DisplayName;
            config.EntraApp.AuthMode = GraphAuthMode.ClientSecret;
            config.EntraApp.ProtectedClientSecret = ConfigurationContext.Instance.ProtectSecret(result.ClientSecret);
            config.EntraApp.ClientSecretExpiresOn = result.ClientSecretExpiresOn;
            config.EntraApp.AdminConsentGranted = result.AdminConsentGranted;
            config.EntraApp.AdminConsentUrl = result.AdminConsentUrl;
            ConfigurationContext.Instance.Save();

            StatusMessage = result.AdminConsentGranted
                ? "App registration created and admin consent granted."
                : "App registration created. Click \"Grant consent in browser\" below and accept as a Global/Application Administrator to finish.";

            RaiseAllChanged();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task DeleteAppAsync()
    {
        if (!IsConfigured) return;

        if (MessageBox.Show("This deletes the Entra app registration and its service principal. Continue?",
                "Delete Entra app", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
        {
            return;
        }

        IsBusy = true;
        StatusMessage = "Signing in...";

        try
        {
            var client = EntraSessionService.Instance.EnsureClient(Settings.TenantId, UseDeviceCodeSignIn,
                message => Application.Current.Dispatcher.Invoke(() => StatusMessage = message));

            var manager = new EntraAppManager(client, NullLogger<EntraAppManager>.Instance);
            await manager.DeleteAppAsync(Settings.ApplicationObjectId, Settings.ServicePrincipalObjectId);

            ConfigurationContext.Instance.Current.EntraApp = new EntraAppSettings();
            ConfigurationContext.Instance.Save();

            StatusMessage = "App registration deleted.";
            RaiseAllChanged();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task CheckConsentAsync()
    {
        if (!IsConfigured) return;

        IsBusy = true;
        try
        {
            var client = EntraSessionService.Instance.EnsureClient(Settings.TenantId, UseDeviceCodeSignIn);
            var manager = new EntraAppManager(client, NullLogger<EntraAppManager>.Instance);

            var granted = await manager.HasAdminConsentAsync(Settings.ServicePrincipalObjectId);
            Settings.AdminConsentGranted = granted;
            ConfigurationContext.Instance.Save();

            StatusMessage = granted ? "Admin consent confirmed." : "Admin consent is not confirmed yet.";
            RaiseAllChanged();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void OpenConsentUrl()
    {
        if (string.IsNullOrWhiteSpace(AdminConsentUrl)) return;
        Process.Start(new ProcessStartInfo(AdminConsentUrl) { UseShellExecute = true });
    }

    private void RaiseAllChanged()
    {
        OnPropertyChanged(nameof(Settings));
        OnPropertyChanged(nameof(IsConfigured));
        OnPropertyChanged(nameof(ApplicationId));
        OnPropertyChanged(nameof(AdminConsentGranted));
        OnPropertyChanged(nameof(AdminConsentUrl));
        OnPropertyChanged(nameof(CanOpenConsentUrl));
    }
}
