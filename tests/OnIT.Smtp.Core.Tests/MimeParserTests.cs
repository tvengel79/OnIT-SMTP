using System.Text;
using OnIT.Smtp.Core.Smtp;
using Xunit;

namespace OnIT.Smtp.Core.Tests;

public class MimeParserTests
{
    [Fact]
    public void ParsesSimplePlainTextMessage()
    {
        var raw = "Subject: Hello\r\nContent-Type: text/plain; charset=utf-8\r\n\r\nHello world!\r\n";
        var entity = MimeParser.Parse(Encoding.ASCII.GetBytes(raw));

        Assert.Equal("Hello", entity.GetHeader("Subject"));
        Assert.Equal("Hello world!\r\n", entity.GetDecodedText());
    }

    [Fact]
    public void ParsesBase64EncodedBody()
    {
        var body = System.Convert.ToBase64String(Encoding.UTF8.GetBytes("Encoded body"));
        var raw = $"Content-Type: text/plain\r\nContent-Transfer-Encoding: base64\r\n\r\n{body}\r\n";
        var entity = MimeParser.Parse(Encoding.ASCII.GetBytes(raw));

        Assert.Equal("Encoded body", entity.GetDecodedText());
    }

    [Fact]
    public void ParsesMultipartMixedWithAttachment()
    {
        const string boundary = "BOUNDARY123";
        var attachmentBytes = Encoding.UTF8.GetBytes("PDF-CONTENT");
        var attachmentBase64 = System.Convert.ToBase64String(attachmentBytes);

        var raw =
            $"Content-Type: multipart/mixed; boundary=\"{boundary}\"\r\n\r\n" +
            $"--{boundary}\r\n" +
            "Content-Type: text/plain\r\n\r\n" +
            "Body text\r\n" +
            $"--{boundary}\r\n" +
            "Content-Type: application/pdf\r\n" +
            "Content-Transfer-Encoding: base64\r\n" +
            "Content-Disposition: attachment; filename=\"scan.pdf\"\r\n\r\n" +
            $"{attachmentBase64}\r\n" +
            $"--{boundary}--\r\n";

        var entity = MimeParser.Parse(Encoding.ASCII.GetBytes(raw));

        Assert.True(entity.IsMultipart);
        Assert.Equal(2, entity.Children.Count);

        var message = OutboundMessageBuilder.Build("from@example.com", new[] { "to@example.com" }, Encoding.ASCII.GetBytes(raw));

        // Per RFC 2046, the CRLF immediately before a boundary delimiter belongs to the
        // delimiter, not the preceding part's content, so it's correctly stripped here.
        Assert.Equal("Body text", message.Body);
        Assert.Single(message.Attachments);
        Assert.Equal("scan.pdf", message.Attachments[0].FileName);
        Assert.Equal(attachmentBytes, message.Attachments[0].Content);
    }

    [Fact]
    public void DecodesRfc2047EncodedSubject()
    {
        var raw = "Subject: =?UTF-8?B?SGVsbG8gV29ybGQ=?=\r\nContent-Type: text/plain\r\n\r\nBody\r\n";
        var message = OutboundMessageBuilder.Build("from@example.com", new[] { "to@example.com" }, Encoding.ASCII.GetBytes(raw));

        Assert.Equal("Hello World", message.Subject);
    }
}
