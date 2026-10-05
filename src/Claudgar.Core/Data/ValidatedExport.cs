using System.Text.Json.Nodes;

namespace Claudgar.Core.Data;

/// <summary>Validated export data, independent of its local filesystem location.</summary>
public sealed record ValidatedExport(JsonObject Client, IReadOnlyDictionary<string, JsonObject> Characters);
