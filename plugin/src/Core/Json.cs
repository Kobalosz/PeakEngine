using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace PeakEngine.Core
{
    /// <summary>
    /// A very small JSON writer (the C# side of JavaScript's JSON.stringify).
    ///
    /// The game does not reliably ship a JSON library we can use, so we build the text ourselves.
    /// It understands: null, strings, booleans, numbers, dictionaries (become objects) and lists (become arrays).
    ///
    /// We never need to PARSE JSON in C#, because the control panel sends its inputs as URL query
    /// parameters (?player=3&amp;value=0.5), which are much easier to read.
    /// </summary>
    public static class Json
    {
        public static string Write(object value)
        {
            var text = new StringBuilder();
            WriteValue(text, value);
            return text.ToString();
        }

        private static void WriteValue(StringBuilder text, object value)
        {
            switch (value)
            {
                case null:
                    text.Append("null");
                    return;
                case string s:
                    WriteString(text, s);
                    return;
                case bool b:
                    text.Append(b ? "true" : "false");
                    return;
                case float f:
                    WriteNumber(text, f);
                    return;
                case double d:
                    WriteNumber(text, d);
                    return;
                case Enum e:
                    WriteString(text, e.ToString());
                    return;
                case IDictionary dictionary:
                    WriteObject(text, dictionary);
                    return;
                case IEnumerable list:
                    WriteArray(text, list);
                    return;
            }

            if (value is IFormattable number && IsInteger(value))
            {
                text.Append(number.ToString(null, CultureInfo.InvariantCulture));
                return;
            }

            // Anything else (a game object we forgot to convert) becomes its text form.
            WriteString(text, value.ToString());
        }

        private static void WriteNumber(StringBuilder text, double number)
        {
            // JSON has no NaN/Infinity, so send 0 instead of producing invalid JSON.
            if (double.IsNaN(number) || double.IsInfinity(number)) number = 0;
            // InvariantCulture makes sure we write 0.5 and never 0,5 (which some languages use).
            text.Append(Math.Round(number, 4).ToString("0.####", CultureInfo.InvariantCulture));
        }

        private static void WriteObject(StringBuilder text, IDictionary dictionary)
        {
            text.Append('{');
            bool first = true;
            foreach (DictionaryEntry entry in dictionary)
            {
                if (!first) text.Append(',');
                first = false;
                WriteString(text, Convert.ToString(entry.Key, CultureInfo.InvariantCulture));
                text.Append(':');
                WriteValue(text, entry.Value);
            }
            text.Append('}');
        }

        private static void WriteArray(StringBuilder text, IEnumerable list)
        {
            text.Append('[');
            bool first = true;
            foreach (var item in list)
            {
                if (!first) text.Append(',');
                first = false;
                WriteValue(text, item);
            }
            text.Append(']');
        }

        private static void WriteString(StringBuilder text, string value)
        {
            text.Append('"');
            foreach (char c in value)
            {
                switch (c)
                {
                    case '"': text.Append("\\\""); break;
                    case '\\': text.Append("\\\\"); break;
                    case '\n': text.Append("\\n"); break;
                    case '\r': text.Append("\\r"); break;
                    case '\t': text.Append("\\t"); break;
                    default:
                        // Escape control characters and "<" (so a player name can never break out of a <script>).
                        if (c < 0x20 || c == '<' || c == 0x2028 || c == 0x2029)
                            text.Append("\\u").Append(((int)c).ToString("x4"));
                        else
                            text.Append(c);
                        break;
                }
            }
            text.Append('"');
        }

        private static bool IsInteger(object value)
        {
            switch (Type.GetTypeCode(value.GetType()))
            {
                case TypeCode.Byte: case TypeCode.SByte: case TypeCode.Int16: case TypeCode.UInt16:
                case TypeCode.Int32: case TypeCode.UInt32: case TypeCode.Int64: case TypeCode.UInt64:
                case TypeCode.Decimal:
                    return true;
                default:
                    return false;
            }
        }
    }

    /// <summary>
    /// Shorthand for building the dictionaries that <see cref="Json.Write"/> turns into JSON objects.
    /// <c>new JsonObject { ["name"] = "Bing", ["dead"] = false }</c> becomes <c>{"name":"Bing","dead":false}</c>.
    /// </summary>
    public class JsonObject : Dictionary<string, object> { }
}
