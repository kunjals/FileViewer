
using CentralApi.Services;
using Serilog;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.OpenApi.Models;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using System.Threading.RateLimiting;
using CentralApi.Data;
using Microsoft.EntityFrameworkCore;
using CentralApi.Models;
using Microsoft.AspNetCore.Identity;

namespace CentralApi
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Configure Serilog
            Log.Logger = new LoggerConfiguration()
                .WriteTo.Console()
                .WriteTo.File("logs/centralapi-.txt", rollingInterval: RollingInterval.Day)
                .CreateLogger();

            builder.Host.UseSerilog();

            builder.Services.AddAuthorization();

            // Configure services
            builder.Services
                .AddControllers()
                .AddJsonOptions(options =>
                {
                    options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
                    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
                });

            builder.Services.AddDbContext<ApplicationDbContext>(options =>
            {
                options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection"));
            });

            builder.Services.AddIdentityApiEndpoints<ApplicationUser>()
                .AddRoles<IdentityRole>()
                .AddEntityFrameworkStores<ApplicationDbContext>();

            builder.Services.AddAuthentication();
                //.AddBearerToken(IdentityConstants.BearerScheme);

            // Configure CORS
            //builder.Services.AddCors(options =>
            //{
            //    options.AddPolicy("AllowPortal", policy =>
            //    {
            //        policy
            //            .WithOrigins(builder.Configuration.GetSection("AllowedPortals").Get<string[]>() ?? Array.Empty<string>())
            //            .AllowAnyMethod()
            //            .AllowAnyHeader()
            //            .WithExposedHeaders("Content-Disposition");
            //    });
            //});

            // Configure HTTP client
            builder.Services.AddHttpClient("FileServerClient", client =>
            {
                client.Timeout = TimeSpan.FromMinutes(5); // Increased timeout for large file operations
            })
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) =>
                {
                    // In production, properly handle certificate validation
                    return true; // TODO: Implement proper certificate validation
                }
            });

            // Add memory cache
            builder.Services.AddMemoryCache();

            // Add health checks
            builder.Services.AddHealthChecks()
                .AddCheck("self", () => HealthCheckResult.Healthy());

            // Configure API documentation
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo
                {
                    Title = "Central File Server API",
                    Version = "v1",
                    Description = "API bridge between portal and file servers"
                });

                //// Add API key authentication to Swagger
                c.AddSecurityDefinition("ApiKey", new OpenApiSecurityScheme
                {
                    Type = SecuritySchemeType.ApiKey,
                    In = ParameterLocation.Header,
                    Name = "X-API-Key",
                    Description = "API key needed to access the endpoints"
                });

                c.AddSecurityRequirement(new OpenApiSecurityRequirement
                {
                    {
                        new OpenApiSecurityScheme
                        {
                            Reference = new OpenApiReference
                            {
                                Type = ReferenceType.SecurityScheme,
                                Id = "ApiKey"
                            }
                        },
                        Array.Empty<string>()
                    }
                });
            });

            // Register services
            builder.Services.AddScoped<IFileServerManager, FileServerManager>();

            // Configure rate limiting
            builder.Services.AddRateLimiter(options =>
            {
                options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
                    RateLimitPartition.GetFixedWindowLimiter(
                        partitionKey: httpContext.User.Identity?.Name ?? httpContext.Request.Headers.Host.ToString(),
                        factory: partition => new FixedWindowRateLimiterOptions
                        {
                            AutoReplenishment = true,
                            PermitLimit = 1000,
                            QueueLimit = 0,
                            Window = TimeSpan.FromMinutes(1)
                        }));
            });

            // Configure compression
            builder.Services.AddResponseCompression(options =>
            {
                options.EnableForHttps = true;
            });

            var app = builder.Build();

            // Configure request pipeline
            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI(c =>
                {
                    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Central File Server API V1");
                });
            }
            else
            {
                // Production security headers
                //app.Use(async (context, next) =>
                //{
                //    context.Response.Headers.Add("X-Content-Type-Options", "nosniff");
                //    context.Response.Headers.Add("X-Frame-Options", "DENY");
                //    context.Response.Headers.Add("X-XSS-Protection", "1; mode=block");
                //    context.Response.Headers.Add("Referrer-Policy", "strict-origin-when-cross-origin");
                //    context.Response.Headers.Add("Content-Security-Policy", "default-src 'self'");
                //    await next();
                //});
            }

            // Global exception handler
            app.UseExceptionHandler(errorApp =>
            {
                errorApp.Run(async context =>
                {
                    context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                    context.Response.ContentType = "application/json";

                    var error = context.Features.Get<IExceptionHandlerFeature>();
                    if (error != null)
                    {
                        var exception = error.Error;
                        Log.Error(exception, "Unhandled exception occurred");

                        await context.Response.WriteAsJsonAsync(new
                        {
                            StatusCode = context.Response.StatusCode,
                            Message = app.Environment.IsDevelopment() ? exception.Message : "An error occurred processing your request."
                        });
                    }
                });
            });

            // Forward headers from proxy
            app.UseForwardedHeaders(new ForwardedHeadersOptions
            {
                ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
            });

            //app.UseHttpsRedirection();
            app.UseResponseCompression();
            //app.UseRateLimiter();
            //app.UseCors("AllowPortal");
            app.MapIdentityApi<ApplicationUser>();
            // API key middleware
            //app.UseMiddleware<ApiKeyMiddleware>();
            app.UseAuthentication();
            app.UseAuthorization();

            // Health check endpoint
            app.MapHealthChecks("/health");

            app.MapControllers();

            // Apply migrations at application startup
            using (var scope = app.Services.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                dbContext.Database.Migrate();
            }

            using (var scope = app.Services.CreateScope())
            {
                var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
                var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

                // Create Admin role if it doesn't exist
                if (!roleManager.RoleExistsAsync("Admin").Result)
                {
                    roleManager.CreateAsync(new IdentityRole("Admin"));
                }

                // Create admin user if it doesn't exist
                if (userManager.FindByNameAsync("admin").Result == null)
                {
                    var adminUser = new ApplicationUser
                    {
                        UserName = "admin",
                        Email = "admin@example.com",
                        FirstName = "Admin",
                        LastName = "User",
                        EmailConfirmed = true
                    };

                    userManager.CreateAsync(adminUser, "Admin@123");
                    userManager.AddToRoleAsync(adminUser, "Admin");
                }
            }

            using (var scope = app.Services.CreateScope())
            {
                var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
                var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

                // Create Admin role if it doesn't exist
                if (!roleManager.RoleExistsAsync("Reports").Result)
                {
                    roleManager.CreateAsync(new IdentityRole("Reports"));
                }

                // Create admin user if it doesn't exist
                if (userManager.FindByNameAsync("reports").Result == null)
                {
                    var reportUser = new ApplicationUser
                    {
                        UserName = "reports",
                        Email = "reports@example.com",
                        FirstName = "Report",
                        LastName = "User",
                        EmailConfirmed = true
                    };

                    userManager.CreateAsync(reportUser, "Reports@123");
                    userManager.AddToRoleAsync(reportUser, "Reports");
                }
            }

            using (var scope = app.Services.CreateScope())
            {
                var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
                var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

                // Create Admin role if it doesn't exist
                if (!roleManager.RoleExistsAsync("Reports").Result)
                {
                    roleManager.CreateAsync(new IdentityRole("Reports"));
                }

                // Create admin user if it doesn't exist
                if (userManager.FindByNameAsync("reports1").Result == null)
                {
                    var reportUser = new ApplicationUser
                    {
                        UserName = "reports1",
                        Email = "reports1@example.com",
                        FirstName = "Report",
                        LastName = "User1",
                        EmailConfirmed = true
                    };

                    userManager.CreateAsync(reportUser, "tduKCY3TDuE&V*mz");
                    userManager.AddToRoleAsync(reportUser, "Reports");
                }
            }

            using (var scope = app.Services.CreateScope())
            {
                var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
                var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

                // Create Admin role if it doesn't exist
                if (!roleManager.RoleExistsAsync("Reports").Result)
                {
                    roleManager.CreateAsync(new IdentityRole("Reports"));
                }

                // Create admin user if it doesn't exist
                if (userManager.FindByNameAsync("reports2").Result == null)
                {
                    var reportUser = new ApplicationUser
                    {
                        UserName = "reports2",
                        Email = "reports2@example.com",
                        FirstName = "Report",
                        LastName = "User2",
                        EmailConfirmed = true
                    };

                    userManager.CreateAsync(reportUser, "IMGIfbq9@$HWWw*&");
                    userManager.AddToRoleAsync(reportUser, "Reports");
                }
            }

            using (var scope = app.Services.CreateScope())
            {
                var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
                var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

                // Create Admin role if it doesn't exist
                if (!roleManager.RoleExistsAsync("Reports").Result)
                {
                    roleManager.CreateAsync(new IdentityRole("Reports"));
                }

                // Create admin user if it doesn't exist
                if (userManager.FindByNameAsync("reports3").Result == null)
                {
                    var reportUser = new ApplicationUser
                    {
                        UserName = "reports3",
                        Email = "reports3@example.com",
                        FirstName = "Report",
                        LastName = "User3",
                        EmailConfirmed = true
                    };

                    userManager.CreateAsync(reportUser, "QD2sbgh$^AxqNzxY");
                    userManager.AddToRoleAsync(reportUser, "Reports");
                }
            }
            try
            {
                Log.Information("Starting Central API");
                app.Run();
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Application terminated unexpectedly");
            }
            finally
            {
                Log.CloseAndFlush();
            }
        }
    }
}
