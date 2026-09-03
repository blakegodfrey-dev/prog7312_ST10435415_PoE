using SmartX.Infrastructure.Attachments;

namespace SmartX.Tests.Infrastructure;

public sealed class LocalAttachmentFileStorageTests
{
    [Fact]
    public async Task SaveAsync_StoresContentWithGeneratedSafeName()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var storage = new LocalAttachmentFileStorage(
            temporaryDirectory.Path);
        await using var content = CreateContent("sensor configuration");

        var stored = await storage.SaveAsync(content, ".JSON");

        Assert.EndsWith(".json", stored.StoredFileName);
        Assert.DoesNotContain("..", stored.RelativePath);
        Assert.DoesNotContain('\\', stored.RelativePath);
        Assert.True(File.Exists(GetFullPath(
            temporaryDirectory.Path,
            stored.RelativePath)));
    }

    [Fact]
    public async Task SaveAsync_PreservesFileContent()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var storage = new LocalAttachmentFileStorage(
            temporaryDirectory.Path);
        await using var content = CreateContent("hydroponic log entry");
        var stored = await storage.SaveAsync(content, ".txt");

        await using var opened = await storage.OpenReadAsync(
            stored.RelativePath);
        using var reader = new StreamReader(opened);

        Assert.Equal("hydroponic log entry", await reader.ReadToEndAsync());
    }

    [Fact]
    public async Task SaveAsync_SameExtension_GeneratesUniqueNames()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var storage = new LocalAttachmentFileStorage(
            temporaryDirectory.Path);
        await using var firstContent = CreateContent("first");
        await using var secondContent = CreateContent("second");

        var first = await storage.SaveAsync(firstContent, ".csv");
        var second = await storage.SaveAsync(secondContent, ".csv");

        Assert.NotEqual(first.StoredFileName, second.StoredFileName);
        Assert.NotEqual(first.RelativePath, second.RelativePath);
    }

    [Theory]
    [InlineData("")]
    [InlineData("json")]
    [InlineData("../json")]
    [InlineData(".tar.gz")]
    [InlineData(".extensiontoolong")]
    public async Task SaveAsync_UnsafeExtension_ThrowsArgumentException(
        string extension)
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var storage = new LocalAttachmentFileStorage(
            temporaryDirectory.Path);
        await using var content = CreateContent("content");

        await Assert.ThrowsAsync<ArgumentException>(
            () => storage.SaveAsync(content, extension));
    }

    [Theory]
    [InlineData("../outside.txt")]
    [InlineData("sensors/../../outside.txt")]
    [InlineData("/rooted/outside.txt")]
    [InlineData("\\rooted\\outside.txt")]
    public async Task OpenReadAsync_UnsafePath_ThrowsArgumentException(
        string relativePath)
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var storage = new LocalAttachmentFileStorage(
            temporaryDirectory.Path);

        await Assert.ThrowsAsync<ArgumentException>(
            () => storage.OpenReadAsync(relativePath));
    }

    [Fact]
    public async Task OpenReadAsync_MissingFile_ThrowsFileNotFoundException()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var storage = new LocalAttachmentFileStorage(
            temporaryDirectory.Path);

        await Assert.ThrowsAsync<FileNotFoundException>(
            () => storage.OpenReadAsync("sensors/aa/missing.txt"));
    }

    [Fact]
    public async Task DeleteAsync_StoredFile_RemovesFile()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var storage = new LocalAttachmentFileStorage(
            temporaryDirectory.Path);
        await using var content = CreateContent("delete me");
        var stored = await storage.SaveAsync(content, ".txt");

        var wasDeleted = await storage.DeleteAsync(stored.RelativePath);

        Assert.True(wasDeleted);
        Assert.False(File.Exists(GetFullPath(
            temporaryDirectory.Path,
            stored.RelativePath)));
        Assert.False(await storage.DeleteAsync(stored.RelativePath));
    }

    private static MemoryStream CreateContent(string value)
    {
        return new MemoryStream(System.Text.Encoding.UTF8.GetBytes(value));
    }

    private static string GetFullPath(
        string rootPath,
        string relativePath)
    {
        return Path.Combine(
            rootPath,
            relativePath.Replace('/', Path.DirectorySeparatorChar));
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                $"smartx-attachments-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}
