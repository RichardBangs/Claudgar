using System.Text.Json.Nodes;
using Claudgar.Core.Data;

namespace Claudgar.Core.Exports;

/// <summary>Checks version, client identity, section coverage, and the shared collector field types.</summary>
public sealed class SnapshotValidator
{
    private static readonly HashSet<string> ListFields = new(
        ["active", "completed", "completedDetails", "objectives", "trees", "nodes", "entries", "visibleEdges", "groupIds", "currencies", "groups", "bags", "items", "warnings"],
        StringComparer.Ordinal);
    private static readonly HashSet<string> StringFields = new(
        ["guid", "name", "realm", "className", "classToken", "raceName", "raceToken", "faction", "locale", "zone",
         "subZone", "title", "text", "description", "type", "mode", "configName", "link", "subclassName", "equipLocation", "slotName"],
        StringComparer.Ordinal);
    private static readonly HashSet<string> BooleanFields = new(
        ["isComplete", "isFailed", "isHeader", "isHidden", "isOnMap", "isTask", "isBounty", "isRepeatable", "isDaily", "isWeekly",
         "isAvailable", "isVisible", "isActive", "hasStagedChanges", "locked", "bound", "isCraftingReagent", "empty", "complete", "finished"],
        StringComparer.Ordinal);
    private static readonly HashSet<string> NumberFields = new(
        ["level", "classId", "raceId", "mapId", "money", "xp", "maxXp", "restedXp", "questId", "suggestedGroup", "frequency",
         "numObjectives", "numFulfilled", "numRequired", "objectiveType", "index", "objectiveIndex", "required", "fulfilled", "activeConfigId", "activeSpecGroup",
         "treeId", "nodeId", "activeEntryId", "activeRank", "currentRank", "ranksPurchased", "maxRanks", "posX", "posY", "entryId",
         "definitionId", "spellId", "rank", "targetNodeId", "edgeType", "visualStyle", "currencyId", "quantity", "maxQuantity", "spent", "groupId", "iconFileId", "slotIconFileId", "bagId",
         "slotCount", "freeSlots", "bagFamily", "slot", "itemId", "count", "quality", "itemLevel", "requiredLevel", "subclassId",
         "maxStackCount", "sellPrice", "durability", "maxDurability", "requiredMoney", "logIndex", "objectiveId", "zoneId", "maxLevel"],
        StringComparer.Ordinal);

    public ValidatedExport Validate(JsonObject document)
    {
        if (!TryInteger(document["schemaVersion"], out var version) || version != ExportContract.SchemaVersion)
            throw Invalid("Unsupported schemaVersion. Update Claudgar and its addon together.");
        var client = RequireObject(document["client"], "client");
        if (ReadString(client["flavor"]) != ExportContract.ClientFlavor)
            throw Invalid("This export is not from the WoW Forever addon.");
        CheckOptionalString(client, "version");
        CheckOptionalString(client, "locale");
        if (client["build"] is { } build && ReadString(build) is null && !TryInteger(build, out _))
            throw Invalid("client.build must be a string or integer.");
        if (client["interface"] is { } clientInterface && (!TryInteger(clientInterface, out var number) || number < 1))
            throw Invalid("client.interface must be a positive integer.");
        var safeClient = new JsonObject { ["flavor"] = ExportContract.ClientFlavor };
        foreach (var field in new[] { "version", "build", "interface", "locale" })
            if (client.TryGetPropertyValue(field, out var value)) safeClient[field] = value?.DeepClone();

        var sourceCharacters = RequireObject(document["characters"], "characters");
        var characters = new Dictionary<string, JsonObject>(StringComparer.Ordinal);
        foreach (var (key, rawCharacter) in sourceCharacters)
        {
            if (string.IsNullOrWhiteSpace(key) || key.Length > 512)
                throw Invalid("Character keys must be nonempty strings of at most 512 characters.");
            var character = RequireObject(rawCharacter, "character record");
            if (ReadString(character["characterKey"]) != key)
                throw Invalid("A characterKey does not match its table key.");
            var sections = RequireObject(character["sections"], "sections");
            var safeSections = new JsonObject();
            foreach (var sectionName in ExportContract.SectionNames)
            {
                if (!sections.TryGetPropertyValue(sectionName, out var rawSection))
                {
                    safeSections[sectionName] = new JsonObject
                    {
                        ["status"] = "unavailable", ["observedAt"] = null, ["data"] = null,
                        ["warnings"] = new JsonArray("The export does not include this section.")
                    };
                    continue;
                }
                var section = RequireObject(rawSection, sectionName);
                var status = ReadString(section["status"]);
                if (status is null || !ExportContract.CoverageStatuses.Contains(status))
                    throw Invalid($"Invalid coverage status in {sectionName}.");
                if (!TryInteger(section["observedAt"], out var observedAt) || observedAt < 0 || observedAt > 253402300799)
                    throw Invalid($"Invalid Unix observation time in {sectionName}.");
                JsonObject? data = null;
                if (section["data"] is { } rawData) data = RequireObject(rawData, sectionName + ".data");
                if ((status is "complete" or "partial") && data is null)
                    throw Invalid($"{sectionName} has data coverage but no data object.");
                var safeData = data?.DeepClone().AsObject();
                if (safeData is not null)
                {
                    ValidateAndNormalize(safeData, sectionName + ".data");
                    ValidateSection(sectionName, safeData, status);
                }
                var warnings = section["warnings"]?.DeepClone();
                if (warnings is JsonObject { Count: 0 }) warnings = new JsonArray();
                if (warnings is not null && (warnings is not JsonArray warningList || warningList.Any(value => ReadString(value) is null)))
                    throw Invalid($"{sectionName}.warnings must contain strings.");
                var error = section["error"];
                if (error is not null && ReadString(error) is null)
                    throw Invalid($"{sectionName}.error must be a string.");
                safeSections[sectionName] = new JsonObject
                {
                    ["status"] = status, ["observedAt"] = observedAt, ["data"] = safeData,
                    ["warnings"] = warnings ?? new JsonArray()
                };
                if (error is not null) safeSections[sectionName]!["error"] = error.DeepClone();
            }
            var identity = safeSections["character"]?["data"] as JsonObject;
            if (identity is null || string.IsNullOrWhiteSpace(ReadString(identity["name"])) ||
                string.IsNullOrWhiteSpace(ReadString(identity["realm"])))
                throw Invalid("A character must include its name and realm.");
            characters.Add(key, new JsonObject { ["characterKey"] = key, ["sections"] = safeSections });
        }
        return new ValidatedExport(safeClient, characters);
    }

    private static void ValidateSection(string section, JsonObject data, string status)
    {
        switch (section)
        {
            case "character":
                if (data["level"] is { } level && (!TryInteger(level, out var value) || value < 1 || value > 1000))
                    throw Invalid("character.level must be a positive integer.");
                if (status == "complete")
                {
                    foreach (var field in new[] { "guid", "name", "realm", "className", "classToken", "raceName", "raceToken", "faction", "locale", "zone" })
                        if (ReadString(data[field]) is null) throw Invalid($"Complete character data lacks {field}.");
                    foreach (var field in new[] { "level", "classId", "raceId" })
                        if (!TryInteger(data[field], out var id) || id < 1) throw Invalid($"Complete character data lacks a positive {field}.");
                }
                break;
            case "quests":
                RequireListIfPresent(data, "active");
                RequireListIfPresent(data, "completed");
                if (status == "complete" && (data["active"] is not JsonArray || data["completed"] is not JsonArray))
                    throw Invalid("Complete quest data must include active and completed lists.");
                if (data["completed"] is JsonArray completed && completed.Any(value => !TryInteger(value, out var id) || id < 1))
                    throw Invalid("Completed quests must contain positive quest IDs.");
                if (data["active"] is JsonArray active && active.Any(value => value is not JsonObject))
                    throw Invalid("Active quests must contain objects.");
                if (data["completedDetails"] is JsonArray details)
                {
                    var seen = new HashSet<long>();
                    var completedIds = data["completed"] is JsonArray ids
                        ? ids.Select(value => { TryInteger(value, out var id); return id; }).ToHashSet() : null;
                    foreach (var detail in details.OfType<JsonObject>())
                        if (!TryInteger(detail["questId"], out var id) || id < 1 || !seen.Add(id) ||
                            string.IsNullOrWhiteSpace(ReadString(detail["title"])) || completedIds is not null && !completedIds.Contains(id))
                            throw Invalid("Completed quest details must have unique completed IDs and saved titles.");
                }
                break;
            case "talents": ValidateMainList(data, "trees", status); break;
            case "inventory": ValidateMainList(data, "bags", status); break;
            case "equipment": ValidateMainList(data, "items", status); break;
        }
    }

    private static void ValidateAndNormalize(JsonNode node, string path)
    {
        if (node is JsonArray list)
        {
            foreach (var item in list) if (item is not null) ValidateAndNormalize(item, path + "[]");
            return;
        }
        if (node is not JsonObject table) return;
        foreach (var (key, value) in table.ToArray())
        {
            // Paths, accounts, credentials, and arbitrary extension fields are never part of the public prototype.
            if (!ListFields.Contains(key) && !StringFields.Contains(key) && !BooleanFields.Contains(key) &&
                !NumberFields.Contains(key) && key != "stats")
                throw Invalid($"Unknown data field {path}.{key}.");
            if (value is null) continue;
            if (ListFields.Contains(key))
            {
                if (value is JsonObject { Count: 0 }) table[key] = new JsonArray();
                else if (value is not JsonArray) throw Invalid($"{path}.{key} must be a list.");
                var entries = table[key]!.AsArray();
                if (key == "groupIds" && entries.Any(entry => !TryInteger(entry, out var id) || id < 1))
                    throw Invalid($"{path}.{key} must contain positive group IDs.");
                if (key != "completed" && key != "warnings" && key != "groupIds" && entries.Any(entry => entry is not JsonObject))
                    throw Invalid($"{path}.{key} must contain objects.");
            }
            else if (StringFields.Contains(key) && ReadString(value) is null)
                throw Invalid($"{path}.{key} must be a string.");
            else if (BooleanFields.Contains(key) && (value is not JsonValue boolean || !boolean.TryGetValue<bool>(out _)))
                throw Invalid($"{path}.{key} must be a boolean.");
            else if (NumberFields.Contains(key))
            {
                if (!IsFiniteNumber(value)) throw Invalid($"{path}.{key} must be a finite number.");
            }
            if (key == "stats")
            {
                var stats = RequireObject(value, path + ".stats");
                if (stats.Any(pair => !IsFiniteNumber(pair.Value))) throw Invalid("Item stats must contain numbers.");
            }
            else if (table[key] is { } child) ValidateAndNormalize(child, path + "." + key);
        }
    }

    private static void RequireListIfPresent(JsonObject data, string field)
    {
        if (data[field] is { } value && value is not JsonArray) throw Invalid($"{field} must be a list.");
    }
    private static void ValidateMainList(JsonObject data, string field, string status)
    {
        RequireListIfPresent(data, field);
        if (status == "complete" && data[field] is not JsonArray)
            throw Invalid($"Complete section data must include {field}.");
    }
    private static void CheckOptionalString(JsonObject node, string field)
    {
        if (node[field] is { } value && ReadString(value) is null) throw Invalid($"client.{field} must be a string.");
    }
    private static JsonObject RequireObject(JsonNode? node, string path) => node as JsonObject ?? throw Invalid($"{path} must be a keyed table.");
    internal static string? ReadString(JsonNode? node) => node is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;
    internal static bool TryInteger(JsonNode? node, out long number)
    {
        number = 0;
        if (node is not JsonValue value) return false;
        if (value.TryGetValue<long>(out number)) return true;
        if (value.TryGetValue<int>(out var integer)) { number = integer; return true; }
        if (!value.TryGetValue<double>(out var real) || !double.IsFinite(real) || real != Math.Truncate(real) || real < long.MinValue || real >= long.MaxValue) return false;
        number = (long)real;
        return true;
    }
    private static bool IsFiniteNumber(JsonNode? node) => TryInteger(node, out _) ||
        node is JsonValue value && value.TryGetValue<double>(out var number) && double.IsFinite(number);
    private static ExportFormatException Invalid(string message) => new(message);
}
