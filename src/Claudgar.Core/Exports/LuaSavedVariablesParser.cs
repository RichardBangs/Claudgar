using System.Globalization;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.Json.Nodes;
using Claudgar.Core.Data;

namespace Claudgar.Core.Exports;

/// <summary>
/// Reads the literal subset written by WoW SavedVariables. It never loads a Lua runtime,
/// evaluates expressions, resolves identifiers, or calls functions.
/// </summary>
public sealed class LuaSavedVariablesParser
{
    public const int MaximumCharacters = 16 * 1024 * 1024;
    private const int MaximumDepth = 48;
    private const int MaximumTokens = 2_000_000;
    private const int MaximumTableEntries = 250_000;
    private const int MaximumStringCharacters = 1024 * 1024;

    private string source = "";
    private int position;
    private int tokens;

    public JsonObject Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (text.Length > MaximumCharacters)
            throw new ExportFormatException("The export is larger than the supported limit.");
        source = text;
        position = 0;
        tokens = 0;
        if (source.Length > 0 && source[0] == '\uFEFF') position++;
        SkipTrivia();
        if (ReadIdentifier() != ExportContract.SavedVariableName)
            Fail("Expected the ClaudgarDB saved variable.");
        Expect('=');
        var result = ReadValue(0) as JsonObject
            ?? throw Error("The saved variable must be a keyed table.");
        SkipTrivia();
        if (Peek(';')) { position++; SkipTrivia(); }
        if (position != source.Length)
            Fail("Only one literal ClaudgarDB assignment is permitted.");
        return result;
    }

    private JsonNode? ReadValue(int depth)
    {
        if (++tokens > MaximumTokens) Fail("The export contains too many tokens.");
        if (depth > MaximumDepth) Fail("The export is nested too deeply.");
        SkipTrivia();
        if (position >= source.Length) Fail("Unexpected end of export.");
        var next = source[position];
        if (next == '{') return ReadTable(depth + 1);
        if (next is '"' or '\'') return JsonValue.Create(ReadQuotedString());
        if (next == '[' && TryLongBracket(position, out _, out _))
            return JsonValue.Create(ReadLongString());
        if (next == '-' || next == '+' || next == '.' || char.IsAsciiDigit(next))
            return ReadNumber();
        var identifier = ReadIdentifier();
        return identifier switch
        {
            "true" => JsonValue.Create(true),
            "false" => JsonValue.Create(false),
            "nil" => null,
            _ => throw Error("Expressions and identifier values are not permitted.")
        };
    }

    private JsonNode ReadTable(int depth)
    {
        Expect('{');
        var entries = new Dictionary<string, JsonNode?>(StringComparer.Ordinal);
        var numericKeys = new HashSet<int>();
        var implicitIndex = 1;
        var hasStringKeys = false;
        SkipTrivia();
        while (!Peek('}'))
        {
            if (entries.Count >= MaximumTableEntries) Fail("A table contains too many entries.");
            string key;
            int? numericKey = null;
            JsonNode? value;
            if (Peek('[') && !TryLongBracket(position, out _, out _))
            {
                position++;
                SkipTrivia();
                if (Peek('"') || Peek('\''))
                {
                    key = ReadQuotedString();
                    hasStringKeys = true;
                }
                else
                {
                    var number = ReadNumber();
                    if (!number.TryGetValue<long>(out var integer) || integer < 1 || integer > int.MaxValue)
                        Fail("Table index keys must be positive integers or strings.");
                    numericKey = checked((int)integer);
                    key = integer.ToString(CultureInfo.InvariantCulture);
                }
                Expect(']');
                Expect('=');
                value = ReadValue(depth);
            }
            else if (position < source.Length && IsIdentifierStart(source[position]))
            {
                var savedPosition = position;
                var candidate = ReadIdentifier();
                SkipTrivia();
                if (Peek('='))
                {
                    position++;
                    key = candidate;
                    hasStringKeys = true;
                    value = ReadValue(depth);
                }
                else
                {
                    position = savedPosition;
                    numericKey = implicitIndex++;
                    key = numericKey.Value.ToString(CultureInfo.InvariantCulture);
                    value = ReadValue(depth);
                }
            }
            else
            {
                numericKey = implicitIndex++;
                key = numericKey.Value.ToString(CultureInfo.InvariantCulture);
                value = ReadValue(depth);
            }
            if (!entries.TryAdd(key, value)) Fail("Duplicate table keys are not permitted.");
            if (numericKey.HasValue) numericKeys.Add(numericKey.Value);
            SkipTrivia();
            if (Peek(',') || Peek(';')) { position++; SkipTrivia(); }
            else if (!Peek('}')) Fail("Expected a table field separator.");
        }
        position++;
        if (!hasStringKeys && entries.Count > 0 && numericKeys.Count == entries.Count &&
            numericKeys.Min() == 1 && numericKeys.Max() == entries.Count)
        {
            var array = new JsonArray();
            for (var index = 1; index <= entries.Count; index++)
                array.Add(entries[index.ToString(CultureInfo.InvariantCulture)]);
            return array;
        }
        var table = new JsonObject();
        foreach (var entry in entries) table.Add(entry.Key, entry.Value);
        return table;
    }

    private JsonValue ReadNumber()
    {
        SkipTrivia();
        var start = position;
        if (Peek('+') || Peek('-')) position++;
        if (position + 1 < source.Length && source[position] == '0' && source[position + 1] is 'x' or 'X')
        {
            position += 2;
            var digitsStart = position;
            while (position < source.Length && Uri.IsHexDigit(source[position])) position++;
            if (digitsStart == position) throw Error("Invalid hexadecimal integer.");
            if (!long.TryParse(source[digitsStart..position], NumberStyles.HexNumber,
                    CultureInfo.InvariantCulture, out var hexValue) || hexValue < 0)
                throw Error("Invalid hexadecimal integer.");
            if (source[start] == '-') hexValue = -hexValue;
            return JsonValue.Create(hexValue)!;
        }
        while (position < source.Length && char.IsAsciiDigit(source[position])) position++;
        if (Peek('.'))
        {
            position++;
            while (position < source.Length && char.IsAsciiDigit(source[position])) position++;
        }
        if (Peek('e') || Peek('E'))
        {
            position++;
            if (Peek('+') || Peek('-')) position++;
            var exponentStart = position;
            while (position < source.Length && char.IsAsciiDigit(source[position])) position++;
            if (position == exponentStart) Fail("Invalid number exponent.");
        }
        var literal = source[start..position];
        if (long.TryParse(literal, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var integer))
            return JsonValue.Create(integer)!;
        if (!double.TryParse(literal, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) ||
            !double.IsFinite(number)) Fail("Invalid or non-finite number.");
        return JsonValue.Create(number)!;
    }

    private string ReadQuotedString()
    {
        var quote = source[position++];
        var result = new StringBuilder();
        var escapedBytes = new List<byte>();
        void FlushBytes()
        {
            if (escapedBytes.Count == 0) return;
            try { result.Append(new UTF8Encoding(false, true).GetString(escapedBytes.ToArray())); }
            catch (DecoderFallbackException) { Fail("String byte escapes must contain valid UTF-8."); }
            escapedBytes.Clear();
        }
        while (position < source.Length)
        {
            var current = source[position++];
            if (current == quote) { FlushBytes(); return result.ToString(); }
            if (current is '\n' or '\r') Fail("Unescaped line break in quoted string.");
            if (current == '\\')
            {
                if (position >= source.Length) Fail("Incomplete string escape.");
                current = source[position++];
                if (char.IsAsciiDigit(current))
                {
                    var value = current - '0';
                    for (var count = 1; count < 3 && position < source.Length && char.IsAsciiDigit(source[position]); count++)
                        value = value * 10 + source[position++] - '0';
                    if (value > 255) Fail("Decimal string escape is out of range.");
                    escapedBytes.Add((byte)value);
                }
                else if (current == 'x')
                {
                    if (position + 2 > source.Length) throw Error("Invalid hexadecimal string escape.");
                    if (!byte.TryParse(source.AsSpan(position, 2),
                            NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var value))
                        Fail("Invalid hexadecimal string escape.");
                    escapedBytes.Add(value);
                    position += 2;
                }
                else if (current == 'z')
                {
                    while (position < source.Length && char.IsWhiteSpace(source[position])) position++;
                }
                else if (current is '\n' or '\r')
                {
                    FlushBytes();
                    if (current == '\r' && Peek('\n')) position++;
                    result.Append('\n');
                }
                else
                {
                    FlushBytes();
                    result.Append(current switch
                    {
                        'a' => '\a', 'b' => '\b', 'f' => '\f', 'n' => '\n', 'r' => '\r',
                        't' => '\t', 'v' => '\v', '\\' => '\\', '\'' => '\'', '"' => '"',
                        _ => throw Error("Unsupported string escape.")
                    });
                }
            }
            else { FlushBytes(); result.Append(current); }
            if (result.Length + escapedBytes.Count > MaximumStringCharacters) Fail("A string exceeds the supported limit.");
        }
        throw Error("Unterminated quoted string.");
    }

    private string ReadLongString()
    {
        if (!TryLongBracket(position, out var equalsCount, out var contentStart))
            throw Error("Invalid long string.");
        var closing = "]" + new string('=', equalsCount) + "]";
        var closingPosition = source.IndexOf(closing, contentStart, StringComparison.Ordinal);
        if (closingPosition < 0) Fail("Unterminated long string.");
        if (source.AsSpan(contentStart).StartsWith("\r\n")) contentStart += 2;
        else if (contentStart < source.Length && source[contentStart] is '\r' or '\n') contentStart++;
        if (closingPosition - contentStart > MaximumStringCharacters) Fail("A string exceeds the supported limit.");
        position = closingPosition + closing.Length;
        return source[contentStart..closingPosition];
    }

    private void SkipTrivia()
    {
        while (position < source.Length)
        {
            if (char.IsWhiteSpace(source[position])) { position++; continue; }
            if (position + 1 >= source.Length || source[position] != '-' || source[position + 1] != '-') return;
            position += 2;
            if (TryLongBracket(position, out var equalsCount, out var contentStart))
            {
                var closing = "]" + new string('=', equalsCount) + "]";
                var end = source.IndexOf(closing, contentStart, StringComparison.Ordinal);
                if (end < 0) Fail("Unterminated block comment.");
                position = end + closing.Length;
            }
            else while (position < source.Length && source[position] is not '\n' and not '\r') position++;
        }
    }

    private bool TryLongBracket(int start, out int equalsCount, out int contentStart)
    {
        equalsCount = 0;
        contentStart = start;
        if (start >= source.Length || source[start] != '[') return false;
        var cursor = start + 1;
        while (cursor < source.Length && source[cursor] == '=') { equalsCount++; cursor++; }
        if (cursor >= source.Length || source[cursor] != '[') return false;
        contentStart = cursor + 1;
        return true;
    }

    private string ReadIdentifier()
    {
        SkipTrivia();
        if (position >= source.Length || !IsIdentifierStart(source[position]))
            throw Error("Expected an identifier.");
        var start = position++;
        while (position < source.Length && (IsIdentifierStart(source[position]) || char.IsAsciiDigit(source[position]))) position++;
        return source[start..position];
    }

    private static bool IsIdentifierStart(char value) => char.IsAsciiLetter(value) || value == '_';
    private bool Peek(char value) => position < source.Length && source[position] == value;
    private void Expect(char value) { SkipTrivia(); if (!Peek(value)) Fail($"Expected '{value}'."); position++; }
    private ExportFormatException Error(string message) => new($"{message} (character {position + 1}).");
    [DoesNotReturn]
    private void Fail(string message) => throw Error(message);
}
