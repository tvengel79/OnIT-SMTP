using Microsoft.Graph;
using Microsoft.Graph.Models;
using Microsoft.Graph.Models.ODataErrors;
using OnIT.Smtp.Core.Configuration;

namespace OnIT.Smtp.Core.Mail;

/// <summary>
/// Sends mail via Microsoft Graph using the app-only (client credentials) app registration
/// configured for this bridge. Used both for the config tool's "test configuration" button
/// and by the Windows Service's SMTP relay engine for real traffic.
/// </summary>
public sealed class GraphMailService
{
    private static readonly string[] Scopes = { "https://graph.microsoft.com/.default" };

    private readonly GraphCredentialFactory _credentialFactory;

    public GraphMailService(GraphCredentialFactory credentialFactory)
    {
        _credentialFactory = credentialFactory;
    }

    public async Task SendAsync(EntraAppSettings appSettings, OutboundMessage message, CancellationToken ct = default)
    {
        var credential = _credentialFactory.Create(appSettings);
        var client = new GraphServiceClient(credential, Scopes);

        var graphMessage = new Message
        {
            Subject = message.Subject,
            Body = new ItemBody
            {
                ContentType = message.IsHtml ? BodyType.Html : BodyType.Text,
                Content = message.Body
            },
            ToRecipients = message.To.Select(ToRecipient).ToList(),
            CcRecipients = message.Cc.Select(ToRecipient).ToList(),
            BccRecipients = message.Bcc.Select(ToRecipient).ToList(),
            // Always a real (possibly empty) list, never null -- an explicit null on a
            // Kiota-generated collection property serializes differently than an untouched
            // one and has been observed to trip up the SDK's own response parsing.
            Attachments = message.Attachments.Select(a => (Attachment)new FileAttachment
            {
                OdataType = "#microsoft.graph.fileAttachment",
                Name = a.FileName,
                ContentType = a.ContentType,
                ContentBytes = a.Content
            }).ToList()
        };

        // Application permissions send "as" the mailbox in the path, so From must match
        // (and, when AdvancedSettings.RestrictToAllowedSenders is on, has already been
        // checked against the configured allow list before we get here).
        try
        {
            await client.Users[message.From].SendMail.PostAsync(new()
            {
                Message = graphMessage,
                SaveToSentItems = false
            }, cancellationToken: ct);
        }
        catch (ODataError ex)
        {
            var detail = ex.Error?.Message ?? ex.Message;
            var code = ex.Error?.Code;
            throw new InvalidOperationException(
                $"Microsoft Graph rejected the message (HTTP {ex.ResponseStatusCode}{(code is null ? "" : $", {code}")}): {detail}", ex);
        }
    }

    private static Recipient ToRecipient(string address) => new()
    {
        EmailAddress = new EmailAddress { Address = address }
    };
}
