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

        [Test]
        public void should_return_false_when_source_is_missing()
        {
            var destination = GetTempFilePath();

            Subject.TryCreateRefLink(GetTempFilePath(), destination).Should().BeFalse();

            File.Exists(destination).Should().BeFalse();
        }

        [Test]
        public void should_not_delete_existing_destination_when_source_is_missing()
        {
            var destination = GetTempFilePath();
            File.WriteAllText(destination, "existing");

            Subject.TryCreateRefLink(GetTempFilePath(), destination).Should().BeFalse();

            File.ReadAllText(destination).Should().Be("existing");
        }

        [Test]
        [Platform("Linux")]
        public void should_remove_link_file_when_filesystem_does_not_support_reflinks()
        {
            // tmpfs has no FICLONE support
            Assume.That(Directory.Exists("/dev/shm"));

            var folder = Path.Combine("/dev/shm", Path.GetRandomFileName());
            Directory.CreateDirectory(folder);

            try
            {
                var source = Path.Combine(folder, "source");
                var destination = Path.Combine(folder, "destination");
                File.WriteAllText(source, "test");

                Subject.TryCreateRefLink(source, destination).Should().BeFalse();

                File.Exists(destination).Should().BeFalse();
            }
            finally
            {
                Directory.Delete(folder, true);
            }
        }
    }
}
