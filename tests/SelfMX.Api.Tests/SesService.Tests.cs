using Amazon.SimpleEmailV2;
using Amazon.SimpleEmailV2.Model;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using SelfMX.Api.Services;
using Xunit;

namespace SelfMX.Api.Tests;

public class SesServiceTests
{
    private readonly IAmazonSimpleEmailServiceV2 _mockSes;
    private readonly ILogger<SesService> _mockLogger;
    private readonly SesService _service;

    public SesServiceTests()
    {
        _mockSes = Substitute.For<IAmazonSimpleEmailServiceV2>();
        _mockLogger = Substitute.For<ILogger<SesService>>();
        _service = new SesService(_mockSes, _mockLogger);
    }

    [Fact]
    public async Task CreateDomainIdentityAsync_CreatesSesIdentityAndReturnsDkimRecords()
    {
        // Arrange
        var domainName = "example.com";
        var dkimTokens = new List<string> { "token1", "token2", "token3" };

        _mockSes.CreateEmailIdentityAsync(
            Arg.Any<CreateEmailIdentityRequest>(),
            Arg.Any<CancellationToken>())
            .Returns(new CreateEmailIdentityResponse
            {
                DkimAttributes = new DkimAttributes
                {
                    Tokens = dkimTokens
                }
            });

        _mockSes.GetEmailIdentityAsync(
            Arg.Any<GetEmailIdentityRequest>(),
            Arg.Any<CancellationToken>())
            .Returns(new GetEmailIdentityResponse
            {
                IdentityType = IdentityType.DOMAIN
            });

        // Act
        var (identityArn, dnsRecords) = await _service.CreateDomainIdentityAsync(domainName);

        // Assert - 3 DKIM + 1 SPF + 1 DMARC = 5 records
        dnsRecords.Should().HaveCount(5);

        // DKIM records
        dnsRecords[0].Type.Should().Be("CNAME");
        dnsRecords[0].Name.Should().Be("token1._domainkey.example.com");
        dnsRecords[0].Value.Should().Be("token1.dkim.amazonses.com");

        // SPF record
        dnsRecords[3].Type.Should().Be("TXT");
        dnsRecords[3].Name.Should().Be("example.com");
        dnsRecords[3].Value.Should().Be("v=spf1 include:amazonses.com ~all");

        // DMARC record
        dnsRecords[4].Type.Should().Be("TXT");
        dnsRecords[4].Name.Should().Be("_dmarc.example.com");
        dnsRecords[4].Value.Should().Be("v=DMARC1; p=none;");
    }

    [Fact]
    public async Task CheckDkimVerificationAsync_ReturnsTrueWhenDkimSucceeded()
    {
        // Arrange
        _mockSes.GetEmailIdentityAsync(
            Arg.Any<GetEmailIdentityRequest>(),
            Arg.Any<CancellationToken>())
            .Returns(new GetEmailIdentityResponse
            {
                DkimAttributes = new DkimAttributes
                {
                    Status = DkimStatus.SUCCESS
                }
            });

        // Act
        var result = await _service.CheckDkimVerificationAsync("example.com");

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task CheckDkimVerificationAsync_ReturnsFalseWhenDkimPending()
    {
        // Arrange
        _mockSes.GetEmailIdentityAsync(
            Arg.Any<GetEmailIdentityRequest>(),
            Arg.Any<CancellationToken>())
            .Returns(new GetEmailIdentityResponse
            {
                DkimAttributes = new DkimAttributes
                {
                    Status = DkimStatus.PENDING
                }
            });

        // Act
        var result = await _service.CheckDkimVerificationAsync("example.com");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task CheckDkimVerificationAsync_ReturnsFalseWhenNotFound()
    {
        // Arrange
        _mockSes.GetEmailIdentityAsync(
            Arg.Any<GetEmailIdentityRequest>(),
            Arg.Any<CancellationToken>())
            .Returns<GetEmailIdentityResponse>(_ => throw new NotFoundException("Not found"));

        // Act
        var result = await _service.CheckDkimVerificationAsync("example.com");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task DeleteDomainIdentityAsync_DeletesSesIdentity()
    {
        // Arrange
        _mockSes.DeleteEmailIdentityAsync(
            Arg.Any<DeleteEmailIdentityRequest>(),
            Arg.Any<CancellationToken>())
            .Returns(new DeleteEmailIdentityResponse());

        // Act
        await _service.DeleteDomainIdentityAsync("example.com");

        // Assert
        await _mockSes.Received(1).DeleteEmailIdentityAsync(
            Arg.Is<DeleteEmailIdentityRequest>(r => r.EmailIdentity == "example.com"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteDomainIdentityAsync_HandlesNotFoundGracefully()
    {
        // Arrange
        _mockSes.DeleteEmailIdentityAsync(
            Arg.Any<DeleteEmailIdentityRequest>(),
            Arg.Any<CancellationToken>())
            .Returns<DeleteEmailIdentityResponse>(_ => throw new NotFoundException("Not found"));

        // Act & Assert - should not throw
        await _service.DeleteDomainIdentityAsync("example.com");
    }

    [Fact]
    public async Task SendEmailAsync_SendsEmailViaSes()
    {
        // Arrange
        var messageId = "test-message-id";
        _mockSes.SendEmailAsync(
            Arg.Any<SendEmailRequest>(),
            Arg.Any<CancellationToken>())
            .Returns(new SendEmailResponse { MessageId = messageId });

        // Act
        var result = await _service.SendEmailAsync(
            "sender@example.com",
            ["recipient@example.com"],
            "Test Subject",
            "<p>HTML body</p>",
            "Text body");

        // Assert
        result.Should().Be(messageId);
        await _mockSes.Received(1).SendEmailAsync(
            Arg.Is<SendEmailRequest>(r =>
                r.FromEmailAddress == "sender@example.com" &&
                r.Destination.ToAddresses.Contains("recipient@example.com") &&
                r.Content.Simple.Subject.Data == "Test Subject"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SendEmailAsync_IncludesCcBccAndReplyTo()
    {
        // Arrange
        _mockSes.SendEmailAsync(
            Arg.Any<SendEmailRequest>(),
            Arg.Any<CancellationToken>())
            .Returns(new SendEmailResponse { MessageId = "id" });

        // Act
        await _service.SendEmailAsync(
            "sender@example.com",
            ["to@example.com"],
            "Subject",
            "<p>Body</p>",
            null,
            cc: ["cc@example.com"],
            bcc: ["bcc@example.com"],
            replyTo: ["reply@example.com"]);

        // Assert
        await _mockSes.Received(1).SendEmailAsync(
            Arg.Is<SendEmailRequest>(r =>
                r.Destination.CcAddresses.Contains("cc@example.com") &&
                r.Destination.BccAddresses.Contains("bcc@example.com") &&
                r.ReplyToAddresses.Contains("reply@example.com")),
            Arg.Any<CancellationToken>());
    }
}
