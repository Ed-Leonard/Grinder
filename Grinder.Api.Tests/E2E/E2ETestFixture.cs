using System.Diagnostics;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Playwright;
using Grinder.Api.Tests.Integration;
using Grinder.Api.DataAccess;
using Grinder.Api.Models.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace Grinder.Api.Tests.E2E;

public class E2ETestFixture : BaseIntegrationTestFixture
{
    private IPlaywright _playwright = null!;
    private IBrowser _browser = null!;

    protected IBrowserContext Context = null!;
    protected IPage Page = null!;
    protected string ApiBaseUrl = null!;
    private string _svelteBuildPath = null!;

    // Frontend build lock and flag
    private static readonly SemaphoreSlim _buildLock = new(1, 1);
    private static bool _isBuilt = false;

    public E2ETestFixture(WebApplicationFactory<Program> factory) : base(factory)
    {
    }

    private static string GetSolutionRoot()
    {
        var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (directory != null && !directory.GetFiles("*.sln*").Any())
        {
            directory = directory.Parent;
        }
        return directory?.FullName ?? throw new Exception("Could not find solution root directory");
    }

    public override async Task InitializeAsync()
    {
        // Build the Svelte app first
        await BuildSvelteApp();

        await base.InitializeAsync();

        await using (var dbContext = await DbContextFactory.CreateDbContextAsync())
        {
            SeedData.Initialize(dbContext, new PasswordHasher<User>(Options.Create(new PasswordHasherOptions { IterationCount = 1 })));
        }

        ApiBaseUrl = HttpClient.BaseAddress!.ToString().TrimEnd('/');

        // Store the build path for serving static files via Playwright
        var solutionRoot = GetSolutionRoot();
        _svelteBuildPath = Path.Combine(solutionRoot, "Grinder.App", "build");

        if (!Directory.Exists(_svelteBuildPath))
        {
            throw new Exception($"Svelte build directory not found at: {_svelteBuildPath}");
        }

        // Initialize Playwright
        _playwright = await Playwright.CreateAsync();
        var headless = Environment.GetEnvironmentVariable("HEADED") != "true";
        _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true,
            SlowMo = headless ? 0 : 500 // Slow down by 500ms when headed for visibility
        });
        Context = await _browser.NewContextAsync();
        Page = await Context.NewPageAsync();

        // Log console messages and errors for debugging
        Page.Console += (_, msg) => Console.WriteLine($"[Browser Console] {msg.Type}: {msg.Text}");
        Page.PageError += (_, error) => Console.WriteLine($"[Browser Error] {error}");

        // Intercept all requests and handle them appropriately
        await Page.RouteAsync("**/*", async route =>
        {
            var request = route.Request;
            var url = new Uri(request.Url);

            // Handle API requests - forward to test server
            if (url.AbsolutePath.StartsWith("/api/"))
            {
                var apiUrl = $"{ApiBaseUrl}{url.PathAndQuery}";
                var apiRequest = new HttpRequestMessage(new HttpMethod(request.Method), apiUrl);

                // Copy headers
                foreach (var header in request.Headers)
                {
                    if (header.Key.ToLower() != "host")
                    {
                        apiRequest.Headers.TryAddWithoutValidation(header.Key, header.Value);
                    }
                }

                // Copy body for POST/PUT
                if (request.PostDataBuffer != null)
                {
                    apiRequest.Content = new ByteArrayContent(request.PostDataBuffer);
                    if (request.Headers.ContainsKey("content-type"))
                    {
                        apiRequest.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(request.Headers["content-type"]);
                    }
                }

                // Make the request to the test server
                var response = await HttpClient.SendAsync(apiRequest);

                // Return the response
                var responseBody = await response.Content.ReadAsByteArrayAsync();
                var responseHeaders = response.Headers.ToDictionary(h => h.Key, h => string.Join(", ", h.Value));

                await route.FulfillAsync(new RouteFulfillOptions
                {
                    Status = (int)response.StatusCode,
                    Headers = responseHeaders,
                    BodyBytes = responseBody
                });
            }
            // Handle static file requests
            else
            {
                var path = url.AbsolutePath.TrimStart('/');
                if (string.IsNullOrEmpty(path)) path = "index.html";

                var filePath = Path.Combine(_svelteBuildPath, path);

                // If file doesn't exist, serve index.html (SPA fallback)
                if (!File.Exists(filePath))
                {
                    filePath = Path.Combine(_svelteBuildPath, "index.html");
                }

                if (File.Exists(filePath))
                {
                    var bytes = await File.ReadAllBytesAsync(filePath);
                    var contentType = GetContentType(filePath);

                    await route.FulfillAsync(new RouteFulfillOptions
                    {
                        Status = 200,
                        Headers = new Dictionary<string, string> { { "Content-Type", contentType } },
                        BodyBytes = bytes
                    });
                }
                else
                {
                    await route.FulfillAsync(new RouteFulfillOptions { Status = 404 });
                }
            }
        });
    }

    private static string GetContentType(string filePath)
    {
        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        return extension switch
        {
            ".html" => "text/html",
            ".js" => "application/javascript",
            ".css" => "text/css",
            ".json" => "application/json",
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".svg" => "image/svg+xml",
            ".woff" => "font/woff",
            ".woff2" => "font/woff2",
            _ => "application/octet-stream"
        };
    }

    private async Task BuildSvelteApp()
    {
        // If we're in the CI pipeline, assume the build is passed to us 🙏
        if (Environment.GetEnvironmentVariable("GITLAB_CI") == "true")
        {
            return;
        }

        var solutionRoot = GetSolutionRoot();
        var appPath = Path.Combine(solutionRoot, "Grinder.App");
        var buildPath = Path.Combine(appPath, "build");

        // If we've already built it
        if (_isBuilt)
        {
            return;
        }

        // Otherwise we have to actually build the app :sadge:
        // Ensure thread safety across parallel test runners
        await _buildLock.WaitAsync();
        try
        {
            // Re-check for built inside lock (thanks Gemini)
            if (_isBuilt) return;


            // Optional environment variable to skip build if artifacts already exist on disk (e.g., built manually or prior run)
            var skipBuild = Environment.GetEnvironmentVariable("SKIP_BUILD") == "true";
            if (skipBuild && Directory.Exists(buildPath) && Directory.EnumerateFileSystemEntries(buildPath).Any())
            {
                _isBuilt = true;
                return;
            }

            // Run npm ci if node_modules is missing
            var nodeModulesPath = Path.Combine(appPath, "node_modules");
            if (!Directory.Exists(nodeModulesPath))
            {
                var installProcess = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = OperatingSystem.IsWindows() ? "cmd.exe" : "npm",
                        Arguments = OperatingSystem.IsWindows() ? "/c npm ci" : "ci",
                        WorkingDirectory = appPath,
                        RedirectStandardOutput = !OperatingSystem.IsWindows(),
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };
                installProcess.StartInfo.EnvironmentVariables["CI"] = "true"; // Pretend like its a CI install so npm doesn't complain

                installProcess.Start();
                await installProcess.WaitForExitAsync();

                if (installProcess.ExitCode != 0)
                {
                    var error = await installProcess.StandardError.ReadToEndAsync();
                    throw new Exception($"npm ci failed: {error}");
                }
            }

            // Run npm run build
            var buildProcess = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = OperatingSystem.IsWindows() ? "cmd.exe" : "npm",
                    Arguments = OperatingSystem.IsWindows() ? "/c npm run build" : "run build",
                    WorkingDirectory = appPath,
                    RedirectStandardOutput = !OperatingSystem.IsWindows(),
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            buildProcess.StartInfo.EnvironmentVariables["CI"] = "true"; // Pretend like its a CI build so npm/vite doesn't complain
            buildProcess.StartInfo.EnvironmentVariables["NODE_ENV"] = "development";

            buildProcess.Start();
            await buildProcess.WaitForExitAsync();

            if (buildProcess.ExitCode != 0)
            {
                var error = await buildProcess.StandardError.ReadToEndAsync();
                throw new Exception($"npm run build failed: {error}");
            }

            _isBuilt = true;
        }
        finally
        {
            _buildLock.Release();
        }
    }

    public override async Task DisposeAsync()
    {
        await Page.CloseAsync();
        await Context.CloseAsync();
        await _browser.CloseAsync();
        _playwright.Dispose();

        await base.DisposeAsync();
    }

    /// <summary>
    /// Helper method to log in an already-seeded user via the UI
    /// </summary>
    protected async Task LoginViaUI(string email, string password = "P4$$word")
    {
        await Page.GotoAsync("http://localhost/login");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        await Page.Locator("#floatingEmail").FillAsync(email);
        await Page.Locator("#password").FillAsync(password);
        await Page.Locator("button[type='submit']").ClickAsync();

        // Post-login redirect lands on the logged-in user's own profile page
        await Page.WaitForURLAsync("**/profile/*");
    }

    /// <summary>
    /// Helper method to register and login a user via the UI
    /// </summary>
    protected async Task<string> RegisterAndLoginViaUI(string email = "test@example.com", string password = "TestPassword123", string displayName = "Test User", string country = "NZ")
    {
        await Page.GotoAsync($"http://localhost/register");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        await Page.Locator("#floatingEmail").FillAsync(email);
        await Page.Locator("#floatingName").FillAsync(password);
        await Page.Locator("#password").FillAsync(password);
        await Page.Locator("#confirmPassword").FillAsync(password);
        await Page.Locator("#confirmPassword").FillAsync(password);
        await Page.GetByLabel("Country").SelectOptionAsync(new SelectOptionValue { Label = "Australia" });
        await Page.Locator("button[type='submit']").ClickAsync();

        // Wait for redirect to home page
        await Page.WaitForURLAsync($"http://localhost/email-verification?email=test@example.com");

        return email;
    }
}

// Helper record for CSRF token response
file record CsrfTokenResponse(string Token);

