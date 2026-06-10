using FluentAssertions;
using Tun.Server.Domain.Services;

namespace Tun.UnitTests.Domain.Services;

public class TunnelValidationServiceTests
{
    private readonly TunnelValidationService _validator = new();

    [Fact]
    public void Validate_WithValidTunnelIdAndUrl_ShouldReturnSuccess()
    {
        var result = _validator.Validate("mytunnel", "http://localhost:5000");

        result.IsSuccess.Should().BeTrue();
    }

    [Theory]
    [InlineData("www")]
    [InlineData("api")]
    [InlineData("admin")]
    [InlineData("dashboard")]
    public void Validate_WithReservedSubdomain_ShouldReturnFailure(string reservedId)
    {
        var result = _validator.Validate(reservedId, "http://localhost:5000");

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("保留子域名");
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void Validate_WithEmptyTunnelId_ShouldReturnFailure(string tunnelId)
    {
        var result = _validator.Validate(tunnelId, "http://localhost:5000");

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("不能为空");
    }

    [Theory]
    [InlineData("not-a-url")]
    [InlineData("ftp://localhost")]
    public void Validate_WithInvalidUrl_ShouldReturnFailure(string url)
    {
        var result = _validator.Validate("mytunnel", url);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("格式无效");
    }

    [Fact]
    public void Validate_WithEmptyUrl_ShouldReturnFailure()
    {
        var result = _validator.Validate("mytunnel", "");

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("不能为空");
    }
}
