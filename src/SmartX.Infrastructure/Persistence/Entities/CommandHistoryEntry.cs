namespace SmartX.Infrastructure.Persistence.Entities;

public sealed class CommandHistoryEntry
{
    public Guid Id { get; set; }
    public long Sequence { get; set; }
    public Guid SensorId { get; set; }
    public bool DesiredState { get; set; }
    public bool? PreviousState { get; set; }
    public bool Successful { get; set; }
    public bool IsUndo { get; set; }
    public Guid? UndoOfId { get; set; }
    public bool Undone { get; set; }
    public DateTimeOffset AtUtc { get; set; }
    public string Message { get; set; } = "";
    public string Context { get; set; } = "";
    public Guid? TelemetryId { get; set; }
}
