namespace AnyAPI.Core.Tests.Validation;

using AnyAPI.Core.Validation;
using Xunit;

public class UrlValidatorTests
{
    [Theory]
    [InlineData("https://api.github.com")]
    [InlineData("https://api.example.com/v1/users")]
    [InlineData("http://public-api.com")]
    public void ValidateExternalUrl_WithValidPublicUrl_DoesNotThrow(string url)
    {
        // Act & Assert - should not throw
        UrlValidator.ValidateExternalUrl(url);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ValidateExternalUrl_WithEmptyUrl_ThrowsArgumentException(string? url)
    {
        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() => UrlValidator.ValidateExternalUrl(url!));
        Assert.Contains("empty", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("not-a-url")]
    [InlineData("just some text")]
    [InlineData("ftp://files.example.com")]
    [InlineData("file:///etc/passwd")]
    public void ValidateExternalUrl_WithInvalidFormat_ThrowsArgumentException(string url)
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => UrlValidator.ValidateExternalUrl(url));
    }

    // SSRF Protection - Blocked Hosts
    [Theory]
    [InlineData("http://localhost/admin")]
    [InlineData("http://localhost:8080/api")]
    [InlineData("http://127.0.0.1/secret")]
    [InlineData("http://127.0.0.1:3000")]
    [InlineData("http://0.0.0.0/")]
    public void ValidateExternalUrl_WithLocalhostVariants_ThrowsArgumentException(string url)
    {
        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() => UrlValidator.ValidateExternalUrl(url));
        Assert.Contains("not allowed", ex.Message);
    }

    // SSRF Protection - Cloud Metadata Endpoints
    [Theory]
    [InlineData("http://169.254.169.254/latest/meta-data/")]
    [InlineData("http://169.254.169.254/metadata/instance")]
    [InlineData("http://metadata.google.internal/computeMetadata/v1/")]
    public void ValidateExternalUrl_WithCloudMetadataEndpoints_ThrowsArgumentException(string url)
    {
        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() => UrlValidator.ValidateExternalUrl(url));
        Assert.Contains("not allowed", ex.Message);
    }

    // SSRF Protection - Private IP Ranges (RFC 1918)
    [Theory]
    [InlineData("http://10.0.0.1/api")]
    [InlineData("http://10.255.255.255/")]
    [InlineData("http://172.16.0.1/admin")]
    [InlineData("http://172.31.255.255/")]
    [InlineData("http://192.168.0.1/")]
    [InlineData("http://192.168.255.255/")]
    public void ValidateExternalUrl_WithPrivateIpRanges_ThrowsArgumentException(string url)
    {
        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() => UrlValidator.ValidateExternalUrl(url));
        Assert.Contains("private", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    // SSRF Protection - Link-local addresses
    [Theory]
    [InlineData("http://169.254.0.1/")]
    [InlineData("http://169.254.255.254/")]
    public void ValidateExternalUrl_WithLinkLocalIp_ThrowsArgumentException(string url)
    {
        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() => UrlValidator.ValidateExternalUrl(url));
        Assert.Contains("private", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    // SSRF Protection - Internal hostnames
    [Theory]
    [InlineData("http://myservice.local/api")]
    [InlineData("http://database.internal/")]
    [InlineData("http://app.localhost/")]
    public void ValidateExternalUrl_WithInternalHostnames_ThrowsArgumentException(string url)
    {
        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() => UrlValidator.ValidateExternalUrl(url));
        Assert.Contains("not allowed", ex.Message);
    }

    // Edge cases - Valid IP ranges that should be allowed
    [Theory]
    [InlineData("http://172.15.0.1/")] // Just outside 172.16-31 range
    [InlineData("http://172.32.0.1/")] // Just outside 172.16-31 range
    [InlineData("http://8.8.8.8/")] // Google DNS - public
    [InlineData("http://1.1.1.1/")] // Cloudflare DNS - public
    public void ValidateExternalUrl_WithPublicIps_DoesNotThrow(string url)
    {
        // Act & Assert - should not throw
        UrlValidator.ValidateExternalUrl(url);
    }

    [Fact]
    public void ValidateExternalUrl_WithLoopbackVariations_ThrowsArgumentException()
    {
        // 127.x.x.x is all loopback
        Assert.Throws<ArgumentException>(() => UrlValidator.ValidateExternalUrl("http://127.0.0.2/"));
        Assert.Throws<ArgumentException>(() => UrlValidator.ValidateExternalUrl("http://127.255.255.255/"));
    }

    [Theory]
    [InlineData("ftp://files.example.com")]
    [InlineData("file:///etc/passwd")]
    [InlineData("gopher://example.com")]
    public void ValidateExternalUrl_WithNonHttpSchemes_ThrowsArgumentException(string url)
    {
        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() => UrlValidator.ValidateExternalUrl(url));
        Assert.Contains("HTTP", ex.Message);
    }
}
