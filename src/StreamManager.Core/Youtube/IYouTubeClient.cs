namespace StreamManager.Core.Youtube;

// Thin wrapper around Google.Apis.YouTube.v3 for the read/write surface
// described in design.md §4. Slice 4 only needs GetActiveBroadcastAsync;
// liveBroadcasts.update / videos.update land in slice 5.
//
// 401 responses are translated to Core's UnauthorizedException so the
// IReauthOrchestrator pipeline can drive the §6.7 reconnect modal.
public interface IYouTubeClient
{
    Task<BroadcastSnapshot?> GetActiveBroadcastAsync(CancellationToken ct);
}
