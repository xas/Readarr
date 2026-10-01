using System.Collections.Generic;
using System.IO;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Common.Disk;
using NzbDrone.Mono.Disk;
using NzbDrone.Test.Common;

namespace NzbDrone.Mono.Test.DiskProviderTests
{
    [TestFixture]
    [Platform(Exclude = "Win")]
    public class ProcMountFixture : TestBase
    {
        private static ProcMount GivenMount(string mount)
        {
            return new ProcMount(DriveType.Fixed, "/dev/test", mount, "myfs", new MountOptions(new Dictionary<string, string>()));
        }

        [Test]
        public void should_return_space_of_root_mount()
        {
            var mount = GivenMount("/");

            mount.IsReady.Should().BeTrue();
            mount.TotalSize.Should().BeGreaterThan(0);
            mount.TotalFreeSpace.Should().BeInRange(0, mount.TotalSize);
            mount.AvailableFreeSpace.Should().BeInRange(0, mount.TotalFreeSpace);
        }

        [Test]
        public void should_use_device_name_as_volume_name()
        {
            var mount = GivenMount("/");

            mount.VolumeLabel.Should().BeEmpty();
            mount.VolumeName.Should().Be("/dev/test");
        }

        [Test]
        public void should_not_be_ready_when_mount_point_is_missing()
        {
            var mount = GivenMount(Path.Combine(GetTempFilePath(), "missing"));

            mount.IsReady.Should().BeFalse();
        }
    }
}
