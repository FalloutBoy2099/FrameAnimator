namespace FrameAnimator.Core.Formats
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;

    /// <summary>Équivalents des routines <c>SysUtils</c> de Delphi utilisées par l'original (séparateurs <c>\</c> et <c>:</c>).</summary>
    public static class PathHelpers
    {
        private static Int32 LastDelimiter(String path)
        {
            for (var i = path.Length - 1; i >= 0; i--)
                if (path[i] is '\\' or '/' or ':')
                    return i;
            return -1;
        }

        /// <summary><c>ExtractFilePath</c> : répertoire avec séparateur final (chaîne vide si aucun).</summary>
        public static String ExtractFilePath(String path)
        {
            var i = LastDelimiter(path);
            return i < 0 ? "" : path[..(i + 1)];
        }

        /// <summary><c>ExtractFileName</c> : partie après le dernier séparateur.</summary>
        public static String ExtractFileName(String path)
        {
            var i = LastDelimiter(path);
            return i < 0 ? path : path[(i + 1)..];
        }

        /// <summary><c>ExtractFileExt</c> : extension avec le point ou chaîne vide.</summary>
        public static String ExtractFileExt(String path)
        {
            var name = ExtractFileName(path);
            var dot = name.LastIndexOf('.');
            return dot < 0 ? "" : name[dot..];
        }

        /// <summary><c>ExtractFileDir</c> : répertoire sans séparateur final.</summary>
        public static String ExtractFileDir(String path)
        {
            var p = ExtractFilePath(path);
            if (p.Length > 1 && p[^1] is '\\' or '/' && !(p.Length == 3 && p[1] == ':'))
                return p[..^1];
            return p;
        }

        /// <summary><c>ChangeFileExt</c> (extension fournie avec son point ; vide pour supprimer).</summary>
        public static String ChangeFileExt(String path, String newExt)
        {
            var name = ExtractFileName(path);
            var dot = name.LastIndexOf('.');
            var cut = dot < 0 ? path.Length : path.Length - (name.Length - dot);
            return path[..cut] + newExt;
        }

        /// <summary>
        /// Fichiers correspondant à un masque avec chemin optionnel (<c>FindFirst/FindNext</c> avec attribut 0 : ni répertoires,
        /// ni fichiers cachés/système). Résultat trié sans tenir compte de la casse (ordre NTFS).
        /// </summary>
        public static IEnumerable<String> FindFiles(String mask)
        {
            var dir = ExtractFilePath(mask);
            var pattern = ExtractFileName(mask);
            var searchDir = dir.Length == 0 ? Directory.GetCurrentDirectory() : dir;
            if (!Directory.Exists(searchDir))
                return [];

            var files = new DirectoryInfo(searchDir).EnumerateFiles(pattern)
                .Where(f => (f.Attributes & (FileAttributes.Hidden | FileAttributes.System | FileAttributes.Directory)) == 0)
                .Select(f => f.Name)
                .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
                .Select(n => dir + n);

            return files;
        }
    }
}