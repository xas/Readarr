using System.IO;
using NzbDrone.Common.Disk;

namespace NzbDrone.Mono.Disk
{
    public class ProcMount : IMount
    {
        private readonly DriveInfo _driveInfo;

        public ProcMount(DriveType driveType, string name, string mount, string type, MountOptions mountOptions)
        {
            DriveType = driveType;
            Name = name;
            RootDirectory = mount;
            DriveFormat = type;
            MountOptions = mountOptions;

            _driveInfo = new DriveInfo(mount);
        }

        public long AvailableFreeSpace => _driveInfo.AvailableFreeSpace;

        public string DriveFormat { get; private set; }

        public DriveType DriveType { get; private set; }

        public bool IsReady => _driveInfo.IsReady;

        public MountOptions MountOptions { get; private set; }

        public string Name { get; private set; }

        public string RootDirectory { get; private set; }

        public long TotalFreeSpace => _driveInfo.TotalFreeSpace;

        public long TotalSize => _driveInfo.TotalSize;

        // No label on Unix: .NET's DriveInfo.VolumeLabel is the mount path, and Mono's fstab fs_spec ("UUID=...") was not useful
        public string VolumeLabel => string.Empty;

        public string VolumeName => Name;
    }
}
