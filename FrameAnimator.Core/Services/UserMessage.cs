namespace FrameAnimator.Core.Services
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;

    /// <summary>
    /// A message for the user: an English format string (the key used to translate it) and its arguments. An argument can itself
    /// be a <see cref="UserMessage"/> (for example a script error followed by the line it occurred on). The core never builds
    /// final texts: the interface renders the message in its language (<see cref="Render(Func{String, String}?)"/>).
    /// </summary>
    public sealed class UserMessage(String format, params Object[] args)
    {
        public String Format { get; } = format;

        public IReadOnlyList<Object> Args { get; } = args;

        /// <summary>A message made of two messages on two lines.</summary>
        public static UserMessage Lines(UserMessage first, UserMessage second) => new(CoreMessages.TwoLines, first, second);

        public static implicit operator UserMessage(String format) => new(format);

        /// <summary>
        /// The text of the message. <paramref name="translate"/> maps a format string to its translation (identity when
        /// <c>null</c>); nested messages are rendered the same way. Numbers and dates use the invariant culture.
        /// </summary>
        public String Render(Func<String, String>? translate = null)
        {
            String text = translate is null ? Format : translate(Format);
            if (Args.Count == 0)
                return text;
            Object[] values = Args.Select(argument => argument is UserMessage nested ? nested.Render(translate) : argument).ToArray();
            return String.Format(CultureInfo.InvariantCulture, text, values);
        }

        /// <summary>The text of the message in a language (see <see cref="MessageCatalog"/>); English when it is not translated.</summary>
        public String Render(String language) => Render(format => MessageCatalog.Translate(format, language));

        /// <summary>The English text.</summary>
        public override String ToString() => Render();
    }

    /// <summary>Convenience overloads that build the <see cref="UserMessage"/> from a format and its arguments.</summary>
    public static class UserInteractionExtensions
    {
        public static void ShowError(this IUserInteraction ui, String format, params Object[] args) => ui.ShowError(new UserMessage(format, args));

        public static Boolean Confirm(this IUserInteraction ui, String format, params Object[] args) => ui.Confirm(new UserMessage(format, args));
    }
}
