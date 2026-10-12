namespace FrameAnimator.App.Services
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Where the user's application-specific preferences are kept: "key=value" entries, a key may hold several values (list).
    /// Everything that remembers something from one run to the next (theme, language, folders, windows) goes through it.
    /// </summary>
    public interface ISettingsStore
    {
        /// <summary>First value of the key, or <c>null</c>.</summary>
        String? Get(String key);

        /// <summary>All the values of the key, in order.</summary>
        IReadOnlyList<String> GetAll(String key);

        /// <summary>Stores a single value for the key.</summary>
        void Set(String key, String value);

        /// <summary>Replaces all values of the key; the other keys are kept as they are.</summary>
        void SetAll(String key, IEnumerable<String> values);
    }
}
