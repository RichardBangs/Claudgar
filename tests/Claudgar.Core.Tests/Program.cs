using System.Text.Json.Nodes;
using Claudgar.Core.Data;
using Claudgar.Core.Exports;

// Package-free regression suite. Intentionally not run before the user's first test.
var tests = new (string Name, Action Run)[]
{
    ("Literal parsing and UTF-8 escapes", ParserLiterals),
    ("Executable Lua and resource limits are rejected", ParserRejectsCode),
    ("All sections share a validated data contract", ValidateFixture),
    ("Saved quest names and talent layout remain compatible with older exports", PresentationMetadata),
    ("Equipment artwork and character stats are optional validated metadata", EquipmentMetadata),
    ("Wrong clients, versions, coverage and field types are rejected", InvalidSchemas),
    ("Characters remain isolated across accounts and installations", CharacterIsolation),
    ("Every query rereads files and corrupt or missing files retain labeled cache", RefreshAndCache),
    ("A bad account does not hide another account", BadAccountIsolation),
    ("Export checks compare metadata without opening saved data", ExportChangeTrackerTests.MetadataChanges),
    ("Export checks discover added and deleted files and installations", ExportChangeTrackerTests.DiscoveryChanges),
    ("Failed refreshes and changes during refresh are retried", ExportChangeTrackerTests.UnacceptedReadsAreRetried),
    ("Incomplete export discovery remains eligible for retry", ExportChangeTrackerTests.IncompleteDiscoveryIsRetried),
    ("Setup health detects missing and changed files without writing", SetupHealthRegressionTests.SetupHealthChecksAreReadOnlyAndDetectIssues),
    ("Managed TOML preserves unrelated and multiline configuration", SetupRegressionTests.ManagedTomlPreservesUnrelatedConfiguration),
    ("Conflicting user MCP configuration is preserved", SetupRegressionTests.ConflictingTomlIsPreserved),
    ("Owned installs are repeatable and preserve user changes", SetupRegressionTests.OwnedInstallIsRepeatableAndPreservesChanges),
    ("Packaged resources cannot escape their install folder", SetupRegressionTests.OwnedInstallRejectsEscapingPaths),
    ("Interrupted owned installs can resume", SetupRegressionTests.InterruptedOwnedInstallCanResume),
    ("Malformed settings and unrelated settings are preserved", SetupRegressionTests.MalformedSettingsArePreserved),
    ("Forever beta validation discriminates the shared beta slot", SetupRegressionTests.ForeverValidationDiscriminatesSharedBetaSlot),
    ("Game discovery resolves root and beta paths and suggests the existing folder", SetupRegressionTests.GameDiscoveryFindsRootOrBetaDirectory),
};
var failed = 0;
foreach (var test in tests)
{
    try { test.Run(); Console.WriteLine($"PASS {test.Name}"); }
    catch (Exception exception) { failed++; Console.Error.WriteLine($"FAIL {test.Name}: {exception.Message}"); }
}
return failed == 0 ? 0 : 1;

static string Fixture() => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "fixtures", "Claudgar.lua"));
static JsonObject ParsedFixture() => new LuaSavedVariablesParser().Parse(Fixture());
static void Assert(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}
static void Reject(Action action)
{
    try { action(); }
    catch (ExportFormatException) { return; }
    throw new InvalidOperationException("Invalid export was accepted.");
}
static void ParserLiterals()
{
    var parsed = new LuaSavedVariablesParser().Parse("""
        -- line comment
        --[=[ block comment ]=]
        ClaudgarDB = { message = "caf\195\169\nquote\"", exponent = 1.25e2,
                       long = [=[
        literal text
        ]=], list = { [1] = true, [2] = false }, empty = {}, hex = 0x10 }; -- end
        """);
    Assert(parsed["message"]!.GetValue<string>() == "café\nquote\"", "UTF-8 byte escapes were not decoded.");
    Assert(parsed["exponent"]!.GetValue<double>() == 125, "Exponent number was not parsed.");
    Assert(parsed["list"] is JsonArray { Count: 2 }, "Numeric list was not normalized.");
    Assert(parsed["empty"] is JsonObject { Count: 0 }, "Ambiguous empty tables must remain objects before validation.");
}
static void ParserRejectsCode()
{
    foreach (var source in new[]
             {
                 "ClaudgarDB = os.execute('anything')", "ClaudgarDB = {}; doAnything()",
                 "local ClaudgarDB = {}", "ClaudgarDB = { value = 1 + 2 }",
                 "ClaudgarDB = { duplicate = 1, duplicate = 2 }", "ClaudgarDB = { [1] = 1, [1] = 2 }",
                 "ClaudgarDB = { value = 1e999 }", "ClaudgarDB = { value = 'unfinished }",
                 "ClaudgarDB = {" + new string('{', 60) + new string('}', 60) + "}"
             })
        Reject(() => new LuaSavedVariablesParser().Parse(source));
    Reject(() => new LuaSavedVariablesParser().Parse(new string(' ', LuaSavedVariablesParser.MaximumCharacters + 1)));
}
static void ValidateFixture()
{
    var export = new SnapshotValidator().Validate(ParsedFixture());
    Assert(export.Characters.Count == 1, "Fixture must contain one character.");
    var sections = export.Characters.Values.Single()["sections"]!.AsObject();
    Assert(ExportContract.SectionNames.All(section => sections[section] is JsonObject), "One of the five sections is missing.");
    Assert(sections["inventory"]!["data"]!["bags"]![0]!["items"]![0]!["stats"] is JsonObject, "Item stat maps must remain objects.");
    Assert(sections["talents"]!["data"]!["trees"]![0]!["groups"]![0]!["currencies"] is JsonArray { Count: 0 }, "Empty currencies must be arrays.");
    Assert(sections["character"]!["warnings"] is JsonArray { Count: 0 }, "Empty warning tables must be arrays.");
    Assert(sections["quests"]!["data"]!["active"]![0]!["objectives"]![0]!["objectiveType"]!.GetValue<long>() == 0,
        "The quest objective enum must remain numeric.");
    var failedDocument = ParsedFixture();
    var failedSection = failedDocument["characters"]!["Player-1-00001234"]!["sections"]!["talents"]!;
    failedSection["status"] = "failed";
    failedSection["error"] = "Collector failed in the current game state.";
    var failed = new SnapshotValidator().Validate(failedDocument);
    Assert(failed.Characters.Values.Single()["sections"]!["talents"]!["error"]!.GetValue<string>() == "Collector failed in the current game state.",
        "The collector failure explanation was lost.");
}
static void InvalidSchemas()
{
    void Invalid(Action<JsonObject> mutate)
    {
        var document = ParsedFixture();
        mutate(document);
        Reject(() => new SnapshotValidator().Validate(document));
    }
    Invalid(document => document["schemaVersion"] = 2);
    Invalid(document => document["client"]!["flavor"] = "retail");
    Invalid(document => document["characters"]!["Player-1-00001234"]!["characterKey"] = "another");
    Invalid(document => document["characters"]!["Player-1-00001234"]!["sections"]!["quests"]!["status"] = "fresh");
    Invalid(document => document["characters"]!["Player-1-00001234"]!["sections"]!["quests"]!["observedAt"] = -1);
    Invalid(document => document["characters"]!["Player-1-00001234"]!["sections"]!["inventory"]!["data"]!["bags"] = "empty");
    Invalid(document => document["characters"]!["Player-1-00001234"]!["sections"]!["quests"]!["data"]!["active"]![0]!["objectives"]![0]!["objectiveType"] = "monster");
    Invalid(document => document["characters"]!["Player-1-00001234"]!["sections"]!["talents"]!["error"] = new JsonObject());
    Invalid(document => document["characters"]!["Player-1-00001234"]!["sections"]!["quests"]!["data"]!["active"] = new JsonObject { ["2"] = new JsonObject { ["questId"] = 123 } });
    Invalid(document => document["characters"]!["Player-1-00001234"]!["sections"]!["character"]!["data"]!["localAccountPath"] = "private");
}
static void PresentationMetadata()
{
    var document = ParsedFixture();
    document["ui"] = new JsonObject { ["minimapAngle"] = 225 };
    var data = document["characters"]!["Player-1-00001234"]!["sections"]!;
    data["quests"]!["data"]!["completedDetails"] = new JsonArray(new JsonObject { ["questId"] = 7, ["title"] = "A saved quest title" });
    var node = data["talents"]!["data"]!["trees"]![0]!["nodes"]![0]!;
    node["groupIds"] = new JsonArray(1, 2);
    node["visibleEdges"] = new JsonArray(new JsonObject { ["targetNodeId"] = 2000, ["edgeType"] = 1, ["visualStyle"] = 0, ["isActive"] = true });
    node["entries"]![0]!["iconFileId"] = 1234;
    var validated = new SnapshotValidator().Validate(document).Characters.Values.Single()["sections"]!;
    Assert(!validated.ToJsonString().Contains("minimapAngle"), "Local addon display preferences leaked into public sections.");
    Assert(validated["quests"]!["data"]!["completed"]!.AsArray().Count == 3, "Saved names changed completed IDs.");
    Assert(validated["quests"]!["data"]!["completedDetails"]![0]!["title"]!.GetValue<string>() == "A saved quest title", "Saved quest name was lost.");
    Assert(validated["talents"]!["data"]!["trees"]![0]!["nodes"]![0]!["groupIds"] is JsonArray { Count: 2 }, "Numeric group IDs were lost.");
    Assert(validated["talents"]!["data"]!["trees"]![0]!["nodes"]![0]!["visibleEdges"]![0]!["edgeType"]!.GetValue<int>() == 1, "Talent edges were lost.");
    node["groupIds"] = new JsonObject();
    node["visibleEdges"] = new JsonObject();
    data["quests"]!["data"]!["completedDetails"] = new JsonObject();
    validated = new SnapshotValidator().Validate(document).Characters.Values.Single()["sections"]!;
    Assert(validated["quests"]!["data"]!["completedDetails"] is JsonArray { Count: 0 }, "Empty saved title table was not normalized.");
    node["groupIds"] = new JsonArray("private-path");
    Reject(() => new SnapshotValidator().Validate(document));
    node["groupIds"] = new JsonArray(1);
    data["quests"]!["data"]!["completedDetails"] = new JsonArray(new JsonObject { ["questId"] = 9999, ["title"] = "Not completed" });
    Reject(() => new SnapshotValidator().Validate(document));
    data["quests"]!["data"]!["completedDetails"]![0]!["questId"] = 7;
    data["quests"]!["data"]!["completedDetails"]![0]!["title"] = 123;
    Reject(() => new SnapshotValidator().Validate(document));
    _ = new SnapshotValidator().Validate(ParsedFixture());
}
static void EquipmentMetadata()
{
    var document = ParsedFixture();
    var sections = document["characters"]!["Player-1-00001234"]!["sections"]!;
    sections["character"]!["data"]!["stats"] = new JsonObject { ["strength"] = 23, ["maxHealth"] = 392, ["meleeCritChance"] = 6.24 };
    sections["equipment"]!["data"]!["items"]![0]!["slotIconFileId"] = 136516;
    var validated = new SnapshotValidator().Validate(document).Characters.Values.Single()["sections"]!;
    Assert(validated["character"]!["data"]!["stats"]!["meleeCritChance"]!.GetValue<double>() == 6.24,
        "Character stat maps must retain fractional native values.");
    Assert(validated["equipment"]!["data"]!["items"]![0]!["slotIconFileId"]!.GetValue<int>() == 136516,
        "The exported empty-slot artwork ID must remain available.");
    sections["character"]!["data"]!["stats"]!["strength"] = "private-path";
    Reject(() => new SnapshotValidator().Validate(document));
    sections["character"]!["data"]!["stats"]!["strength"] = 23;
    sections["equipment"]!["data"]!["items"]![0]!["slotIconFileId"] = "136516";
    Reject(() => new SnapshotValidator().Validate(document));
    _ = new SnapshotValidator().Validate(ParsedFixture());
}
static string ExportPath(string installation, string account) => Path.Combine(installation, "WTF", "Account", account, "SavedVariables", ExportContract.ExportFileName);
static void WriteExport(string path, string? contents = null)
{
    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
    File.WriteAllText(path, contents ?? Fixture());
}
static void InTemporaryFolder(Action<string> test)
{
    SetupRegressionTests.InTemporaryFolder(test);
}
static void CharacterIsolation() => InTemporaryFolder(folder =>
{
    var first = Path.Combine(folder, "ForeverOne");
    var second = Path.Combine(folder, "ForeverTwo");
    WriteExport(ExportPath(first, "ACCOUNT-A"));
    WriteExport(ExportPath(first, "ACCOUNT-B"));
    WriteExport(ExportPath(second, "ACCOUNT-A"));
    var repository = new ExportRepository(() => new[] { first, second });
    var result = repository.ListCharacters();
    var characters = result["characters"]!.AsArray();
    Assert(characters.Count == 3, "Same named characters must remain separate.");
    Assert(characters.Select(character => character!["characterId"]!.GetValue<string>()).Distinct().Count() == 3, "Character ids collided.");
    Assert(characters.Select(character => character!["accountId"]!.GetValue<string>()).Distinct().Count() == 3, "Accounts across installations collided.");
    var json = result.ToJsonString();
    Assert(!json.Contains(folder, StringComparison.OrdinalIgnoreCase) && !json.Contains("ACCOUNT-A", StringComparison.Ordinal), "Local account paths leaked.");
});
static void RefreshAndCache() => InTemporaryFolder(folder =>
{
    var path = ExportPath(folder, "ACCOUNT-A");
    WriteExport(path);
    var repository = new ExportRepository(() => new[] { folder });
    var id = repository.ListCharacters()["characters"]![0]!["characterId"]!.GetValue<string>();
    WriteExport(path, Fixture().Replace("[\"level\"] = 20", "[\"level\"] = 21", StringComparison.Ordinal));
    Assert(repository.GetCharacter(id)["character"]!["level"]!.GetValue<long>() == 21, "A fresh call returned an old valid save.");
    WriteExport(path, "ClaudgarDB = { broken");
    var cached = repository.GetCharacter(id);
    Assert(cached["character"]!["level"]!.GetValue<long>() == 21, "Corruption discarded the last valid save.");
    Assert(cached["freshness"]!["cached"]!.GetValue<bool>(), "Corruption was not labeled stale.");
    File.Delete(path);
    Assert(repository.GetSection(id, "quests")["freshness"]!["issueCode"]!.GetValue<string>() == "export_missing", "Missing cache was not reported.");
    WriteExport(path);
    Assert(!repository.GetCharacter(id)["freshness"]!["cached"]!.GetValue<bool>(), "A valid save did not clear stale status.");
});
static void BadAccountIsolation() => InTemporaryFolder(folder =>
{
    WriteExport(ExportPath(folder, "VALID"));
    WriteExport(ExportPath(folder, "INVALID"), "ClaudgarDB = os.execute('no')");
    var result = new ExportRepository(() => new[] { folder }).ListCharacters();
    Assert(result["characters"]!.AsArray().Count == 1, "A bad account hid a valid account.");
    Assert(result["exportState"]!.GetValue<string>() == "partial", "Partial failures were hidden.");
    Assert(result["issues"]!.AsArray().Count == 1, "Invalid account failure was not reported.");
    Assert(new ExportRepository(() => new[] { Path.Combine(folder, "empty") }).GetHealth().State == "no_export", "No-export status was not preserved.");
});
