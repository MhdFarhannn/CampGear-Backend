using CampGearBackend.Models;

namespace CampGearBackend.Services
{
    public class ImageService
    {
        private const long MaxSizeBytes = 2 * 1024 * 1024;

        private static readonly string[] AllowedExtensions = { ".jpg", ".jpeg", ".png", ".webp" };

        private static readonly string[] AllowedContentTypes =
        {
            "image/jpeg",
            "image/png",
            "image/webp"
        };

        private readonly string _folder;
        private readonly string _urlPrefix;

        #pragma warning disable CS8602
        public ImageService(IWebHostEnvironment environment)
        {
            var basePath = environment.WebRootPath ?? Path.Combine(environment.ContentRootPath, "wwwroot");

            _folder = Path.Combine(basePath, "uploads", "alat");
            _urlPrefix = Env.Value["Uploads:AlatUrl"] ?? "/uploads/alat";
        }
        #pragma warning restore CS8602

        //SAVE AN UPLOADED IMAGE AND RETURN THE URL STORED IN alat.image_path
        public async Task<string?> SaveAlatImage(IFormFile? file)
        {
            if (file == null || file.Length == 0)
            {
                return null;
            }

            if (file.Length > MaxSizeBytes)
            {
                throw new AppException(400, "gambar must not exceed 2 MB");
            }

            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();

            if (!AllowedExtensions.Contains(extension) || !AllowedContentTypes.Contains(file.ContentType.ToLowerInvariant()))
            {
                throw new AppException(400, "gambar must be a jpg, jpeg, png or webp file");
            }

            Directory.CreateDirectory(_folder);

            var fileName = $"{Guid.NewGuid():N}{extension}";
            var target = Path.Combine(_folder, fileName);

            using (var stream = File.Create(target))
            {
                await file.CopyToAsync(stream);
            }

            return $"{_urlPrefix}/{fileName}";
        }

        //DELETE A PREVIOUSLY SAVED IMAGE WHEN IT IS REPLACED
        public void Delete(string? imagePath)
        {
            if (string.IsNullOrWhiteSpace(imagePath) || !imagePath.StartsWith(_urlPrefix))
            {
                return;
            }

            var fileName = Path.GetFileName(imagePath);
            var target = Path.Combine(_folder, fileName);

            if (File.Exists(target))
            {
                File.Delete(target);
            }
        }
    }
}
