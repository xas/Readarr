using System.IO;
using FluentAssertions;
using Mono.Unix;
using NUnit.Framework;
using NzbDrone.Mono.Disk;
using NzbDrone.Test.Common;

namespace NzbDrone.Mono.Test.DiskProviderTests
{
    [TestFixture]
    [Platform(Exclude = "Win")]
    public class SymbolicLinkResolverFixture : TestBase<SymbolicLinkResolver>
    {
        [Test]
        public void should_follow_nested_symlinks()
        {
            var rootDir = GetTempFilePath();
            var tempDir1 = Path.Combine(rootDir, "dir1");
            var tempDir2 = Path.Combine(rootDir, "dir2");
            var subDir1 = Path.Combine(tempDir1, "subdir1");
            var file1 = Path.Combine(tempDir2, "file1");
            var file2 = Path.Combine(tempDir2, "file2");

            Directory.CreateDirectory(tempDir1);
            Directory.CreateDirectory(tempDir2);
            File.WriteAllText(file2, "test");

            new UnixSymbolicLinkInfo(subDir1).CreateSymbolicLinkTo("../dir2");
            new UnixSymbolicLinkInfo(file1).CreateSymbolicLinkTo("file2");

            var realPath = Subject.GetCompleteRealPath(Path.Combine(subDir1, "file1"));

            realPath.Should().Be(file2);
        }

        [Test]
        public void should_throw_on_infinite_loop()
        {
            var rootDir = GetTempFilePath();
            var tempDir1 = Path.Combine(rootDir, "dir1");
            var subDir1 = Path.Combine(tempDir1, "subdir1");
            var file1 = Path.Combine(tempDir1, "file1");

            Directory.CreateDirectory(tempDir1);

            new UnixSymbolicLinkInfo(subDir1).CreateSymbolicLinkTo("../../dir1/subdir1/baddir");

            var realPath = Subject.GetCompleteRealPath(file1);

            realPath.Should().Be(file1);
        }

        [Test]
        public void should_resolve_symlinked_parent_of_missing_path()
        {
            var rootDir = GetTempFilePath();
            var realDir = Path.Combine(rootDir, "real");
            var linkDir = Path.Combine(rootDir, "link");

            Directory.CreateDirectory(realDir);
            Directory.CreateSymbolicLink(linkDir, realDir);

            var realPath = Subject.GetCompleteRealPath(Path.Combine(linkDir, "missing", "file"));

            realPath.Should().Be(Path.Combine(realDir, "missing", "file"));
        }

        [Test]
        public void should_follow_dangling_symlink()
        {
            var rootDir = GetTempFilePath();
            var link = Path.Combine(rootDir, "link");
            var target = Path.Combine(rootDir, "missing");

            Directory.CreateDirectory(rootDir);
            File.CreateSymbolicLink(link, "missing");

            var realPath = Subject.GetCompleteRealPath(link);

            realPath.Should().Be(target);
        }

        [Test]
        public void should_return_original_path_on_symlink_loop()
        {
            var rootDir = GetTempFilePath();
            var linkA = Path.Combine(rootDir, "a");
            var linkB = Path.Combine(rootDir, "b");
            var path = Path.Combine(linkA, "file");

            Directory.CreateDirectory(rootDir);
            Directory.CreateSymbolicLink(linkA, "b");
            Directory.CreateSymbolicLink(linkB, "a");

            var realPath = Subject.GetCompleteRealPath(path);

            realPath.Should().Be(path);
            ExceptionVerification.ExpectedWarns(1);
        }
    }
}
