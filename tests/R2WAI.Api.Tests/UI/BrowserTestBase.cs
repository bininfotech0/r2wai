using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using Microsoft.Playwright;

namespace R2WAI.Api.Tests.UI;

public class BrowserFixture : IAsyncLifetime
{
    public IPlaywright PlaywrightInstance { get; private set; } = null!;
    public IBrowser Browser { get; private set; } = null!;
    public IBrowserContext Context { get; private set; } = null!;
    public IPage Page { get; private set; } = null!;
    public string BaseUrl { get; private set; } = null!;

    private Process? _appProcess;
    private readonly int _port = GetAvailablePort();

    public async Task InitializeAsync()
    {
        Environment.SetEnvironmentVariable("PLAYWRIGHT_BROWSERS_PATH", "0");

        BaseUrl = $"http://localhost:{_port}";

        var webProjectDir = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "R2WAI.Web"));

        _appProcess = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"run --no-build --urls {BaseUrl}",
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

        var ready = await WaitForServerAsync(BaseUrl, TimeSpan.FromSeconds(30));
        if (!ready)
        {
            var stderr = await _appProcess.StandardError.ReadToEndAsync();
            _appProcess.Kill();
            throw new Exception($"Web server failed to start on {BaseUrl}. Stderr: {stderr}");
        }

        PlaywrightInstance = await Playwright.CreateAsync();
        Browser = await PlaywrightInstance.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true
        });

        Context = await Browser.NewContextAsync(new BrowserNewContextOptions
        {
            IgnoreHTTPSErrors = true,
            ViewportSize = new ViewportSize { Width = 1280, Height = 720 }
        });

        Page = await Context.NewPageAsync();
    }

    public async Task DisposeAsync()
    {
        if (Page is not null) try { await Page.CloseAsync(); } catch { }
        if (Context is not null) try { await Context.DisposeAsync(); } catch { }
        if (Browser is not null) await Browser.DisposeAsync();
        PlaywrightInstance?.Dispose();

        if (_appProcess is not null && !_appProcess.HasExited)
        {
            _appProcess.Kill(true);
            _appProcess.Dispose();
        }
    }

    private static async Task<bool> WaitForServerAsync(string url, TimeSpan timeout)
    {
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
        var deadline = DateTime.UtcNow + timeout;

        while (DateTime.UtcNow < deadline)
        {
            try
            {
                var response = await client.GetAsync(url);
                if (response.StatusCode != HttpStatusCode.ServiceUnavailable)
                    return true;
            }
            catch { }
            await Task.Delay(500);
        }
        return false;
    }

    private static int GetAvailablePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }
}

public class BrowserTestBase : IClassFixture<BrowserFixture>, IAsyncLifetime
{
    private readonly BrowserFixture _fixture;

    protected IPlaywright PlaywrightInstance => _fixture.PlaywrightInstance;
    protected IBrowser Browser => _fixture.Browser;
    protected IBrowserContext Context => _fixture.Context;
    protected IPage Page => _fixture.Page;
    protected string BaseUrl => _fixture.BaseUrl;

    private static readonly string ArtifactsRoot = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "test-results"));

    private string _testArtifactDir = null!;
    private string _testName = null!;
    private int _screenshotCounter;

    public BrowserTestBase(BrowserFixture fixture)
    {
        _fixture = fixture;
    }

    public Task InitializeAsync()
    {
        _testName = GetType().Name + "_" + DateTime.UtcNow.ToString("HHmmss_fff");
        _testArtifactDir = Path.Combine(ArtifactsRoot, _testName);
        Directory.CreateDirectory(_testArtifactDir);
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        try
        {
            await Page.ScreenshotAsync(new PageScreenshotOptions
            {
                Path = Path.Combine(_testArtifactDir, "final-state.png"),
                FullPage = true
            });
        }
        catch { }

        try { await Page.SetViewportSizeAsync(1280, 720); } catch { }
    }

    protected async Task<string> TakeScreenshot([CallerMemberName] string? label = null)
    {
        var seq = Interlocked.Increment(ref _screenshotCounter);
        var fileName = $"{seq:D3}_{label ?? "screenshot"}.png";
        var path = Path.Combine(_testArtifactDir, fileName);

        await Page.ScreenshotAsync(new PageScreenshotOptions
        {
            Path = path,
            FullPage = true
        });

        return path;
    }

    protected async Task<string> TakeElementScreenshot(ILocator locator, [CallerMemberName] string? label = null)
    {
        var seq = Interlocked.Increment(ref _screenshotCounter);
        var fileName = $"{seq:D3}_{label ?? "element"}.png";
        var path = Path.Combine(_testArtifactDir, fileName);

        await locator.ScreenshotAsync(new LocatorScreenshotOptions { Path = path });

        return path;
    }

    protected async Task NavigateAndWait(string path, int waitMs = 2000)
    {
        await Page.GotoAsync($"{BaseUrl}{path}", new PageGotoOptions
        {
            WaitUntil = WaitUntilState.Commit,
            Timeout = 30000
        });
        await Page.WaitForTimeoutAsync(waitMs);
    }

    protected string GetArtifactDir() => _testArtifactDir;
}
