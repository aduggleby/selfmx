namespace SelfMX.Api.Contracts.Requests;

public record SendEmailRequest(
    string From,
    string[] To,
    string Subject,
    string? Html = null,
    string? Text = null,
    string[]? Cc = null,
    string[]? Bcc = null,
    string[]? ReplyTo = null,
    Dictionary<string, string>? Headers = null
);

public record CreateDomainRequest(string Name);
