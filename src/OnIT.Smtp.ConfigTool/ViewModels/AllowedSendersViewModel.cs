using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OnIT.Smtp.ConfigTool.Services;
using OnIT.Smtp.Core.Configuration;
using OnIT.Smtp.Core.Entra;

namespace OnIT.Smtp.ConfigTool.ViewModels;

public partial class AllowedSendersViewModel : ObservableObject
{
    [ObservableProperty] private string _searchTerm = string.Empty;
    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private MailboxUser? _selectedSearchResult;
    [ObservableProperty] private AllowedSender? _selectedAllowedSender;
    [ObservableProperty] private bool _restrictToAllowedSenders;

    public ObservableCollection<MailboxUser> SearchResults { get; } = new();
    public ObservableCollection<AllowedSender> AllowedSenders { get; } = new();

    public AllowedSendersViewModel()
    {
        foreach (var sender in ConfigurationContext.Instance.Current.AllowedSenders)
        {
            AllowedSenders.Add(sender);
        }
        RestrictToAllowedSenders = ConfigurationContext.Instance.Current.Advanced.RestrictToAllowedSenders;
    }

    partial void OnRestrictToAllowedSendersChanged(bool value)
    {
        ConfigurationContext.Instance.Current.Advanced.RestrictToAllowedSenders = value;
        ConfigurationContext.Instance.Save();
    }

    [RelayCommand]
    private async Task SearchUsersAsync()
    {
        var tenantId = ConfigurationContext.Instance.Current.EntraApp.TenantId;
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            StatusMessage = "Create the Entra app first (on the Entra App tab) so a tenant is known.";
            return;
        }

        IsBusy = true;
        // Accumulated (not overwritten) for this one operation, so the sign-in diagnostics
        // below stay visible instead of being stomped by the final result message.
        StatusMessage = "Signing in and searching...";
        try
        {
            var client = await EntraSessionService.Instance.EnsureClientAsync(
                tenantId, useDeviceCode: false, message => StatusMessage += Environment.NewLine + message);
            var directory = new EntraUserDirectory(client);
            var results = await directory.ListMailboxUsersAsync(string.IsNullOrWhiteSpace(SearchTerm) ? null : SearchTerm);

            SearchResults.Clear();
            foreach (var user in results) SearchResults.Add(user);

            StatusMessage += Environment.NewLine + $"{results.Count} mailbox user(s) found.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Search failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void AddSelected()
    {
        if (SelectedSearchResult is null) return;
        if (AllowedSenders.Any(s => string.Equals(s.UserPrincipalName, SelectedSearchResult.UserPrincipalName, StringComparison.OrdinalIgnoreCase)))
        {
            StatusMessage = "That user is already in the allowed-senders list.";
            return;
        }

        var sender = new AllowedSender
        {
            UserPrincipalName = SelectedSearchResult.UserPrincipalName,
            DisplayName = SelectedSearchResult.DisplayName,
            EntraObjectId = SelectedSearchResult.Id,
            Enabled = true
        };

        AllowedSenders.Add(sender);
        ConfigurationContext.Instance.Current.AllowedSenders.Add(sender);
        ConfigurationContext.Instance.Save();
    }

    [RelayCommand]
    private void RemoveSelected()
    {
        if (SelectedAllowedSender is null) return;

        ConfigurationContext.Instance.Current.AllowedSenders.Remove(SelectedAllowedSender);
        AllowedSenders.Remove(SelectedAllowedSender);
        ConfigurationContext.Instance.Save();
    }
}
