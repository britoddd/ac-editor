using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using AcMod.Core.Text;

namespace AcMod.Core.Formats;

/// <summary>
/// A lossless ui/ui_car.json. Values are addressed with JSON Pointer paths such as "/name", "/specs/bhp" or "/torqueCurve/3/1".
/// The reader is lenient in the one way real files need: raw control characters (line breaks, tabs) inside strings,
/// which 171 of 177 Kunos files contain (V-10). An edit replaces only the characters of one value (QS-2).
/// </summary>
public sealed partial class UiCarJson
{
    private readonly Dictionary<string, Token> _values;

    private UiCarJson(TextDocument document)
    {
        Document = document;
        try
        {
            _values = new Parser(document.Text).ParseDocument();
        }
        catch (FormatException e)
        {
            _values = [];
            Error = e.Message;
        }
    }

    private enum Kind { String, Number, Other }

    /// <param name="Start">Where the value's text starts in <see cref="TextDocument.Text"/>.</param>
    private sealed record Token(Kind Kind, int Start, int Length, string? String = null, double? Number = null);

    public TextDocument Document { get; }

    /// <summary>Why the file is not valid JSON even when read leniently, or null when it is. The file still round-trips.</summary>
    public string? Error { get; }

    /// <summary>Never fails. Invalid JSON is reported in <see cref="Error"/>.</summary>
    public static UiCarJson Parse(ReadOnlySpan<byte> bytes) => new(TextDocument.Parse(bytes));

    public byte[] ToBytes() => Document.ToBytes();

    /// <summary>Every value's pointer, objects and arrays included ("" is the whole document). Empty when <see cref="Error"/> is set.</summary>
    public IReadOnlyCollection<string> Pointers => _values.Keys;

    /// <summary>The string at <paramref name="pointer"/>, or null when it is missing or not a string.</summary>
    public string? GetString(string pointer) => _values.GetValueOrDefault(pointer)?.String;

    /// <summary>The number at <paramref name="pointer"/>, or null when it is missing or not a number.</summary>
    public double? GetNumber(string pointer) => _values.GetValueOrDefault(pointer)?.Number;

    /// <summary>Replaces an existing string value. Quotes, backslashes and control characters are escaped.</summary>
    public UiCarJson WithString(string pointer, string value) => Replace(pointer, Kind.String, Quote(value));

    /// <summary>Replaces an existing number value, written with invariant culture (QS-11).</summary>
    public UiCarJson WithNumber(string pointer, double value)
    {
        if (!double.IsFinite(value))
        {
            throw new ArgumentException("JSON cannot hold NaN or infinity.", nameof(value));
        }

        return Replace(pointer, Kind.Number, value.ToString(CultureInfo.InvariantCulture));
    }

    private UiCarJson Replace(string pointer, Kind kind, string replacement)
    {
        if (Error != null)
        {
            throw new InvalidOperationException("The file is not valid JSON, so it cannot be edited safely: " + Error);
        }

        if (!_values.TryGetValue(pointer, out var token))
        {
            throw new ArgumentException($"No value at {pointer}.", nameof(pointer));
        }

        if (token.Kind != kind)
        {
            throw new ArgumentException($"The value at {pointer} is not a {kind.ToString().ToLowerInvariant()}.", nameof(pointer));
        }

        var text = Document.Text;
        var edited = text[..token.Start] + replacement + text[(token.Start + token.Length)..];
        return new UiCarJson(Document.WithText(edited));
    }

    // Only '"', '\' and control characters need escaping. Real files use only \" (M1 step 7 survey), and
    // whether AC decodes \n, \t or \u escapes in ui_car.json is not verified.
    private static string Quote(string value)
    {
        var sb = new StringBuilder("\"");
        foreach (var c in value)
        {
            sb.Append(c switch
            {
                '"' => "\\\"",
                '\\' => "\\\\",
                '\n' => "\\n",
                '\r' => "\\r",
                '\t' => "\\t",
                < ' ' => $"\\u{(int)c:x4}",
                _ => c.ToString(),
            });
        }

        return sb.Append('"').ToString();
    }

    [GeneratedRegex(@"\G-?(?:0|[1-9]\d*)(?:\.\d+)?(?:[eE][-+]?\d+)?")]
    private static partial Regex NumberPattern();

    /// <summary>A small recursive JSON reader that records where every value sits in the text.</summary>
    private sealed class Parser(string text)
    {
        private readonly Dictionary<string, Token> _values = [];
        private int _pos;

        public Dictionary<string, Token> ParseDocument()
        {
            ParseValue("");
            SkipWhitespace();
            if (_pos != text.Length)
            {
                throw Fail("unexpected text after the end of the JSON value");
            }

            return _values;
        }

        private void ParseValue(string pointer)
        {
            SkipWhitespace();
            var start = _pos;
            Token token;
            switch (Peek())
            {
                case '{':
                    ParseObject(pointer);
                    token = new Token(Kind.Other, start, _pos - start);
                    break;
                case '[':
                    ParseArray(pointer);
                    token = new Token(Kind.Other, start, _pos - start);
                    break;
                case '"':
                    var s = ReadString();
                    token = new Token(Kind.String, start, _pos - start, String: s);
                    break;
                default:
                    var number = NumberPattern().Match(text, _pos);
                    if (number.Success && number.Length > 0)
                    {
                        _pos += number.Length;
                        token = new Token(Kind.Number, start, number.Length,
                            Number: double.Parse(number.Value, NumberStyles.Float, CultureInfo.InvariantCulture));
                    }
                    else if (TryReadLiteral("true") || TryReadLiteral("false") || TryReadLiteral("null"))
                    {
                        token = new Token(Kind.Other, start, _pos - start);
                    }
                    else
                    {
                        throw Fail("expected a value");
                    }

                    break;
            }

            // ponytail: with duplicate keys the first one wins; real ui_car.json files are not known to have any.
            _values.TryAdd(pointer, token);
        }

        private void ParseObject(string pointer)
        {
            _pos++; // '{'
            SkipWhitespace();
            if (Peek() == '}')
            {
                _pos++;
                return;
            }

            while (true)
            {
                SkipWhitespace();
                if (Peek() != '"')
                {
                    throw Fail("expected a property name");
                }

                var key = ReadString();
                SkipWhitespace();
                Expect(':');
                ParseValue(pointer + "/" + key.Replace("~", "~0").Replace("/", "~1"));
                SkipWhitespace();
                if (Peek() == ',')
                {
                    _pos++;
                    continue;
                }

                Expect('}');
                return;
            }
        }

        private void ParseArray(string pointer)
        {
            _pos++; // '['
            SkipWhitespace();
            if (Peek() == ']')
            {
                _pos++;
                return;
            }

            for (var index = 0; ; index++)
            {
                ParseValue(pointer + "/" + index.ToString(CultureInfo.InvariantCulture));
                SkipWhitespace();
                if (Peek() == ',')
                {
                    _pos++;
                    continue;
                }

                Expect(']');
                return;
            }
        }

        // Lenient: raw control characters inside the quotes are accepted as they are.
        private string ReadString()
        {
            _pos++; // opening '"'
            var sb = new StringBuilder();
            while (true)
            {
                if (_pos >= text.Length)
                {
                    throw Fail("unterminated string");
                }

                var c = text[_pos++];
                if (c == '"')
                {
                    return sb.ToString();
                }

                if (c != '\\')
                {
                    sb.Append(c);
                    continue;
                }

                if (_pos >= text.Length)
                {
                    throw Fail("unterminated escape");
                }

                var e = text[_pos++];
                switch (e)
                {
                    case '"' or '\\' or '/': sb.Append(e); break;
                    case 'b': sb.Append('\b'); break;
                    case 'f': sb.Append('\f'); break;
                    case 'n': sb.Append('\n'); break;
                    case 'r': sb.Append('\r'); break;
                    case 't': sb.Append('\t'); break;
                    case 'u' when _pos + 4 <= text.Length
                                  && int.TryParse(text.AsSpan(_pos, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var code):
                        sb.Append((char)code);
                        _pos += 4;
                        break;
                    default: throw Fail($"invalid escape \\{e}");
                }
            }
        }

        private bool TryReadLiteral(string literal)
        {
            if (string.CompareOrdinal(text, _pos, literal, 0, literal.Length) != 0)
            {
                return false;
            }

            _pos += literal.Length;
            return true;
        }

        private void SkipWhitespace()
        {
            while (_pos < text.Length && text[_pos] is ' ' or '\t' or '\r' or '\n')
            {
                _pos++;
            }
        }

        private char Peek() => _pos < text.Length ? text[_pos] : '\0';

        private void Expect(char c)
        {
            if (Peek() != c)
            {
                throw Fail($"expected '{c}'");
            }

            _pos++;
        }

        private FormatException Fail(string message) => new($"{message} at character {_pos}");
    }
}
