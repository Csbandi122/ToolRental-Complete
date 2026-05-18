using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using ToolRental.Data;
using ToolRental.WebApp.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

var appDataDirectory = Path.Combine(builder.Environment.ContentRootPath, "App_Data");
var dataProtectionKeysDirectory = Path.Combine(appDataDirectory, "keys");
Directory.CreateDirectory(dataProtectionKeysDirectory);

builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(dataProtectionKeysDirectory))
    .SetApplicationName("ToolRental.WebApp");

builder.Services.AddSingleton<RuntimeSqlSettingsStore>();
builder.Services.AddSingleton<SettingsFileStorage>();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddDbContext<ToolRentalDbContext>((serviceProvider, options) =>
{
    var runtimeSqlSettingsStore = serviceProvider.GetRequiredService<RuntimeSqlSettingsStore>();

    options.UseSqlServer(
            runtimeSqlSettingsStore.BuildConnectionString(),
            sqlOptions =>
            {
                sqlOptions.EnableRetryOnFailure(
                    maxRetryCount: 3,
                    maxRetryDelay: TimeSpan.FromSeconds(5),
                    errorNumbersToAdd: null);
            })
        .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
});

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.UseCors();
app.UseStaticFiles();

app.MapControllers();

app.MapFallbackToFile("index.html");

var uploadsDir = Path.Combine(app.Environment.WebRootPath ?? Path.Combine(app.Environment.ContentRootPath, "wwwroot"), "uploads", "devices");
Directory.CreateDirectory(uploadsDir);

var settingsUploadsDir = Path.Combine(app.Environment.ContentRootPath, "App_Data", "settings-uploads");
Directory.CreateDirectory(settingsUploadsDir);

app.Run("http://0.0.0.0:2481");
