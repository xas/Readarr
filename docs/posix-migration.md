# Mono.Posix migration

Replace `Mono.Posix.NETStandard` in `Readarr.Mono` with .NET APIs and a few direct libc calls.

**Why:** the package is a Servarr fork (`5.20.1.34-servarr24`) that is only published on the retired upstream's Azure DevOps feed (`NuGet.config`). It also ships a native `libMonoPosixHelper` for every runtime, which `build.sh` copies by hand into the update package. Most of what it provides is built into .NET since version 6/7 (`UnixFileMode`, `File.CreateSymbolicLink`, `LinkTarget`, `DriveInfo`).

**Scope:** the `Readarr.Mono` project stays. It is the Unix disk, mount and OS-version layer, loaded at runtime by `AssemblyLoader`. Only its dependency on Mono.Posix goes away.

**Test setup**
- CachyOS (glibc; the repo, and so the test temp files, is on an NFS 4.2 mount): `dotnet test tests/NzbDrone.Mono.Test/Readarr.Mono.Test.csproj -p:Platform=Posix`.
- Alpine 3.24 (musl): podman, `mcr.microsoft.com/dotnet/sdk:10.0-alpine`, run on a copy of the sources with `-p:RuntimeIdentifier=linux-musl-x64`.

Test temp files are created under `_tests/net10.0/_temp_*` (the test output folder), not in `TMPDIR`.

## Status

| Step | Topic | Status | Commit |
|---|---|---|---|
| 1 | Baseline | Done | `54416e274` |
| 2 | `LibC` interop class | Done | `54416e274` |
| 3 | Permissions | Done | `051d00aef` |
| 4 | Symlinks in copy, move and clone | Done | `e910cd9f9` |
| 5 | Rename and hard link | Done (5b `TransferFilePatched` removal skipped) | `430441069` |
| 6 | `SymbolicLinkResolver` | Done | `18330b35f` |
| 7 | `ProcMount` (`UnixDriveInfo`) | Done | `47f773624` |
| 8 | `RefLinkCreator` / `SafeUnixHandle` | Done | `e1cbeea22` |
| 9 | Tests off `Mono.Unix` | Done | `d72911dc0` |
| 10 | Remove package, feed and `build.sh` lines | Done | not committed |
| 11 | Final checks | Done | not committed |

## Step 1: baseline

**What changed:** nothing; the Mono tests were run before any change.

**Result (CachyOS):** 50 pass, 3 fail, 5 skipped. The 3 failures were already there and none of them involve Mono.Posix:
- `should_get_version_info` (×2, `ReleaseFileVersionAdapterFixture`): CachyOS is a rolling release, so `/etc/os-release` has no `VERSION_ID`.
- `should_return_true_for_unlocked_file`: it tests `IsFileLocked` in `Readarr.Common`, which doesn't use Mono.Posix.

**Found on the way (Alpine):** a plain `dotnet test` crashes the test host on Alpine. `Directory.Build.props:192` defaults `RuntimeIdentifier` to `linux-$(Architecture)` (glibc) even on musl. The build therefore copies the glibc `libMonoPosixHelper.so`, and the first Mono.Posix permission call crashes. With `-p:RuntimeIdentifier=linux-musl-x64` it works. Release builds aren't affected, because `build.sh` passes `-r linux-musl-x64`. The problem disappears once Mono.Posix is removed.

## Step 2: `LibC` interop class

**What changed**
- `src/NzbDrone.Mono/Interop/LibC.cs` (new) declares, with `[LibraryImport("libc", SetLastError = true)]`, the calls .NET has no equivalent for:
  - `Rename`: unlike `File.Move`, it never falls back to copy + delete.
  - `Link`: hard link.
  - `Chown`: `uint.MaxValue` (-1) leaves the owner or group unchanged.
  - `Ioctl`: used for reflinks.
  - `getgrnam`, wrapped in `TryGetGroupId(name, out gid)`:
    - It holds a lock, because libc returns a shared buffer.
    - It reads `gr_gid` at offset `2 * IntPtr.Size`. `struct group` has the same layout on glibc, musl, macOS and FreeBSD.
  - Helpers: `EXDEV` (18, the same everywhere), `LastError` and `LastErrorMessage` (from `Marshal.GetLastPInvokeError` / `GetPInvokeErrorMessage`).
- `Interop/NativeMethods.cs`: the reflink `ioctl` now calls `LibC.Ioctl`; its own `DllImport` is gone.
- `Readarr.Mono.csproj`: `<AllowUnsafeBlocks>true</AllowUnsafeBlocks>`, which the `LibraryImport` source generator needs.
- `src/NzbDrone.Mono/Properties/AssemblyInfo.cs` (new): `InternalsVisibleTo("Readarr.Mono.Test")`. The csproj item doesn't work here because `GenerateAssemblyInfo` is off for the whole repo; `NzbDrone.Core` uses the same file.

**Behaviour difference:** none. Only the reflink `ioctl` uses the new class so far, with the same arguments.

**Tests**
- `tests/NzbDrone.Mono.Test/Interop/LibCFixture.cs` (6 tests):
  - the group id of the current user's primary group matches `id -g`. A non-zero gid would expose a wrong read position.
  - an unknown group is rejected.
  - rename works.
  - a hard link really shares the file contents.
  - linking onto an existing file returns -1 with `EEXIST` (17).
  - `chown` with "unchanged" ids succeeds.
- `tests/NzbDrone.Mono.Test/DiskProviderTests/RefLinkCreatorFixture.cs`: the first test of the reflink code. Either the reflink is created with the right contents, or no destination file is left behind.
  - In the repo, which is on NFS 4.2, a real reflink is created (the NFS server does the clone); this was checked once with a temporary print. An earlier note said xfs: that was wrong.
  - On Alpine it falls back cleanly (`EOPNOTSUPP` on tmpfs).
- **CachyOS:** 56 pass, the same 3 failures.
- **Alpine/musl:** all `LibC` tests pass, also as gid 100 (`--user 0:100`), which checks the `gr_gid` position on musl. musl answers any request for `libc.*` with itself, so `"libc"` loads.

## Step 3: permissions

**What changed** (`src/NzbDrone.Mono/Disk/DiskProvider.cs`)
- `ParsePermissions(mask)` = `(UnixFileMode)Convert.ToUInt32(mask, 8)`. This is what Mono's `NativeConvert.FromOctalPermissionString` did, so invalid input still throws `FormatException`.
- `SetPermissions` / `SetFilePermissions`:
  - The mode is read and written with `_fileSystem.File.GetUnixFileMode` / `SetUnixFileMode` (System.IO.Abstractions; works on folders too).
  - The setuid, setgid and sticky bits are kept when the mask has 3 digits and cleared when it has 4, as before.
- New constants: `AccessPermissions` (0777) and `OwnerPermissions` (0700), replacing `FilePermissions.ACCESSPERMS` and `S_IRWXU`.
- `GetFilePermissions` removes the execute bits with `UnixFileMode` flags.
- The group is set with `LibC.TryGetGroupId` + `LibC.Chown`. An unknown group still throws `LinuxPermissionsException`.
- `IsValidFolderPermissionMask` keeps the same rules: no special bits, and at least 700.
- `CopyPermissions` uses `GetUnixFileMode` / `SetUnixFileMode`.
- The unused `GetUserId` (`getpwnam`) is deleted.

**Behaviour differences**
- A failing read or chmod now throws the .NET `IOException` / `UnauthorizedAccessException`, whose message includes the path, instead of `LinuxPermissionsException` with an errno name. All callers (`MediaFileAttributeService`, both `InstallUpdateService`s, `DiskTransferService`) catch any `Exception`, so they behave the same.
- When `chown` fails, the message now uses the system error text (for example "Operation not permitted") instead of the errno name (`EPERM`).
- `CopyPermissions` with a missing source now only logs at debug level. The old code ignored the failed `Syscall.stat`, so the mode it read was all zeros, and it then set the target to `chmod 0000`.

**Tests**
- The existing permission tests still pass: file, folder, keeping setgid, clearing setgid with a 4-digit mask, copying folder permissions, `IsValidFolderPermissionMask`. They still read results through Mono.Posix, which independently checks the new code until step 9.
- 2 new tests in `DiskProviderFixture`:
  - `should_set_group_by_name` takes the last group of `id -G`, a supplementary group when the user has one, so the group really changes. On CachyOS it is `wheel` (998). The test also checks the mode (`0644`).
  - `should_throw_for_unknown_group`.
- **CachyOS:** 59 pass, the same 3 failures.
- **Alpine/musl:** 60 pass. The only failure is `should_return_free_disk_space`: `ApplicationData` is `""` when `~/.config` doesn't exist in the container.
- The whole solution builds with 0 warnings.

## Step 4: symlinks in copy, move and clone

**What changed** (`src/NzbDrone.Mono/Disk/DiskProvider.cs`)
- `GetSymbolicLinkTarget(path)` = `_fileSystem.FileInfo.New(path).LinkTarget`. It returns the link's stored target (relative or absolute), or `null` if the path isn't a link. It replaces `UnixFileSystemInfo.GetFileSystemEntry(...).IsSymbolicLink` and `UnixSymbolicLinkInfo.ContentsPath`.
- `CreateSymbolicLinkCopy(source, destination, linkTarget)` replaces the block that was duplicated in copy and move:
  - In the same folder, a relative target is kept as it is.
  - Elsewhere it becomes `Path.Combine(sourceFolder, target)`.
  - The link is then created with `_fileSystem.File.CreateSymbolicLink`.
- `MoveFileInternal` removes the original link with `_fileSystem.File.Delete`. If that fails, the new link is deleted again and the error is rethrown, as before.
- `CloneFileInternal` still never tries a reflink when the source is a link.
- `UnixPath.GetDirectoryName` / `Combine` → `Path.GetDirectoryName` / `Combine`.

**Behaviour difference:** if the source file is missing, the error now comes from the normal copy or move instead of from the symlink check (Mono's `GetFileSystemEntry` threw first). The operation fails either way.

**Tests**
- The existing `should_copy_symlink` and `should_move_symlink` still pass.
- 3 new tests in `DiskProviderFixture`:
  - `should_keep_relative_symlink_when_copied_in_same_folder`
  - `should_make_relative_symlink_absolute_when_moved_to_other_folder`
  - `should_copy_symlink_when_cloning`: the link is copied and `IRefLinkCreator` is never called.
- **CachyOS:** 62 pass, the same 3 failures.
- **Alpine/musl:** 63 pass, the same container-only failure.
- The whole solution builds with 0 warnings.

## Step 5: rename and hard link

**What changed** (`src/NzbDrone.Mono/Disk/DiskProvider.cs`, `Interop/LibC.cs`)
- `TryRenameFile` → `LibC.Rename(source, destination) == 0`. It stays a pure rename: `DiskTransferService` only calls it on btrfs/zfs on the same mount and falls back to reflink/copy itself.
- `TryCreateHardLink`:
  - A symlink source is still refused (`GetSymbolicLinkTarget(source) != null`).
  - The link is made with `LibC.Link`.
  - The error code is read right after the call. `EXDEV` (different disk) is logged at trace level, any other error at debug level with the system error text. Unexpected exceptions are still caught and logged, and the method returns `false`.
- `TransferFilePatched` (rename before move): `Syscall.lstat` ×2 + `Syscall.rename` → `_fileSystem.Path.Exists` ×2 + `LibC.Rename`.
  - `Path.Exists` does not follow symlinks, so it matches `lstat`. Checked with a probe: it is true for a dangling link and for a folder, false only when nothing exists. `File.Exists` would be false for a folder.
  - The trace message now says "using rename" instead of "using Syscall.rename".
- `LibC.GetErrorMessage(int)` was added, so an error code read earlier can be turned into text.
- The `using Mono.Unix` / `Mono.Unix.Native` lines are gone: `DiskProvider.cs` no longer references Mono.Posix.

**Behaviour differences**
- When a hard link fails with something other than `EXDEV`, the debug log now contains the system error text instead of a `UnixIOException` stack trace.
- A missing source no longer throws inside the hard-link check. `link` fails with `ENOENT` instead, which is logged at debug level and returns `false`, the same result as before.

**Not done (optional, separate commit):** removing `TransferFilePatched`. It works around Mono 6.x bugs: rename not tried first, and chmod/utime errors after a successful copy. .NET's `File.Move` already tries a rename first. Removing it changes behaviour, so it needs its own decision.

**Tests**
- The existing hard-link test (`should_be_able_to_rename_open_hardlinks_with_fileshare_delete`) and the move tests still pass.
- 5 new tests in `DiskProviderFixture`:
  - `should_rename_file`
  - `should_return_false_when_renaming_missing_file`
  - `should_not_hardlink_symlink`: returns `false` and creates nothing.
  - `should_return_false_when_hardlink_destination_exists`: the existing file is untouched.
  - `should_not_overwrite_dangling_symlink_when_moving`: the rename shortcut must not replace a dangling link at the destination. `MoveFile` throws `FileAlreadyExistsException`, and the source and the link are both kept.
- **CachyOS:** 67 pass, the same 3 failures.
- **Alpine/musl:** 68 pass, the same container-only failure.
- The whole solution builds with 0 warnings.

## Step 6: `SymbolicLinkResolver`

**Why the algorithm is kept:** `GetCompleteRealPath` resolves the symlinks in every part of a path, even when the end of the path doesn't exist yet. `DiskProvider.GetMount` relies on that for folders that are about to be created. `realpath(3)` and `ResolveLinkTarget` can't do it: the first fails on missing paths, the second only follows the last part.

**What changed** (`src/NzbDrone.Mono/Disk/SymbolicLinkResolver.cs`)
- `UnixFileSystemInfo.TryGetFileSystemEntry(...).Exists` → `Path.Exists`. It doesn't follow links, so a dangling link still counts as existing, as with Mono's `lstat`.
- `IsSymbolicLink` + `UnixPath.TryReadLink` → `new FileInfo(path).LinkTarget`: the raw link contents, or `null` if the path isn't a link.
- `UnixPath.GetCanonicalPath` → a private `GetCanonicalPath`. It is the same 3 lines as Mono's version, built on the class's existing `GetPathComponents` (a copy of Mono's), so `.` / `..` / `//` handling is identical. `Path.GetFullPath` was not used, because it would turn a relative path into an absolute one.
- `UnixPath.DirectorySeparatorChar` / `IsPathRooted` / `GetDirectoryName` → `Path.*`.
- The loop warning no longer builds a `UnixIOException(Errno.ELOOP)` just to get its text. It logs "Too many levels of symbolic links" directly (the `ELOOP` number differs between Linux and BSD/macOS anyway).
- `TryFollowSymbolicLink` is now `static`.

**Behaviour difference:** the old code had a separate branch for `readlink` failing on a path that `lstat` had just reported as a link (a race, or a permission error). It logged at trace level and returned the partly resolved path. Now `LinkTarget` throws in that case: the top-level `catch` logs at debug level and returns the original path. Either way the path comes back without full symlink resolution.

**Tests**
- 3 new tests in `SymlinkResolverFixture`:
  - `should_resolve_symlinked_parent_of_missing_path`: `link/missing/file` → `real/missing/file`.
  - `should_follow_dangling_symlink`: a link to a missing file resolves to the target path.
  - `should_return_original_path_on_symlink_loop`: `a → b → a`. It returns the input path and logs 1 warning. The existing `should_throw_on_infinite_loop` queries a file that isn't under the looping folder, so it never reaches the loop.
- All 5 resolver tests were also run against the **old** Mono version (restored from `HEAD` for the run): the same results. The loop test first failed in both versions only because the test framework rejects warnings that weren't announced. It now declares `ExceptionVerification.ExpectedWarns(1)`.
- **CachyOS:** 70 pass, the same 3 failures.
- **Alpine/musl:** 71 pass, the same container-only failure.
- The whole solution builds with 0 warnings. One run reported 1 warning that two rebuilds didn't reproduce, most likely a transient file-copy retry.

## Step 7: `ProcMount`

**Comparison first:** a temporary test, since deleted, printed Mono's `UnixDriveInfo` and .NET's `DriveInfo` side by side for 12 real mounts: xfs, vfat, tmpfs, devtmpfs, nfs4, fuse.
- `AvailableFreeSpace`, `TotalFreeSpace`, `TotalSize` and `IsReady` were **identical** on every mount.
- Only `VolumeLabel` differed:
  - **Mono** returns the `fs_spec` column of the matching `/etc/fstab` line (`UUID=ad5b…`, `tmpfs`, `192.168.74.212:/mnt/nas/repos`), or `""` when the mount isn't in fstab.
  - **.NET** always returns the mount path (`/`, `/home`, …).

**Decision (yours):** no label. The other options were reading `/etc/fstab` to keep Mono's label exactly, or using the device as the label.

**What changed** (`src/NzbDrone.Mono/Disk/ProcMount.cs`)
- `UnixDriveInfo` → `DriveInfo` for free space, total size and `IsReady`.
- `VolumeLabel` → `string.Empty`.
- `VolumeName` → `Name`, the device from `/proc/mounts`. That's what Mono's rule produced for an empty label, a `UUID=` label, or a label equal to the device.
- The `Mono.Unix` and `NzbDrone.Common.Extensions` usings were removed.

**Behaviour differences**
- **System → Disk Space** shows only the path. The ` (label)` suffix is gone, for example `/ (UUID=ad5b743d-…)` → `/` and `/home/xas/repos (192.168.74.212:/mnt/nas/repos)` → `/home/xas/repos`. Mounts that weren't in fstab, such as Docker volumes, had no suffix before either.
- **The drive list of the folder browser** (`VolumeName`) used to show `Name (label)` when an fstab `fs_spec` was neither `UUID=…` nor equal to the device, for example `LABEL=data` or `/dev/disk/by-label/…`. It now always shows just the device. In every other case it is unchanged.

**Tests**
- New `DiskProviderTests/ProcMountFixture` (3 tests):
  - the root mount is ready, and free ≤ total, available ≤ free
  - the label is empty and `VolumeName` is the device
  - a missing mount point is not ready
- The existing free-space and special-mount tests still pass.
- **CachyOS:** 73 pass, the same 3 failures.
- **Alpine/musl:** 74 pass, the same container-only failure.
- The whole solution builds with 0 warnings. One build right after a test run reported 5 warnings. Three rebuilds, including the same "test, then build" sequence, showed 0, so the cause wasn't found. It is probably MSBuild retrying a file copy on the NFS mount.

## Step 8: `RefLinkCreator`

**What changed**
- `src/NzbDrone.Mono/Disk/RefLinkCreator.cs`:
  - `Syscall.uname(...).machine == "x86_64"` → `RuntimeInformation.OSArchitecture == Architecture.X64`. It is still Linux only.
  - `NativeMethods.open(src, O_RDONLY)` → `File.OpenHandle(src, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete)`.
  - `NativeMethods.open(link, O_WRONLY | O_CREAT | O_TRUNC)` → `File.OpenHandle(link, FileMode.Create, FileAccess.Write)`. New files get the same 0666 & umask mode.
  - `NativeMethods.clone_file` → `LibC.Ioctl(linkHandle, FICLONE, srcHandle)`. The `FICLONE` constant moved into `RefLinkCreator`.
  - `Syscall.unlink` → a private best-effort `TryDelete` (`File.Delete` with errors ignored, like the unlink it replaces).
  - `new UnixIOException().Message` → `LibC.LastErrorMessage`, read right after the `ioctl`.
- `Interop/NativeMethods.cs` and `Interop/SafeUnixHandle.cs` are deleted. `File.OpenHandle` returns a `SafeFileHandle`, which `LibC.Ioctl` accepts. `Interop/` now contains only `LibC.cs`.
- **No source file in `src` references `Mono.Unix` any more.** The package reference itself is removed in step 10.

**The same rule as before for cleanup:** `File.OpenHandle` throws where `open` returned -1, so each open has its own `try` that logs the old trace message ("Couldn't open source file" / "Couldn't create new link file") and returns `false` without deleting anything. `linkPath` is only deleted after this method created it. A first draft deleted it in a catch-all that also covered the source open, which could have deleted a file that was already at the destination. That was fixed before testing, and a test now covers it.

**Behaviour differences**
- **Advisory locks:** .NET takes an advisory `flock(LOCK_SH)` on the handles it opens. The clone would now fail, falling back to a copy, only if another **.NET** process holds the source with `FileShare.None` (`LOCK_EX`). Other programs, such as download clients, don't use `flock` this way, so nothing changes for them.
- The trace messages for a failed open now include the .NET exception (for example `FileNotFoundException`). The `ioctl` error text is the system text ("Operation not supported", or "Not supported" on musl) instead of Mono's "Not supported [EOPNOTSUPP]".

**Tests**
- 3 new tests in `RefLinkCreatorFixture`:
  - `should_return_false_when_source_is_missing`: no destination file is created.
  - `should_not_delete_existing_destination_when_source_is_missing`: the existing file is untouched.
  - `should_remove_link_file_when_filesystem_does_not_support_reflinks` (Linux): source and destination are on `/dev/shm` (tmpfs). The `ioctl` fails and the link file is removed. On Alpine the trace shows the failure comes from the clone call itself.
- The existing `should_create_reflink_or_leave_no_file` still creates a real reflink on the NFS 4.2 repo; this was checked again with a temporary print.
- **CachyOS:** 76 pass, the same 3 failures.
- **Alpine/musl:** 77 pass, the same container-only failure.
- The whole solution builds with 0 warnings.

## Step 9: tests off `Mono.Unix`

**What changed** (`tests/NzbDrone.Mono.Test/DiskProviderTests/`)
- `DiskProviderFixture.cs`:
  - New `GetMode(path)` = `Convert.ToString((int)File.GetUnixFileMode(path), 8).PadLeft(4, '0')`. This is the same 4-digit format as Mono's `NativeConvert.ToOctalPermissionString` (`"0644"`, `"2775"`), so all 19 `Syscall.stat` + `ToOctalPermissionString` assertions became `GetMode(path).Should().Be("…")` with unchanged expected values.
  - New `GetGroupId(path)` reads the numeric group from `ls -nd` (field 4). .NET has no API for a file's group, and `ls -n` prints the same output on glibc, busybox (Alpine) and macOS. It replaces `stat.st_gid` in `should_set_group_by_name`.
  - The `Id(args)` helper became `Run(command, args)`, used for `id` and `ls`.
  - `SetWritePermissionsInternal` and the teardown use `File.GetUnixFileMode` / `SetUnixFileMode` with `UnixFileMode` flags. A failed chmod now throws the .NET exception instead of a `LinuxPermissionsException`; this is a test helper only.
  - `should_copy_folder_permissions` compares `UnixFileMode` values instead of `st_mode`.
  - `UnixSymbolicLinkInfo.CreateSymbolicLinkTo` → `File.CreateSymbolicLink`; `IsSymbolicLink` → `new FileInfo(path).LinkTarget != null`.
- `SymlinkResolverFixture.cs`: `UnixSymbolicLinkInfo` → `Directory.CreateSymbolicLink` / `File.CreateSymbolicLink`.
- No test file references `Mono.Unix` any more. The package reference in the test csproj is removed in step 10.

**Behaviour difference:** none in the product. The tests now read modes with the same .NET API the code uses, instead of Mono, so they are no longer an independent check of `File.GetUnixFileMode` itself. To make sure they can still catch a bug, two temporary mutations of `DiskProvider.cs` were run (backed up and restored, the diff is empty afterwards):
- setgid no longer preserved (`mask.Length < 4` → `< 0`): `should_preserve_setgid_on_set_folder_permissions` fails ("2755" expected, "0755" found).
- group not changed (`Chown(…, groupId)` → `Chown(…, UNCHANGED_ID)`): `should_set_group_by_name` fails (998 expected, 1000 found).

**Tests**
- **CachyOS:** 76 pass, the same 3 failures.
- **Alpine/musl:** 77 pass with busybox `ls` (run as gid 100), the same container-only failure.
- The whole solution builds with 0 warnings.

## Step 10: remove the package

**What changed**
- `Directory.Packages.props`: the `Mono.Posix.NETStandard` 5.20.1.34-servarr24 `PackageVersion` is removed.
- `src/NzbDrone.Mono/Readarr.Mono.csproj` and `tests/NzbDrone.Mono.Test/Readarr.Mono.Test.csproj`: the `PackageReference` is removed (the test csproj's `ItemGroup` held nothing else).
- `NuGet.config`: the Servarr `Mono.Posix.NETStandard` feed and its `packageSourceMapping` entry are removed. The other Servarr feeds (`dotnet-bsd-crossbuild`, `coverlet-nightly`, `FluentMigrator`) are untouched.
- `build.sh`:
  - `PackageLinux` / `PackageMacOS`: the `if [ "$framework" = "net10.0" ]` blocks that copied `Mono.Posix.NETStandard.*` and `libMonoPosixHelper.*` into `Readarr.Update` are removed. `Readarr.Mono.*` is still copied.
  - `PackageWindows`: the two `rm -f` lines for those files are removed.
  - `bash -n build.sh` passes.
- No file in the repo mentions `Mono.Posix` or `MonoPosixHelper` any more, apart from this document and `AGENTS.md`.

**Check that nothing still needs it**
- Restore: no `project.assets.json` and no `*.deps.json` mentions Mono.Posix.
- Earlier builds had left 8 files in `_output` / `_tests` (`Mono.Posix.NETStandard.dll`, `libMonoPosixHelper.so`, dated from the 2022 package); incremental builds never remove them. They were deleted, then the tests were run again, so nothing can load them by accident.

**Behaviour differences:** none at run time. The Linux, macOS and Windows packages are smaller: no `Mono.Posix.NETStandard.dll`, and no native `libMonoPosixHelper` per runtime in the app or update folder. Restore no longer depends on the retired upstream's Azure feed for this package.

**Side effect, the step 1 problem is gone:** a plain `dotnet test` on Alpine (no `-p:RuntimeIdentifier`) used to crash the test host, because the default `linux-x64` runtime copied the glibc `libMonoPosixHelper.so`. On a clean copy of the sources it now runs: 77 pass. `Directory.Build.props` still defaults to `linux-$(Architecture)` on musl; other native packages (for example SQLite) have not been checked for this.

**Tests**
- **CachyOS:** Mono tests: 76 pass, the same 3 failures. `Readarr.Common.Test` (run once as a wider check): 613 pass, 4 fail. None of the 4 involves Mono or the disk code, and that project doesn't reference `Readarr.Mono`: version and branch are unknown in a local build (×2), and the cookie test calls the external `httpbin.servarr.com` service (×2).
- **Alpine/musl:** plain `dotnet test`: 77 pass, the same container-only failure, no Mono.Posix file in the output.
- The whole solution builds with 0 warnings.

## Step 11: final checks

All builds ran on a fresh copy of the sources in the scratchpad, because `build.sh`'s `Build()` starts with `rm -rf _output`, which would wipe the repo's `_output/UI` used by `pnpm start` / `dotnet run`.

**Packages (`./build.sh --backend --frontend --packages --skip-tests -r <rid> -f net10.0`)**

| Runtime | Result | Mono.Posix files | `Readarr.Mono.*` in app and `Readarr.Update` |
|---|---|---|---|
| linux-x64 | OK, about 1 min, 0 errors | 0 | yes |
| osx-arm64 | OK (cross-built: arm64 Mach-O, `.app` bundle created). The package used to ship `libMonoPosixHelper.dylib` | 0 | yes |
| win-x64 | **Fails before the edited lines**: `cp _output/net10.0-windows/win-x64/publish/*` finds nothing | 0 (until the failure) | — |

The win-x64 failure was already there. The tray app `src/NzbDrone/Readarr.csproj` (`net10.0-windows`, WinForms) is excluded from `Posix` builds (`Readarr.slnx:10`), and `build.sh` always builds `Platform=Posix` on Linux. A complete Windows package can only be built on Windows, as upstream's CI did. The step 10 change in `PackageWindows` (2 `rm -f` lines removed) was checked by reading it only.

**App smoke test** (linux-x64 package, `-data=<scratchpad>`, port 8799, a fresh database; `~/.config/Readarr` was not used)
- Starts in about 1 s. `isLinux: true`. The Unix platform code (`Readarr.Mono`) is loaded through `AssemblyLoader`. The log has 0 errors; its only warning is the usual ASP.NET data-protection key warning.
- `GET /api/v1/diskspace`: `/` and `/home` with `label: ''`. Free and total space are the same values as Mono's in the step 7 comparison.
- Root folder that is a symlink, created in the scratchpad (tmpfs) and pointing to `_temp/smoke-root` on NFS: `POST /api/v1/rootfolder` → 201, `accessible: true`, `freeSpace` 5.43 TB of 8.0 TB. These are the NFS values, not tmpfs's (about 4.6 GB), so `SymbolicLinkResolver` + `ProcMount` find the right mount.
- `PUT /api/v1/config/mediamanagement` with `setPermissionsLinux`, group `wheel`, hard links on:
  - `chmodFolder` `775` → 202
  - `1775`, `600`, `abc` → 400 "Must contain a valid Unix permissions octal" (`IsValidFolderPermissionMask`)
- On Linux the folder browser lists the contents of `/`, not mounts, so `VolumeName` isn't visible there.

**Import chain:** a real import through the app can't be done, because author lookup fails ("Invalid response received from Goodreads") now that upstream's metadata service is gone. Instead, a temporary test (deleted afterwards) ran the exact calls an import makes, with real classes and no mocks: `DiskTransferService.TransferFile` with the real `DiskProvider`, `ProcMountProvider`, `SymbolicLinkResolver` and `RefLinkCreator`, then `DiskProvider.SetPermissions(file, "775", "wheel")`, which is what `MediaFileAttributeService` does on Linux.
- **Same disk (NFS), hard link or copy:** `HardLink`. Source and import share one inode (2 links). The mode is `664`: 775 minus the execute bits, as for files. The group is `wheel`.
- **tmpfs → NFS, hard link or copy:** `link` fails with `EXDEV`, so it copies. The copy is a new inode, mode `664`, group `wheel`, contents intact.
- **tmpfs → NFS, move:** `rename` fails across disks, so it copies and then deletes. The source is gone and the contents are intact.

**Not done**
- A full build of all 10 runtimes (only linux-x64, osx-arm64 and win-x64 were run).
- Running on real macOS or FreeBSD (osx-arm64 was only cross-built).
- An import on a CIFS/SMB mount (see 5b).

## Possible follow-ups

- **5b (optional, skipped for now).** Remove `TransferFilePatched` and call `base.MoveFileInternal` / `base.CopyFileInternal` directly. Its rename-first part is redundant with .NET's `File.Move`. Its recovery from permission/timestamp errors after a complete copy (CIFS/SMB on NAS) may still be needed. Only do this after testing on a real CIFS mount.
