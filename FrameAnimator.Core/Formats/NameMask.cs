namespace FrameAnimator.Core.Formats
{
    using System;
    using System.Text;

    /// <summary>
    /// Application d'un masque de nom de fichier avec <c>*</c> et <c>?</c> à un nom source, à la manière de <c>REN</c>
    /// (fonction 0x4028cc de l'original). Exemples : <c>*.*</c> sur <c>abc.fpr</c> → <c>abc.fpr</c> ;
    /// <c>??????ab.fpr</c> sur <c>x.fpr</c> → <c>xab.fpr</c>.
    /// </summary>
    public static class NameMask
    {
        public static String Apply(String source, String mask)
        {
            var result = new StringBuilder();
            Int32 s = 0; // indice 0‑based dans la source (ebx − 1 dans l'original)
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