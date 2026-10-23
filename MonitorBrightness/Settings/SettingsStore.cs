using System.Text.Json;
using System.Text.Json.Serialization;

namespace MonitorBrightness.Settings;

internal sealed record SettingsLoad(AppSettings Settings, string? Warning);

internal sealed class SettingsStore(string path)
{
    private const int MaximumBytes = 65536;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };
    internal static string DefaultPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MonitorBrightness", "settings.json");

    internal SettingsLoad Load()
    {
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (stream.Length > MaximumBytes) throw new InvalidOperationException("Settings file is too large.");
            var settings = JsonSerializer.Deserialize<AppSettings>(stream, JsonOptions)
                ?? throw new InvalidOperationException("Settings must contain a JSON object.");
            settings.Validate();
            return new(settings, null);
        }
        catch (FileNotFoundException) { return new(new(), null); }
        catch (DirectoryNotFoundException) { return new(new(), null); }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException)
        {
            // Do not silently overwrite the original. An explicit Save backs it up.
            return new(new(), "Settings were ignored; safe defaults are in use. " + exception.Message
                + " Select your monitor in Settings. The original file has been left unchanged.");
        }
    }

    internal void Save(AppSettings settings)
    {
        settings.Validate();
        var data = JsonSerializer.SerializeToUtf8Bytes(settings, JsonOptions);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                stream.Write(data);
                stream.Flush(true);
            }
            // Same-directory replacement: readers see the old or new complete document.
            if (File.Exists(path)) File.Replace(temporary, path, path + ".bak");
            else File.Move(temporary, path);
        }
        finally
        {
            // Only our uniquely named temporary file is eligible for cleanup.
            try { if (File.Exists(temporary)) File.Delete(temporary); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
}
