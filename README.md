# TweetDeleter

A .NET CLI tool that uses Playwright to delete tweets via UI interactions. This is ToS-compliant as it simulates real user actions in the browser rather than using restricted APIs.

## Features

- **UI-based deletion**: Uses Playwright to interact with Twitter/X exactly as a human would
- **Direct Chromium launch with persistent session**: Automatically opens Chromium and keeps your session logged in across runs via `.browser-profile`
- **Zero debug flags required**: No need to manually start external Chrome instances with remote debugging flags
- **Automatic operation**: Continuously deletes tweets and reposts until none remain
- **Safe operation**: Only deletes YOUR tweets (the delete option only appears for your own content)

## Prerequisites

- .NET 10.0 SDK
- PowerShell / pwsh (to install Playwright browser binaries)

## Installation

1. Build the project:
   ```bash
   dotnet build
   ```

2. Install Playwright Chromium browser and OS dependencies (first time only):
   - **Linux / DevContainer / macOS:**
     ```bash
     pwsh bin/Debug/net10.0/playwright.ps1 install chromium
     pwsh bin/Debug/net10.0/playwright.ps1 install-deps chromium
     ```
   - **Windows (PowerShell):**
     ```powershell
     powershell -ExecutionPolicy Bypass -File bin/Debug/net10.0/playwright.ps1 install chromium
     ```

## Usage

### Step 1: Run the Tool

```bash
dotnet run
```

Or with your Twitter/X username as an argument:

```bash
dotnet run -- yourusername
```

### Step 2: Choose What to Delete

The tool will prompt you to choose:
1. **Posts** - Your original tweets
2. **Replies** - Your replies to other tweets

### Step 3: Log in (First Time Only)

1. The tool will automatically launch a Chromium browser window.
2. If you are not logged in, the browser will navigate to `https://x.com/login` and prompt you in the console to sign in.
3. Sign in to your Twitter/X account in the opened browser window.
4. The tool will **automatically detect** when you have finished logging in (via the session cookie) and proceed to delete your tweets.
5. Your login session is saved in `.browser-profile/`, so subsequent runs will skip login entirely.

The tool will then automatically:
- Navigate to the selected section
- Find and delete your tweets
- Unrepost any reposted content
- Scroll to load more tweets
- Continue until no more tweets are found

## How It Works

1. Launches Chromium with a persistent context stored in `.browser-profile/`
2. Checks whether your Twitter/X account is authenticated; prompts for interactive login if needed
3. Navigates to your profile's posts or replies section
4. For each visible tweet:
   - If it's a repost → unrepost it
   - If it's your original tweet → delete it
   - If it's someone else's tweet → skip it
5. Scrolls down to load more tweets
6. Periodically refreshes the page to ensure fresh content
7. Stops after 5 consecutive attempts with no deletions
8. Flushes and saves session context upon completion

## Output

The tool provides real-time feedback:
- 🗑️ Deleted! - Original tweet was deleted
- ↩️ Unreposted! - Repost was removed
- ⏭️ Skipped - Tweet couldn't be deleted (not yours)
- 🔄 Refreshing - Page refresh to load fresh content

Final summary shows total deleted and unreposted counts.

## Configuration

The following constants can be adjusted in `Program.cs`:

| Constant | Default | Description |
|----------|---------|-------------|
| `MAX_CONSECUTIVE_FAILURES` | 5 | Stop after this many failed attempts |
| `CHECKS_BEFORE_REFRESH` | 3 | Refresh page after this many check cycles |
| `DELAY_BETWEEN_TWEETS_MS` | 1500 | Delay between processing tweets |
| `DELAY_BETWEEN_BATCHES_MS` | 2000 | Delay between batches |
| `SCROLL_DELAY_MS` | 2000 | Delay after scrolling |

## Troubleshooting

**"error while loading shared libraries: libnspr4.so: cannot open shared object file" or "Failed to launch Chromium"**
- On Linux / DevContainers, install the required native system dependencies:
  ```bash
  pwsh bin/Debug/net10.0/playwright.ps1 install-deps chromium
  ```
- Make sure the Chromium binary itself is installed:
  - Linux/macOS: `pwsh bin/Debug/net10.0/playwright.ps1 install chromium`
  - Windows: `powershell -ExecutionPolicy Bypass -File bin/Debug/net10.0/playwright.ps1 install chromium`

**"No delete option"**
- You're viewing someone else's tweet
- Twitter's UI may have changed (selectors need updating)

**Tool stops too early**
- Increase `MAX_CONSECUTIVE_FAILURES` in the code
- Some tweets may be stuck - try manually scrolling in the browser

## Notes

- The browser opens in non-headless mode so you can interact with login and observe the progress.
- Rate limiting is built-in with delays between actions.
- Your session is preserved in `.browser-profile/` (which is ignored by Git).
- Press `Ctrl+C` to stop the tool at any time.

## License

MIT