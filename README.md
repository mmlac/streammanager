# StreamManager

A desktop app for managing a YouTube Live broadcast's metadata without
re-entering it every time you switch games.

The main view is a form populated from your **active broadcast**. You edit
it directly, or **load a preset** into it, then click **Apply** to push the
form to YouTube. Presets are a source/sink — load from, save as, update.

Supported platforms: **Windows** and **macOS** (Linux may work via Avalonia
but is not tested or packaged).

See [`docs/design.md`](docs/design.md) for the full design.

## Features

- Fetch the current live broadcast's metadata into an editable form.
- One-click **Apply** pushes the form (title, description, category, tags,
  privacy, DVR / embed / captions / latency / projection / language,
  thumbnail, and everything else the YouTube Data API v3 exposes on a
  broadcast) to the active stream.
- Save any form state as a named **preset**, or update an existing one.
- One thumbnail image per preset, stored as an absolute reference to the
  file on disk (no copy).
- Local-only storage: presets live on your machine; the app never talks
  to anything except YouTube.

## Requirements

- .NET 10 runtime (the self-contained release bundles it; no separate
  install needed).
- A Google account with YouTube Live streaming enabled.
- A Google Cloud OAuth 2.0 **Desktop** client ID (you create this once —
  see [First-run setup](#first-run-setup)).
- macOS 12+ or Windows 10+.

## Install

### Prebuilt binaries

Download the latest release from the [Releases page](https://github.com/mmlac/streammanager/releases):

- **Windows:** `StreamManager-win-x64.zip` — unzip, run `StreamManager.exe`.
- **macOS (Apple Silicon):** `StreamManager-osx-arm64.zip`.
- **macOS (Intel):** `StreamManager-osx-x64.zip`.

On macOS the app is unsigned. On first launch macOS will refuse to open it
with a "cannot be opened because the developer cannot be verified" dialog.
To allow it, run once in Terminal:

```sh
xattr -d com.apple.quarantine /path/to/StreamManager.app
```

Or: right-click the app → **Open** → **Open** in the warning dialog.

### Build from source

```sh
git clone https://github.com/mmlac/streammanager.git
cd streammanager
dotnet run --project src/StreamManager.App
```

Requires the .NET 10 SDK.

To build the distributable binaries yourself (self-contained, single-file,
iconned for each platform), bump `VERSION` if you want and run:

```sh
# macOS / Linux
scripts/publish.sh --verify

# Windows (PowerShell 7+)
pwsh scripts/publish.ps1 -Verify
```

Artifacts land in `artifacts/<rid>/` — `StreamManager.exe` for `win-x64`,
`StreamManager.app/` for the `osx-arm64` / `osx-x64` runtimes. The scripts
generate the app icon from `streammanager.png`, stamp the `VERSION` file
into the exe properties and the `.app` `Info.plist`, and with `--verify`
sanity-check the output (no stray `config.json` / `*.log`, Info.plist
version matches, PE32 magic on the Windows binary).

## First-run setup

StreamManager needs a Google Cloud OAuth client to talk to YouTube on your
behalf. This is a one-time setup — roughly 5 minutes once you know where
things live in the Cloud Console.

> **Screenshots were captured 2026-04.** Google redesigns this Console often;
> if yours looks different, the flow and the field names below should still
> match. When in doubt, Google's own [OAuth client creation docs](https://developers.google.com/identity/protocols/oauth2)
> are the source of truth.

### 1. Create a Google Cloud project

1. Go to <https://console.cloud.google.com/> and sign in.
2. Click the project selector at the top → **New Project**.
3. Name it anything (e.g. `StreamManager`). Click **Create**.
4. Make sure the new project is selected in the top bar.

![New project dialog](docs/img/01-new-project.png)

### 2. Enable the YouTube Data API v3

1. In the left menu: **APIs & Services** → **Library**.
2. Search for **YouTube Data API v3**. Click it.
3. Click **Enable**.

![Enable YouTube Data API v3](docs/img/02-enable-api.png)

### 3. Configure the OAuth consent screen

1. **APIs & Services** → **OAuth consent screen**.
2. User type: **External**. Click **Create**.
3. Fill in:
   - **App name:** `StreamManager` (or whatever you want).
   - **User support email:** your email.
   - **Developer contact email:** your email.
4. On the **Scopes** step, you can skip adding scopes here — StreamManager
   requests them at sign-in time.
5. On the **Test users** step, add the Google account(s) you plan to sign
   in with. This is what lets you use the app while it's still in "Testing"
   mode without hitting "unverified app" warnings.
6. Click **Save and Continue** → **Back to Dashboard**.

![OAuth consent screen — app info](docs/img/03-consent-screen.png)

The app will stay in "Testing" mode. This is fine for personal use — up to
100 test users, refresh tokens don't expire from mode alone, and you never
need to submit for verification.

### 4. Create OAuth credentials

1. **APIs & Services** → **Credentials**.
2. **+ Create Credentials** → **OAuth client ID**.
3. **Application type:** `Desktop app`. (This matters — StreamManager uses
   the loopback redirect flow, which is only enabled for Desktop clients.)
4. **Name:** `StreamManager desktop`.
5. Click **Create**.
6. Copy the **Client ID** and **Client secret** from the dialog (or click
   **Download JSON** — both values are in there).

![Create OAuth client ID](docs/img/04-create-client.png)

> **A note on the "upload videos" scope.** At sign-in time, StreamManager
> requests `youtube` **and** `youtube.upload`. The upload scope is
> required by the `thumbnails.set` endpoint (Google groups thumbnail upload
> under the same scope as video upload). StreamManager never uploads videos
> — the only write-side calls it makes are `liveBroadcasts.update`,
> `videos.update`, and `thumbnails.set`. See [`docs/design.md §4`](docs/design.md#4-youtube-api-surface)
> for the exact API surface.

### 5. Paste the credentials into StreamManager

Launch StreamManager. On first launch you'll see the **Welcome to
StreamManager** wizard:

![First-run wizard](docs/img/05-first-run.png)

1. Paste the **Client ID** into the first field.
2. Paste the **Client Secret** into the second field (hidden with dots as
   you type — the value is stored in `config.json`; see [Data locations](#data-locations)).
3. Click **Save and continue**.

You'll land on the main window with a **Connect YouTube account** button
in the top bar:

![Connect account button](docs/img/06-connect-account.png)

1. Click **Connect YouTube account**. Your default browser opens to the
   Google sign-in page.
2. Sign in with the streaming account you added as a Test user in step 3.
3. Grant the requested scopes (YouTube account + YouTube upload, per the
   scope note above).
4. The browser redirects back to a local loopback URL; StreamManager
   finishes the handshake and the top bar now shows your account email
   with a **Disconnect** button next to it.

**Where are my credentials stored?**

- The OAuth client ID + secret: in `config.json` in the app data folder
  (see [Data locations](#data-locations)).
- The refresh token from Google: in your OS keychain. Never in a file.
  - **macOS:** a generic password with service `streammanager` and
    account `youtube_refresh_token`. View it in Keychain Access or with
    `security find-generic-password -s streammanager -a youtube_refresh_token -g`.
  - **Windows:** a generic credential in Credential Manager with target
    `streammanager:youtube_refresh_token` (Control Panel → User Accounts
    → Credential Manager → **Windows Credentials**).

## Using StreamManager

### Daily flow

1. Go live in OBS (or whatever you use) as normal.
2. Open StreamManager. Click **Refresh** — the form populates from the
   active broadcast.
3. Edit what you want directly, or click **Load preset ▾** and pick a
   saved preset.
4. Click **Apply to live stream**. On success, the form re-fetches so you
   see exactly what YouTube applied.

### Presets

- **Load preset ▾** replaces the form with a saved preset's values. If
  the form has unsaved changes, you'll be asked to confirm.
- **Save as preset…** stores the current form as a new named preset.
- **Update preset "X"** (only visible when the form was loaded from preset
  X and has since been edited) overwrites preset X with the current form.

### Switching games mid-stream

1. Click **Load preset ▾** → pick the game's preset.
2. Click **Apply to live stream**.
3. Done.

## Data locations

| What                          | Path                                                                 |
|-------------------------------|----------------------------------------------------------------------|
| App data root (Windows)       | `%APPDATA%\streammanager\`                                           |
| App data root (macOS)         | `~/Library/Application Support/streammanager/`                       |
| OAuth client ID + secret      | `<AppData>/config.json`                                              |
| Presets                       | `<AppData>/presets.json`                                             |
| API response caches           | `<AppData>/cache/categories.json`, `<AppData>/cache/languages.json`  |
| Rolling logs                  | `<AppData>/logs/streammanager-<date>.log`                            |
| Refresh token (macOS)         | Keychain — service `streammanager`, account `youtube_refresh_token`  |
| Refresh token (Windows)       | Credential Manager — target `streammanager:youtube_refresh_token`    |

Thumbnails are **not** copied into `<AppData>`. Each preset stores the
absolute path to the image file you picked, and StreamManager reads the
bytes from that path at Apply time (see [Troubleshooting](#troubleshooting)
for what happens when the file is unreachable).

## Settings

The gear icon in the top bar opens the Settings menu:

- **Open data folder** — reveal `<AppData>/streammanager/` in your file
  manager.
- **Log level** — toggle between **Warn** (default; quiet) and **Debug**
  (verbose; for capturing a bug). The change takes effect immediately —
  no restart. Flip to Debug, reproduce the issue, then attach the latest
  file in `<AppData>/logs/` to your bug report.
- **Refresh categories / languages** — force a re-fetch of the YouTube
  category and language dropdown data (normally cached for 30 days).
- **Region code** — which country's category list to fetch (default `US`).
  Changing it re-fetches the category dropdown without a restart.
- **Disconnect** — forget the refresh token from the keychain (equivalent
  to the top-bar **Disconnect** button).

## Troubleshooting

- **"Not live" when you're actually live.** Click **Refresh**. YouTube
  can take ~30s after going live before `liveBroadcasts.list(active)`
  returns it.

- **"YouTube access expired" modal on Apply or Refresh.** Your refresh
  token was revoked (e.g. you changed your Google password, or 180 days
  passed without use). Click **Reconnect** in the modal to redo the
  consent flow; your original action retries once that finishes. If the
  modal doesn't appear, click **Disconnect** in the top bar and Connect
  again manually.

- **"Thumbnail file not reachable" warning on Apply.** StreamManager
  couldn't read the file referenced by the preset's `thumbnailPath`. The
  file may be on a detached external drive, an evicted cloud-sync folder
  (OneDrive / Dropbox online-only files), or has been moved or deleted.
  Two choices:
  - **Apply without updating thumbnail** — push title / description /
    everything else, skip `thumbnails.set`. The live stream keeps its
    current thumbnail.
  - **Cancel** — fix the file (plug the drive back in, sync the folder,
    re-pick the image) and try again.

  **StreamManager never auto-clears a `thumbnailPath` reference.** If
  the path is unreachable we keep it saved, because the common cause is
  a temporarily-unavailable file the user will reconnect. To point a
  preset at a different image, load it and pick a new file.

- **Apply fails with 403 + "quota exceeded".** Extremely unlikely — one
  Apply is ~150 quota units against a daily 10,000 default. Check that
  the YouTube Data API v3 is still enabled in Google Cloud.

- **macOS "cannot be opened because the developer cannot be verified".**
  See [macOS install notes](#prebuilt-binaries) for the
  `xattr -d com.apple.quarantine` workaround.

- **Dropdowns stuck at "Loading…".** You launched before the initial
  category / language fetch finished and no cache existed yet. The fields
  fall back to free-text so Apply isn't blocked. Try **Refresh categories /
  languages** in the Settings menu once you have network access; after
  that, the cache lives for 30 days.

## Uninstalling

StreamManager leaves state in two places: the app data folder and the OS
keychain. Remove both to fully wipe it.

**macOS:**

```sh
# Delete local app data (presets, config, caches, logs).
rm -rf "$HOME/Library/Application Support/streammanager"

# Remove the refresh token from Keychain.
security delete-generic-password -s streammanager -a youtube_refresh_token

# If you installed the .app bundle:
rm -rf /Applications/StreamManager.app
```

**Windows (PowerShell):**

```powershell
# Delete local app data.
Remove-Item -Recurse -Force "$env:APPDATA\streammanager"

# Remove the refresh token from Credential Manager.
cmdkey /delete:streammanager:youtube_refresh_token
```

To also revoke the app's access to your Google account (independent of
uninstalling), visit <https://myaccount.google.com/permissions> and
remove **StreamManager** from the list.

## Contributing

Design doc: [`docs/design.md`](docs/design.md). Work is tracked as beads
in `.beads/` (Gas Town tooling). Open an issue or a PR.

## License

See [LICENSE](LICENSE).
