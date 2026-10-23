using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Time.Testing;
using Grinder.Api.DataAccess;
using Grinder.Api.Models.Entities;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using System.Net;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace Grinder.Api.Tests.Integration;

public abstract class BaseIntegrationTestFixture : IClassFixture<WebApplicationFactory<Program>>, IAsyncLifetime
{
    private readonly WebApplicationFactory<Program> _webAppFactory;

    // In-memory sqlite connections will get immediately closed if they aren't in use
    private readonly SqliteConnection _connection;

    protected IServiceProvider ServiceProvider => _webAppFactory.Server.Services.CreateScope().ServiceProvider;

    protected IDbContextFactory<DatabaseContext> DbContextFactory => ServiceProvider.GetRequiredService<IDbContextFactory<DatabaseContext>>();

    // As our tests run, time passes, which can make it difficult to check exactly when something happened.
    // For this reason, we fake (similar to mocking) our time provider, effectively freezing time while we run our tests.
    // Note: This will only work if we remember to use a TimeProvider instead of DateTime.Now or DateTimeOffset.Now in our code.
    protected DateTimeOffset TestNow;
    protected readonly FakeTimeProvider FakeTimeProvider;

    protected HttpClient HttpClient { get; private init; }
    protected HttpClient UnauthenticatedClient { get; private init; }

    protected BaseIntegrationTestFixture(WebApplicationFactory<Program> webAppFactory, bool handleCookies = false)
    {
        TestNow = DateTimeOffset.Now;
        FakeTimeProvider = new FakeTimeProvider(TestNow);

        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        _webAppFactory = webAppFactory.WithWebHostBuilder(config =>
        {
            // There are some changes we need to make between how our app is configured normally, and how it should be configured for tests
            // In our tests, we want to mock / fake some things, or completely replace others
            config.ConfigureTestServices(services =>
            {
                // We want to use a dedicated in-memory database for tests so that we know exactly what should be in it
                // So, we first remove any database factories that are present
                services.RemoveAll<IDbContextFactory<DatabaseContext>>();
                // Then, we add a test-specific database factory, which will run everything in memory
                services.AddDbContextFactory<DatabaseContext>(dbConfig => dbConfig.UseSqlite(_connection));

                // Background hosted services (e.g. cleanup services) run on their own schedule and
                // query the database as soon as the host starts. Left running, they race against
                // EnsureDeleted/EnsureCreated in InitializeAsync and can crash the whole test host.
                // Tests that exercise these services construct them directly instead of relying on DI.
                services.RemoveAll<IHostedService>();

                // Remove the existing time provider, which uses the real system time (and will change constantly through our tests)
                services.RemoveAll<TimeProvider>();
                // Replace it with our fake time provider, which keeps a consistent time throughout
                services.AddSingleton<TimeProvider>(FakeTimeProvider);

                // Manually configure options and pass them into the constructor
                var options = Options.Create(new PasswordHasherOptions { IterationCount = 1 });
                var fastHasher = new PasswordHasher<User>(options);
                // Register the pre-configured instance as a singleton
                services.AddSingleton<IPasswordHasher<User>>(fastHasher);

                // Add this to allow cookie auth to work in tests
                services.AddDataProtection()
                    .SetApplicationName("TestApp");

                ConfigureCustomServicesForTesting(services);
            });
        });

        HttpClient = _webAppFactory.CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = handleCookies,
        });

        UnauthenticatedClient = _webAppFactory.CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = false,
        });
    }

    public virtual async Task InitializeAsync()
    {
        // At the start of a test, make sure the database is created fresh
        await using var dbContext = await DbContextFactory.CreateDbContextAsync();
        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.EnsureCreatedAsync();
    }

    public virtual async Task DisposeAsync()
    {
        // Close and dispose the sqlite connection when tests are done
        await _connection.CloseAsync();
        await _connection.DisposeAsync();
    }

    protected virtual void ConfigureCustomServicesForTesting(IServiceCollection services) { }

    protected static CookieContainer BuildCookieContainerFromResponse(HttpResponseMessage response, Uri baseAddress)
    {
        var container = new CookieContainer();

        if (response.Headers.TryGetValues("Set-Cookie", out var setCookieHeaders))
        {
            foreach (var header in setCookieHeaders)
            {
                container.SetCookies(baseAddress, header);
            }
        }

        return container;
    }

    // required for hubs cookie auth
    protected HttpMessageHandler CreateServerHandler() => _webAppFactory.Server.CreateHandler();
}

