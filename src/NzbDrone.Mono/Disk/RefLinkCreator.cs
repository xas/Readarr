using System;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using NLog;
using NzbDrone.Common.EnvironmentInfo;
using NzbDrone.Mono.Interop;

namespace NzbDrone.Mono.Disk
{
    public interface IRefLinkCreator
    {
        bool TryCreateRefLink(string srcPath, string linkPath);
    }

    public class RefLinkCreator : IRefLinkCreator
    {
        // Hardcoded ioctl for FICLONE on a typical linux system
        // #define FICLONE _IOW(0x94, 9, int)
        private const uint FICLONE = 0x40049409;

        private readonly Logger _logger;
        private readonly bool _supported;

        public RefLinkCreator(Logger logger)
        {
            _logger = logger;

            // Only support x86_64 because we know the FICLONE value is valid for it
            _supported = OsInfo.IsLinux && RuntimeInformation.OSArchitecture == Architecture.X64;
        }

        public bool TryCreateRefLink(string srcPath, string linkPath)
        {
            if (!_supported)
            {
                return false;
            }

            SafeFileHandle srcHandle;

            try
            {
                // Share everything: the source may still be open, e.g. by a download client
                srcHandle = File.OpenHandle(srcPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            }
            catch (Exception ex)
            {
                _logger.Trace(ex, "Failed to create reflink at '{0}' to '{1}': Couldn't open source file", linkPath, srcPath);
                return false;
            }

            using (srcHandle)
            {
                SafeFileHandle linkHandle;

                try
                {
                    linkHandle = File.OpenHandle(linkPath, FileMode.Create, FileAccess.Write);
                }
                catch (Exception ex)
                {
                    _logger.Trace(ex, "Failed to create reflink at '{0}' to '{1}': Couldn't create new link file", linkPath, srcPath);
                    return false;
                }

                // From here on the file at linkPath is ours: remove it if the clone fails
                try
                {
                    using (linkHandle)
                    {
                        if (LibC.Ioctl(linkHandle, FICLONE, srcHandle) == -1)
                        {
                            var error = LibC.LastErrorMessage;
                            linkHandle.Dispose();
                            TryDelete(linkPath);
                            _logger.Trace("Failed to create reflink at '{0}' to '{1}': {2}", linkPath, srcPath, error);
                            return false;
                        }
                    }

                    _logger.Trace("Created reflink at '{0}' to '{1}'", linkPath, srcPath);
                    return true;
                }
                catch (Exception ex)
                {
                    TryDelete(linkPath);
                    _logger.Trace(ex, "Failed to create reflink at '{0}' to '{1}'", linkPath, srcPath);
                    return false;
                }
            }
        }

        private static void TryDelete(string path)
        {
            try
            {
                File.Delete(path);
            }
            catch
            {
                // Same as the unlink it replaces: best effort, the error does not matter
            }
        }
    }
}
