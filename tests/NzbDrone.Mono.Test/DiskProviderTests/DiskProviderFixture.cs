using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using FluentAssertions;
using Mono.Unix;
using Mono.Unix.Native;
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
                    Syscall.chmod(_tempPath, FilePermissions.S_IRWXU);
                }
                else if (File.Exists(_tempPath))
                {
                    Syscall.chmod(_tempPath, FilePermissions.S_IRUSR | FilePermissions.S_IWUSR);
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
            Syscall.stat(path, out var stat);
            var mode = stat.st_mode;

            if (writable)
            {
                mode |= FilePermissions.S_IWUSR | FilePermissions.S_IWGRP | FilePermissions.S_IWOTH;
            }
            else
            {
                mode &= ~(FilePermissions.S_IWUSR | FilePermissions.S_IWGRP | FilePermissions.S_IWOTH);
            }

            if (setgid)
            {
                mode |= FilePermissions.S_ISGID;
            }
            else
            {
                mode &= ~FilePermissions.S_ISGID;
            }

            if (stat.st_mode != mode)
            {
                if (Syscall.chmod(path, mode) < 0)
                {
                    var error = Stdlib.GetLastError();
                    throw new LinuxPermissionsException("Error setting group: " + error);
                }
            }
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

            new UnixSymbolicLinkInfo(source).CreateSymbolicLinkTo(file);

            Subject.MoveFile(source, destination);

            File.Exists(file).Should().BeTrue();
            File.Exists(source).Should().BeFalse();
            File.Exists(destination).Should().BeTrue();
            UnixFileSystemInfo.GetFileSystemEntry(destination).IsSymbolicLink.Should().BeTrue();

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

            new UnixSymbolicLinkInfo(source).CreateSymbolicLinkTo(file);

            Subject.CopyFile(source, destination);

            File.Exists(file).Should().BeTrue();
            File.Exists(source).Should().BeTrue();
            File.Exists(destination).Should().BeTrue();
            UnixFileSystemInfo.GetFileSystemEntry(source).IsSymbolicLink.Should().BeTrue();
            UnixFileSystemInfo.GetFileSystemEntry(destination).IsSymbolicLink.Should().BeTrue();

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
            Syscall.stat(src, out var origStat);
            Syscall.chmod(src, origStat.st_mode ^ FilePermissions.S_IWGRP);

            // Verify test setup
            Syscall.stat(src, out var srcStat);
            srcStat.st_mode.Should().NotBe(origStat.st_mode);

            Subject.CreateFolder(dst);

            // Verify test setup
            Syscall.stat(dst, out var dstStat);
            dstStat.st_mode.Should().Be(origStat.st_mode);

            Subject.CopyPermissions(src, dst);

            // Verify CopyPermissions
            Syscall.stat(dst, out dstStat);
            dstStat.st_mode.Should().Be(srcStat.st_mode);
        }

        [Test]
        public void should_set_file_permissions()
        {
            var tempFile = GetTempFilePath();

            File.WriteAllText(tempFile, "File1");
            SetWritePermissionsInternal(tempFile, false, false);
            _tempPath = tempFile;

            // Verify test setup
            Syscall.stat(tempFile, out var fileStat);
            NativeConvert.ToOctalPermissionString(fileStat.st_mode).Should().Be("0444");

            Subject.SetPermissions(tempFile, "755", null);
            Syscall.stat(tempFile, out fileStat);
            NativeConvert.ToOctalPermissionString(fileStat.st_mode).Should().Be("0644");

            Subject.SetPermissions(tempFile, "0755", null);
            Syscall.stat(tempFile, out fileStat);
            NativeConvert.ToOctalPermissionString(fileStat.st_mode).Should().Be("0644");

            if (OsInfo.Os != Os.Bsd)
            {
                // This is not allowed on BSD
                Subject.SetPermissions(tempFile, "1775", null);
                Syscall.stat(tempFile, out fileStat);
                NativeConvert.ToOctalPermissionString(fileStat.st_mode).Should().Be("1664");
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
            Syscall.stat(tempPath, out var fileStat);
            NativeConvert.ToOctalPermissionString(fileStat.st_mode).Should().Be("0555");

            Subject.SetPermissions(tempPath, "755", null);
            Syscall.stat(tempPath, out fileStat);
            NativeConvert.ToOctalPermissionString(fileStat.st_mode).Should().Be("0755");

            Subject.SetPermissions(tempPath, "775", null);
            Syscall.stat(tempPath, out fileStat);
            NativeConvert.ToOctalPermissionString(fileStat.st_mode).Should().Be("0775");

            Subject.SetPermissions(tempPath, "750", null);
            Syscall.stat(tempPath, out fileStat);
            NativeConvert.ToOctalPermissionString(fileStat.st_mode).Should().Be("0750");

            Subject.SetPermissions(tempPath, "051", null);
            Syscall.stat(tempPath, out fileStat);
            NativeConvert.ToOctalPermissionString(fileStat.st_mode).Should().Be("0051");
        }

        [Test]
        public void should_preserve_setgid_on_set_folder_permissions()
        {
            var tempPath = GetTempFilePath();

            Directory.CreateDirectory(tempPath);
            SetWritePermissionsInternal(tempPath, false, true);
            _tempPath = tempPath;

            // Verify test setup
            Syscall.stat(tempPath, out var fileStat);
            NativeConvert.ToOctalPermissionString(fileStat.st_mode).Should().Be("2555");

            Subject.SetPermissions(tempPath, "755", null);
            Syscall.stat(tempPath, out fileStat);
            NativeConvert.ToOctalPermissionString(fileStat.st_mode).Should().Be("2755");

            Subject.SetPermissions(tempPath, "775", null);
            Syscall.stat(tempPath, out fileStat);
            NativeConvert.ToOctalPermissionString(fileStat.st_mode).Should().Be("2775");

            Subject.SetPermissions(tempPath, "750", null);
            Syscall.stat(tempPath, out fileStat);
            NativeConvert.ToOctalPermissionString(fileStat.st_mode).Should().Be("2750");

            Subject.SetPermissions(tempPath, "051", null);
            Syscall.stat(tempPath, out fileStat);
            NativeConvert.ToOctalPermissionString(fileStat.st_mode).Should().Be("2051");
        }

        [Test]
        public void should_clear_setgid_on_set_folder_permissions()
        {
            var tempPath = GetTempFilePath();

            Directory.CreateDirectory(tempPath);
            SetWritePermissionsInternal(tempPath, false, true);
            _tempPath = tempPath;

            // Verify test setup
            Syscall.stat(tempPath, out var fileStat);
            NativeConvert.ToOctalPermissionString(fileStat.st_mode).Should().Be("2555");

            Subject.SetPermissions(tempPath, "0755", null);
            Syscall.stat(tempPath, out fileStat);
            NativeConvert.ToOctalPermissionString(fileStat.st_mode).Should().Be("0755");

            Subject.SetPermissions(tempPath, "0775", null);
            Syscall.stat(tempPath, out fileStat);
            NativeConvert.ToOctalPermissionString(fileStat.st_mode).Should().Be("0775");

            Subject.SetPermissions(tempPath, "0750", null);
            Syscall.stat(tempPath, out fileStat);
            NativeConvert.ToOctalPermissionString(fileStat.st_mode).Should().Be("0750");

            Subject.SetPermissions(tempPath, "0051", null);
            Syscall.stat(tempPath, out fileStat);
            NativeConvert.ToOctalPermissionString(fileStat.st_mode).Should().Be("0051");
        }

        private static string Id(string args)
        {
            using var process = Process.Start(new ProcessStartInfo("id", args) { RedirectStandardOutput = true });
            var output = process.StandardOutput.ReadToEnd().Trim();
            process.WaitForExit();
            return output;
        }

        [Test]
        public void should_set_group_by_name()
        {
            var tempFile = GetTempFilePath();
            File.WriteAllText(tempFile, "File1");

            // Last group of the current user: a supplementary group if there is one, so the group really changes
            var name = Id("-Gn").Split(' ').Last();
            var expected = uint.Parse(Id("-G").Split(' ').Last());

            Subject.SetPermissions(tempFile, "755", name);

            Syscall.stat(tempFile, out var fileStat);
            fileStat.st_gid.Should().Be(expected);
            NativeConvert.ToOctalPermissionString(fileStat.st_mode).Should().Be("0644");
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
