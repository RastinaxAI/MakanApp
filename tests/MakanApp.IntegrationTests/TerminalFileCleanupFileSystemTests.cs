using System.Diagnostics;
using MakanApp.Application.Storage;
using MakanApp.Infrastructure.Storage;
using Xunit;

namespace MakanApp.IntegrationTests;

public sealed class TerminalFileCleanupFileSystemTests
{
    // این تست‌ها وضعیت ثابت مسیر را می‌سنجند؛ تضمین اتمیک TOCTOU یا جایگزین ACL قابل‌اعتماد نیستند.
    [Fact]
    public async Task DeletesOnlyTheSelectedLocalBlobAndRepeatedDeleteIsIdempotent()
    {
        using var sandbox = new FileSystemSandbox();
        var storage = sandbox.CreateStorage();
        var terminalPath = sandbox.WriteFile("storage/terminal/selected.bin", "terminal bytes");
        var retainedPath = sandbox.WriteFile("storage/terminal/retained.bin", "retained bytes");

        Assert.True(await storage.DeleteTerminalBlobAsync("terminal/selected.bin", CancellationToken.None));
        Assert.False(File.Exists(terminalPath));
        Assert.Equal("retained bytes", await File.ReadAllTextAsync(retainedPath));
        Assert.False(await storage.DeleteTerminalBlobAsync("terminal/selected.bin", CancellationToken.None));
    }

    [Fact]
    public async Task MissingBlobDoesNotCreateItsParentDirectories()
    {
        using var sandbox = new FileSystemSandbox();
        var storage = sandbox.CreateStorage();

        Assert.False(await storage.DeleteTerminalBlobAsync("never-created/missing.bin", CancellationToken.None));

        Assert.False(Directory.Exists(sandbox.PathFor("storage/never-created")));
    }

    [Fact]
    public async Task PreCancelledDeletePreservesBytesAndCallerToken()
    {
        using var sandbox = new FileSystemSandbox();
        var storage = sandbox.CreateStorage();
        var blobPath = sandbox.WriteFile("storage/terminal.bin", "must remain");
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        var exception = await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            storage.DeleteTerminalBlobAsync("terminal.bin", cancellation.Token));

        Assert.Equal(cancellation.Token, exception.CancellationToken);
        Assert.Equal("must remain", await File.ReadAllTextAsync(blobPath));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(".")]
    [InlineData("..")]
    [InlineData("../outside/victim.bin")]
    [InlineData("nested/../../outside/victim.bin")]
    [InlineData("nested/../victim.bin")]
    [InlineData("nested/./victim.bin")]
    [InlineData("nested//victim.bin")]
    [InlineData("victim.bin/")]
    [InlineData(@"..\outside\victim.bin")]
    [InlineData(@"nested\victim.bin")]
    [InlineData("/victim.bin")]
    [InlineData(@"\victim.bin")]
    [InlineData(@"C:\outside\victim.bin")]
    [InlineData(@"\\server\share\victim.bin")]
    [InlineData(@"\\?\C:\outside\victim.bin")]
    [InlineData(@"\\.\C:\outside\victim.bin")]
    [InlineData("terminal.bin.")]
    [InlineData("terminal.bin ")]
    [InlineData("nested./victim.bin")]
    [InlineData(".. /victim.bin")]
    [InlineData("NUL")]
    [InlineData("NUL.txt")]
    [InlineData("aux.bin")]
    [InlineData("CONIN$")]
    [InlineData("CONOUT$")]
    [InlineData("COM1.bin")]
    [InlineData("LPT9")]
    [InlineData("COM¹.bin")]
    public async Task InvalidTraversalRootedNetworkAndDeviceKeysCannotDeleteOutsideBytes(string? storageKey)
    {
        using var sandbox = new FileSystemSandbox();
        var storage = sandbox.CreateStorage();
        var outsidePath = sandbox.WriteFile("outside/victim.bin", "outside bytes");
        var insidePath = sandbox.WriteFile("storage/victim.bin", "inside bytes");

        await AssertRejectedAsync(storage, storageKey!);

        Assert.Equal("outside bytes", await File.ReadAllTextAsync(outsidePath));
        Assert.Equal("inside bytes", await File.ReadAllTextAsync(insidePath));
    }

    [Fact]
    public async Task FullyQualifiedPathInsideStorageIsStillNotAnInternalStorageKey()
    {
        using var sandbox = new FileSystemSandbox();
        var storage = sandbox.CreateStorage();
        var blobPath = sandbox.WriteFile("storage/terminal.bin", "must remain");

        await AssertRejectedAsync(storage, blobPath);

        Assert.Equal("must remain", await File.ReadAllTextAsync(blobPath));
    }

    [Theory]
    [InlineData("folder./leaf.bin")]
    [InlineData("folder /leaf.bin")]
    [InlineData("folder/leaf.bin.")]
    [InlineData("folder/leaf.bin ")]
    public async Task NormalizationAliasCannotDeleteTheExistingCanonicalBlob(string storageKey)
    {
        using var sandbox = new FileSystemSandbox();
        var storage = sandbox.CreateStorage();
        var canonicalPath = sandbox.WriteFile("storage/folder/leaf.bin", "canonical bytes");

        await AssertRejectedAsync(storage, storageKey);

        Assert.Equal("canonical bytes", await File.ReadAllTextAsync(canonicalPath));
    }

    [Fact]
    public async Task FilesystemRootCannotBeUsedForTerminalDeletion()
    {
        using var sandbox = new FileSystemSandbox();
        var filesystemRoot = Path.GetPathRoot(sandbox.RootPath)!;
        var storage = new LocalFileStorage(new StorageOptions { RootPath = filesystemRoot });

        await AssertRejectedAsync(storage, $"Makan-TerminalCleanup-{Guid.NewGuid():N}.missing");
    }

    [Fact]
    public async Task DirectoryTargetIsNotRecursivelyDeleted()
    {
        using var sandbox = new FileSystemSandbox();
        var storage = sandbox.CreateStorage();
        var blobPath = sandbox.WriteFile("storage/terminal/child.bin", "child bytes");

        await AssertRejectedAsync(storage, "terminal");

        Assert.Equal("child bytes", await File.ReadAllTextAsync(blobPath));
    }

    [Fact]
    public async Task JunctionAtConfiguredStorageRootCannotExposeOutsideBytes()
    {
        using var sandbox = new FileSystemSandbox();
        var outsidePath = sandbox.WriteFile("outside/terminal.bin", "outside bytes");
        var link = sandbox.CreateDirectoryJunction("root-link", "outside");
        var storage = new LocalFileStorage(new StorageOptions { RootPath = link });

        await AssertRejectedAsync(storage, "terminal.bin");

        Assert.Equal("outside bytes", await File.ReadAllTextAsync(outsidePath));
    }

    [Fact]
    public async Task JunctionAboveConfiguredStorageRootCannotExposeOutsideBytes()
    {
        using var sandbox = new FileSystemSandbox();
        var outsidePath = sandbox.WriteFile("outside/child/terminal.bin", "outside bytes");
        var link = sandbox.CreateDirectoryJunction("ancestor-link", "outside");
        var storage = new LocalFileStorage(new StorageOptions { RootPath = Path.Combine(link, "child") });

        await AssertRejectedAsync(storage, "terminal.bin");

        Assert.Equal("outside bytes", await File.ReadAllTextAsync(outsidePath));
    }

    [Fact]
    public async Task JunctionBelowStorageRootCannotExposeOutsideBytes()
    {
        using var sandbox = new FileSystemSandbox();
        var storage = sandbox.CreateStorage();
        var outsidePath = sandbox.WriteFile("outside/terminal.bin", "outside bytes");
        sandbox.CreateDirectoryJunction("storage/nested-link", "outside");

        await AssertRejectedAsync(storage, "nested-link/terminal.bin");

        Assert.Equal("outside bytes", await File.ReadAllTextAsync(outsidePath));
    }

    [Fact]
    public async Task FileSymbolicLinkTargetCannotDeleteOrFollowOutsideBytes()
    {
        using var sandbox = new FileSystemSandbox();
        var storage = sandbox.CreateStorage();
        var outsidePath = sandbox.WriteFile("outside/terminal.bin", "outside bytes");
        var link = sandbox.CreateFileSymbolicLink("storage/terminal-link.bin", "outside/terminal.bin");

        await AssertRejectedAsync(storage, "terminal-link.bin");

        Assert.Equal("outside bytes", await File.ReadAllTextAsync(outsidePath));
        Assert.True(File.GetAttributes(link).HasFlag(FileAttributes.ReparsePoint));
    }

    [Fact]
    public async Task DanglingFileSymbolicLinkIsRejectedRatherThanReportedAlreadyMissing()
    {
        using var sandbox = new FileSystemSandbox();
        var storage = sandbox.CreateStorage();
        var link = sandbox.CreateFileSymbolicLink("storage/terminal-link.bin", "outside/missing.bin");

        await AssertRejectedAsync(storage, "terminal-link.bin");

        Assert.NotNull(new FileInfo(link).LinkTarget);
    }

    private static async Task AssertRejectedAsync(LocalFileStorage storage, string storageKey)
    {
        var exception = await Assert.ThrowsAsync<StorageException>(() =>
            storage.DeleteTerminalBlobAsync(storageKey, CancellationToken.None));

        Assert.Equal(StorageErrorCodes.FileStorageFailed, exception.Code);
        if (storageKey?.Length > 3 && !string.IsNullOrWhiteSpace(storageKey))
        {
            Assert.DoesNotContain(storageKey, exception.Message, StringComparison.Ordinal);
        }
    }

    private sealed class FileSystemSandbox : IDisposable
    {
        private readonly HashSet<string> _directories = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _files = new(StringComparer.OrdinalIgnoreCase);
        private readonly List<string> _directoryLinks = [];
        private readonly List<string> _fileLinks = [];

        public FileSystemSandbox()
        {
            RootPath = Path.Combine(Path.GetTempPath(), $"Makan-TerminalCleanup-{Guid.NewGuid():N}");
            Directory.CreateDirectory(RootPath);
            _directories.Add(RootPath);
        }

        public string RootPath { get; }

        public LocalFileStorage CreateStorage()
        {
            var root = CreateDirectory("storage");
            return new LocalFileStorage(new StorageOptions { RootPath = root });
        }

        public string PathFor(string relativePath)
        {
            var path = Path.GetFullPath(Path.Combine(RootPath, relativePath.Replace('/', Path.DirectorySeparatorChar)));
            if (!path.StartsWith(RootPath + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("مسیر fixture باید داخل پوشهٔ موقت اختصاصی تست باشد.");
            }

            return path;
        }

        public string WriteFile(string relativePath, string content)
        {
            var path = PathFor(relativePath);
            CreateDirectory(Path.GetRelativePath(RootPath, Path.GetDirectoryName(path)!));
            File.WriteAllText(path, content);
            _files.Add(path);
            return path;
        }

        public string CreateFileSymbolicLink(string relativeLink, string relativeTarget)
        {
            var link = PathFor(relativeLink);
            var target = PathFor(relativeTarget);
            CreateDirectory(Path.GetRelativePath(RootPath, Path.GetDirectoryName(link)!));
            File.CreateSymbolicLink(link, target);
            _fileLinks.Add(link);
            Assert.True(File.GetAttributes(link).HasFlag(FileAttributes.ReparsePoint));
            return link;
        }

        public string CreateDirectoryJunction(string relativeLink, string relativeTarget)
        {
            var link = PathFor(relativeLink);
            var target = PathFor(relativeTarget);
            if (OperatingSystem.IsWindows())
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "cmd.exe"),
                    Arguments = $"/d /c mklink /J \"{link}\" \"{target}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true
                };
                using var process = Process.Start(startInfo)!;
                if (!process.WaitForExit(5_000))
                {
                    process.Kill(entireProcessTree: true);
                    throw new TimeoutException("ایجاد junction آزمایشی در مهلت محدود تمام نشد.");
                }

                Assert.Equal(0, process.ExitCode);
            }
            else
            {
                Directory.CreateSymbolicLink(link, target);
            }

            _directoryLinks.Add(link);
            Assert.True(File.GetAttributes(link).HasFlag(FileAttributes.ReparsePoint));
            return link;
        }

        public void Dispose()
        {
            // فقط مسیرهای دقیق ساخته‌شده توسط همین fixture؛ هیچ حذف recursive یا دنبال‌کردن link نداریم.
            foreach (var link in _fileLinks)
            {
                File.Delete(link);
            }

            foreach (var link in _directoryLinks)
            {
                Directory.Delete(link, recursive: false);
            }

            foreach (var file in _files)
            {
                File.Delete(file);
            }

            foreach (var directory in _directories.OrderByDescending(path => path.Length))
            {
                Directory.Delete(directory, recursive: false);
            }
        }

        private string CreateDirectory(string relativePath)
        {
            var path = PathFor(relativePath);
            var pending = new Stack<string>();
            for (var current = path; !_directories.Contains(current); current = Path.GetDirectoryName(current)!)
            {
                pending.Push(current);
            }

            while (pending.TryPop(out var directory))
            {
                Directory.CreateDirectory(directory);
                _directories.Add(directory);
            }

            return path;
        }
    }
}
