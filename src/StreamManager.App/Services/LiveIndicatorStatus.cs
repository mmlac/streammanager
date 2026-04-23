namespace StreamManager.App.Services;

// Top-bar live indicator per design.md §8:
//   Live         — green, an active broadcast was fetched.
//   NotLive      — grey, connected but no active broadcast.
//   FetchFailed  — amber, the last fetch errored; previous form state preserved.
//   Unknown      — initial state before any fetch / while disconnected.
public enum LiveIndicatorStatus
{
    Unknown,
    Live,
    NotLive,
    FetchFailed,
}
