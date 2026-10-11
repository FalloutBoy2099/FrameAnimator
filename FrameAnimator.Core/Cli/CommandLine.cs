namespace FrameAnimator.Core.Cli
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>Invalid command line (unknown option, missing value…): the message is displayed as is.</summary>
    internal sealed class CommandLineException(String message) : Exception(message);

    /// <summary>A recognized option: its canonical name, its aliases and whether it expects a value.</summary>
    internal sealed class OptionSpec(String name, Boolean takesValue, params String[] aliases)
    {
        public String Name { get; } = name;
        public Boolean TakesValue { get; } = takesValue;
        public String[] Aliases { get; } = aliases;
    }

    /// <summary>Parsing result: the command, its positional arguments and its options.</summary>
    internal sealed class ParsedCommandLine
    {
        private readonly Dictionary<String, List<String>> _options = new(StringComparer.OrdinalIgnoreCase);

        public String Command { get; init; } = "";

        public List<String> Positional { get; } = [];

        public Boolean Has(String name) => _options.ContainsKey(name);

        /// <summary>Last value given to the option (the last one wins), or <c>null</c>.</summary>
        public String? Get(String name) => _options.TryGetValue(name, out List<String>? values) ? values[^1] : null;

        public IReadOnlyList<String> GetAll(String name) => _options.TryGetValue(name, out List<String>? values) ? values : [];

        public IEnumerable<String> OptionNames => _options.Keys;

        internal void Add(String name, String? value)
        {
            if (!_options.TryGetValue(name, out List<String>? values))
                _options[name] = values = [];
            if (value is not null)
                values.Add(value);
            else if (values.Count == 0)
                values.Add("");
        }
    }

    /// <summary>
    /// Command-line parsing. An option starts with <c>/</c>, <c>-</c> or <c>--</c>; its value follows after
    /// <c>:</c>, <c>=</c> or a space (<c>/to:frm</c>, <c>--to=frm</c>, <c>-to frm</c>). Names are case-insensitive.
    /// A word that starts with <c>/</c> without being a known option (for example a path) remains a positional argument.
    /// </summary>
    internal static class CommandLine
    {
        public static readonly OptionSpec[] Options =
        [
            new OptionSpec("help", false, "?", "h", "help"),
            new OptionSpec("version", false, "version", "v"),
            new OptionSpec("palette", true, "palette", "p"),
            new OptionSpec("script", true, "script"),
            new OptionSpec("frames", true, "frames", "f"),
            new OptionSpec("format", true, "format", "to", "t"),
            new OptionSpec("dest", true, "dest", "out", "o", "output"),
            new OptionSpec("source", true, "source", "src"),
            new OptionSpec("open", true, "open"),
            new OptionSpec("continue", false, "continue", "c", "yes", "y"),
            new OptionSpec("quiet", false, "quiet", "q"),
        ];

        /// <summary>
        /// <c>/out:"C:\folder\"</c>: in a Windows command line, the trailing <c>\"</c> is read as a literal quote
        /// (the program receives <c>C:\folder"</c>). A file name cannot contain a quote: one at the end of a
        /// value is therefore the trailing separator that was swallowed, so it is restored.
        /// </summary>
        internal static String FixTrailingQuote(String value)
            => value.EndsWith('"') && value.IndexOf('"') == value.Length - 1 ? value[..^1] + '\\' : value;

        public static ParsedCommandLine Parse(IReadOnlyList<String> args)
        {
            String command = "";
            Int32 start = 0;
            if (args.Count > 0 && !LooksLikeOption(args[0]))
            {
                command = args[0].ToLowerInvariant();
                start = 1;
            }

            ParsedCommandLine result = new ParsedCommandLine { Command = command };
            Boolean onlyPositional = false;
            for (Int32 i = start; i < args.Count; i++)
            {
                String token = args[i];
                if (onlyPositional)
                {
                    result.Positional.Add(FixTrailingQuote(token));
                    continue;
                }
                if (token == "--")
                {
                    onlyPositional = true;
                    continue;
                }
                if (!TryParseOption(token, out OptionSpec? spec, out String? inlineValue))
                {
                    result.Positional.Add(FixTrailingQuote(token));
                    continue;
                }

                if (spec.TakesValue)
                {
                    String? value = inlineValue;
                    if (value is null)
                    {
                        if (i + 1 >= args.Count)
                            throw new CommandLineException($"Option '{token}' needs a value.");
                        value = args[++i];
                    }
                    result.Add(spec.Name, FixTrailingQuote(value));
                }
                else
                {
                    if (inlineValue is not null)
                        throw new CommandLineException($"Option '{token}' does not take a value.");
                    result.Add(spec.Name, null);
                }
            }
            return result;
        }

        /// <summary>True for <c>/?</c>, <c>-?</c>, <c>-h</c>, <c>--help</c>, <c>/help</c>, <c>--version</c>, <c>/version</c>…</summary>
        public static Boolean IsHelpOrVersion(String token)
        {
            String body = token.StartsWith("--", StringComparison.Ordinal) ? token[2..]
                : token.Length > 1 && token[0] is '-' or '/' ? token[1..]
                : "";
            return body.Length > 0 && Options.Where(option => option.Name is "help" or "version")
                .Any(option => option.Aliases.Contains(body, StringComparer.OrdinalIgnoreCase));
        }

        private static Boolean LooksLikeOption(String token) => TryParseOption(token, out _, out _);

        /// <summary>Recognizes an option. An unknown word starting with <c>-</c> is an error; an unknown word starting with <c>/</c> is a path.</summary>
        private static Boolean TryParseOption(String token, out OptionSpec spec, out String? inlineValue)
        {
            spec = null!;
            inlineValue = null;
            String body;
            Boolean slash = false;
            if (token.StartsWith("--", StringComparison.Ordinal) && token.Length > 2)
                body = token[2..];
            else if (token.StartsWith('-') && token.Length > 1)
                body = token[1..];
            else if (token.StartsWith('/') && token.Length > 1)
            {
                body = token[1..];
                slash = true;
            }
            else
                return false;

            Int32 separator = body.IndexOfAny([':', '=']);
            String name = separator < 0 ? body : body[..separator];
            // "/folder/file" or "/c:\path": the name contains a path separator => it is not an option
            if (slash && (name.Contains('/') || name.Contains('\\')))
                return false;

            OptionSpec? found = Options.FirstOrDefault(option => option.Aliases.Contains(name, StringComparer.OrdinalIgnoreCase));
            if (found is null)
            {
                if (slash)
                    return false;
                throw new CommandLineException($"Unknown option '{token}'.");
            }

            spec = found;
            inlineValue = separator < 0 ? null : body[(separator + 1)..];
            return true;
        }
    }
}