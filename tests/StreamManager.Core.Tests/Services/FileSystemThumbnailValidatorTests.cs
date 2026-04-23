using System;
using System.IO;
using StreamManager.App.Services;
using Xunit;

namespace StreamManager.Core.Tests.Services;

public class FileSystemThumbnailValidatorTests
{
    [Fact]
    public void SmallJpg_IsOk()
    {
        using var tmp = TempFile.CreateWithBytes(".jpg", new byte[1024]);
        var validator = new FileSystemThumbnailValidator();
        Assert.Equal(ThumbnailValidationIssue.Ok, validator.Validate(tmp.Path));
    }

    [Fact]
    public void OversizeFile_IsTooLarge()
    {
        using var tmp = TempFile.CreateWithBytes(
            ".png",
            new byte[FileSystemThumbnailValidator.MaxSizeBytes + 1]);
        var validator = new FileSystemThumbnailValidator();
        Assert.Equal(ThumbnailValidationIssue.TooLarge, validator.Validate(tmp.Path));
    }

    [Fact]
    public void UnsupportedExtension_IsBadExtension()
    {
        using var tmp = TempFile.CreateWithBytes(".tiff", new byte[8]);
        var validator = new FileSystemThumbnailValidator();
        Assert.Equal(ThumbnailValidationIssue.BadExtension, validator.Validate(tmp.Path));
    }

    [Fact]
    public void MissingFile_IsUnreadable()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".jpg");
        var validator = new FileSystemThumbnailValidator();
        Assert.Equal(ThumbnailValidationIssue.Unreadable, validator.Validate(path));
    }

    [Theory]
    [InlineData(".jpg")]
    [InlineData(".jpeg")]
    [InlineData(".png")]
    [InlineData(".bmp")]
    [InlineData(".gif")]
    public void AllAllowedExtensions_AreOk(string ext)
    {
        using var tmp = TempFile.CreateWithBytes(ext, new byte[128]);
        var validator = new FileSystemThumbnailValidator();
        Assert.Equal(ThumbnailValidationIssue.Ok, validator.Validate(tmp.Path));
    }

    [Theory]
    [InlineData(".JPG")]
    [InlineData(".PnG")]
    public void ExtensionMatchIsCaseInsensitive(string ext)
    {
        using var tmp = TempFile.CreateWithBytes(ext, new byte[128]);
        var validator = new FileSystemThumbnailValidator();
        Assert.Equal(ThumbnailValidationIssue.Ok, validator.Validate(tmp.Path));
    }

    private sealed class TempFile : IDisposable
    {
        public string Path { get; }
        public TempFile(string path) { Path = path; }

        public static TempFile CreateWithBytes(string ext, byte[] bytes)
        {
            var path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                Guid.NewGuid() + ext);
            File.WriteAllBytes(path, bytes);
            return new TempFile(path);
        }

        public void Dispose()
        {
            try { File.Delete(Path); } catch { /* best effort */ }
        }
    }
}
