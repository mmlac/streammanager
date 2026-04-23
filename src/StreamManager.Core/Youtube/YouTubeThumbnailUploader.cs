using System.Net;
using Google.Apis.Services;
using Google.Apis.Upload;
using Google.Apis.YouTube.v3;
using Microsoft.Extensions.Logging;
using StreamManager.Core.Auth;

namespace StreamManager.Core.Youtube;

// §6.6 step 4 — real implementation of thumbnails.set backed by
// Google.Apis.YouTube.v3. Separate from YouTubeClient because the upload API
// is a MediaUpload rather than the plain list/update request used elsewhere,
// and keeping it behind its own interface lets the orchestrator mock it
// independently of the broadcast/video update paths.
//
// 401 → UnauthorizedException so IReauthOrchestrator drives the §6.7 reconnect
// modal, matching the behavior of YouTubeClient.
public sealed class YouTubeThumbnailUploader : IThumbnailUploader, IDisposable
{
    private const string ApplicationName = "StreamManager";

    private readonly IAuthState _authState;
    private readonly YouTubeService _service;
    private readonly ILogger<YouTubeThumbnailUploader> _log;

    public YouTubeThumbnailUploader(IAuthState authState, ILogger<YouTubeThumbnailUploader> log)
    {
        _authState = authState;
        _log = log;
        _service = new YouTubeService(new BaseClientService.Initializer
        {
            ApplicationName = ApplicationName,
        });
    }

    public async Task SetThumbnailAsync(string videoId, string filePath, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(videoId))
        {
            throw new ArgumentException("videoId is required.", nameof(videoId));
        }
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("filePath is required.", nameof(filePath));
        }

        var token = _authState.AccessToken;
        if (string.IsNullOrEmpty(token))
        {
            throw new InvalidOperationException(
                "Cannot call YouTube API: no access token available (not connected).");
        }

        // FileStream is opened just before upload — the orchestrator does a
        // reachability pre-flight, but disk state can change between then and
        // here. A FileNotFoundException on open surfaces as a step-3 failure
        // in the orchestrator (design §6.6 step 4 race case).
        await using var stream = File.OpenRead(filePath);
        var contentType = ContentTypeFor(filePath);

        var req = _service.Thumbnails.Set(videoId, stream, contentType);
        req.OauthToken = token;

        var progress = await req.UploadAsync(ct).ConfigureAwait(false);
        if (progress.Status == UploadStatus.Failed)
        {
            var ex = progress.Exception;
            if (ex is Google.GoogleApiException gae
                && gae.HttpStatusCode == HttpStatusCode.Unauthorized)
            {
                throw new UnauthorizedException("YouTube API returned 401.", gae);
            }
            _log.LogWarning(ex, "thumbnails.set upload failed for {VideoId}", videoId);
            throw ex ?? new InvalidOperationException(
                "thumbnails.set failed with no exception attached.");
        }
    }

    // YouTube accepts JPG/PNG/BMP/GIF (design §6.8); pick the MIME type from
    // the extension since the UploadStatus path depends on it being correct.
    // Anything else falls through to application/octet-stream; validation at
    // pick time should have rejected those already.
    internal static string ContentTypeFor(string path)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        return ext switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".bmp" => "image/bmp",
            ".gif" => "image/gif",
            _ => "application/octet-stream",
        };
    }

    public void Dispose() => _service.Dispose();
}
