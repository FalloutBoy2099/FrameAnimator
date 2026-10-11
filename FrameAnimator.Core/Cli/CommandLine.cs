namespace FrameAnimator.Core.Cli
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>Ligne de commande invalide (option inconnue, valeur manquante…) : le message est affiché tel quel.</summary>
    internal sealed class CommandLineException(String message) : Exception(message);

    /// <summary>Une option reconnue : son nom canonique, ses alias et si elle attend une valeur.</summary>
    internal sealed class OptionSpec(String name, Boolean takesValue, params String[] aliases)
    {
        public String Name { get; } = name;
        public Boolean TakesValue { get; } = takesValue;
        public String[] Aliases { get; } = aliases;
    }

    /// <summary>Résultat de l'analyse : la commande, ses arguments positionnels et ses options.</summary>
    internal sealed class ParsedCommandLine
    {
        private readonly Dictionary<String, List<String>> _options = new(StringComparer.OrdinalIgnoreCase);

        public String Command { get; init; } = "";

        public List<String> Positional { get; } = [];

        public Boolean Has(String name) => _options.ContainsKey(name);

        /// <summary>Dernière valeur donnée à l'option (la dernière l'emporte), ou <c>null</c>.</summary>
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
    /// Analyse de la ligne de commande. Une option commence par <c>/</c>, <c>-</c> ou <c>--</c> ; sa valeur suit après
    /// <c>:</c>, <c>=</c> ou un espace (<c>/to:frm</c>, <c>--to=frm</c>, <c>-to frm</c>). Les noms sont insensibles à la casse.
    /// Un mot qui commence par <c>/</c> sans être une option connue (par exemple un chemin) reste un argument positionnel.
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
        /// <c>/out:"C:\dossier\"</c> : dans une ligne de commande Windows, le <c>\"</c> final est lu comme un guillemet littéral
        /// (le programme reçoit <c>C:\dossier"</c>). Un nom de fichier ne peut pas contenir de guillemet : celui d'une fin de
        /// valeur est donc le séparateur final qui a été avalé, on le rétablit.
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

        /// <summary>Vrai pour <c>/?</c>, <c>-?</c>, <c>-h</c>, <c>--help</c>, <c>/help</c>, <c>--version</c>, <c>/version</c>…</summary>
        public static Boolean IsHelpOrVersion(String token)
        {
            String body = token.StartsWith("--", StringComparison.Ordinal) ? token[2..]
                : token.Length > 1 && token[0] is '-' or '/' ? token[1..]
                : "";
            return body.Length > 0 && Options.Where(option => option.Name is "help" or "version")
                .Any(option => option.Aliases.Contains(body, StringComparer.OrdinalIgnoreCase));
        }

        private static Boolean LooksLikeOption(String token) => TryParseOption(token, out _, out _);

        /// <summary>Reconnaît une option. Un mot en <c>-</c> inconnu est une erreur ; un mot en <c>/</c> inconnu est un chemin.</summary>
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
            // « /dossier/fichier » ou « /c:\chemin » : le nom contient un séparateur de chemin => ce n'est pas une option
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
