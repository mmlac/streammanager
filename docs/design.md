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
| Thumbnail storage | Absolute path to the user-picked image, referenced in place (no copy) | Keeps the tool zero-footprint outside `<AppData>`; user retains full control of their image files |
| Logging | `Microsoft.Extensions.Logging` + Serilog file sink, level toggled in-app (Warn / Debug) via Settings | Standard; live toggle means users can flip to Debug to capture a bug without restarting |
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

`presets.json` is versioned at the file level so future migrations are sane:

```jsonc
{
  "schemaVersion": 1,
  "presets": [ /* preset objects below */ ]
}
```

Each preset object:

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
  "thumbnailPath": "/Users/me/Pictures/elden-ring-chill.jpg" // absolute path to user's image, referenced in place (not copied); null = don't touch thumbnail
}
```

Notes:
- Fields the API accepts but we **won't expose** in v1: `contentDetails.boundStreamId` (binding an RTMP stream — out of scope), `contentDetails.monitorStream.broadcastStreamDelayMs` boundaries beyond simple ms input, `liveChat.*` (read-only on broadcasts).
- "Tags" is a flat list; YouTube enforces a 500-char total limit across all tags — we validate on save.
- `thumbnailPath == null` means "don't change the current thumbnail on apply."
- `thumbnailPath` is stored as an absolute path and read from there at Apply time. If the file is missing/unreachable at Apply, we warn (§6.6) but never auto-clear the reference — the file may be on a detached external drive or an evicted cloud-sync folder.

## 6. Core flows

**Mental model.** The main form IS "the current stream". It is populated from
YouTube on connect/refresh. The user edits it directly OR loads a preset into
it. Presets are a source/sink — you can load from a preset, save the form as a
new preset, or overwrite the loaded preset with the form's current values.
Applying pushes the **form's current values** to YouTube.

### 6.1 First-run OAuth
1. **Prerequisite: OAuth client configured.** The user provides their own Google Cloud OAuth client (client ID + client secret, obtained by following the README). First launch shows a setup screen that accepts these values and writes them to `config.json`. Connect is disabled until a client is configured. (See §12 for why user-provided rather than shipped-in-repo.)
2. User clicks "Connect YouTube account".
3. Loopback redirect OAuth flow (`http://127.0.0.1:<ephemeral>`) opens system browser, using the user's configured client ID/secret.
4. Receive code → exchange for refresh + access token.
5. Store refresh token in OS keychain; access token in memory only.

### 6.2 Fetch current stream into form
1. Trigger: app start, Refresh button, or post-Apply re-fetch (§6.6).
2. If the form is dirty (relative to live or to its loaded preset) and the trigger would overwrite user edits, confirm before overwriting — mirrors §6.3. The post-Apply re-fetch skips this (user just pushed the edits, so "overwrite" is the desired outcome).
3. Call `liveBroadcasts.list(mine, active)`.
4. If one active broadcast: fetch the broadcast + its video snippet/contentDetails + current thumbnail URL, populate the form.
5. If no active broadcast: form shows last state (or empty on first run), Apply disabled, "Not live" indicator.
6. Thumbnail in the form is the remote URL (read-only preview) until the user picks a new local file.

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
1. **Pre-flight: thumbnail reachability.** If the form has a `thumbnailPath` set and the file is unreachable (doesn't exist, permission denied, unmounted volume), show a warning dialog: "Thumbnail file not reachable at `<path>`. Apply without updating thumbnail, or cancel?" Choosing *Apply without thumbnail* proceeds with steps 2–3 skipping `thumbnails.set`; Cancel aborts. **The `thumbnailPath` reference is never auto-cleared** — the file may be on a detached external drive or evicted cloud-sync folder that the user will reconnect.
2. `liveBroadcasts.update` with snippet + status + contentDetails from the form.
3. `videos.update` with categoryId, tags, languages from the form.
4. If the user picked a new thumbnail file since the last fetch AND the file is reachable: `thumbnails.set` (reads bytes from `thumbnailPath` directly). Otherwise skipped.
5. On success: re-fetch current stream into form (§6.2) so we see what YouTube actually applied, clear dirty indicators.
6. On any step failure: surface which step failed. No rollback — YouTube's API is not transactional.

### 6.7 Reauth on 401
If any YouTube API call returns 401 (refresh token revoked — user changed password, revoked app access, scope changed, etc.):
1. Surface a modal: "YouTube access expired. Reconnect?" with a Connect button.
2. On Connect, run §6.1 steps 2–5 against the existing configured OAuth client. New refresh token replaces the old one in the keychain.
3. After successful reauth, retry the original request. If the original request was a re-fetch (§6.2) that would overwrite a dirty form, apply the §6.2 step 2 confirmation before proceeding — reauth doesn't bypass dirty-form protection.
4. On reauth cancel or failure, surface the error and leave the form as-is. User can retry from the top bar.

### 6.8 Editor details
- Category and language dropdowns populated from API (cached in JSON, refreshed on demand).
- Thumbnail picker: file dialog → store the selected file's **absolute path** in form state (no copy). The remote thumbnail URL is replaced with a local preview rendered from that path. If the path becomes unreachable after pick (drive unmounted, file moved), the preview falls back to a placeholder with the path text; the reference is preserved.
- Validation (on Apply and on Save-as-preset): title ≤ 100 chars, description ≤ 5000, tags combined ≤ 500 chars. Thumbnail must exist and be ≤ 2MB and JPG/PNG/BMP/GIF per YouTube's rules — validated at pick time; at Apply time reachability is re-checked (§6.6 step 1) but not re-validated for size/format unless the file changed.

## 7. Storage layout

```
<AppData>/streammanager/
  config.json          # OAuth client ID + secret, log level, window geometry
  presets.json         # all presets (with schemaVersion); thumbnails referenced by absolute path, no app-managed copies
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

- **Top bar:** connected account, live indicator (green=live, grey=not live), Refresh, Settings menu (disconnect, open data folder, log level toggle: **Warn** / **Debug**).
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
streammanager.png                 # app icon (used for Windows .exe and macOS .app bundle)
README.md                         # user setup incl. creating Google Cloud OAuth client
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
8. **Thumbnail picker + upload path.** File dialog, store absolute path in form state, preview in form, Apply-time reachability check, `thumbnails.set` on Apply when changed and reachable.
9. **Packaging.** `dotnet publish` profiles for `win-x64` and `osx-arm64` (+ `osx-x64` if wanted), wiring `streammanager.png` as the Windows `.exe` icon and macOS `.app` bundle icon (converted to `.icns` at build time). No code-signing for v1.
10. **README + first-run docs** (how to get a Google Cloud OAuth client ID).

## 11. Known risks / unknowns

- **`thumbnails.set` scope.** Setting a thumbnail requires `youtube.upload` scope — noted in §4; means the consent screen will mention "upload videos" which may look scary. Document in README.
- **API field drift.** YouTube occasionally deprecates `contentDetails` fields (e.g., `enableLowLatency` is legacy in favor of `latencyPreference`). Store both, prefer `latencyPreference` on apply, log a warning if the API rejects a field so we notice.
- **No transactional apply.** If `videos.update` succeeds but `thumbnails.set` fails, the broadcast is in a partially-updated state. Acceptable for v1; we surface which step failed so the user can retry.
- **macOS Gatekeeper.** Unsigned binaries trigger a "cannot be opened" prompt. For v1 we document the `xattr -d com.apple.quarantine` workaround in the README; code-signing + notarization is out of scope.

## 12. Resolved design decisions

All items previously flagged as open are now decided; captured here with pointers to the body.

- **OAuth client ID provisioning** → user brings their own Google Cloud OAuth client (client ID + secret) via first-run setup. Sidesteps Google verification and quota caps; README walks the user through creation. See §6.1.
- **Reauth on 401** → auto-prompt a reconnect modal, then retry the original request. Dirty-form protection from §6.2 still applies to any post-reauth re-fetch. See §6.7.
- **Refresh vs. dirty form** → confirm-before-overwrite, mirroring §6.3. Applies to Refresh button and post-reauth re-fetch. Post-Apply re-fetch does not prompt (user just pushed the edits). See §6.2.
- **`presets.json` schema versioning** → added `schemaVersion: 1` at the file envelope level. See §5.
- **Orphaned thumbnail files** → no longer possible: thumbnails are referenced in place by absolute path, never copied. At Apply time we check reachability and warn if the file is missing, but never auto-clear the reference (may be on a detached drive or evicted cloud-sync folder the user will reconnect). See §3, §5, §6.6, §6.8.
- **Log level control** → in-app Settings toggle with two levels: **Warn** (default) and **Debug**. Takes effect immediately without restart; persisted in `config.json`. Log files rotate daily, keep last 7. See §6.8 (surfaced in Settings menu) and §7.
- **App icon + branding** → `streammanager.png` lives at the repo root and is the app icon for Windows + macOS builds (wired into `dotnet publish` per-OS asset config). No further branding in v1.
