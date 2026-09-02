using SmartX.Domain.Enums;
using SmartX.Infrastructure.Persistence.Entities;

namespace SmartX.Tests.Infrastructure;

public sealed class SensorAttachmentRecordTests
{
    [Fact]
    public void Constructor_ValidMetadata_StoresNormalisedValues()
    {
        var uploadedAt = new DateTimeOffset(
            2026,
            9,
            2,
            14,
            30,
            0,
            TimeSpan.FromHours(2));

        var attachment = new SensorAttachmentRecord(
            Guid.NewGuid(),
            Guid.NewGuid(),
            SensorAttachmentCategory.ConfigurationFile,
            @"C:\fake-client-path\ nutrient-config.json ",
            "5ce13489b4ab4c25a7f92dd4bd9c81b2.json",
            "application/json",
            2_048,
            "sensors/configurations/5ce13489b4ab4c25a7f92dd4bd9c81b2.json",
            uploadedAt);

        Assert.Equal("nutrient-config.json", attachment.OriginalFileName);
        Assert.Equal(2_048, attachment.SizeBytes);
        Assert.Equal(uploadedAt.ToUniversalTime(), attachment.UploadedAtUtc);
    }

    [Fact]
    public void Constructor_EmptyFile_ThrowsArgumentOutOfRangeException()
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(
            () => CreateAttachment(sizeBytes: 0));

        Assert.Equal("sizeBytes", exception.ParamName);
    }

    [Fact]
    public void Constructor_StoredNameContainingPath_ThrowsArgumentException()
    {
        var exception = Assert.Throws<ArgumentException>(
            () => CreateAttachment(
                storedFileName: @"unsafe\sensor-log.txt"));

        Assert.Equal("storedFileName", exception.ParamName);
    }

    [Fact]
    public void Constructor_RootedStoragePath_ThrowsArgumentException()
    {
        var exception = Assert.Throws<ArgumentException>(
            () => CreateAttachment(
                relativePath: @"C:\SmartX\uploads\sensor-log.txt"));

        Assert.Equal("relativePath", exception.ParamName);
    }

    [Fact]
    public void Constructor_TraversalStoragePath_ThrowsArgumentException()
    {
        var exception = Assert.Throws<ArgumentException>(
            () => CreateAttachment(
                relativePath: "../outside/sensor-log.txt"));

        Assert.Equal("relativePath", exception.ParamName);
    }

    private static SensorAttachmentRecord CreateAttachment(
        long sizeBytes = 1_024,
        string storedFileName = "20a4f27c44c844daa759c722b65fc72a.txt",
        string relativePath =
            "sensors/logs/20a4f27c44c844daa759c722b65fc72a.txt")
    {
        return new SensorAttachmentRecord(
            Guid.NewGuid(),
            Guid.NewGuid(),
            SensorAttachmentCategory.HardwareLog,
            "sensor-log.txt",
            storedFileName,
            "text/plain",
            sizeBytes,
            relativePath,
            DateTimeOffset.UtcNow);
    }
}
