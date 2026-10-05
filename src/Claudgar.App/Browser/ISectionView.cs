using System.Text.Json.Nodes;

namespace Claudgar.App.Browser;

/// <summary>A read-only presentation of a saved section; collection status is shown by its browser.</summary>
internal interface ISectionView
{
    void SetSection(JsonObject? section);
}
