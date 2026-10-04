using System;
using System.Runtime.InteropServices;

namespace NzbDrone.Mono.Interop
{
    // libc calls with no .NET equivalent
    internal static partial class LibC
    {
        private const string Library = "libc";

        // Same value on Linux, macOS and FreeBSD
        public const int EXDEV = 18;

        private static readonly object GroupLock = new ();

        public static int LastError => Marshal.GetLastPInvokeError();

        public static string LastErrorMessage => GetErrorMessage(Marshal.GetLastPInvokeError());

        public static string GetErrorMessage(int error) => Marshal.GetPInvokeErrorMessage(error);

        // Unlike File.Move, never falls back to copy + delete
        [LibraryImport(Library, EntryPoint = "rename", SetLastError = true, StringMarshalling = StringMarshalling.Utf8)]
        public static partial int Rename(string oldPath, string newPath);

        [LibraryImport(Library, EntryPoint = "link", SetLastError = true, StringMarshalling = StringMarshalling.Utf8)]
        public static partial int Link(string oldPath, string newPath);

        // Pass uint.MaxValue (-1) to leave owner or group unchanged
        [LibraryImport(Library, EntryPoint = "chown", SetLastError = true, StringMarshalling = StringMarshalling.Utf8)]
        public static partial int Chown(string path, uint owner, uint group);

        [LibraryImport(Library, EntryPoint = "ioctl", SetLastError = true)]
        public static partial int Ioctl(SafeHandle fd, uint request, SafeHandle arg);

        [LibraryImport(Library, EntryPoint = "getgrnam", SetLastError = true, StringMarshalling = StringMarshalling.Utf8)]
        private static partial IntPtr GetGrNam(string name);

        public static bool TryGetGroupId(string name, out uint groupId)
        {
            // getgrnam returns a static buffer, read it before another thread overwrites it
            lock (GroupLock)
            {
                var group = GetGrNam(name);

                if (group == IntPtr.Zero)
                {
                    groupId = 0;
                    return false;
                }

                // struct group { char *gr_name; char *gr_passwd; gid_t gr_gid; char **gr_mem; } on Linux, macOS and FreeBSD
                groupId = unchecked((uint)Marshal.ReadInt32(group, 2 * IntPtr.Size));
                return true;
            }
        }
    }
}
