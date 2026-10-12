namespace FrameAnimator.App.ViewModels
{
    using System;
    using System.Windows.Input;

    /// <summary>
    /// One entry of the "Recent projects" menu: a file (its name as text, its full path as tooltip and as command parameter), or the
    /// last entry that clears the list.
    /// </summary>
    public sealed class RecentFileItem
    {
        /// <summary>An entry that opens <paramref name="path"/>.</summary>
        public RecentFileItem(String path, ICommand command)
        {
            Path = path;
            Header = System.IO.Path.GetFileName(path);
            Command = command;
        }

        /// <summary>The entry that clears the list.</summary>
        public RecentFileItem(String header, ICommand command, Boolean isClear)
        {
            Path = "";
            Header = header;
            Command = command;
            IsClear = isClear;
        }

        /// <summary>Full path of the project or FRM; empty for the "clear" entry.</summary>
        public String Path { get; }

        /// <summary>The text shown in the menu: the file name.</summary>
        public String Header { get; }

        public ICommand Command { get; }

        /// <summary>This entry clears the list instead of opening a file.</summary>
        public Boolean IsClear { get; }

        public override String ToString() => Header;
    }
}
