using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SmartX.Api.Contracts.Attachments;
using SmartX.Api.Controllers;
using SmartX.Application.Attachments;
using SmartX.Domain.Entities;
using SmartX.Domain.Enums;
using SmartX.Infrastructure.Persistence;
using SmartX.Infrastructure.Persistence.Entities;

namespace SmartX.Tests.Api;

public sealed class SensorAttachmentsControllerTests
{
    [Fact]
    public async Task Upload_ValidFile_PersistsMetadataAndReturnsCreated()
    {
        await using var context = CreateContext();
        var sensor = await AddSensorAsync(context);
        var storage = new FakeAttachmentFileStorage();
        var controller = CreateController(context, storage);
        var request = CreateRequest(
            "nutrients.json",
            "application/json",
            "{\"targetPh\":6.0}",
            SensorAttachmentCategory.ConfigurationFile);

        var action = await controller.Upload(
            sensor.Id,
            request,
            CancellationToken.None);

        var created = Assert.IsType<CreatedAtActionResult>(action.Result);
        var response = Assert.IsType<SensorAttachmentResponse>(created.Value);
        Assert.Equal(sensor.Id, response.SensorId);
        Assert.Equal("nutrients.json", response.OriginalFileName);
        Assert.Equal("application/json", response.ContentType);
        Assert.Single(context.SensorAttachments);
        Assert.Equal(".json", storage.LastSavedExtension);
    }

    [Fact]
    public async Task Upload_MissingSensor_ReturnsNotFoundWithoutSavingFile()
    {
        await using var context = CreateContext();
        var storage = new FakeAttachmentFileStorage();
        var controller = CreateController(context, storage);
        var request = CreateRequest(
            "sensor.log",
            "text/plain",
            "log",
            SensorAttachmentCategory.HardwareLog);

        var action = await controller.Upload(
            Guid.NewGuid(),
            request,
            CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(action.Result);
        Assert.Equal(0, storage.SaveCallCount);
    }

    [Theory]
    [InlineData("firmware.exe", "application/octet-stream")]
    [InlineData("image.png", "application/json")]
    [InlineData("config.json", "image/png")]
    public async Task Upload_DisallowedFile_ReturnsBadRequest(
        string fileName,
        string contentType)
    {
        await using var context = CreateContext();
        var sensor = await AddSensorAsync(context);
        var storage = new FakeAttachmentFileStorage();
        var controller = CreateController(context, storage);
        var request = CreateRequest(
            fileName,
            contentType,
            "content",
            SensorAttachmentCategory.ConfigurationFile);

        var action = await controller.Upload(
            sensor.Id,
            request,
            CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(action.Result);
        Assert.Equal(0, storage.SaveCallCount);
    }

    [Fact]
    public async Task Upload_EmptyFile_ReturnsBadRequest()
    {
        await using var context = CreateContext();
        var sensor = await AddSensorAsync(context);
        var storage = new FakeAttachmentFileStorage();
        var controller = CreateController(context, storage);
        var request = CreateRequest(
            "empty.txt",
            "text/plain",
            string.Empty,
            SensorAttachmentCategory.HardwareLog);

        var action = await controller.Upload(
            sensor.Id,
            request,
            CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(action.Result);
    }

    [Fact]
    public async Task Upload_FileOverMaximumSize_ReturnsBadRequest()
    {
        await using var context = CreateContext();
        var sensor = await AddSensorAsync(context);
        var storage = new FakeAttachmentFileStorage();
        var controller = CreateController(context, storage);
        var file = new FormFile(
            Stream.Null,
            0,
            SensorAttachmentsController.MaximumFileSizeBytes + 1,
            "File",
            "large.log")
        {
            Headers = new HeaderDictionary(),
            ContentType = "text/plain"
        };
        var request = new UploadSensorAttachmentRequest
        {
            File = file,
            Category = SensorAttachmentCategory.HardwareLog
        };

        var action = await controller.Upload(
            sensor.Id,
            request,
            CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(action.Result);
        Assert.Equal(0, storage.SaveCallCount);
    }

    [Fact]
    public async Task List_ReturnsOnlyRequestedSensorsAttachments()
    {
        await using var context = CreateContext();
        var sensor = await AddSensorAsync(context);
        var otherSensor = await AddSensorAsync(context, 2);
        context.SensorAttachments.Add(
            CreateAttachment(sensor.Id, "first.log", 1));
        context.SensorAttachments.Add(
            CreateAttachment(otherSensor.Id, "other.log", 2));
        await context.SaveChangesAsync();
        var controller = CreateController(
            context,
            new FakeAttachmentFileStorage());

        var action = await controller.List(
            sensor.Id,
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(action.Result);
        var attachments = Assert.IsAssignableFrom<
            IReadOnlyList<SensorAttachmentResponse>>(ok.Value);
        var attachment = Assert.Single(attachments);
        Assert.Equal("first.log", attachment.OriginalFileName);
    }

    [Fact]
    public async Task GetMetadata_ExistingAttachment_ReturnsMetadata()
    {
        await using var context = CreateContext();
        var sensor = await AddSensorAsync(context);
        var attachment = CreateAttachment(sensor.Id, "sensor.log", 1);
        context.SensorAttachments.Add(attachment);
        await context.SaveChangesAsync();
        var controller = CreateController(
            context,
            new FakeAttachmentFileStorage());

        var action = await controller.GetMetadata(
            sensor.Id,
            attachment.Id,
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(action.Result);
        var response = Assert.IsType<SensorAttachmentResponse>(ok.Value);
        Assert.Equal(attachment.Id, response.Id);
    }

    [Fact]
    public async Task Download_ExistingAttachment_ReturnsFile()
    {
        await using var context = CreateContext();
        var sensor = await AddSensorAsync(context);
        var attachment = CreateAttachment(sensor.Id, "sensor.log", 1);
        context.SensorAttachments.Add(attachment);
        await context.SaveChangesAsync();
        var storage = new FakeAttachmentFileStorage
        {
            DownloadContent = "downloaded log"
        };
        var controller = CreateController(context, storage);

        var action = await controller.Download(
            sensor.Id,
            attachment.Id,
            CancellationToken.None);

        var result = Assert.IsType<FileStreamResult>(action);
        Assert.Equal("text/plain", result.ContentType);
        Assert.Equal("sensor.log", result.FileDownloadName);
    }

    [Fact]
    public async Task Download_MissingStoredFile_ReturnsNotFound()
    {
        await using var context = CreateContext();
        var sensor = await AddSensorAsync(context);
        var attachment = CreateAttachment(sensor.Id, "sensor.log", 1);
        context.SensorAttachments.Add(attachment);
        await context.SaveChangesAsync();
        var storage = new FakeAttachmentFileStorage
        {
            ThrowFileNotFoundOnOpen = true
        };
        var controller = CreateController(context, storage);

        var action = await controller.Download(
            sensor.Id,
            attachment.Id,
            CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(action);
    }

    [Fact]
    public async Task Delete_ExistingAttachment_RemovesMetadataAndFile()
    {
        await using var context = CreateContext();
        var sensor = await AddSensorAsync(context);
        var attachment = CreateAttachment(sensor.Id, "sensor.log", 1);
        context.SensorAttachments.Add(attachment);
        await context.SaveChangesAsync();
        var storage = new FakeAttachmentFileStorage();
        var controller = CreateController(context, storage);

        var action = await controller.Delete(
            sensor.Id,
            attachment.Id,
            CancellationToken.None);

        Assert.IsType<NoContentResult>(action);
        Assert.Empty(context.SensorAttachments);
        Assert.Equal(attachment.RelativePath, storage.LastDeletedPath);
    }

    [Fact]
    public async Task Delete_MissingAttachment_ReturnsNotFound()
    {
        await using var context = CreateContext();
        var controller = CreateController(
            context,
            new FakeAttachmentFileStorage());

        var action = await controller.Delete(
            Guid.NewGuid(),
            Guid.NewGuid(),
            CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(action);
    }

    private static SensorAttachmentsController CreateController(
        SmartXDbContext context,
        IAttachmentFileStorage storage)
    {
        return new SensorAttachmentsController(
            context,
            storage,
            NullLogger<SensorAttachmentsController>.Instance);
    }

    private static SmartXDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<SmartXDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new SmartXDbContext(options);
    }

    private static async Task<Sensor> AddSensorAsync(
        SmartXDbContext context,
        int number = 1)
    {
        var node = new DeploymentNode(
            Guid.NewGuid(),
            $"Attachment Node {number}",
            $"NODE-ATTACHMENT-{number}",
            DeploymentNodeType.Node);
        var sensor = new Sensor(
            Guid.NewGuid(),
            $"A4:CF:12:8B:80:{number:X2}",
            $"Attachment Sensor {number}",
            SensorCategory.Environmental,
            "Temperature",
            TelemetryValueKind.Float,
            "°C",
            node.Id,
            18,
            28);
        context.AddRange(node, sensor);
        await context.SaveChangesAsync();

        return sensor;
    }

    private static UploadSensorAttachmentRequest CreateRequest(
        string fileName,
        string contentType,
        string content,
        SensorAttachmentCategory category)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(content);
        var stream = new MemoryStream(bytes);
        var file = new FormFile(
            stream,
            0,
            bytes.Length,
            "File",
            fileName)
        {
            Headers = new HeaderDictionary(),
            ContentType = contentType
        };

        return new UploadSensorAttachmentRequest
        {
            File = file,
            Category = category
        };
    }

    private static SensorAttachmentRecord CreateAttachment(
        Guid sensorId,
        string originalFileName,
        int number)
    {
        var storedFileName = $"{number:D32}.log";

        return new SensorAttachmentRecord(
            Guid.NewGuid(),
            sensorId,
            SensorAttachmentCategory.HardwareLog,
            originalFileName,
            storedFileName,
            "text/plain",
            128,
            $"sensors/{number:D2}/{storedFileName}",
            DateTimeOffset.UtcNow.AddMinutes(-number));
    }

    private sealed class FakeAttachmentFileStorage
        : IAttachmentFileStorage
    {
        public int SaveCallCount { get; private set; }

        public string? LastSavedExtension { get; private set; }

        public string? LastDeletedPath { get; private set; }

        public string DownloadContent { get; init; } = "content";

        public bool ThrowFileNotFoundOnOpen { get; init; }

        public Task<StoredAttachmentFile> SaveAsync(
            Stream content,
            string fileExtension,
            CancellationToken cancellationToken = default)
        {
            _ = content;
            cancellationToken.ThrowIfCancellationRequested();
            SaveCallCount++;
            LastSavedExtension = fileExtension;
            var storedFileName = $"{Guid.NewGuid():N}{fileExtension}";

            return Task.FromResult(new StoredAttachmentFile(
                storedFileName,
                $"sensors/aa/{storedFileName}"));
        }

        public Task<Stream> OpenReadAsync(
            string relativePath,
            CancellationToken cancellationToken = default)
        {
            _ = relativePath;
            cancellationToken.ThrowIfCancellationRequested();

            if (ThrowFileNotFoundOnOpen)
            {
                throw new FileNotFoundException();
            }

            Stream stream = new MemoryStream(
                System.Text.Encoding.UTF8.GetBytes(DownloadContent));
            return Task.FromResult(stream);
        }

        public Task<bool> DeleteAsync(
            string relativePath,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LastDeletedPath = relativePath;
            return Task.FromResult(true);
        }
    }
}
