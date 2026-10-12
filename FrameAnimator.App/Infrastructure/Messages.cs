namespace FrameAnimator.App.Infrastructure
{
    using System;
    using FrameAnimator.App.Services;

    /// <summary>Sent (through the messenger) after the interface language changed.</summary>
    public sealed record LanguageChangedMessage(String Language);

    /// <summary>Sent (through the messenger) after the theme preference changed.</summary>
    public sealed record ThemeChangedMessage(ThemePreference Preference);
}
