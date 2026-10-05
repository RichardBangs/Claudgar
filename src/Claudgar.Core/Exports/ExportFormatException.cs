namespace Claudgar.Core.Exports;

/// <summary>A SavedVariables file did not conform to the supported data-only format.</summary>
public sealed class ExportFormatException : Exception
{
    public ExportFormatException(string message) : base(message) { }
}
