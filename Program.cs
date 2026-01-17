using Microsoft.Playwright;

Console.WriteLine("🐦 Tweet Deleter - Fully Automatic Edition");
Console.WriteLine("==========================================\n");

// Get username from args or prompt
string? username = args.Length > 0 ? args[0] : null;
if (string.IsNullOrEmpty(username))
{
    Console.Write("Enter your Twitter/X username (without @): ");
    username = Console.ReadLine()?.Trim();
    if (string.IsNullOrEmpty(username))
    {
        Console.WriteLine("❌ Username is required!");
        return;
    }
}

// Choose which tab to clean
Console.WriteLine("\nWhich section do you want to clean?");
Console.WriteLine("  [1] Posts        - https://x.com/{0}", username);
Console.WriteLine("  [2] Replies      - https://x.com/{0}/with_replies", username);
Console.Write("\nEnter choice (1-2): ");

var choice = Console.ReadLine()?.Trim();
string targetUrl = choice switch
{
    "1" => $"https://x.com/{username}",
    "2" => $"https://x.com/{username}/with_replies",
    _ => $"https://x.com/{username}"
};

string sectionName = choice switch
{
    "1" => "Posts",
    "2" => "Replies",
    _ => "Posts"
};

Console.WriteLine($"\n✅ Selected: {sectionName}");
Console.WriteLine($"   URL: {targetUrl}");

// Initialize Playwright and connect to existing Chrome via CDP
Console.WriteLine("\n🔌 Connecting to existing Chrome browser...");
Console.WriteLine("   Make sure Chrome is running with: --remote-debugging-port=9222\n");

using var playwright = await Playwright.CreateAsync();

IBrowser browser;
try
{
    browser = await playwright.Chromium.ConnectOverCDPAsync("http://localhost:9222");
    Console.WriteLine("✅ Connected to Chrome!");
}
catch (Exception ex)
{
    Console.WriteLine($"❌ Failed to connect to Chrome: {ex.Message}");
    Console.WriteLine("\nTo start Chrome with remote debugging:");
    Console.WriteLine("  Windows: chrome.exe --remote-debugging-port=9222");
    return;
}

var contexts = browser.Contexts;
if (contexts.Count == 0)
{
    Console.WriteLine("❌ No browser contexts found. Open a tab in Chrome first.");
    return;
}

var context = contexts[0];
var pages = context.Pages;

Console.WriteLine($"📑 Found {pages.Count} open tab(s)");

// Find a Twitter/X tab or use the first one
var page = pages.FirstOrDefault(p => p.Url.Contains("x.com") || p.Url.Contains("twitter.com"));
if (page == null)
{
    page = pages[0];
    Console.WriteLine($"   No Twitter tab found, using: {page.Url}");
}
else
{
    Console.WriteLine($"   Using Twitter tab: {page.Url}");
}

// Selectors
const string TWEET_SELECTOR = "article[data-testid=\"tweet\"]";
const string MENU_BUTTON_SELECTOR = "button[data-testid=\"caret\"]";
const string CONFIRM_DELETE_SELECTOR = "button[data-testid=\"confirmationSheetConfirm\"]";
const string UNREPOST_BUTTON_SELECTOR = "button[data-testid=\"unretweet\"]";
const string CONFIRM_UNREPOST_SELECTOR = "[data-testid=\"unretweetConfirm\"]";

// Navigate to selected section
Console.WriteLine($"\n📍 Navigating to {sectionName}...");
await page.GotoAsync(targetUrl, new() { WaitUntil = WaitUntilState.DOMContentLoaded });
await page.WaitForTimeoutAsync(3000);

Console.WriteLine($"\n🚀 Starting fully automatic deletion of {sectionName}...");
Console.WriteLine("   Press Ctrl+C to stop at any time.\n");

var totalDeleted = 0;
var totalUnreposted = 0;
var consecutiveFailures = 0;
var checkedSinceRefresh = 0;
const int MAX_CONSECUTIVE_FAILURES = 5;
const int CHECKS_BEFORE_REFRESH = 3;
const int DELAY_BETWEEN_TWEETS_MS = 1500;
const int DELAY_BETWEEN_BATCHES_MS = 2000;
const int SCROLL_DELAY_MS = 2000;

while (consecutiveFailures < MAX_CONSECUTIVE_FAILURES)
{
    var (deleted, unreposted) = await ProcessVisibleTweets(page, 5);
    var processed = deleted + unreposted;
    totalDeleted += deleted;
    totalUnreposted += unreposted;

    // Track checks regardless of success
    checkedSinceRefresh++;

    if (processed == 0)
    {
        consecutiveFailures++;
        Console.WriteLine($"\n⚠️  No tweets processed. (attempt {consecutiveFailures}/{MAX_CONSECUTIVE_FAILURES}, checked {checkedSinceRefresh}/{CHECKS_BEFORE_REFRESH})");
    }
    else
    {
        consecutiveFailures = 0;
        Console.WriteLine($"\n📊 Progress: Deleted {totalDeleted}, Unreposted {totalUnreposted} (checked {checkedSinceRefresh}/{CHECKS_BEFORE_REFRESH})");
    }

    // Refresh page after N checks (whether successful or not)
    if (checkedSinceRefresh >= CHECKS_BEFORE_REFRESH)
    {
        Console.WriteLine("   🔄 Refreshing page...");
        await page.ReloadAsync(new() { WaitUntil = WaitUntilState.DOMContentLoaded });
        await page.WaitForTimeoutAsync(3000);
        checkedSinceRefresh = 0;
    }
    else if (processed == 0)
    {
        // Scroll down to load more tweets
        await page.Mouse.WheelAsync(0, 1500);
        await page.WaitForTimeoutAsync(SCROLL_DELAY_MS);

        // Check if we've reached the end
        var tweets = await page.QuerySelectorAllAsync(TWEET_SELECTOR);
        if (tweets.Count == 0)
        {
            Console.WriteLine("   No tweets visible after scroll.");
        }
    }
    else
    {
        await page.WaitForTimeoutAsync(DELAY_BETWEEN_BATCHES_MS);
    }
}

Console.WriteLine("\n" + new string('=', 50));
Console.WriteLine("🏁 FINISHED!");
Console.WriteLine($"   Section:         {sectionName}");
Console.WriteLine($"   Total deleted:   {totalDeleted}");
Console.WriteLine($"   Total unreposted: {totalUnreposted}");
Console.WriteLine($"   Total processed: {totalDeleted + totalUnreposted}");
Console.WriteLine(new string('=', 50));

async Task<(int deleted, int unreposted)> ProcessVisibleTweets(IPage page, int maxCount)
{
    var deletedCount = 0;
    var unrepostedCount = 0;

    for (int i = 0; i < maxCount; i++)
    {
        try
        {
            var tweets = await page.QuerySelectorAllAsync(TWEET_SELECTOR);
            if (tweets.Count == 0)
            {
                Console.WriteLine("   No tweets found on page.");
                break;
            }

            // In replies view, there might be multiple tweets in a thread
            // We need to find OUR tweet (the reply), not the parent tweet
            // Our tweet typically has the caret menu, parent tweets don't
            IElementHandle? tweet = null;
            int tweetIndex = 0;

            for (int t = 0; t < Math.Min(tweets.Count, 3); t++)
            {
                var candidate = tweets[t];
                var menuButton = await candidate.QuerySelectorAsync(MENU_BUTTON_SELECTOR);
                if (menuButton != null)
                {
                    // Check if this tweet has delete option (meaning it's ours)
                    await menuButton.ClickAsync();
                    await page.WaitForTimeoutAsync(400);

                    var deleteOption = page.GetByRole(AriaRole.Menuitem, new() { Name = "Delete" });
                    try
                    {
                        await deleteOption.WaitForAsync(new() { Timeout = 1500 });
                        // Found our tweet - close menu and use this one
                        await page.Keyboard.PressAsync("Escape");
                        await page.WaitForTimeoutAsync(200);
                        tweet = candidate;
                        tweetIndex = t;
                        break;
                    }
                    catch
                    {
                        // Not our tweet, close menu and try next
                        await page.Keyboard.PressAsync("Escape");
                        await page.WaitForTimeoutAsync(200);
                    }
                }
            }

            // If no deletable tweet found in thread, check for repost on first tweet
            if (tweet == null)
            {
                tweet = tweets[0];
                tweetIndex = 0;
            }

            await tweet.ScrollIntoViewIfNeededAsync();
            await page.WaitForTimeoutAsync(300);

            // Check if this is a repost
            var socialContext = await tweet.QuerySelectorAsync("[data-testid=\"socialContext\"]");
            var socialContextText = socialContext != null ? await socialContext.TextContentAsync() : "";
            var isRepost = socialContextText?.Contains("reposted", StringComparison.OrdinalIgnoreCase) == true ||
                           socialContextText?.Contains("Retweeted", StringComparison.OrdinalIgnoreCase) == true;

            // Also check if unretweet button shows it's a repost
            var unrepostButton = await tweet.QuerySelectorAsync(UNREPOST_BUTTON_SELECTOR);
            if (unrepostButton != null && !isRepost)
            {
                var ariaLabel = await unrepostButton.GetAttributeAsync("aria-label");
                if (ariaLabel?.Contains("Undo", StringComparison.OrdinalIgnoreCase) == true ||
                    ariaLabel?.Contains("Unrepost", StringComparison.OrdinalIgnoreCase) == true)
                {
                    isRepost = true;
                }
            }

            if (isRepost && unrepostButton != null)
            {
                // This is a repost - unrepost it
                Console.WriteLine($"   🔄 Repost detected, unreposting...");
                await unrepostButton.ClickAsync();
                await page.WaitForTimeoutAsync(500);

                // Look for confirmation dialog
                var confirmUnrepost = page.Locator(CONFIRM_UNREPOST_SELECTOR);
                try
                {
                    await confirmUnrepost.WaitForAsync(new() { Timeout = 2000 });
                    await confirmUnrepost.ClickAsync();
                }
                catch
                {
                    // Some unrepost actions don't need confirmation
                }
                unrepostedCount++;
                Console.WriteLine($"   ↩️  Unreposted!");
                // Hide the processed tweet from DOM instead of refreshing
                await tweet.EvaluateAsync("el => el.style.display = 'none'");
                await page.WaitForTimeoutAsync(DELAY_BETWEEN_TWEETS_MS);
            }
            else
            {
                // This is an original tweet or reply - delete it
                var menuButton = await tweet.QuerySelectorAsync(MENU_BUTTON_SELECTOR);
                if (menuButton == null)
                {
                    Console.WriteLine($"   ⏭️  No menu button, hiding...");
                    await tweet.EvaluateAsync("el => el.style.display = 'none'");
                    await page.WaitForTimeoutAsync(300);
                    continue;
                }

                await menuButton.ClickAsync();
                await page.WaitForTimeoutAsync(500);

                // Look for Delete option
                var deleteOption = page.GetByRole(AriaRole.Menuitem, new() { Name = "Delete" });

                try
                {
                    await deleteOption.WaitForAsync(new() { Timeout = 1500 });
                }
                catch
                {
                    Console.WriteLine($"   ⏭️  No delete option (not your tweet?), hiding...");
                    await page.Keyboard.PressAsync("Escape");
                    await page.WaitForTimeoutAsync(300);
                    // Hide the tweet instead of scrolling
                    await tweet.EvaluateAsync("el => el.style.display = 'none'");
                    await page.WaitForTimeoutAsync(300);
                    continue;
                }

                await deleteOption.ClickAsync();
                await page.WaitForTimeoutAsync(500);

                // Confirm delete
                var confirmButton = page.Locator(CONFIRM_DELETE_SELECTOR);
                await confirmButton.WaitForAsync(new() { Timeout = 2000 });
                await confirmButton.ClickAsync();

                deletedCount++;
                if (tweetIndex > 0)
                {
                    Console.WriteLine($"   🗑️  Deleted reply! (was tweet #{tweetIndex + 1} in thread)");
                }
                else
                {
                    Console.WriteLine($"   🗑️  Deleted!");
                }
                await page.WaitForTimeoutAsync(DELAY_BETWEEN_TWEETS_MS);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"   ❌ Error: {ex.Message.Split('\n')[0]}");
            Console.WriteLine("   🔄 Refreshing page due to error...");
            try { await page.Keyboard.PressAsync("Escape"); } catch { }
            await page.ReloadAsync(new() { WaitUntil = WaitUntilState.DOMContentLoaded });
            await page.WaitForTimeoutAsync(3000);
            break; // Exit batch loop after refresh
        }
    }

    return (deletedCount, unrepostedCount);
}
