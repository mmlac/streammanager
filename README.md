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
- One thumbnail image per preset, stored locally.
- Local-only storage: presets live on your machine; the app never talks
  to anything except YouTube.

## Requirements

- .NET 10 runtime (the installer bundles it; no separate install needed
  if you use the self-contained release).
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

On macOS the app is unsigned. On first launch macOS will refuse to open it.
To allow it:

```sh
xattr -d com.apple.quarantine /path/to/StreamManager.app
```

Or: right-click the app → Open → Open in the warning dialog.

### Build from source

```sh
git clone https://github.com/mmlac/streammanager.git
cd streammanager
dotnet run --project src/StreamManager.App
```

Requires the .NET 10 SDK.

## First-run setup

StreamManager needs a Google Cloud OAuth client to talk to YouTube on your
behalf. This is a one-time setup.

### 1. Create a Google Cloud project

1. Go to https://console.cloud.google.com/ and sign in.
2. Click the project selector at the top → **New Project**.
3. Name it anything (e.g. `StreamManager`). Click **Create**.
4. Make sure the new project is selected in the top bar.

### 2. Enable the YouTube Data API v3

1. In the left menu: **APIs & Services** → **Library**.
2. Search for **YouTube Data API v3**. Click it.
3. Click **Enable**.

### 3. Configure the OAuth consent screen

1. **APIs & Services** → **OAuth consent screen**.
2. User type: **External**. Click **Create**.
3. Fill in:
   - **App name:** `StreamManager` (or whatever you want).
   - **User support email:** your email.
   - **Developer contact email:** your email.
4. Click **Save and Continue** through the Scopes and Test users steps —
   you can add yourself as a Test user on the Test users step, which
   avoids the "unverified app" warning for your own account.
5. Click **Back to Dashboard**.

The app will stay in "Testing" mode. This is fine for personal use — you
won't hit Google's verification requirements as long as you're only
signing in with Test users you added (up to 100).

### 4. Create OAuth credentials

1. **APIs & Services** → **Credentials**.
2. **+ Create Credentials** → **OAuth client ID**.
3. **Application type:** `Desktop app`.
4. **Name:** `StreamManager desktop`.
5. Click **Create**.
6. Copy the **Client ID** and **Client secret** from the dialog (or
   download the JSON).

### 5. Paste the credentials into StreamManager

1. Launch StreamManager.
2. It will prompt for your OAuth client ID + secret on first run. Paste
   them in.
3. Click **Connect YouTube account**. Your browser will open a Google
   sign-in page.
4. Sign in with your streaming account. Grant the requested scopes:
   - View and manage your YouTube account
   - Manage your YouTube videos (required for the "upload" scope that
     `thumbnails.set` needs — we do not actually upload videos)
5. The browser will redirect back and StreamManager will show your
   channel name in the top bar.

**Where are my credentials stored?**
- The OAuth client ID + secret: in `config.json` in the app data folder
  (see [Data locations](#data-locations)).
- The refresh token from Google: in your OS keychain (Keychain on macOS,
  Credential Manager on Windows). Never in a file.

## Using StreamManager

### When you're live
- The form populates from your active broadcast on connect and on
  **Refresh**.
- Edit anything you want.
- **Apply to live stream** pushes the form to YouTube. On success, the
  form re-fetches so you see exactly what YouTube applied.

### Presets
- **Load preset ▾** replaces the form with a saved preset's values. If
  the form has unsaved changes, you'll be asked to confirm.
- **Save as preset…** stores the current form as a new named preset.
- **Update preset "X"** (only when the form was loaded from preset X)
  overwrites preset X with the current form.

### Switching games mid-stream
1. Click **Load preset ▾** → pick the game's preset.
2. Click **Apply to live stream**.
3. Done.

## Data locations

- **Windows:** `%APPDATA%\streammanager\`
- **macOS:** `~/Library/Application Support/streammanager/`

```
streammanager/
  config.json          # OAuth client ID + secret, app settings
  presets.json         # all presets
  thumbnails/          # per-preset thumbnail images
  cache/               # videoCategories + languages
  logs/                # rolling daily log files
```

To wipe all local data: quit the app and delete that directory. To
revoke the app's access to your Google account, visit
https://myaccount.google.com/permissions.

## Troubleshooting

- **"Not live" when you're actually live.** Click **Refresh**. YouTube
  can take ~30s after going live before `liveBroadcasts.list(active)`
  returns it.
- **Apply fails with 401.** Your refresh token was revoked. StreamManager
  pops a "YouTube access expired" modal automatically — click **Reconnect**
  to redo the consent flow, then your action retries. If the modal doesn't
  appear, click **Disconnect** in the top bar and Connect again.
- **Apply fails with 403 + "quota exceeded".** Extremely unlikely — one
  Apply is ~150 quota units against a daily 10,000 default. Check that
  the YouTube Data API v3 is still enabled in Google Cloud.
- **macOS "cannot be opened".** See [macOS install notes](#prebuilt-binaries).

## Contributing

Design doc: [`docs/design.md`](docs/design.md). Work is tracked as beads
in `.beads/` (Gas Town tooling). Open an issue or a PR.

## License

See [LICENSE](LICENSE).
