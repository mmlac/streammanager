# README walkthrough screenshots

The root `README.md` first-run walkthrough references images in this
directory by relative path. They need to be captured against the live
Google Cloud Console + a running StreamManager build before release.

Inventory:

| File                          | What to capture                                                       |
|-------------------------------|------------------------------------------------------------------------|
| `01-new-project.png`          | Cloud Console project selector → **New Project** dialog.              |
| `02-enable-api.png`           | APIs & Services → Library, "YouTube Data API v3" result with Enable.  |
| `03-consent-screen.png`       | OAuth consent screen "App information" step, External / Testing.      |
| `04-create-client.png`        | Create OAuth client ID dialog with **Application type: Desktop app**. |
| `05-first-run.png`            | StreamManager first-run wizard (Client ID / Client Secret / Save).    |
| `06-connect-account.png`      | Main window top bar with **Connect YouTube account** button.          |

Conventions:

- PNG, ~1200px wide max, cropped to the relevant UI region.
- Redact any personal identifiers (email addresses, test-user lists,
  any copied client secret).
- Capture against the current Google Cloud Console UI and the latest
  StreamManager build. Re-verify quarterly — Google redesigns this
  console often.
- The root README has a dated footnote ("Screenshots were captured
  YYYY-MM"). Update it when you re-capture.
