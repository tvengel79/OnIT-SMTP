using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OnIT.Smtp.ConfigTool.Services;
using OnIT.Smtp.Core.Configuration;
using OnIT.Smtp.Core.Ipc;
using OnIT.Smtp.Core.Mail;

namespace OnIT.Smtp.ConfigTool.ViewModels;

public partial class TestEmailViewModel : ObservableObject
{
    [ObservableProperty] private string? _from;
    [ObservableProperty] private string _to = string.Empty;
    [ObservableProperty] private string _subject = "OnIT-SMTP test message";
    [ObservableProperty] private string _body = "This is a test message sent from the OnIT-SMTP configuration tool.";
    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private bool _isBusy;

    public ObservableCollection<string> AvailableSenders { get; } = new();

    public TestEmailViewModel()
    {
        foreach (var sender in ConfigurationContext.Instance.Current.AllowedSenders)
        {
            AvailableSenders.Add(sender.UserPrincipalName);
        }
        From = AvailableSenders.FirstOrDefault();
    }

    [RelayCommand]
    private async Task SendAsync()
    {
        var config = ConfigurationContext.Instance.Current;

        if (!config.EntraApp.IsConfigured)
        {
            StatusMessage = "Create the Entra app first.";
            return;
        }

        if (string.IsNullOrWhiteSpace(From) || string.IsNullOrWhiteSpace(To))
        {
            StatusMessage = "Both From and To are required.";
            return;
        }

        IsBusy = true;
        StatusMessage = "Sending...";

        try
        {
            if (PipeClientService.Instance.IsConnected)
            {
                var result = await PipeClientService.Instance.SendTestEmailAsync(new SendTestEmailRequestPayload
                {
                    From = From,
                    To = To,
                    Subject = Subject,
                    Body = Body
                });

                StatusMessage = result?.Success == true
                    ? "Sent successfully via the running service."
                    : $"Failed: {result?.ErrorMessage ?? "no response from the service"}.";
            }
            else
            {
                var credentialFactory = new GraphCredentialFactory(new DpapiSecretProtector());
                var mailService = new GraphMailService(credentialFactory);

                await mailService.SendAsync(config.EntraApp, new OutboundMessage
                {
                    From = From,
                    To = new[] { To },
                    Subject = Subject,
                    Body = Body,
                    IsHtml = false
                });

                StatusMessage = "Sent successfully (service was not running -- sent directly from the config tool).";
            }
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
}
