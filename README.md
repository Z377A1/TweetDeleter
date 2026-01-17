# TweetDeleter

A .NET CLI tool that uses Playwright to delete tweets via UI interactions. This is ToS-compliant as it simulates real user actions in the browser rather than using restricted APIs.

## Features

- **UI-based deletion**: Uses Playwright to interact with Twitter/X exactly as a human would
- **Connects to existing Chrome**: Uses Chrome DevTools Protocol (CDP) to connect to your already logged-in browser
- **Automatic operation**: Continuously deletes tweets and reposts until none remain
- **Safe operation**: Only deletes YOUR tweets (the delete option only appears for your own content)

## Prerequisites

- .NET 9.0 SDK
- Chrome browser installed

## Installation

1. Build the project:
   ```bash
   dotnet build
   ```

2. Install Playwright browsers (first time only):
   ```powershell
   powershell -ExecutionPolicy Bypass -File bin/Debug/net9.0/playwright.ps1 install chromium
   ```

## Usage

### Step 1: Start Chrome with Remote Debugging

Close all Chrome windows, then start Chrome with the remote debugging port:

**Windows (Command Prompt) - Isolated Instance:**
```cmd
set TMPDIR=%TEMP%\chrome-debug-%RANDOM%
"C:\Program Files\Google\Chrome\Application\chrome.exe" ^
  --remote-debugging-port=9222 ^
  --no-first-run ^
  --no-default-browser-check ^
  --user-data-dir="%TMPDIR%"
```

**Windows (PowerShell) - Isolated Instance:**
```powershell
$tmpdir = "$env:TEMP\chrome-debug-$(Get-Random)"
& "C:\Program Files\Google\Chrome\Application\chrome.exe" `
  --remote-debugging-port=9222 `
  --no-first-run `
  --no-default-browser-check `
  --user-data-dir="$tmpdir"
```

**Simple (uses default profile):**
```cmd
"C:\Program Files\Google\Chrome\Application\chrome.exe" --remote-debugging-port=9222
```

> **Note:** Using `--user-data-dir` creates an isolated Chrome instance with a fresh profile. This avoids conflicts with your main Chrome profile and ensures a clean state for automation.

### Step 2: Log in to Twitter/X

In the Chrome window that opens, navigate to https://x.com and log in to your account.

### Step 3: Run the Tool

```bash
dotnet run
```

Or with your username as an argument:

```bash
dotnet run -- yourusername
```

### Step 4: Choose What to Delete

The tool will prompt you to choose:
1. **Posts** - Your original tweets
2. **Replies** - Your replies to other tweets

The tool will then automatically:
- Navigate to the selected section
- Find and delete your tweets
- Unrepost any reposted content
- Scroll to load more tweets
- Continue until no more tweets are found

## How It Works

1. Connects to your existing Chrome browser via CDP on port 9222
2. Finds a Twitter/X tab (or uses the first available tab)
3. Navigates to your profile's posts or replies section
4. For each visible tweet:
   - If it's a repost → unrepost it
   - If it's your original tweet → delete it
   - If it's someone else's tweet → skip it
5. Scrolls down to load more tweets
6. Periodically refreshes the page to ensure fresh content
7. Stops after 5 consecutive attempts with no deletions

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

**"Failed to connect to Chrome"**
- Make sure Chrome is running with `--remote-debugging-port=9222`
- Close all other Chrome windows before starting with the debug flag

**"No browser contexts found"**
- Open at least one tab in Chrome before running the tool

**"No delete option"**
- You're viewing someone else's tweet
- Twitter's UI may have changed (selectors need updating)

**Tool stops too early**
- Increase `MAX_CONSECUTIVE_FAILURES` in the code
- Some tweets may be stuck - try manually scrolling in the browser

## Notes

- The browser must be visible (non-headless) since you're using an existing Chrome instance
- Rate limiting is built-in with delays between actions
- If Twitter changes their UI, selectors may need updating
- Press Ctrl+C to stop the tool at any time

## License

MIT