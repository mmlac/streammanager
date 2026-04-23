# StreamManager — Design

A desktop app (Windows + macOS) for managing a YouTube Live stream. The main
view is a form populated from the active broadcast; the user edits it
directly or loads a **preset** into it, then pushes the form to YouTube with
one click. Presets are a source/sink — load from, save as, or update. Covers
every metadata field the YouTube Data API v3 exposes on a broadcast.

Status: **design agreed on scope**. Implementation plan pending.

## 1. Goals

- Save named **presets** containing every metadata field settable on a YouTube Live broadcast via the Data API v3 (see §5).
- Apply a preset to the **active live broadcast** with one click.
- Support a **thumbnail per preset** (single image file, uploaded on apply).
- Primary OS targets: **macOS and Windows**. Linux may work incidentally via Avalonia but is not tested.
- Personal tool: single user, single YouTube channel, local storage only.

## 2. Non-goals (for v1)

- Multi-user / team features.
- Multiple YouTube channels under one account.
- OBS / streaming-software integration.
- Chat, moderation, analytics.
- Scheduled broadcast creation wizards (maybe v2).
- Description templating / macros — presets hold literal strings only.
- Mobile.
- Linux as a supported target (may work but not tested or packaged).

## 3. Stack

| Concern | Choice | Why |
|---|---|---|
| UI | Avalonia 11 | Cross-platform .NET UI, mature, XAML-like |
| Runtime | .NET 10 | Current |
| MVVM | CommunityToolkit.Mvvm | Source-generated, less ceremony than ReactiveUI |
| YouTube API | `Google.Apis.YouTube.v3` | Official client for YouTube Data API v3 |
| OAuth | `Google.Apis.Auth` (installed-app flow, PKCE, loopback redirect) | Standard for desktop Google apps |
| Preset storage | JSON file | Simple; ~10–50 presets, not a DB problem |
| Token storage | OS keychain: macOS Keychain + Windows Credential Manager, behind `ITokenStore` | Don't store refresh tokens in plain files |
| Thumbnail storage | Copied into an app-managed `thumbnails/` dir, referenced by filename | Avoids broken paths if the user moves source images |
| Logging | `Microsoft.Extensions.Logging` + Serilog file sink | Standard |
| Packaging | `dotnet publish` self-contained per-OS (`win-x64`, `osx-arm64`, `osx-x64`) | Simple for v1 |

## 4. YouTube API surface

YouTube Data API v3. Broadcasts ARE videos once created — metadata is split across two resources:

| Call | Purpose | Fields set |
|---|---|---|
| `liveBroadcasts.list(mine=true, broadcastStatus=active)` | Find active broadcast | (read) |
| `liveBroadcasts.update` | Update broadcast-specific fields | title, description, scheduledStart/End, privacyStatus, contentDetails.* (DVR, embed, captions, latency, monitor stream, projection, etc.), status.selfDeclaredMadeForKids |
| `videos.update` (part=snippet,status,localizations) | Update the underlying video | categoryId, tags, defaultLanguage, defaultAudioLanguage |
| `videoCategories.list(regionCode)` | Populate category dropdown | (read) |
| `thumbnails.set` | Upload preset thumbnail | image bytes |
| `i18nLanguages.list` | Populate language dropdown | (read) |

**Scopes:**
- `https://www.googleapis.com/auth/youtube`
- `https://www.googleapis.com/auth/youtube.force-ssl` (required for `liveBroadcasts.update`)
- `https://www.googleapis.com/auth/youtube.upload` (required for `thumbnails.set`)

**Quota:** one "Apply" = `liveBroadcasts.update` (~50) + `videos.update` (~50) + `thumbnails.set` (~50) ≈ 150 units. Daily quota 10,000. Not a concern.

## 5. Preset schema

Covers every field the public Data API v3 lets us set on a live broadcast + its underlying video. Fields grouped by API call.

```jsonc
{
  "id": "uuid",
  "name": "Elden Ring — chill run",     // display label only
  "createdAt": "...",
  "updatedAt": "...",

  // --- liveBroadcasts.update → snippet ---
  "title": "Elden Ring — blind playthrough, day 3",
  "description": "...",
  "scheduledStartTime": null,            // ISO-8601 or null (only meaningful for upcoming)
  "scheduledEndTime": null,

  // --- liveBroadcasts.update → status ---
  "privacyStatus": "public",             // public | unlisted | private
  "selfDeclaredMadeForKids": false,

  // --- liveBroadcasts.update → contentDetails ---
  "enableAutoStart": true,
  "enableAutoStop": true,
  "enableClosedCaptions": false,
  "enableDvr": true,
  "enableEmbed": true,
  "recordFromStart": true,
  "startWithSlate": false,
  "enableContentEncryption": false,
  "enableLowLatency": false,
  "latencyPreference": "normal",         // normal | low | ultraLow
  "enableMonitorStream": true,
  "broadcastStreamDelayMs": 0,
  "projection": "rectangular",           // rectangular | 360 | mesh
  "stereoLayout": "mono",                // mono | left_right | top_bottom
  "closedCaptionsType": "closedCaptionsDisabled", // ...Disabled | ...HTTPPost | ...EmbedInVideo

  // --- videos.update → snippet ---
  "categoryId": "20",                    // e.g. Gaming = 20
  "tags": ["elden ring", "soulslike"],
  "defaultLanguage": "en",
  "defaultAudioLanguage": "en",

  // --- thumbnails.set ---
  "thumbnailFile": "elden-ring-chill.jpg" // filename inside app-managed thumbnails/ dir; null = don't touch thumbnail
}
```

Notes:
- Fields the API accepts but we **won't expose** in v1: `contentDetails.boundStreamId` (binding an RTMP stream — out of scope), `contentDetails.monitorStream.broadcastStreamDelayMs` boundaries beyond simple ms input, `liveChat.*` (read-only on broadcasts).
- "Tags" is a flat list; YouTube enforces a 500-char total limit across all tags — we validate on save.
- `thumbnailFile == null` means "don't change the current thumbnail on apply."

## 6. Core flows

**Mental model.** The main form IS "the current stream". It is populated from
YouTube on connect/refresh. The user edits it directly OR loads a preset into
it. Presets are a source/sink — you can load from a preset, save the form as a
new preset, or overwrite the loaded preset with the form's current values.
Applying pushes the **form's current values** to YouTube.

### 6.1 First-run OAuth
1. User clicks "Connect YouTube account".
2. Loopback redirect OAuth flow (`http://127.0.0.1:<ephemeral>`) opens system browser.
3. Receive code → exchange for refresh + access token.
4. Store refresh token in OS keychain; access token in memory only.

### 6.2 Fetch current stream into form
1. On app start and on a Refresh button, call `liveBroadcasts.list(mine, active)`.
2. If one active broadcast: fetch the broadcast + its video snippet/contentDetails + current thumbnail URL, populate the form.
3. If no active broadcast: form shows last state (or empty on first run), Apply disabled, "Not live" indicator.
4. Thumbnail in the form is the remote URL (read-only preview) until the user picks a new local file.

### 6.3 Load preset into form
1. User clicks **Load preset ▾**, picks one from the list.
2. If form is dirty relative to YouTube (unsaved edits to live stream), confirm before overwriting.
3. Form fields replaced with preset values. Form is now "dirty" relative to live (indicator shown) but "clean" relative to the loaded preset.
4. App remembers "this form was loaded from preset X" to enable the **Update preset** path (§6.5).

### 6.4 Save current form as a new preset
1. User clicks **Save as preset…**, enters a name.
2. All current form values + the currently-selected thumbnail file are persisted as a new preset.
3. Form is now "loaded from" the new preset.

### 6.5 Update an existing preset from the form
1. Enabled only when the form was loaded from preset X (see §6.3).
2. User clicks **Update preset "X"** → overwrites preset X's values with the form's current values.
3. No rename; name stays the same. Use Save as preset for a rename.

### 6.6 Apply form to live stream (the main event)
When the user clicks **Apply to live stream**:
1. `liveBroadcasts.update` with snippet + status + contentDetails from the form.
2. `videos.update` with categoryId, tags, languages from the form.
3. If the user picked a new thumbnail file since the last fetch: `thumbnails.set`. Otherwise skipped.
4. On success: re-fetch current stream into form (§6.2) so we see what YouTube actually applied, clear dirty indicators.
5. On any step failure: surface which step failed. No rollback — YouTube's API is not transactional.

### 6.7 Editor details
- Category and language dropdowns populated from API (cached in JSON, refreshed on demand).
- Thumbnail picker: file dialog → copy selected image into `thumbnails/<uuid>.<ext>` → store filename in the form state. The remote thumbnail URL is replaced with a local preview once the user picks a file.
- Validation (on Apply and on Save-as-preset): title ≤ 100 chars, description ≤ 5000, tags combined ≤ 500 chars, thumbnail ≤ 2MB and must be JPG/PNG/BMP/GIF per YouTube's rules.

## 7. Storage layout

```
<AppData>/streammanager/
  config.json          # OAuth client ID + secret, log level, window geometry
  presets.json         # all presets (with schemaVersion)
  thumbnails/          # app-managed copies, named <preset-id>.<ext>
  cache/
    categories.json    # videoCategories.list result, keyed by regionCode
    languages.json     # i18nLanguages.list result
  logs/
    streammanager-<date>.log
```

Paths:
- **Windows:** `%APPDATA%\streammanager\`
- **macOS:** `~/Library/Application Support/streammanager/`

Refresh token lives in the OS keychain, NOT in any file above.

## 8. UI sketch

Single window. The form IS "the current stream"; presets are surfaced via the
action bar above it.

```
┌────────────────────────────────────────────────────────────┐
│ ● LIVE · @channel · [Refresh]              [⚙ Settings]    │  top bar
├────────────────────────────────────────────────────────────┤
│ [Load preset ▾]  [Save as preset…]  [Update preset "X"]    │  preset action bar
│                          ↑                                 │
│                    disabled unless form was loaded from X  │
├────────────────────────────────────────────────────────────┤
│                                                            │
│   Basics                                                   │
│   ├ Title          [________________________________]      │
│   ├ Description    ┌──────────────────────────────────┐    │
│   │                │                                  │    │
│   │                │  (multi-line text block,         │    │
│   │                │   resizable, scrollable)         │    │
│   │                │                                  │    │
│   │                └──────────────────────────────────┘    │
│   ├ Category       [Gaming ▾]                              │
│   └ Tags           [chip, chip, chip, +]                   │
│                                                            │
│   Privacy          [Public ▾]  ☐ Made for kids             │
│                                                            │
│   Playback & DVR   ☑ DVR  ☑ Embed  ☐ CC  ☑ Auto-start ...  │
│                                                            │
│   Advanced         latency [normal ▾]  projection [rect ▾] │
│                    stream delay [ 0 ] ms                   │
│                                                            │
│   Language         default [en ▾]  audio [en ▾]            │
│                                                            │
│   Thumbnail        [preview]  [Pick image…]  [Clear]       │
│                                                            │
├────────────────────────────────────────────────────────────┤
│ Dirty vs live · Loaded from preset "X"                     │  status line
│                              [ Apply to live stream  ▶ ]   │  bottom bar
└────────────────────────────────────────────────────────────┘
```

- **Top bar:** connected account, live indicator (green=live, grey=not live), Refresh, Settings menu (disconnect, open data folder, log level).
- **Preset action bar:**
  - `Load preset ▾` — dropdown of saved presets.
  - `Save as preset…` — opens a small name dialog; always enabled.
  - `Update preset "X"` — only visible/enabled when the form was loaded from preset X AND is dirty relative to X.
- **Form:** grouped sections (Basics · Privacy · Playback & DVR · Advanced · Language · Thumbnail).
- **Status line:** shows dirty state relative to live + the preset lineage ("Loaded from preset X" or "No preset").
- **Apply:** disabled when not live or when the form has validation errors. Pushes the form's current values to YouTube (§6.6).

MVVM:
- `IYouTubeClient` — thin wrapper around `Google.Apis.YouTube.v3`.
- `IPresetStore` — load/save `presets.json`.
- `ITokenStore` — `MacTokenStore` / `WindowsTokenStore` implementations.
- `IThumbnailStore` — manages the `thumbnails/` directory.
- ViewModels: `MainWindowViewModel`, `StreamFormViewModel` (the form + its dirty/lineage tracking), `ConnectAccountViewModel`, `LoadPresetPickerViewModel`, `SavePresetDialogViewModel`.

## 9. Project layout

```
src/
  StreamManager.App/              # Avalonia UI, DI wiring, entry point
  StreamManager.Core/             # Preset models, services, YouTube client wrapper
  StreamManager.Platform.Windows/ # Windows keychain (Credential Manager)
  StreamManager.Platform.Mac/     # macOS keychain
tests/
  StreamManager.Core.Tests/       # unit tests for stores, validators, apply-preset orchestrator
docs/
  design.md                       # this file
```

## 10. Implementation slices (rough)

Each slice is a potential PR / bead.

1. **Solution skeleton.** Avalonia project, CommunityToolkit.Mvvm wired, empty single-window shell, DI container, logging, app-data paths for Win/Mac.
2. **OAuth + "Connect account".** Loopback flow, keychain token store (both platforms), connected/disconnected state in top bar.
3. **Stream form view.** Every field in §5, validation, dirty tracking. No data source yet — just the form.
4. **Fetch current stream into form.** `liveBroadcasts.list` + underlying video + thumbnail URL. Refresh button. Live indicator.
5. **Apply form to live stream.** `liveBroadcasts.update` + `videos.update` + success re-fetch. Error surfacing per step.
6. **Category + language dropdowns from API** with on-disk cache.
7. **Preset store + Load preset / Save as preset / Update preset.** `presets.json` load/save, preset picker dropdown, dialogs, lineage tracking in form.
8. **Thumbnail picker + upload path.** File dialog, copy into `thumbnails/`, preview in form, `thumbnails.set` on Apply when changed.
9. **Packaging.** `dotnet publish` profiles for `win-x64` and `osx-arm64` (+ `osx-x64` if wanted). No code-signing for v1.
10. **README + first-run docs** (how to get a Google Cloud OAuth client ID).

## 11. Known risks / unknowns

- **`thumbnails.set` scope.** Setting a thumbnail requires `youtube.upload` scope — noted in §4; means the consent screen will mention "upload videos" which may look scary. Document in README.
- **API field drift.** YouTube occasionally deprecates `contentDetails` fields (e.g., `enableLowLatency` is legacy in favor of `latencyPreference`). Store both, prefer `latencyPreference` on apply, log a warning if the API rejects a field so we notice.
- **No transactional apply.** If `videos.update` succeeds but `thumbnails.set` fails, the broadcast is in a partially-updated state. Acceptable for v1; we surface which step failed so the user can retry.
- **macOS Gatekeeper.** Unsigned binaries trigger a "cannot be opened" prompt. For v1 we document the `xattr -d com.apple.quarantine` workaround in the README; code-signing + notarization is out of scope.

## 12. Open design decisions

Flagged items still needing a call — not blocking the first slice, but worth settling before we harden things.

1. **OAuth client ID provisioning.** Two options:
   - **(a) Ship a client ID in-repo.** User just clicks Connect and signs in. PKCE protects the flow, so leaking the client ID is not a security issue. Downside: Google's verified-app limits apply to us (100-user test cap until verified; verification requires a privacy policy + homepage).
   - **(b) User brings their own.** First-run wizard asks for a client ID (and optionally secret) from their own Google Cloud project. More setup, but no quota/verification ceiling, and each user owns their own consent screen.
   - **Recommendation for v1:** (b) — it's a personal tool, the README will walk the user through creating one, and it sidesteps Google's verification requirements entirely.

2. **Reauth on 401.** When a refresh token is revoked (user changes password, revokes app, etc.) the next API call returns 401. Should we: auto-prompt a re-connect flow, or just surface the error and let the user click Connect again? Default: surface + require explicit click — keeps UX predictable.

3. **Refresh vs. dirty form.** §6.2 re-fetches from YouTube after Apply and on Refresh click. If the user has unsaved form edits and clicks Refresh, do we: (a) confirm-before-overwrite like §6.3, or (b) silently overwrite? Default: confirm, mirroring §6.3.

4. **Orphaned thumbnail files.** If the user picks a thumbnail in the form but never saves as a preset, the copied file in `thumbnails/` is orphaned. Options: (a) defer the copy until Save-as-preset or Apply; (b) keep a registry + sweep orphans on startup. **Recommendation:** (a) — don't copy into `thumbnails/` until the file is committed to a preset or actually uploaded. Use a temp path in the meantime.

5. **`presets.json` schema versioning.** Include a top-level `"schemaVersion": 1` so future migrations are sane. Cheap to add now, painful to retrofit.

6. **Log level control.** Default Information; expose a Settings toggle for Debug. Log files rotate daily, keep last 7.

7. **App icon + branding.** Out of scope for v1 beyond a placeholder. Tracked here so we don't forget before a public release.
