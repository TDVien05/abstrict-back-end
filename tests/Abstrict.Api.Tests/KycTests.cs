using Abstrict.Api.Services.Implementations;
using Microsoft.AspNetCore.DataProtection;

namespace Abstrict.Api.Tests;

public sealed class KycTests
{
    [Fact]
    public void KycImage_DetectsJpegAndPngByContent()
    {
        Assert.Equal(("image/jpeg", ".jpg"), KycImage.Detect(new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 }));
        Assert.Equal(("image/png", ".png"), KycImage.Detect(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }));
    }

    [Theory]
    [InlineData(new byte[] { })]
    [InlineData(new byte[] { 0x47, 0x49, 0x46, 0x38, 0x39, 0x61 })] // GIF
    [InlineData(new byte[] { 0x25, 0x50, 0x44, 0x46 })] // PDF
    [InlineData(new byte[] { 0x3C, 0x73, 0x76, 0x67 })] // SVG/HTML text
    public void KycImage_RejectsOtherContent(byte[] header) => Assert.Null(KycImage.Detect(header));

    [Fact]
    public void CitizenIdProtector_RoundTripsAndHashesDeterministically()
    {
        var provider = new EphemeralDataProtectionProvider();
        var protector = new CitizenIdProtector(provider, "test-hash-key-test-hash-key-test-hash-key");

        var first = protector.Protect("079195001234");
        var second = protector.Protect("079195001234");

        Assert.Equal("1234", first.Last4);
        Assert.Equal(first.Hash, second.Hash);
        Assert.DoesNotContain("079195001234", first.Protected);
        Assert.Equal("079195001234", protector.Unprotect(first.Protected));
        Assert.NotEqual(first.Hash, protector.Protect("079195001235").Hash);
    }

    [Fact]
    public void CitizenIdProtector_RefusesToRunWithoutHashKey()
    {
        var protector = new CitizenIdProtector(new EphemeralDataProtectionProvider(), null);
        Assert.Throws<KycFlowException>(() => protector.Protect("079195001234"));
    }
}
