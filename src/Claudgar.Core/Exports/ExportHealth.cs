namespace Claudgar.Core.Exports;

/// <summary>Path-free status for the app's local connection display.</summary>
public sealed record ExportHealth(string State, int CharacterCount, int ExportCount, int ProblemCount, long LastReadAt);
