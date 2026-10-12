namespace FrameAnimator.Core.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using FrameAnimator.Core.Cli;
    using FrameAnimator.Core.Model;
    using FrameAnimator.Core.Services;
    using Xunit;

    /// <summary>User messages of the core: format + arguments, translation catalog, rendering in the console.</summary>
    public class MessageTests
    {
        private static readonly String[] Languages = ["fr", "de", "es", "it", "pt", "pl", "cs", "sv", "zh", "ru"];

        /// <summary>The formats of <see cref="CoreMessages"/> that have to be translated (the plain two-line joiner does not).</summary>
        private static IEnumerable<String> TranslatableFormats()
            => typeof(CoreMessages).GetFields(BindingFlags.Public | BindingFlags.Static)
                .Where(field => field.IsLiteral)
                .Select(field => (String)field.GetRawConstantValue()!)
                .Where(format => format != CoreMessages.TwoLines);

        [Fact]
        public void RendersTheEnglishTextAndNestedMessages()
        {
            UserMessage inner = new(CoreMessages.IndexOutOfRange, 9);
            UserMessage message = new(CoreMessages.ScriptErrorPosition, inner, 2, 3);

            Assert.Equal("Index '9' out of range\nin line 2, parameter 3", message.ToString());
            Assert.Equal("plain", ((UserMessage)"plain").ToString());
            Assert.Equal("a\nb", UserMessage.Lines("a", "b").ToString());
        }

        [Fact]
        public void RenderUsesTheInvariantCulture()
        {
            UserMessage message = new("value {0}", 1234.5);
            Assert.Equal("value 1234.5", message.Render());
        }

        [Theory]
        [InlineData("fr", "Impossible de charger la palette 'a.act' !")]
        [InlineData("de", "Die Palette 'a.act' kann nicht geladen werden!")]
        [InlineData("ru", "Не удалось загрузить палитру «a.act»!")]
        [InlineData("zh", "無法載入調色盤「a.act」！")]
        public void RendersInTheRequestedLanguage(String language, String expected)
        {
            UserMessage message = new(CoreMessages.UnableToLoadPalette, "a.act");
            Assert.Equal(expected, message.Render(language));
        }

        [Fact]
        public void NestedMessagesAreTranslatedToo()
        {
            UserMessage message = new(CoreMessages.ScriptErrorPosition, new UserMessage(CoreMessages.ParameterRequired), 4, 1);
            Assert.Equal("Un paramètre est requis\nà la ligne 4, paramètre 1", message.Render("fr"));
        }

        [Fact]
        public void EnglishAndUnknownLanguagesKeepTheEnglishText()
        {
            UserMessage message = new(CoreMessages.UnableToLoadBackground, "b.bmp");
            Assert.Equal("Unable to load background 'b.bmp'!", message.Render("en"));
            Assert.Equal("Unable to load background 'b.bmp'!", message.Render("xx"));
            Assert.Equal("Unable to load background 'b.bmp'!", message.Render(format => format));
        }

        [Fact]
        public void EveryCoreMessageIsTranslatedInEveryLanguage()
        {
            foreach (String language in Languages)
            {
                IReadOnlyDictionary<String, String> dictionary = MessageCatalog.Load(language);
                foreach (String format in TranslatableFormats())
                    Assert.True(dictionary.ContainsKey(format), $"{language}: missing '{format}'");
            }
        }

        [Fact]
        public void DictionariesHoldNothingButCoreMessagesWithTheSamePlaceholders()
        {
            HashSet<String> formats = TranslatableFormats().ToHashSet();
            foreach (String language in Languages)
            {
                foreach ((String key, String value) in MessageCatalog.Load(language))
                {
                    Assert.True(formats.Contains(key), $"{language}: stale key '{key}'");
                    Assert.Equal(Placeholders(key), Placeholders(value));
                    Assert.False(String.IsNullOrWhiteSpace(value), $"{language}: empty translation of '{key}'");
                }
            }
        }

        private static String Placeholders(String text)
            => String.Join(",", System.Text.RegularExpressions.Regex.Matches(text, @"\{\d+\}").Select(match => match.Value).Order());

        [Fact]
        public void ProjectServiceReportsMessagesWithTheirArguments()
        {
            SilentInteraction ui = new();
            ProjectService service = new(new Project(), ui, appDirectory: "");

            Assert.False(service.LoadPalette("missing.act"));
            Assert.False(service.LoadBackground("missing.bmp"));

            Assert.Equal(CoreMessages.UnableToLoadPalette, ui.ErrorMessages[0].Format);
            Assert.Equal(["missing.act"], ui.ErrorMessages[0].Args);
            Assert.Equal(CoreMessages.UnableToLoadBackground, ui.ErrorMessages[1].Format);
            Assert.Equal("Unable to load palette 'missing.act'!", ui.Errors[0]);
        }

        [Fact]
        public void ScriptErrorsAreNestedMessages()
        {
            using TempDir dir = new();
            String script = dir.File("bad.fas");
            File.WriteAllText(script, "BOGUS");
            SilentInteraction ui = new(confirmAnswer: false);
            ProjectService service = new(new Project(), ui, appDirectory: "");

            Boolean ok = new FrameAnimator.Core.Scripting.ScriptRunner(service) { ChangeCurrentDirectory = false }.Run(script);

            Assert.False(ok);
            UserMessage error = Assert.Single(ui.ErrorMessages);
            Assert.Equal(CoreMessages.ScriptErrorPosition, error.Format);
            UserMessage detail = Assert.IsType<UserMessage>(error.Args[0]);
            Assert.Equal(CoreMessages.UnknownKeyword, detail.Format);
            Assert.Equal("Mot-clé inconnu 'BOGUS'\nà la ligne 1, paramètre 1", error.Render("fr"));
            Assert.Equal(CoreMessages.ContinueExecutingScript, Assert.Single(ui.QuestionMessages).Format);
        }

        [Fact]
        public void ConsoleShowsTheCoreMessagesInItsLanguage()
        {
            using TempDir dir = new();
            StringWriter output = new();
            StringWriter error = new();

            Int32 code = new CliApp(output, error, "fr", "fa").Execute(["convert", dir.File("bad.frm"), "/to:fpr", "/quiet"]);
            Assert.NotEqual(0, code);

            // a corrupt FRM goes through ProjectService, whose messages are rendered in French
            File.WriteAllBytes(dir.File("bad.frm"), [1, 2, 3]);
            error = new StringWriter();
            code = new CliApp(output, error, "fr", "fa").Execute(["convert", dir.File("bad.frm"), "/to:fpr", "/quiet"]);
            Assert.NotEqual(0, code);
            Assert.Contains("Impossible de charger le FRM 'bad.frm' !", error.ToString());
        }
    }
}
