namespace TodoApi.Infrastructure.Configuration;

/// <summary>
/// Extension methods for configuring Cross-Origin Resource Sharing (CORS).
/// Configures CORS policies based on environment-specific settings.
/// </summary>
public static class CorsExtensions
{
    private const string DefaultPolicyName = "DefaultCorsPolicy";

    /// <summary>
    /// Configures CORS policy from application settings.
    /// Allows different origins per environment (Development, QA, Production).
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <param name="configuration">Application configuration</param>
    /// <returns>Service collection for chaining</returns>
    /// <example>
    /// // appsettings.json:
    /// {
    ///   "CorsSettings": {
    ///     "AllowedOrigins": ["http://localhost:5173", "http://localhost:3000"],
    ///     "AllowCredentials": true
    ///   }
    /// }
    ///
    /// // Program.cs:
    /// builder.Services.AddCorsConfiguration(builder.Configuration);
    /// app.UseCorsConfiguration();
    /// </example>
    public static IServiceCollection AddCorsConfiguration(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var corsSettings = configuration.GetSection("CorsSettings").Get<CorsSettings>()
            ?? throw new InvalidOperationException("CorsSettings not configured in appsettings");

        services.AddCors(options =>
        {
            options.AddPolicy(DefaultPolicyName, policy =>
            {
                // Configure allowed origins
                if (corsSettings.AllowedOrigins?.Length > 0)
                {
                    policy.WithOrigins(corsSettings.AllowedOrigins);
                }
                else
                {
                    // Fallback: allow any origin (NOT RECOMMENDED for production)
                    policy.AllowAnyOrigin();
                }

                // Configure allowed methods
                if (corsSettings.AllowedMethods?.Length > 0)
                {
                    policy.WithMethods(corsSettings.AllowedMethods);
                }
                else
                {
                    policy.AllowAnyMethod();
                }

                // Configure allowed headers
                if (corsSettings.AllowedHeaders?.Length > 0)
                {
                    policy.WithHeaders(corsSettings.AllowedHeaders);
                }
                else
                {
                    policy.AllowAnyHeader();
                }

                // Configure credentials
                if (corsSettings.AllowCredentials)
                {
                    policy.AllowCredentials();
                }

                // Configure exposed headers
                if (corsSettings.ExposedHeaders?.Length > 0)
                {
                    policy.WithExposedHeaders(corsSettings.ExposedHeaders);
                }

                // Configure max age for preflight cache
                if (corsSettings.MaxAgeSeconds > 0)
                {
                    policy.SetPreflightMaxAge(TimeSpan.FromSeconds(corsSettings.MaxAgeSeconds));
                }
            });
        });

        return services;
    }

    /// <summary>
    /// Applies the CORS policy to the application.
    /// Must be called before UseAuthentication() and UseAuthorization().
    /// </summary>
    /// <param name="app">Application builder</param>
    /// <returns>Application builder for chaining</returns>
    public static IApplicationBuilder UseCorsConfiguration(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.UseCors(DefaultPolicyName);

        return app;
    }
}

/// <summary>
/// CORS configuration settings from appsettings.
/// </summary>
public class CorsSettings
{
    /// <summary>
    /// List of allowed origins. Example: ["http://localhost:5173", "https://app.zea.com"]
    /// </summary>
    public string[]? AllowedOrigins { get; set; }

    /// <summary>
    /// List of allowed HTTP methods. Default: all methods if not specified.
    /// </summary>
    public string[]? AllowedMethods { get; set; }

    /// <summary>
    /// List of allowed headers. Default: all headers if not specified.
    /// </summary>
    public string[]? AllowedHeaders { get; set; }

    /// <summary>
    /// Headers that should be exposed to the browser. Example: ["X-Total-Count"]
    /// </summary>
    public string[]? ExposedHeaders { get; set; }

    /// <summary>
    /// Whether to allow credentials (cookies, authorization headers). Default: false.
    /// </summary>
    public bool AllowCredentials { get; set; }

    /// <summary>
    /// How long (in seconds) the preflight response can be cached. Default: 0 (no cache).
    /// </summary>
    public int MaxAgeSeconds { get; set; }
}
