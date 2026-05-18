namespace ToolRental.Bikes;

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

        var categoryDirectory = Path.Combine(_rootDirectory, category);
        Directory.CreateDirectory(categoryDirectory);

        var safeBaseName = Sanitize(Path.GetFileNameWithoutExtension(file.FileName));
        var fileName = $"{DateTime.UtcNow:yyyyMMddHHmmss}_{Guid.NewGuid():N}_{safeBaseName}{normalizedExtension}";
        var absolutePath = Path.Combine(categoryDirectory, fileName);

        await using var stream = File.Create(absolutePath);
        await file.CopyToAsync(stream, cancellationToken);

        return absolutePath;
    }

    public void TryDeleteManagedFile(string? storedPath)
    {
        if (string.IsNullOrWhiteSpace(storedPath))
            return;

        try
        {
            var fullPath = Path.GetFullPath(storedPath);
            if (!IsManagedFile(fullPath))
                return;

            if (File.Exists(fullPath))
                File.Delete(fullPath);
        }
        catch
        {
            // A régi fájl törlésének hibája ne akadályozza meg a mentést.
        }
    }

    public bool IsManagedFile(string? storedPath)
    {
        if (string.IsNullOrWhiteSpace(storedPath))
            return false;

        var fullPath = Path.GetFullPath(storedPath);
        var managedRoot = Path.GetFullPath(_rootDirectory);
        return fullPath.StartsWith(managedRoot, StringComparison.OrdinalIgnoreCase);
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
