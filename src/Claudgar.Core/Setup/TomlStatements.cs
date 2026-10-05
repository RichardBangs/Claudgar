using System.Text;
using System.Text.Json;

namespace Claudgar.Core.Setup;

/// <summary>
/// A conservative statement scanner, not a TOML serializer. It locates tables and dotted keys while
/// respecting comments, quoted names, multiline strings, arrays and inline tables. Original text is preserved.
/// </summary>
internal static class TomlStatements
{
    internal sealed record Statement(int Start, int End, string Text);

    public static IReadOnlyList<Statement> Scan(string text)
    {
        var statements = new List<Statement>();
        var cleaned = new StringBuilder();
        int start = 0, square = 0, curly = 0;
        char quote = '\0';
        bool triple = false, escaped = false, comment = false;
        for (int index = 0; index < text.Length; index++)
        {
            char character = text[index];
            if (comment)
            {
                if (character != '\n') continue;
                comment = false;
            }
            else if (quote != '\0')
            {
                cleaned.Append(character);
                if (escaped) { escaped = false; continue; }
                if (quote == '"' && character == '\\') { escaped = true; continue; }
                if (character == quote)
                {
                    if (!triple) quote = '\0';
                    else if (index + 2 < text.Length && text[index + 1] == quote && text[index + 2] == quote)
                    {
                        cleaned.Append(quote, 2);
                        index += 2;
                        quote = '\0';
                        triple = false;
                    }
                }
                if (character == '\n' && quote != '\0' && !triple)
                    throw new FormatException("Codex configuration contains an unterminated quoted string.");
                continue;
            }
            else if (character == '#') { comment = true; continue; }
            else if (character is '\'' or '"')
            {
                quote = character;
                cleaned.Append(character);
                triple = index + 2 < text.Length && text[index + 1] == character && text[index + 2] == character;
                if (triple) { cleaned.Append(character, 2); index += 2; }
                continue;
            }
            else
            {
                if (character == '[') square++;
                if (character == ']') square--;
                if (character == '{') curly++;
                if (character == '}') curly--;
                if (square < 0 || curly < 0) throw new FormatException("Codex configuration contains unbalanced brackets.");
            }
            cleaned.Append(character);
            if (character == '\n' && square == 0 && curly == 0)
            {
                statements.Add(new(start, index + 1, cleaned.ToString().Trim()));
                start = index + 1;
                cleaned.Clear();
            }
        }
        if (quote != '\0' || square != 0 || curly != 0)
            throw new FormatException("Codex configuration contains an unfinished TOML value.");
        if (start < text.Length) statements.Add(new(start, text.Length, cleaned.ToString().Trim()));
        return statements;
    }

    public static IReadOnlyList<string> KeyPath(string value)
    {
        var keys = new List<string>();
        int index = 0;
        while (index < value.Length)
        {
            while (index < value.Length && char.IsWhiteSpace(value[index])) index++;
            if (index == value.Length) throw new FormatException("A TOML key is empty.");
            int begin = index;
            string key;
            if (value[index] is '\'' or '"')
            {
                char quote = value[index++];
                bool escaped = false;
                while (index < value.Length)
                {
                    var character = value[index++];
                    if (escaped) { escaped = false; continue; }
                    if (quote == '"' && character == '\\') { escaped = true; continue; }
                    if (character == quote) break;
                }
                if (value[index - 1] != quote || index - begin == 1) throw new FormatException("A TOML key is unfinished.");
                var raw = value[begin..index];
                key = quote == '\'' ? raw[1..^1] : JsonSerializer.Deserialize<string>(raw)
                    ?? throw new FormatException("A TOML key is invalid.");
            }
            else
            {
                while (index < value.Length && (char.IsAsciiLetterOrDigit(value[index]) || value[index] is '_' or '-')) index++;
                if (index == begin) throw new FormatException("A TOML key uses an unsupported spelling.");
                key = value[begin..index];
            }
            keys.Add(key);
            while (index < value.Length && char.IsWhiteSpace(value[index])) index++;
            if (index == value.Length) break;
            if (value[index++] != '.' || index == value.Length) throw new FormatException("A TOML key is invalid.");
        }
        return keys;
    }

    public static int AssignmentSeparator(string statement)
    {
        char quote = '\0';
        bool escaped = false;
        for (int index = 0; index < statement.Length; index++)
        {
            char character = statement[index];
            if (escaped) { escaped = false; continue; }
            if (quote == '"' && character == '\\') { escaped = true; continue; }
            if (quote != '\0') { if (character == quote) quote = '\0'; continue; }
            if (character is '\'' or '"') { quote = character; continue; }
            if (character == '=') return index;
        }
        return -1;
    }
}
