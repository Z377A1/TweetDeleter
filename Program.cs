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

// Initialize Playwright and launch Chromium with persistent profile
var profileDir = Path.Combine(Directory.GetCurrentDirectory(), ".browser-profile");
Directory.CreateDirectory(profileDir);

Console.WriteLine("\n🌐 Launching Chromium browser...");
Console.WriteLine($"📁 Profile directory: {profileDir}");

using var playwright = await Playwright.CreateAsync();

IBrowserContext context;
try
{
    context = await playwright.Chromium.LaunchPersistentContextAsync(profileDir, new BrowserTypeLaunchPersistentContextOptions
    {
        Headless = false,
        Args = new[]
        {
            "--disable-blink-features=AutomationControlled",
            "--no-sandbox"
        },
        ViewportSize = ViewportSize.NoViewport
    });
    Console.WriteLine("✅ Chromium launched successfully!");
}
catch (Exception ex)
{
    Console.WriteLine($"❌ Failed to launch Chromium: {ex.Message}");
    Console.WriteLine("\nMake sure Playwright browsers and OS dependencies are installed:");
    Console.WriteLine("  Linux/macOS:");
    Console.WriteLine("    pwsh bin/Debug/net10.0/playwright.ps1 install chromium");
    Console.WriteLine("    pwsh bin/Debug/net10.0/playwright.ps1 install-deps chromium");
    Console.WriteLine("  Windows PowerShell:");
    Console.WriteLine("    powershell -ExecutionPolicy Bypass -File bin/Debug/net10.0/playwright.ps1 install chromium");
    return;
}

var page = context.Pages.Count > 0 ? context.Pages[0] : await context.NewPageAsync();

// Check login status
Console.WriteLine("\n🔍 Checking Twitter/X authentication status...");
await page.GotoAsync("https://x.com/home", new() { WaitUntil = WaitUntilState.DOMContentLoaded });
await page.WaitForTimeoutAsync(2500);

// Check if we need to log in
bool isLoginRequired = page.Url.Contains("/login", StringComparison.OrdinalIgnoreCase) ||
                       page.Url.Contains("/i/flow/login", StringComparison.OrdinalIgnoreCase);

if (!isLoginRequired)
{
    var loginButtonCount = await page.Locator("a[href='/login'], [data-testid='loginButton']").CountAsync();
    var accountSwitcherCount = await page.Locator("[data-testid='SideNav_AccountSwitcher_Button'], [data-testid='AppTabBar_Profile_Link']").CountAsync();
    if (loginButtonCount > 0 && accountSwitcherCount == 0)
    {
        isLoginRequired = true;
    }
}

if (isLoginRequired)
{
    Console.WriteLine("\n🔑 You are not logged in yet.");
    Console.WriteLine("👉 Please log in to your Twitter/X account in the opened Chromium browser window.");
    Console.WriteLine("   (Your login session will be saved in .browser-profile for future runs)");
    Console.WriteLine("   Once you have completed logging in, press [Enter] here in the terminal to continue...");
    Console.ReadLine();

    await page.WaitForTimeoutAsync(2000);
}

Console.WriteLine("✅ Logged in successfully!");

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

await context.CloseAsync();

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
