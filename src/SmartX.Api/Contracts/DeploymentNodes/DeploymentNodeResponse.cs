using System.Text.Json.Serialization;
using SmartX.Domain.Enums;

namespace SmartX.Api.Contracts.DeploymentNodes;

public sealed record DeploymentNodeResponse(
    Guid Id,
    string Name,
    string Code,
    [property: JsonConverter(
        typeof(JsonStringEnumConverter<DeploymentNodeType>))]
    DeploymentNodeType NodeType,
    Guid? ParentId);
