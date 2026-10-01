using System;
using System.IO;
using NLog;

namespace NzbDrone.Mono.Disk
{
    public interface ISymbolicLinkResolver
    {
        string GetCompleteRealPath(string path);
    }

    public class SymbolicLinkResolver : ISymbolicLinkResolver
    {
        private readonly Logger _logger;

        public SymbolicLinkResolver(Logger logger)
        {
            _logger = logger;
        }

        public string GetCompleteRealPath(string path)
        {
            if (path == null)
            {
                return null;
            }

            try
            {
                var realPath = path;
                for (var links = 0; links < 32; links++)
                {
                    var wasSymLink = TryFollowFirstSymbolicLink(ref realPath);
                    if (!wasSymLink)
                    {
                        return realPath;
                    }
                }

                _logger.Warn("Failed to check for symlinks in the path {0}: Too many levels of symbolic links", path);
                return path;
            }
            catch (Exception ex)
            {
                _logger.Debug(ex, "Failed to check for symlinks in the path {0}", path);
                return path;
            }
        }

        private static void GetPathComponents(string path, out string[] components, out int lastIndex)
        {
            var dirs = path.Split(Path.DirectorySeparatorChar);
            var target = 0;
            for (var i = 0; i < dirs.Length; ++i)
            {
                if (dirs[i] == "." || dirs[i] == string.Empty)
                {
                    continue;
                }

                if (dirs[i] == "..")
                {
                    if (target != 0)
                    {
                        target--;
                    }
                    else
                    {
                        target++;
                    }
                }
                else
                {
                    dirs[target++] = dirs[i];
                }
            }

            components = dirs;
            lastIndex = target;
        }

        private bool TryFollowFirstSymbolicLink(ref string path)
        {
            GetPathComponents(path, out var dirs, out var lastIndex);

            if (lastIndex == 0)
            {
                return false;
            }

            var realPath = "";

            for (var i = 0; i < lastIndex; ++i)
            {
                if (i != 0 || Path.IsPathRooted(path))
                {
                    realPath = string.Concat(realPath, Path.DirectorySeparatorChar, dirs[i]);
                }
                else
                {
                    realPath = string.Concat(realPath, dirs[i]);
                }

                var pathValid = TryFollowSymbolicLink(ref realPath, out var wasSymLink);

                if (!pathValid || wasSymLink)
                {
                    // If the path does not exist, or it was a symlink then we need to concat the remaining dir components and start over (or return)
                    var count = lastIndex - i - 1;

                    if (count > 0)
                    {
                        realPath = string.Concat(realPath, Path.DirectorySeparatorChar, string.Join(Path.DirectorySeparatorChar, dirs, i + 1, lastIndex - i - 1));
                    }

                    path = realPath;
                    return pathValid;
                }
            }

            return false;
        }

        private static bool TryFollowSymbolicLink(ref string path, out bool wasSymLink)
        {
            // Path.Exists does not follow symlinks (like lstat): true for a dangling link
            if (!Path.Exists(path))
            {
                wasSymLink = false;
                return false;
            }

            var link = new FileInfo(path).LinkTarget;

            if (link == null)
            {
                wasSymLink = false;
                return true;
            }

            if (Path.IsPathRooted(link))
            {
                path = link;
            }
            else
            {
                path = Path.GetDirectoryName(path) + Path.DirectorySeparatorChar + link;
                path = GetCanonicalPath(path);
            }

            wasSymLink = true;
            return true;
        }

        // Resolves "." and ".." and duplicate separators without touching the disk (same as Mono's UnixPath.GetCanonicalPath)
        private static string GetCanonicalPath(string path)
        {
            GetPathComponents(path, out var dirs, out var lastIndex);

            var end = string.Join(Path.DirectorySeparatorChar, dirs, 0, lastIndex);

            return Path.IsPathRooted(path) ? Path.DirectorySeparatorChar + end : end;
        }
    }
}
