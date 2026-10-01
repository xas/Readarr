using System.IO;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Mono.Disk;
using NzbDrone.Test.Common;

namespace NzbDrone.Mono.Test.DiskProviderTests
{
    [TestFixture]
    [Platform(Exclude = "Win")]
    public class RefLinkCreatorFixture : TestBase<RefLinkCreator>
    {
        [Test]
        public void should_create_reflink_or_leave_no_file()
        {
            var source = GetTempFilePath();
            var destination = GetTempFilePath();
            File.WriteAllText(source, "test");

            // Reflinks need btrfs or xfs (not tmpfs or ext4)
            if (Subject.TryCreateRefLink(source, destination))
            {
                File.ReadAllText(destination).Should().Be("test");
            }
            else
            {
                File.Exists(destination).Should().BeFalse();
            }
        }
    }
}
