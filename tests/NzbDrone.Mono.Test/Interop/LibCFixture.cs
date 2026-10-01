using System.Diagnostics;
using System.IO;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Mono.Interop;
using NzbDrone.Test.Common;

namespace NzbDrone.Mono.Test.Interop
{
    [TestFixture]
    [Platform(Exclude = "Win")]
    public class LibCFixture : TestBase
    {
        private const int EEXIST = 17;

        private static string Id(string args)
        {
            using var process = Process.Start(new ProcessStartInfo("id", args) { RedirectStandardOutput = true });
            var output = process.StandardOutput.ReadToEnd().Trim();
            process.WaitForExit();
            return output;
        }

        [Test]
        public void should_get_group_id_of_current_user_group()
        {
            // Primary group of the current user, usually not 0, so a wrong struct offset shows up
            var name = Id("-gn");
            var expected = uint.Parse(Id("-g"));

            LibC.TryGetGroupId(name, out var groupId).Should().BeTrue();
            groupId.Should().Be(expected);
        }

        [Test]
        public void should_not_get_group_id_of_unknown_group()
        {
            LibC.TryGetGroupId("readarr-unknown-group", out _).Should().BeFalse();
        }

        [Test]
        public void should_rename_file()
        {
            var source = GetTempFilePath();
            var destination = GetTempFilePath();
            File.WriteAllText(source, "test");

            LibC.Rename(source, destination).Should().Be(0);

            File.Exists(source).Should().BeFalse();
            File.ReadAllText(destination).Should().Be("test");
        }

        [Test]
        public void should_create_hard_link()
        {
            var source = GetTempFilePath();
            var destination = GetTempFilePath();
            File.WriteAllText(source, "test");

            LibC.Link(source, destination).Should().Be(0);

            File.AppendAllText(source, "-changed");
            File.ReadAllText(destination).Should().Be("test-changed");
        }

        [Test]
        public void should_return_error_when_hard_link_destination_exists()
        {
            var source = GetTempFilePath();
            var destination = GetTempFilePath();
            File.WriteAllText(source, "test");
            File.WriteAllText(destination, "other");

            LibC.Link(source, destination).Should().Be(-1);
            LibC.LastError.Should().Be(EEXIST);
        }

        [Test]
        public void should_chown_with_unchanged_ids()
        {
            var file = GetTempFilePath();
            File.WriteAllText(file, "test");

            LibC.Chown(file, uint.MaxValue, uint.MaxValue).Should().Be(0);
        }
    }
}
