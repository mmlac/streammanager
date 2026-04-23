using StreamManager.Core.Youtube;
using Xunit;

namespace StreamManager.Core.Tests.Youtube;

public class YouTubeThumbnailUploaderTests
{
    [Theory]
    [InlineData("/tmp/thing.jpg", "image/jpeg")]
    [InlineData("/tmp/thing.jpeg", "image/jpeg")]
    [InlineData("/tmp/thing.JPG", "image/jpeg")]
    [InlineData("/tmp/thing.png", "image/png")]
    [InlineData("/tmp/thing.bmp", "image/bmp")]
    [InlineData("/tmp/thing.gif", "image/gif")]
    [InlineData("/tmp/thing.tiff", "application/octet-stream")]
    public void ContentTypeFor_MapsExtension(string path, string expected)
    {
        Assert.Equal(expected, YouTubeThumbnailUploader.ContentTypeFor(path));
    }
}
