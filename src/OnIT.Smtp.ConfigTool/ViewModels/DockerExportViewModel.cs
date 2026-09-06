using System.IO;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using OnIT.Smtp.ConfigTool.Services;

namespace OnIT.Smtp.ConfigTool.ViewModels;

/// <summary>
/// Exports the current configuration in the shape the Part 2 Docker bridge consumes:
/// the same AppConfiguration JSON, but with the client secret decrypted to plain text
/// (the container has no access to this machine's DPAPI keys) so the operator can hand
/// it to the container via a mounted config file or environment variables.
/// </summary>
public partial class DockerExportViewModel : ObservableObject
{
    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private bool _includePlainTextSecret = true;

    [RelayCommand]
    private void Export()
    {
        var config = ConfigurationContext.Instance.Current;

        if (!config.EntraApp.IsConfigured)
        {
            StatusMessage = "Create the Entra app first -- there is nothing to export yet.";
            return;
        }

        var exportObject = new
        {
            schemaVersion = config.SchemaVersion,
            entraApp = new
            {
                tenantId = config.EntraApp.TenantId,
                applicationId = config.EntraApp.ApplicationId,
                displayName = config.EntraApp.DisplayName,
                authMode = config.EntraApp.AuthMode.ToString(),
                clientSecret = IncludePlainTextSecret && config.EntraApp.ProtectedClientSecret is not null
                    ? ConfigurationContext.Instance.UnprotectSecret(config.EntraApp.ProtectedClientSecret)
                    : null,
                clientSecretExpiresOn = config.EntraApp.ClientSecretExpiresOn
            },
            allowedSenders = config.AllowedSenders,
            ipAllowRules = config.IpAllowRules,
            smtpListener = config.SmtpListener,
            advanced = config.Advanced,
            logging = new
            {
                verbosity = config.Logging.Verbosity.ToString()
            }
        };

        var json = JsonSerializer.Serialize(exportObject, new JsonSerializerOptions { WriteIndented = true });

        var dialog = new SaveFileDialog
        {
            FileName = "onit-smtp-docker-config.json",
            Filter = "JSON file (*.json)|*.json|All files (*.*)|*.*"
        };

        if (dialog.ShowDialog() != true) return;

        File.WriteAllText(dialog.FileName, json);
        StatusMessage = IncludePlainTextSecret
            ? $"Exported to {dialog.FileName}. This file contains the client secret in plain text -- handle and transport it securely, then delete the local copy."
            : $"Exported to {dialog.FileName} (secret omitted -- set it via the container's own secret/env mechanism).";
    }
}
