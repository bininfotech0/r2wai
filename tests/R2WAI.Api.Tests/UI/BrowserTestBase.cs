using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using Microsoft.Playwright;

namespace R2WAI.Api.Tests.UI;

public class BrowserTestBase : IAsyncLifetime
{
    protected IPlaywright PlaywrightInstance = null!;
    protected IBrowser Browser = null!;
    protected IBrowserContext Context = null!;
    protected IPage Page = null!;
    protected string BaseUrl = null!;

    private Process? _appProcess;
    private readonly int _port = GetAvailablePort();

    private static readonly string ArtifactsRoot = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "test-results"));

    private string _testArtifactDir = null!;
    private string _testName = null!;
    private int _screenshotCounter;

    public async Task InitializeAsync()
    {
        _testName = GetType().Name + "_" + DateTime.UtcNow.ToString("HHmmss_fff");
        _testArtifactDir = Path.Combine(ArtifactsRoot, _testName);
        Directory.CreateDirectory(_testArtifactDir);

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

        var videoDir = Path.Combine(_testArtifactDir, "videos");
        Directory.CreateDirectory(videoDir);

        Context = await Browser.NewContextAsync(new BrowserNewContextOptions
        {
            IgnoreHTTPSErrors = true,
            RecordVideoDir = videoDir,
            RecordVideoSize = new RecordVideoSize { Width = 1280, Height = 720 },
            ViewportSize = new ViewportSize { Width = 1280, Height = 720 }
        });

        await Context.Tracing.StartAsync(new TracingStartOptions
        {
            Screenshots = true,
            Snapshots = true,
            Sources = true
        });

        Page = await Context.NewPageAsync();

        Page.Console += (_, msg) =>
        {
            File.AppendAllText(
                Path.Combine(_testArtifactDir, "console.log"),
                $"[{msg.Type}] {msg.Text}\n");
        };

        Page.PageError += (_, error) =>
        {
            File.AppendAllText(
                Path.Combine(_testArtifactDir, "errors.log"),
                $"[PAGE ERROR] {error}\n");
        };
    }

    public async Task DisposeAsync()
    {
        try
        {
            if (Page is not null)
            {
                await Page.ScreenshotAsync(new PageScreenshotOptions
                {
                    Path = Path.Combine(_testArtifactDir, "final-state.png"),
                    FullPage = true
                });
            }
        }
        catch { }

        try
        {
            if (Context is not null)
            {
                await Context.Tracing.StopAsync(new TracingStopOptions
                {
                    Path = Path.Combine(_testArtifactDir, "trace.zip")
                });
            }
        }
        catch { }

        if (Page is not null) await Page.CloseAsync();
        if (Context is not null) await Context.DisposeAsync();
        if (Browser is not null) await Browser.DisposeAsync();
        PlaywrightInstance?.Dispose();

        if (_appProcess is not null && !_appProcess.HasExited)
        {
            _appProcess.Kill(true);
            _appProcess.Dispose();
        }
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

    protected async Task NavigateAndWait(string path, int waitMs = 3000)
    {
        await Page.GotoAsync($"{BaseUrl}{path}", new PageGotoOptions
        {
            WaitUntil = WaitUntilState.NetworkIdle,
            Timeout = 15000
        });
        await Page.WaitForTimeoutAsync(waitMs);
    }

    protected string GetArtifactDir() => _testArtifactDir;

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
