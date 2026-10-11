namespace FrameAnimator.Core.Formats
{
    using System;
    using System.Text;

    /// <summary>
    /// Applies a file name mask containing <c>*</c> and <c>?</c> to a source name, the way <c>REN</c> does
    /// (function 0x4028cc of the original). Examples: <c>*.*</c> on <c>abc.fpr</c> → <c>abc.fpr</c>;
    /// <c>??????ab.fpr</c> on <c>x.fpr</c> → <c>xab.fpr</c>.
    /// </summary>
    public static class NameMask
    {
        public static String Apply(String source, String mask)
        {
            var result = new StringBuilder();
            Int32 s = 0; // 0-based index into the source (ebx − 1 in the original)
            for (Int32 m = 0; m < mask.Length; m++)
            {
                Char c = mask[m];
                switch (c)
                {
                    case '*':
                        while (s < source.Length && source[s] != '.')
                            result.Append(source[s++]);
                        break;
                    case '.':
                        while (s < source.Length && source[s] != '.')
                            s++;
                        result.Append('.');
                        s++;
                        break;
                    case '?':
                        if (s < source.Length && source[s] != '.')
                            result.Append(source[s++]);
                        break;
                    default:
                        result.Append(c);
                        if (s < source.Length && source[s] != '.')
                            s++;
                        break;
                }
            }
            return result.ToString();
        }

        public static Boolean HasWildcards(String name) => name.Contains('*') || name.Contains('?');
    }
}