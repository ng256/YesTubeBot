using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace System.Ini
{
    internal class CmdLineRegexParser
    {
        // Determines how strings are compared when working with command line data.
        // Initialized in the constructor based on settings.
        private StringComparison _comparison = StringComparison.InvariantCultureIgnoreCase;
        public StringComparison Comparison => _comparison;

        // Regular expression for parsing command line arguments.
        private readonly Regex _regex;

        // Indicates whether escape characters are allowed in the values.
        private readonly bool _allowEscapeChars;

        // Stores the matches from the command line arguments.
        private readonly List<Match> _matches;

        private readonly CultureInfo _culture = CultureInfo.InvariantCulture;
        private readonly HashSet<string> _trueValues ;
        private readonly HashSet<string> _falseValues;

        // Property to access the matches, either cached or newly iterated.
        private IEnumerable<Match> Matches => _matches;

        // Initializes a new instance with Environment command line arguments and settings.
        public CmdLineRegexParser(StringComparison comparison = StringComparison.InvariantCultureIgnoreCase, bool allowEscapeChars = false)
            : this(Environment.CommandLine, comparison, allowEscapeChars)
        {
        }

        // Initializes a new instance with a custom command line string and settings.
        public CmdLineRegexParser(string cmdLine, StringComparison comparison = StringComparison.InvariantCultureIgnoreCase, bool allowEscapeChars = false)
        {
            _allowEscapeChars = allowEscapeChars;
            _matches = new List<Match>(16);
            StringComparer comparer = GetComparer(comparison);
            _trueValues = new HashSet<string>(comparer) { "true", "yes", "on", "enable", "1" };
            _falseValues = new HashSet<string>(comparer) { "false", "no", "off", "disable", "0" };
            _regex = new Regex(@"(?:[/-]+(?<key>\S+?))|(?:'[/-]+(?<key>.+?)')|(?:""[/-]+(?<key>.+?)\"")|(?:'(?<value>.+?)')|(?:""(?<value>.+?)\"")|(?<value>\S+)", GetRegexOptions(comparison, RegexOptions.Compiled));
            
            // Iterate over matches using the regex pattern and collect sections and entries names.
            for (Match match = _regex.Match(cmdLine); match.Success; match = match.NextMatch())
            {
                GroupCollection groups = match.Groups;
                if (groups["key"].Success || groups["value"].Success)
                    _matches.Add(match);
            }
        }

        // Initializes a new instance with an array of arguments and settings.
        public CmdLineRegexParser(string[] arguments, StringComparison comparison = StringComparison.InvariantCultureIgnoreCase, bool allowEscapeChars = false)
        {
            _allowEscapeChars = allowEscapeChars;
            _matches = new List<Match>(arguments.Length);
            StringComparer comparer = GetComparer(comparison);
            _trueValues = new HashSet<string>(comparer) { "true", "yes", "on", "enable", "1" };
            _falseValues = new HashSet<string>(comparer) { "false", "no", "off", "disable", "0" };
            _regex = new Regex(@"(?:[/-]+(?<key>\S+?))|(?:'[/-]+(?<key>.+?)')|(?:""[/-]+(?<key>.+?)\"")|(?:'(?<value>.+?)')|(?:""(?<value>.+?)\"")|(?<value>\S+)", GetRegexOptions(comparison, RegexOptions.Compiled));

            // Match each argument against the regex pattern.
            foreach (string argument in arguments)
            {
                Match match = _regex.Match(argument);
                GroupCollection groups = match.Groups;
                if (match.Success && groups["key"].Success || groups["value"].Success)
                    _matches.Add(match);
            }
        }

        // Sets or clears the RegexOptions flags based on the specified StringComparison, returning the modified value.
        private static RegexOptions GetRegexOptions(StringComparison comparison, RegexOptions options = RegexOptions.None)
        {
            switch (comparison)
            {
                case StringComparison.CurrentCulture:
                    options &= ~RegexOptions.CultureInvariant;
                    break;
                case StringComparison.CurrentCultureIgnoreCase:
                    options &= ~RegexOptions.CultureInvariant;
                    options |= RegexOptions.IgnoreCase;
                    break;
                case StringComparison.InvariantCulture:
                    options |= RegexOptions.CultureInvariant;
                    break;
                case StringComparison.InvariantCultureIgnoreCase:
                    options |= RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;
                    break;
                case StringComparison.OrdinalIgnoreCase:
                    options |= RegexOptions.IgnoreCase;
                    break;
            }

            return options;
        }

        // Returns the StringComparer based on the specified StringComparison.
        private static StringComparer GetComparer(StringComparison comparison)
        {
            switch (comparison)
            {
                case StringComparison.CurrentCulture:
                    return StringComparer.CurrentCulture;
                case StringComparison.CurrentCultureIgnoreCase:
                    return StringComparer.CurrentCultureIgnoreCase;
                case StringComparison.InvariantCulture:
                    return StringComparer.InvariantCulture;
                case StringComparison.InvariantCultureIgnoreCase:
                    return StringComparer.InvariantCultureIgnoreCase;
                case StringComparison.Ordinal:
                    return StringComparer.Ordinal;
                case StringComparison.OrdinalIgnoreCase:
                    return StringComparer.OrdinalIgnoreCase;
                default:
                    return StringComparer.InvariantCultureIgnoreCase;
            }
        }

        // Escape characters in the input string with backslashes.
        private static string ToEscape(string text)
        {
            int pos = 0;
            int inputLength = text.Length;

            if (inputLength == 0) return text;

            StringBuilder sb = new StringBuilder(inputLength * 2);
            do
            {
                char c = text[pos++];

                switch (c)
                {
                    case '\\':
                        sb.Append(@"\\");
                        break;
                    case '\0':
                        sb.Append(@"\0");
                        break;
                    case '\a':
                        sb.Append(@"\a");
                        break;
                    case '\b':
                        sb.Append(@"\b");
                        break;
                    case '\n':
                        sb.Append(@"\n");
                        break;
                    case '\r':
                        sb.Append(@"\r");
                        break;
                    case '\f':
                        sb.Append(@"\f");
                        break;
                    case '\t':
                        sb.Append(@"\t");
                        break;
                    case '\v':
                        sb.Append(@"\v");
                        break;
                    default:
                        sb.Append(c);
                        break;
                }
            } while (pos < inputLength);

            return sb.ToString();
        }

        // Converts hex number to unicode character.
        private static char UnHex(string hex)
        {
            int c = 0;
            for (int i = 0; i < hex.Length; i++)
            {
                int r = hex[i]; // Obtain next digit.
                if (r > 0x2F && r < 0x3A) r -= 0x30;
                else if (r > 0x40 && r < 0x47) r -= 0x37;
                else if (r > 0x60 && r < 0x67) r -= 0x57;
                else return '?';
                c = (c << 4) + r; // Insert next digit.
            }

            return (char)c;
        }

        // Converts any escaped characters in the input string.
        private static string UnEscape(string text)
        {
            int pos = -1;
            int inputLength = text.Length;

            if (inputLength == 0) return text;

            // Find the first occurrence of backslash or return the original text.
            for (int i = 0; i < inputLength; ++i)
            {
                if (text[i] == '\\')
                {
                    pos = i;
                    break;
                }
            }

            if (pos < 0) return text; // Backslash not found.

            // If backslash is found.
            StringBuilder sb = new StringBuilder(text.Substring(0, pos));

            do
            {
                char c = text[pos++];
                if (c == '\\')
                {
                    c = pos < inputLength ? text[pos] : '\\';
                    switch (c)
                    {
                        case '\\':
                            c = '\\';
                            break;
                        case '0':
                            c = '\0';
                            break;
                        case 'a':
                            c = '\a';
                            break;
                        case 'b':
                            c = '\b';
                            break;
                        case 'n':
                            c = '\n';
                            break;
                        case 'r':
                            c = '\r';
                            break;
                        case 'f':
                            c = '\f';
                            break;
                        case 't':
                            c = '\t';
                            break;
                        case 'v':
                            c = '\v';
                            break;
                        case 'u' when pos < inputLength - 3:
                            c = UnHex(text.Substring(++pos, 4));
                            pos += 3;
                            break;
                        case 'x' when pos < inputLength - 1:
                            c = UnHex(text.Substring(++pos, 2));
                            pos++;
                            break;
                        case 'c' when pos < inputLength:
                            c = text[++pos];
                            if (c >= 'a' && c <= 'z')
                                c -= ' ';
                            if ((c = (char)(c - 0x40U)) >= ' ')
                                c = '?';
                            break;
                        default:
                            sb.Append("\\" + c);
                            pos++;
                            continue;
                    }
                    pos++;
                }
                sb.Append(c);

            } while (pos < inputLength);

            return sb.ToString();
        }

        internal static bool IsNewLine(char c)
        {
            return c == '\n' || c == '\r';
        }

        // Converts a string to lowercase based on the specified comparison.
        private static string MayBeToLower(string text, StringComparison comparison)
        {
            if ((int)comparison % 2 > 0)
                switch (comparison)
                {
                    case StringComparison.CurrentCultureIgnoreCase:
                        return text.ToLower(CultureInfo.CurrentCulture);
                    case StringComparison.InvariantCultureIgnoreCase:
                        return text.ToLower(CultureInfo.InvariantCulture);
                    case StringComparison.OrdinalIgnoreCase:
                        return text.ToLower();
                }

            return text;
        }

        // Returns unique keys found in the command line arguments.
        private IEnumerable<string> GetKeys()
        {
            StringComparison comparison = Comparison;
            HashSet<string> keys = new HashSet<string>(GetComparer(comparison));

            // Iterate over matches to extract keys.
            foreach (Match match in Matches)
            {
                Group group = match.Groups["key"];
                if (group.Success)
                {
                    // If ignoring case, convert the key to lower case.
                    string key = MayBeToLower(group.Value, comparison);

                    keys.Add(key);
                }
            }

            return keys;
        }

        // Checks if a specific key exists in the command line arguments.
        private bool GetFlag(string key)
        {
            StringComparison comparison = Comparison;

            // Iterate over matches to check for the key.
            foreach (Match match in Matches)
            {
                // Check if the key matches the specified key.
                Group group = match.Groups["key"];
                if (group.Success)
                {
                    if (group.Value.Equals(key, comparison))
                        return true;
                }
            }

            return false; // Return false if the key is not found.
        }

        // Retrieves the value associated with a specific key, or returns a default value if not found.
        private string GetValue(string key, string defaultValue)
        {
            StringComparison comparison = Comparison;
            bool emptyKey = string.IsNullOrEmpty(key);
            bool inKey = emptyKey;

            // Iterate over matches to find the value for the specified key.
            foreach (Match match in Matches)
            {
                // Check if the current key matches the specified key.
                Group group = match.Groups["key"];
                if (group.Success)
                {
                    inKey = group.Value.Equals(key, comparison);
                    if (emptyKey) break;
                    continue;
                }

                // If the value group is successful and matches the key, return the value.
                if (inKey && (group = match.Groups["value"]).Success)
                {
                    string value = group.Value;

                    // Unescape value if allowed.
                    if (_allowEscapeChars) value = UnEscape(value);

                    return value;
                }
            }

            return defaultValue;
        }

        // Retrieves all values associated with a specific key.
        private IEnumerable<string> GetValues(string key)
        {
            StringComparison comparison = Comparison;
            bool emptyKey = string.IsNullOrEmpty(key);
            bool inKey = emptyKey;
            List<string> values = new List<string>();

            // Iterate over matches to collect values for the specified key.
            foreach (Match match in Matches)
            {
                Group group = match.Groups["key"];
                if (group.Success)
                {
                    inKey = group.Value.Equals(key, comparison);
                    if (emptyKey) break;
                    continue;
                }

                // Check if the current key matches the specified key.
                if (inKey && (group = match.Groups["value"]).Success)
                {
                    string value = group.Value;

                    // Unescape value if allowed.
                    if (_allowEscapeChars) value = UnEscape(value);

                    values.Add(value);
                }
            }

            return values;
        }

        // Retrieves all values regardless of key.
        private IEnumerable<string> GetValues()
        {
            List<string> values = new List<string>();
            bool keyFound = false;

            // Iterate over matches to collect values that do not have a key.
            foreach (Match match in Matches)
            {
                Group group = match.Groups["key"];
                if (group.Success)
                {
                    keyFound = true;
                    continue;
                }

                if (!keyFound)
                {
                    group = match.Groups["value"];
                    if (group.Success)
                    {
                        string value = group.Value;

                        // Unescape value if allowed.
                        if (_allowEscapeChars) value = UnEscape(value);

                        values.Add(value);
                    }
                }
            }

            return values;
        }
    }
}
