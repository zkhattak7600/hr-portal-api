using EmployeeLeaveApi.Models;
using Microsoft.AspNetCore.Http;

namespace EmployeeLeaveApi.Services;

/// <summary>Stores employee avatars in wwwroot/uploads/avatars. PhotoUrl is stored relative ("/uploads/avatars/x.png").</summary>
public static class PhotoStorage
{
    public const long MaxBytes = 2 * 1024 * 1024;
    private static readonly Dictionary<string, string[]> Allowed = new(StringComparer.OrdinalIgnoreCase)
    {
        [".jpg"] = new[] { "image/jpeg", "image/jpg", "image/pjpeg" },
        [".jpeg"] = new[] { "image/jpeg", "image/jpg", "image/pjpeg" },
        [".png"] = new[] { "image/png" },
        [".webp"] = new[] { "image/webp" },
    };

    public static string? Validate(IFormFile? file)
    {
        if (file == null || file.Length == 0) return "Please choose an image file (form field 'file').";
        if (file.Length > MaxBytes) return "Image must be 2 MB or smaller.";
        var ext = Path.GetExtension(file.FileName);
        if (string.IsNullOrEmpty(ext) || !Allowed.TryGetValue(ext, out var types))
            return "Only JPG, PNG or WEBP images are allowed.";
        if (!string.IsNullOrEmpty(file.ContentType) && file.ContentType != "application/octet-stream"
            && !types.Contains(file.ContentType.ToLowerInvariant()))
            return "Only JPG, PNG or WEBP images are allowed.";
        return null;
    }

    private static string Dir(IWebHostEnvironment env)
    {
        var root = env.WebRootPath ?? Path.Combine(env.ContentRootPath, "wwwroot");
        var dir = Path.Combine(root, "uploads", "avatars");
        Directory.CreateDirectory(dir);
        return dir;
    }

    public static async Task Save(Employee e, IFormFile file, IWebHostEnvironment env)
    {
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (ext == ".jpeg") ext = ".jpg";
        var name = $"{e.Id}_{Guid.NewGuid():N}{ext}";
        await using (var fs = File.Create(Path.Combine(Dir(env), name)))
            await file.CopyToAsync(fs);
        DeleteFile(e.PhotoUrl, env);
        e.PhotoUrl = "/uploads/avatars/" + name;
    }

    public static void Remove(Employee e, IWebHostEnvironment env)
    {
        DeleteFile(e.PhotoUrl, env);
        e.PhotoUrl = null;
    }

    private static void DeleteFile(string? url, IWebHostEnvironment env)
    {
        if (string.IsNullOrWhiteSpace(url)) return;
        var name = Path.GetFileName(url);
        if (string.IsNullOrEmpty(name)) return;
        try { var p = Path.Combine(Dir(env), name); if (File.Exists(p)) File.Delete(p); } catch { /* ignore */ }
    }
}