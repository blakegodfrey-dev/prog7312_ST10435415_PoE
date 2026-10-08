namespace SmartX.Infrastructure.Persistence.Entities;
public sealed class InteractionRecord
{
    public Guid Id { get; set; }
    public Guid TargetId { get; set; }
    public string Kind { get; set; } = "";
    public string Query { get; set; } = "";
    public string Context { get; set; } = "";
    public DateTimeOffset AtUtc { get; set; }
}
public sealed class GatewayReceipt
{
    public Guid SensorId { get; set; }
    public DateTimeOffset LastSeenAtUtc { get; set; }
}
