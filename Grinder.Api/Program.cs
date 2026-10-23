using Microsoft.EntityFrameworkCore;
using Grinder.Api.DataAccess;
using Grinder.Api.Models.Entities;
using Grinder.Api.Services;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Grinder.Api;

public class Program
{
    public static void Main(string[] args)
    {
        // Set environment at builder creation time so configuration providers (including user secrets)
        // are loaded for the correct environment.
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            Args = args,
            EnvironmentName = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? Environments.Development
        });
        var envPath = Path.Combine(Directory.GetCurrentDirectory(), "..", ".env");
        if (!builder.Environment.IsEnvironment("Testing") && File.Exists(envPath))
        {
            foreach (var line in File.ReadAllLines(envPath))
            {
                var parts = line.Split('=', 2);
                if (parts.Length == 2)
                {
                    Environment.SetEnvironmentVariable(parts[0], parts[1]);
                }
            }
        }

        builder.Configuration.AddJsonFile("{environment}.json", optional: true).AddEnvironmentVariables();
        builder.Services.Configure<EmailSettings>(builder.Configuration.GetSection("EmailSettings"));

        Console.WriteLine($"*** CURRENT ENVIRONMENT: {builder.Environment.EnvironmentName} ***");

        if (builder.Environment.IsDevelopment())
        {
            builder.Services.AddOpenApi();
        }

        // Add services to the container. Using `WithViews` registers the Antiforgery filters required for [ValidateAntiForgeryToken]
        builder.Services.AddControllersWithViews();


        //Session Cookie configuration
        builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(options =>
        {
            options.ExpireTimeSpan = TimeSpan.FromDays(7);
            options.SlidingExpiration = true;
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = SameSiteMode.Strict;
            options.Cookie.SecurePolicy = builder.Environment.IsProduction() || builder.Environment.IsStaging() ? CookieSecurePolicy.Always : CookieSecurePolicy.SameAsRequest;
        });


        // Configure database context with factory pattern
        if (builder.Environment.IsProduction() || builder.Environment.IsStaging())
        {
            // These variables are set in our gitlab pipeline (and must be defined in the GitLab project CI/CD variables)
            var dbHost = builder.Configuration["DB_HOST"];
            var dbName = builder.Configuration["DB_NAME"];
            var dbUser = builder.Configuration["DB_USER"];
            var dbPass = builder.Configuration["DB_PASS"];

            if (dbHost == null || dbName == null || dbUser == null || dbPass == null)
            {
                throw new InvalidOperationException("Missing required database environment variables.");
            }

            // Build the connection string
            var connectionString = $"Host={dbHost};Database={dbName};Username={dbUser};Password={dbPass};SSL Mode=Require;Trust Server Certificate=true";

            builder.Services.AddDbContextFactory<DatabaseContext>((_, options) =>
                options.UseNpgsql(connectionString)
                       .ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning))
            );
        }
        else
        {
            builder.Services.AddDbContextFactory<DatabaseContext>((_, options) =>
                options.UseSqlite("Data Source=app.db")
            );
        }

        builder.Services.AddHttpContextAccessor();

        builder.Services.AddDistributedMemoryCache();
        builder.Services.AddSession(options =>
        {
            options.IdleTimeout = TimeSpan.FromMinutes(10);
            options.Cookie.HttpOnly = true;
            options.Cookie.IsEssential = true;
        });

        // Register custom services
        RegisterServices(builder.Services);

        // Setup antiforgery (CSRF)
        var cookiePolicy = builder.Environment.IsProduction() || builder.Environment.IsStaging()
            ? CookieSecurePolicy.Always
            : CookieSecurePolicy.SameAsRequest;

        builder.Services.AddAntiforgery(options =>
        {
            options.HeaderName = "X-CSRF-TOKEN";
            options.Cookie.Name = "X-CSRF-TOKEN-COOKIE";
            options.Cookie.SameSite = SameSiteMode.Strict;
            options.Cookie.HttpOnly = true;

            options.Cookie.SecurePolicy = cookiePolicy;
        });

        // Add CORS for development only
        if (builder.Environment.IsDevelopment())
        {
            builder.Services.AddCors(options =>
            {
                options.AddPolicy("AllowFrontend", policy =>
                {
                    policy.WithOrigins("http://localhost:5173")
                        .AllowAnyHeader()
                        .AllowAnyMethod()
                        .AllowCredentials();
                });
            });
        }

        builder.Services.AddSignalR();

        var app = builder.Build();

        // Make sure we use forwarded headers in production so our api works behind reverse proxy (nginx) with https
        if (builder.Environment.IsProduction() || builder.Environment.IsStaging())
        {
            app.UseForwardedHeaders(new ForwardedHeadersOptions
            {
                ForwardedHeaders = ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedFor
            });
        }

        InitializeDatabase(app.Services.CreateScope().ServiceProvider, !(app.Environment.IsStaging() || app.Environment.IsProduction()));


        var pathBase = app.Configuration["PathBase"];
        if (!string.IsNullOrEmpty(pathBase))
        {
            app.UsePathBase(pathBase);
        }

        app.UseStaticFiles();

        app.UseRouting();

        app.UseAuthentication();
        app.UseAuthorization();

        app.UseSession();

        // Only use https redirects in production
        if (builder.Environment.IsProduction() || builder.Environment.IsStaging())
        {
            app.UseHttpsRedirection();
        }

        if (app.Environment.IsDevelopment())
        {
            app.UseCors("AllowFrontend");
        }


        app.UseAntiforgery();

        // CSRF token endpoint
        app.MapGet("/api/csrf-token", (Microsoft.AspNetCore.Antiforgery.IAntiforgery antiforgery, HttpContext context) =>
        {
            var tokens = antiforgery.GetAndStoreTokens(context);
            return Results.Ok(new { token = tokens.RequestToken });
        });

        app.MapControllers();

        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi();
            app.UseSwaggerUI(options =>
            {
                options.SwaggerEndpoint("/openapi/v1.json", "Grinder API v1");
            });
        }

        app.MapHub<Hubs.NotificationHub>("/api/hubs/notifications");
        app.MapHub<Hubs.ChatHub>("/api/hubs/chat");

        app.Run();
    }

    protected static void InitializeDatabase(IServiceProvider serviceProvider, bool isDevelopment)
    {
        var dbContextFactory = serviceProvider.GetRequiredService<IDbContextFactory<DatabaseContext>>();
        var dbContext = dbContextFactory.CreateDbContext();

        if (isDevelopment)
        {
            dbContext.Database.EnsureCreated();
        }
        else
        {
            dbContext.Database.Migrate();
        }

        SeedData.Initialize(dbContext, serviceProvider.GetRequiredService<IPasswordHasher<User>>());
        // dbContext.Database.EnsureDeleted();
    }

    protected static void RegisterServices(IServiceCollection services)
    {
        // Register a TimeProvider so we don't need to rely on DateTime.Now, and can mock the time in automated tests
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IPasswordHasher<User>, PasswordHasher<User>>();
        // Make sure you know the differences between AddSingleton, AddScoped, and AddTransient.
        // (If in doubt, you probably just want AddScoped)
        services.AddScoped<ITaskService, TaskService>();
        services.AddSingleton<IAttachmentValidator, AttachmentValidator>();
        services.AddScoped<IAttachmentService, AttachmentService>();

        services.AddScoped<IUserService, UserService>();
        services.AddScoped<ISearchService, SearchService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IVerificationCodeService, VerificationCodeService>();

        // GC services
        services.AddHostedService<VerificationCodeCleanupService>();
        services.AddHostedService<TemporaryAttachmentCleanupService>();
        // Register the email service
        services.AddScoped<IEmailService, EmailService>();

        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<IFollowService, FollowService>();
        services.AddScoped<IFeedService, FeedService>();
        services.AddScoped<IClipService, ClipService>();
        services.AddScoped<ICategoryService, CategoryService>();
        services.AddScoped<IBadgeService, BadgeService>();
        services.AddScoped<IBadgeProgressEvaluator, BadgeProgressEvaluator>();
        services.AddScoped<ICommentService, CommentService>();
        services.AddScoped<IDirectMessageService, DirectMessageService>();
        services.AddScoped<ILiveStreamChatService, LiveStreamChatService>();
        services.AddScoped<IReactionService, ReactionService>();
        services.AddScoped<IExploreService, ExploreService>();
        services.AddScoped<IWrappedService, WrappedService>();
        services.AddScoped<ILiveStreamService, LiveStreamService>();
        services.AddScoped<ILiveKitRoomService, LiveKitRoomService>();
    }

}
