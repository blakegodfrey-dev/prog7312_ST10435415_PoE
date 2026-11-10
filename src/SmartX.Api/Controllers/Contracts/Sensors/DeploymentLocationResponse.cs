using System.Text.Json.Serialization;
using SmartX.Domain.Enums;

namespace SmartX.Api.Contracts.Sensors;

public sealed record DeploymentLocationResponse(
    Guid Id,
    string Name,
    string Code,
    [property: JsonConverter(
        typeof(JsonStringEnumConverter<DeploymentNodeType>))]
    DeploymentNodeType NodeType,
    Guid? ParentId);
