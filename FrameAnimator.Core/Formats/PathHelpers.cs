namespace FrameAnimator.Core.Formats
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;

    /// <summary>Equivalents of the Delphi <c>SysUtils</c> routines used by the original (separators <c>\</c> and <c>:</c>).</summary>
    public static class PathHelpers
    {
        private static Int32 LastDelimiter(String path)
        {
            for (var i = path.Length - 1; i >= 0; i--)
                if (path[i] is '\\' or '/' or ':')
                    return i;
            return -1;
        }

        /// <summary><c>ExtractFilePath</c> : directory with trailing separator (empty string if none).</summary>
        public static String ExtractFilePath(String path)
        {
            var i = LastDelimiter(path);
            return i < 0 ? "" : path[..(i + 1)];
        }

        /// <summary><c>ExtractFileName</c> : part after the last separator.</summary>
        public static String ExtractFileName(String path)
        {
            var i = LastDelimiter(path);
            return i < 0 ? path : path[(i + 1)..];
        }

        /// <summary><c>ExtractFileExt</c> : extension including the dot, or an empty string.</summary>
        public static String ExtractFileExt(String path)
        {
            var name = ExtractFileName(path);
            var dot = name.LastIndexOf('.');
            return dot < 0 ? "" : name[dot..];
        }

        /// <summary><c>ExtractFileDir</c> : directory without trailing separator.</summary>
        public static String ExtractFileDir(String path)
        {
            var p = ExtractFilePath(path);
            if (p.Length > 1 && p[^1] is '\\' or '/' && !(p.Length == 3 && p[1] == ':'))
                return p[..^1];
            return p;
        }

        /// <summary><c>ChangeFileExt</c> (extension given with its dot; empty to remove).</summary>
        public static String ChangeFileExt(String path, String newExt)
        {
            var name = ExtractFileName(path);
            var dot = name.LastIndexOf('.');
            var cut = dot < 0 ? path.Length : path.Length - (name.Length - dot);
            return path[..cut] + newExt;
        }

        /// <summary>
        /// Files matching a mask with an optional path (<c>FindFirst/FindNext</c> with attribute 0: no directories,
        /// no hidden/system files). Result sorted case-insensitively (NTFS order).
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