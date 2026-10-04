using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Common.Disk;
using NzbDrone.Common.EnvironmentInfo;
using NzbDrone.Common.Test.DiskTests;
using NzbDrone.Mono.Disk;

namespace NzbDrone.Mono.Test.DiskProviderTests
{
    [TestFixture]
    [Platform(Exclude = "Win")]
    public class DiskProviderFixture : DiskProviderFixtureBase<DiskProvider>
    {
        private string _tempPath;

        public DiskProviderFixture()
        {
            PosixOnly();
        }

        [TearDown]
        public void MonoDiskProviderFixtureTearDown()
        {
            // Give ourselves back write permissions so we can delete it
            if (_tempPath != null)
            {
                if (Directory.Exists(_tempPath))
                {
                    File.SetUnixFileMode(_tempPath, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
                }
                else if (File.Exists(_tempPath))
                {
                    File.SetUnixFileMode(_tempPath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
                }

                _tempPath = null;
            }
        }

        protected override void SetWritePermissions(string path, bool writable)
        {
            if (Environment.UserName == "root")
            {
                Assert.Inconclusive("Need non-root user to test write permissions.");
            }

            SetWritePermissionsInternal(path, writable, false);
        }

        protected void SetWritePermissionsInternal(string path, bool writable, bool setgid)
        {
            // Remove Write permissions, we're still owner so we can clean it up, but we'll have to do that explicitly.
            var currentMode = File.GetUnixFileMode(path);
            var mode = currentMode;

            if (writable)
            {
                mode |= UnixFileMode.UserWrite | UnixFileMode.GroupWrite | UnixFileMode.OtherWrite;
            }
            else
            {
                mode &= ~(UnixFileMode.UserWrite | UnixFileMode.GroupWrite | UnixFileMode.OtherWrite);
            }

            if (setgid)
            {
                mode |= UnixFileMode.SetGroup;
            }
            else
            {
                mode &= ~UnixFileMode.SetGroup;
            }

            if (currentMode != mode)
            {
                File.SetUnixFileMode(path, mode);
            }
        }

        // Same format as Mono's NativeConvert.ToOctalPermissionString: 4 octal digits, e.g. "0644" or "2775"
        private static string GetMode(string path)
        {
            return Convert.ToString((int)File.GetUnixFileMode(path), 8).PadLeft(4, '0');
        }

        private static string Run(string command, string args)
        {
            using var process = Process.Start(new ProcessStartInfo(command, args) { RedirectStandardOutput = true });
            var output = process.StandardOutput.ReadToEnd().Trim();
            process.WaitForExit();
            return output;
        }

        // .NET has no API for a file's group: `ls -n` prints numeric ids, the same on glibc, busybox and macOS
        private static uint GetGroupId(string path)
        {
            return uint.Parse(Run("ls", $"-nd \"{path}\"").Split(' ', StringSplitOptions.RemoveEmptyEntries)[3]);
        }

        [Test]
        public void should_move_symlink()
        {
            var tempFolder = GetTempFilePath();
            Directory.CreateDirectory(tempFolder);

            var file = Path.Combine(tempFolder, "target.txt");
            var source = Path.Combine(tempFolder, "symlink_source.txt");
            var destination = Path.Combine(tempFolder, "symlink_destination.txt");

            File.WriteAllText(file, "Some content");

            File.CreateSymbolicLink(source, file);

            Subject.MoveFile(source, destination);

            File.Exists(file).Should().BeTrue();
            File.Exists(source).Should().BeFalse();
            File.Exists(destination).Should().BeTrue();
            new FileInfo(destination).LinkTarget.Should().NotBeNull();

            File.ReadAllText(destination).Should().Be("Some content");
        }

        [Test]
        public void should_copy_symlink()
        {
            var tempFolder = GetTempFilePath();
            Directory.CreateDirectory(tempFolder);

            var file = Path.Combine(tempFolder, "target.txt");
            var source = Path.Combine(tempFolder, "symlink_source.txt");
            var destination = Path.Combine(tempFolder, "symlink_destination.txt");

            File.WriteAllText(file, "Some content");

            File.CreateSymbolicLink(source, file);

            Subject.CopyFile(source, destination);

            File.Exists(file).Should().BeTrue();
            File.Exists(source).Should().BeTrue();
            File.Exists(destination).Should().BeTrue();
            new FileInfo(source).LinkTarget.Should().NotBeNull();
            new FileInfo(destination).LinkTarget.Should().NotBeNull();

            File.ReadAllText(source).Should().Be("Some content");
            File.ReadAllText(destination).Should().Be("Some content");
        }

        [Test]
        public void should_keep_relative_symlink_when_copied_in_same_folder()
        {
            var tempFolder = GetTempFilePath();
            Directory.CreateDirectory(tempFolder);

            var source = Path.Combine(tempFolder, "symlink_source.txt");
            var destination = Path.Combine(tempFolder, "symlink_destination.txt");

            File.WriteAllText(Path.Combine(tempFolder, "target.txt"), "Some content");
            File.CreateSymbolicLink(source, "target.txt");

            Subject.CopyFile(source, destination);

            new FileInfo(destination).LinkTarget.Should().Be("target.txt");
            File.ReadAllText(destination).Should().Be("Some content");
        }

        [Test]
        public void should_make_relative_symlink_absolute_when_moved_to_other_folder()
        {
            var tempFolder = GetTempFilePath();
            var otherFolder = Path.Combine(tempFolder, "other");
            Directory.CreateDirectory(otherFolder);

            var file = Path.Combine(tempFolder, "target.txt");
            var source = Path.Combine(tempFolder, "symlink_source.txt");
            var destination = Path.Combine(otherFolder, "symlink_destination.txt");

            File.WriteAllText(file, "Some content");
            File.CreateSymbolicLink(source, "target.txt");

            Subject.MoveFile(source, destination);

            File.Exists(source).Should().BeFalse();
            new FileInfo(destination).LinkTarget.Should().Be(file);
            File.ReadAllText(destination).Should().Be("Some content");
        }

        [Test]
        public void should_copy_symlink_when_cloning()
        {
            var tempFolder = GetTempFilePath();
            Directory.CreateDirectory(tempFolder);

            var file = Path.Combine(tempFolder, "target.txt");
            var source = Path.Combine(tempFolder, "symlink_source.txt");
            var destination = Path.Combine(tempFolder, "symlink_destination.txt");

            File.WriteAllText(file, "Some content");
            File.CreateSymbolicLink(source, file);

            Subject.CloneFile(source, destination);

            Mocker.GetMock<IRefLinkCreator>().Verify(v => v.TryCreateRefLink(It.IsAny<string>(), It.IsAny<string>()), Times.Never());
            new FileInfo(destination).LinkTarget.Should().Be(file);
        }

        [Test]
        public void should_rename_file()
        {
            var source = GetTempFilePath();
            var destination = GetTempFilePath();
            File.WriteAllText(source, "Some content");

            Subject.TryRenameFile(source, destination).Should().BeTrue();

            File.Exists(source).Should().BeFalse();
            File.ReadAllText(destination).Should().Be("Some content");
        }

        [Test]
        public void should_return_false_when_renaming_missing_file()
        {
            Subject.TryRenameFile(GetTempFilePath(), GetTempFilePath()).Should().BeFalse();
        }

        [Test]
        public void should_not_hardlink_symlink()
        {
            var tempFolder = GetTempFilePath();
            Directory.CreateDirectory(tempFolder);

            var file = Path.Combine(tempFolder, "target.txt");
            var source = Path.Combine(tempFolder, "symlink_source.txt");
            var destination = Path.Combine(tempFolder, "hardlink_destination.txt");

            File.WriteAllText(file, "Some content");
            File.CreateSymbolicLink(source, file);

            Subject.TryCreateHardLink(source, destination).Should().BeFalse();

            Path.Exists(destination).Should().BeFalse();
        }

        [Test]
        public void should_return_false_when_hardlink_destination_exists()
        {
            var source = GetTempFilePath();
            var destination = GetTempFilePath();
            File.WriteAllText(source, "Some content");
            File.WriteAllText(destination, "Other content");

            Subject.TryCreateHardLink(source, destination).Should().BeFalse();

            File.ReadAllText(destination).Should().Be("Other content");
        }

        [Test]
        public void should_not_overwrite_dangling_symlink_when_moving()
        {
            var source = GetTempFilePath();
            var destination = GetTempFilePath();
            File.WriteAllText(source, "Some content");
            File.CreateSymbolicLink(destination, "/nonexistent/target");

            Assert.Throws<FileAlreadyExistsException>(() => Subject.MoveFile(source, destination));

            File.ReadAllText(source).Should().Be("Some content");
            new FileInfo(destination).LinkTarget.Should().Be("/nonexistent/target");
        }

        private void GivenSpecialMount(string rootDir)
        {
            Mocker.GetMock<ISymbolicLinkResolver>()
                .Setup(v => v.GetCompleteRealPath(It.IsAny<string>()))
                .Returns<string>(s => s);

            Mocker.GetMock<IProcMountProvider>()
                .Setup(v => v.GetMounts())
                .Returns(new List<IMount>
                {
                    new ProcMount(DriveType.Fixed, rootDir, rootDir, "myfs", new MountOptions(new Dictionary<string, string>()))
                });
        }

        [TestCase("/snap/blaat")]
        [TestCase("/var/lib/docker/zfs-storage-mount")]
        public void should_ignore_special_mounts(string rootDir)
        {
            GivenSpecialMount(rootDir);

            var mounts = Subject.GetMounts();

            mounts.Select(d => d.RootDirectory).Should().NotContain(rootDir);
        }

        [TestCase("/snap/blaat")]
        [TestCase("/var/lib/docker/zfs-storage-mount")]
        public void should_return_special_mount_when_queried(string rootDir)
        {
            GivenSpecialMount(rootDir);

            var mount = Subject.GetMount(Path.Combine(rootDir, "dir/somefile.mkv"));

            mount.Should().NotBeNull();
            mount.RootDirectory.Should().Be(rootDir);
        }

        [Test]
        public void should_copy_folder_permissions()
        {
            var src = GetTempFilePath();
            var dst = GetTempFilePath();

            Directory.CreateDirectory(src);

            // Toggle one of the permission flags
            var origMode = File.GetUnixFileMode(src);
            File.SetUnixFileMode(src, origMode ^ UnixFileMode.GroupWrite);

            // Verify test setup
            var srcMode = File.GetUnixFileMode(src);
            srcMode.Should().NotBe(origMode);

            Subject.CreateFolder(dst);

            // Verify test setup
            File.GetUnixFileMode(dst).Should().Be(origMode);

            Subject.CopyPermissions(src, dst);

            // Verify CopyPermissions
            File.GetUnixFileMode(dst).Should().Be(srcMode);
        }

        [Test]
        public void should_set_file_permissions()
        {
            var tempFile = GetTempFilePath();

            File.WriteAllText(tempFile, "File1");
            SetWritePermissionsInternal(tempFile, false, false);
            _tempPath = tempFile;

            // Verify test setup
            GetMode(tempFile).Should().Be("0444");

            Subject.SetPermissions(tempFile, "755", null);
            GetMode(tempFile).Should().Be("0644");

            Subject.SetPermissions(tempFile, "0755", null);
            GetMode(tempFile).Should().Be("0644");

            if (OsInfo.Os != Os.Bsd)
            {
                // This is not allowed on BSD
                Subject.SetPermissions(tempFile, "1775", null);
                GetMode(tempFile).Should().Be("1664");
            }
        }

        [Test]
        public void should_set_folder_permissions()
        {
            var tempPath = GetTempFilePath();

            Directory.CreateDirectory(tempPath);
            SetWritePermissionsInternal(tempPath, false, false);
            _tempPath = tempPath;

            // Verify test setup
            GetMode(tempPath).Should().Be("0555");

            Subject.SetPermissions(tempPath, "755", null);
            GetMode(tempPath).Should().Be("0755");

            Subject.SetPermissions(tempPath, "775", null);
            GetMode(tempPath).Should().Be("0775");

            Subject.SetPermissions(tempPath, "750", null);
            GetMode(tempPath).Should().Be("0750");

            Subject.SetPermissions(tempPath, "051", null);
            GetMode(tempPath).Should().Be("0051");
        }

        [Test]
        public void should_preserve_setgid_on_set_folder_permissions()
        {
            var tempPath = GetTempFilePath();

            Directory.CreateDirectory(tempPath);
            SetWritePermissionsInternal(tempPath, false, true);
            _tempPath = tempPath;

            // Verify test setup
            GetMode(tempPath).Should().Be("2555");

            Subject.SetPermissions(tempPath, "755", null);
            GetMode(tempPath).Should().Be("2755");

            Subject.SetPermissions(tempPath, "775", null);
            GetMode(tempPath).Should().Be("2775");

            Subject.SetPermissions(tempPath, "750", null);
            GetMode(tempPath).Should().Be("2750");

            Subject.SetPermissions(tempPath, "051", null);
            GetMode(tempPath).Should().Be("2051");
        }

        [Test]
        public void should_clear_setgid_on_set_folder_permissions()
        {
            var tempPath = GetTempFilePath();

            Directory.CreateDirectory(tempPath);
            SetWritePermissionsInternal(tempPath, false, true);
            _tempPath = tempPath;

            // Verify test setup
            GetMode(tempPath).Should().Be("2555");

            Subject.SetPermissions(tempPath, "0755", null);
            GetMode(tempPath).Should().Be("0755");

            Subject.SetPermissions(tempPath, "0775", null);
            GetMode(tempPath).Should().Be("0775");

            Subject.SetPermissions(tempPath, "0750", null);
            GetMode(tempPath).Should().Be("0750");

            Subject.SetPermissions(tempPath, "0051", null);
            GetMode(tempPath).Should().Be("0051");
        }

        [Test]
        public void should_set_group_by_name()
        {
            var tempFile = GetTempFilePath();
            File.WriteAllText(tempFile, "File1");

            // Last group of the current user: a supplementary group if there is one, so the group really changes
            var name = Run("id", "-Gn").Split(' ').Last();
            var expected = uint.Parse(Run("id", "-G").Split(' ').Last());

            Subject.SetPermissions(tempFile, "755", name);

            GetGroupId(tempFile).Should().Be(expected);
            GetMode(tempFile).Should().Be("0644");
        }

        [Test]
        public void should_throw_for_unknown_group()
        {
            var tempFile = GetTempFilePath();
            File.WriteAllText(tempFile, "File1");

            Assert.Throws<LinuxPermissionsException>(() => Subject.SetPermissions(tempFile, "755", "readarr-unknown-group"));
        }

        [Test]
        public void IsValidFolderPermissionMask_should_return_correct()
        {
            // No special bits should be set
            Subject.IsValidFolderPermissionMask("1755").Should().BeFalse();
            Subject.IsValidFolderPermissionMask("2755").Should().BeFalse();
            Subject.IsValidFolderPermissionMask("4755").Should().BeFalse();
            Subject.IsValidFolderPermissionMask("7755").Should().BeFalse();

            // Folder should be readable and writeable by owner
            Subject.IsValidFolderPermissionMask("000").Should().BeFalse();
            Subject.IsValidFolderPermissionMask("100").Should().BeFalse();
            Subject.IsValidFolderPermissionMask("200").Should().BeFalse();
            Subject.IsValidFolderPermissionMask("300").Should().BeFalse();
            Subject.IsValidFolderPermissionMask("400").Should().BeFalse();
            Subject.IsValidFolderPermissionMask("500").Should().BeFalse();
            Subject.IsValidFolderPermissionMask("600").Should().BeFalse();
            Subject.IsValidFolderPermissionMask("700").Should().BeTrue();

            // Folder should be readable and writeable by owner
            Subject.IsValidFolderPermissionMask("0000").Should().BeFalse();
            Subject.IsValidFolderPermissionMask("0100").Should().BeFalse();
            Subject.IsValidFolderPermissionMask("0200").Should().BeFalse();
            Subject.IsValidFolderPermissionMask("0300").Should().BeFalse();
            Subject.IsValidFolderPermissionMask("0400").Should().BeFalse();
            Subject.IsValidFolderPermissionMask("0500").Should().BeFalse();
            Subject.IsValidFolderPermissionMask("0600").Should().BeFalse();
            Subject.IsValidFolderPermissionMask("0700").Should().BeTrue();
        }
    }
}
