namespace ToolRental.WebApp.Infrastructure;

public sealed class SettingsFileStorage
{
    private readonly string _rootDirectory;

    public SettingsFileStorage(IWebHostEnvironment environment)
    {
        _rootDirectory = Path.Combine(environment.ContentRootPath, "App_Data", "settings-uploads");
        Directory.CreateDirectory(_rootDirectory);
    }

    public async Task<string> SaveAsync(IFormFile file, string category, IEnumerable<string> allowedExtensions, CancellationToken cancellationToken = default)
    {
        if (file.Length <= 0)
            throw new InvalidOperationException("Az üres fájl nem menthető.");

        var extension = Path.GetExtension(file.FileName);
        if (string.IsNullOrWhiteSpace(extension))
            throw new InvalidOperationException("A fájlnak kell kiterjesztés.");

        var normalizedExtension = extension.ToLowerInvariant();
        var allowed = allowedExtensions.Select(x => x.ToLowerInvariant()).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (!allowed.Contains(normalizedExtension))
            throw new InvalidOperationException($"Nem támogatott fájltípus: {normalizedExtension}");

        var targetDirectory = Path.Combine(_rootDirectory, category);
        Directory.CreateDirectory(targetDirectory);

        var safeBaseName = Sanitize(Path.GetFileNameWithoutExtension(file.FileName));
        var fileName = $"{DateTime.UtcNow:yyyyMMddHHmmss}_{Guid.NewGuid():N}_{safeBaseName}{normalizedExtension}";
        var absolutePath = Path.Combine(targetDirectory, fileName);

        await using var stream = File.Create(absolutePath);
        await file.CopyToAsync(stream, cancellationToken);

        return absolutePath;
    }

    public bool IsManagedFile(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;

        var fullPath = Path.GetFullPath(path);
        var rootPath = Path.GetFullPath(_rootDirectory);
        return fullPath.StartsWith(rootPath, StringComparison.OrdinalIgnoreCase);
    }

    public void TryDeleteManagedFile(string? path)
    {
        if (!IsManagedFile(path))
            return;

        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
            // A régi fájl törlése nem állíthatja meg a mentést.
        }
    }

    private static string Sanitize(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return "feltoltes";

        var cleaned = input.Trim();
        foreach (var invalidChar in Path.GetInvalidFileNameChars())
            cleaned = cleaned.Replace(invalidChar, '_');

        cleaned = cleaned.Replace(' ', '_');
        return string.IsNullOrWhiteSpace(cleaned) ? "feltoltes" : cleaned;
    }
}
