using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using Microsoft.Playwright;

namespace R2WAI.Api.Tests.UI;

public class CrossBrowserTests : IAsyncLifetime
{
    private IPlaywright _playwright = null!;
    private readonly string _baseUrl;
    private Process? _appProcess;
    private readonly int _port;

    public CrossBrowserTests()
    {
        _port = GetAvailablePort();
        _baseUrl = $"http://localhost:{_port}";
    }

    public async Task InitializeAsync()
    {
        var defaultPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ms-playwright");
        Environment.SetEnvironmentVariable("PLAYWRIGHT_BROWSERS_PATH", defaultPath);

        var webProjectDir = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "R2WAI.Web"));

        _appProcess = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"run --no-build --urls {_baseUrl}",
                WorkingDirectory = webProjectDir,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                Environment =
                {
                    ["ASPNETCORE_ENVIRONMENT"] = "Development",
                    ["ApiBaseUrl"] = "http://localhost:15999"
                }
            }
        };

        _appProcess.Start();
        await WaitForServerAsync(_baseUrl, TimeSpan.FromSeconds(30));

        _playwright = await Playwright.CreateAsync();
    }

    public async Task DisposeAsync()
    {
        _playwright?.Dispose();
        if (_appProcess is not null && !_appProcess.HasExited)
        {
            _appProcess.Kill(true);
            _appProcess.Dispose();
        }
        await Task.CompletedTask;
    }

    // ═══════════════════════════════════════════════════════════════════
    //  FIREFOX TESTS
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Firefox_LoginPage_RendersCorrectly()
    {
        await using var browser = await _playwright.Firefox.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true
        });
        var page = await browser.NewPageAsync();

        await page.GotoAsync($"{_baseUrl}/login", new PageGotoOptions
        {
            WaitUntil = WaitUntilState.Load,
            Timeout = 30000
        });
        await page.WaitForTimeoutAsync(2000);

        var title = await page.TitleAsync();
        Assert.Contains("R2WAI", title);

        var emailInput = page.Locator("input[type='email']").First;
        await emailInput.WaitForAsync(new LocatorWaitForOptions { Timeout = 30000 });
        Assert.True(await emailInput.IsVisibleAsync(), "Email input should be visible in Firefox");

        var passwordInput = page.Locator("input[type='password']").First;
        Assert.True(await passwordInput.IsVisibleAsync(), "Password input should be visible in Firefox");

        var mudPaper = page.Locator(".mud-paper").First;
        Assert.True(await mudPaper.IsVisibleAsync(), "MudBlazor components should render in Firefox");
    }

    [Fact]
    public async Task Firefox_LoginPage_FormInteraction_Works()
    {
        await using var browser = await _playwright.Firefox.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true
        });
        var page = await browser.NewPageAsync();

        await page.GotoAsync($"{_baseUrl}/login", new PageGotoOptions
        {
            WaitUntil = WaitUntilState.Load,
            Timeout = 30000
        });
        await page.WaitForTimeoutAsync(2000);

        var emailInput = page.Locator("input[type='email']").First;
        await emailInput.WaitForAsync(new LocatorWaitForOptions { Timeout = 30000 });
        await emailInput.FillAsync("test@example.com");

        var passwordInput = page.Locator("input[type='password']").First;
        await passwordInput.FillAsync("testpassword");

        Assert.Equal("test@example.com", await emailInput.InputValueAsync());
        Assert.Equal("testpassword", await passwordInput.InputValueAsync());
    }

    [Fact]
    public async Task Firefox_RegisterPage_RendersCorrectly()
    {
        await using var browser = await _playwright.Firefox.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true
        });
        var page = await browser.NewPageAsync();

        await page.GotoAsync($"{_baseUrl}/register", new PageGotoOptions
        {
            WaitUntil = WaitUntilState.Load,
            Timeout = 30000
        });
        await page.WaitForTimeoutAsync(2000);

        var title = await page.TitleAsync();
        Assert.Contains("R2WAI", title);

        var content = await page.ContentAsync();
        Assert.Contains("Request Access", content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Firefox_ProtectedPage_RedirectsToLogin()
    {
        await using var browser = await _playwright.Firefox.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true
        });
        var page = await browser.NewPageAsync();

        await page.GotoAsync($"{_baseUrl}/settings", new PageGotoOptions
        {
            WaitUntil = WaitUntilState.Load,
            Timeout = 30000
        });
        await page.WaitForTimeoutAsync(2000);

        var url = page.Url;
        var content = await page.ContentAsync();
        Assert.True(
            url.Contains("/login") ||
            content.Contains("Sign In", StringComparison.OrdinalIgnoreCase) ||
            content.Contains("Email", StringComparison.OrdinalIgnoreCase),
            "Protected page should redirect to login in Firefox");
    }

    // ═══════════════════════════════════════════════════════════════════
    //  WEBKIT (SAFARI) TESTS
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public async Task WebKit_LoginPage_RendersCorrectly()
    {
        await using var browser = await _playwright.Webkit.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true
        });
        var page = await browser.NewPageAsync();

        await page.GotoAsync($"{_baseUrl}/login", new PageGotoOptions
        {
            WaitUntil = WaitUntilState.Load,
            Timeout = 30000
        });
        await page.WaitForTimeoutAsync(2000);

        var title = await page.TitleAsync();
        Assert.Contains("R2WAI", title);

        var emailInput = page.Locator("input[type='email']").First;
        await emailInput.WaitForAsync(new LocatorWaitForOptions { Timeout = 30000 });
        Assert.True(await emailInput.IsVisibleAsync(), "Email input should be visible in WebKit");

        var passwordInput = page.Locator("input[type='password']").First;
        Assert.True(await passwordInput.IsVisibleAsync(), "Password input should be visible in WebKit");

        var mudPaper = page.Locator(".mud-paper").First;
        Assert.True(await mudPaper.IsVisibleAsync(), "MudBlazor components should render in WebKit");
    }

    [Fact]
    public async Task WebKit_LoginPage_FormInteraction_Works()
    {
        await using var browser = await _playwright.Webkit.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true
        });
        var page = await browser.NewPageAsync();

        await page.GotoAsync($"{_baseUrl}/login", new PageGotoOptions
        {
            WaitUntil = WaitUntilState.Load,
            Timeout = 30000
        });
        await page.WaitForTimeoutAsync(2000);

        var emailInput = page.Locator("input[type='email']").First;
        await emailInput.WaitForAsync(new LocatorWaitForOptions { Timeout = 30000 });
        await emailInput.FillAsync("test@example.com");

        var passwordInput = page.Locator("input[type='password']").First;
        await passwordInput.FillAsync("testpassword");

        Assert.Equal("test@example.com", await emailInput.InputValueAsync());
        Assert.Equal("testpassword", await passwordInput.InputValueAsync());
    }

    [Fact]
    public async Task WebKit_RegisterPage_RendersCorrectly()
    {
        await using var browser = await _playwright.Webkit.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true
        });
        var page = await browser.NewPageAsync();

        await page.GotoAsync($"{_baseUrl}/register", new PageGotoOptions
        {
            WaitUntil = WaitUntilState.Load,
            Timeout = 30000
        });
        await page.WaitForTimeoutAsync(2000);

        var title = await page.TitleAsync();
        Assert.Contains("R2WAI", title);

        var content = await page.ContentAsync();
        Assert.Contains("Request Access", content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task WebKit_ProtectedPage_RedirectsToLogin()
    {
        await using var browser = await _playwright.Webkit.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true
        });
        var page = await browser.NewPageAsync();

        await page.GotoAsync($"{_baseUrl}/settings", new PageGotoOptions
        {
            WaitUntil = WaitUntilState.Load,
            Timeout = 30000
        });
        await page.WaitForTimeoutAsync(2000);

        var url = page.Url;
        var content = await page.ContentAsync();
        Assert.True(
            url.Contains("/login") ||
            content.Contains("Sign In", StringComparison.OrdinalIgnoreCase) ||
            content.Contains("Email", StringComparison.OrdinalIgnoreCase),
            "Protected page should redirect to login in WebKit");
    }

    // ═══════════════════════════════════════════════════════════════════
    //  CROSS-BROWSER CONSISTENCY
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public async Task AllBrowsers_LoginPage_HasConsistentElements()
    {
        var browsers = new (string Name, Func<BrowserTypeLaunchOptions, Task<IBrowser>> Launch)[]
        {
            ("Chromium", opts => _playwright.Chromium.LaunchAsync(opts)),
            ("Firefox", opts => _playwright.Firefox.LaunchAsync(opts)),
            ("WebKit", opts => _playwright.Webkit.LaunchAsync(opts)),
        };

        var launchOptions = new BrowserTypeLaunchOptions { Headless = true };

        foreach (var (name, launch) in browsers)
        {
            await using var browser = await launch(launchOptions);
            var page = await browser.NewPageAsync();

            await page.GotoAsync($"{_baseUrl}/login", new PageGotoOptions
            {
                WaitUntil = WaitUntilState.Load,
                Timeout = 30000
            });
            await page.WaitForTimeoutAsync(2000);

            var emailCount = await page.Locator("input[type='email']").CountAsync();
            Assert.True(emailCount >= 1, $"{name}: Email input should be present");

            var passwordCount = await page.Locator("input[type='password']").CountAsync();
            Assert.True(passwordCount >= 1, $"{name}: Password input should be present");

            var buttonCount = await page.Locator("button").CountAsync();
            Assert.True(buttonCount >= 1, $"{name}: At least one button should be present");

            var mudPaperCount = await page.Locator(".mud-paper").CountAsync();
            Assert.True(mudPaperCount >= 1, $"{name}: MudBlazor paper should be rendered");
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    //  HELPERS
    // ═══════════════════════════════════════════════════════════════════

    private static async Task WaitForServerAsync(string url, TimeSpan timeout)
    {
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                var response = await client.GetAsync(url);
                if (response.StatusCode != System.Net.HttpStatusCode.ServiceUnavailable)
                    return;
            }
            catch { }
            await Task.Delay(500);
        }
    }

    private static int GetAvailablePort()
    {
        var listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
        listener.Start();
        var port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }
}
