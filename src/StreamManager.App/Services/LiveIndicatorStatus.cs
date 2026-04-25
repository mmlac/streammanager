namespace StreamManager.App.Services;

// Top-bar live indicator per design.md §8:
//   Live         — green,  an active broadcast is currently streaming.
//   Ready        — blue,   a broadcast is set up and ready to go live
//                          (visible in YouTube Studio "Go Live" panel).
//   NotLive      — grey,   connected but no broadcast found.
//   FetchFailed  — amber,  the last fetch errored; previous form state preserved.
//   Unknown      — initial state before any fetch / while disconnected.
public enum LiveIndicatorStatus
{
    Unknown,
    Live,
    Ready,
    NotLive,
    FetchFailed,
}
