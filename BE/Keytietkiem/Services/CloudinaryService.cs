using CloudinaryDotNet.Actions;
using CloudinaryDotNet;

namespace Keytietkiem.Services
{
    public interface IPhotoService
    {
        Task<string> UploadPhotoAsync(IFormFile file);
        Task DeletePhotoAsync(string publicId);
    }
    public class CloudinaryService : IPhotoService
    {

        private readonly Cloudinary _cloudinary;

        public CloudinaryService(IConfiguration configuration)
        {
            var acc = new Account(
                configuration["Cloudinary:CloudName"],
                configuration["Cloudinary:ApiKey"],
                configuration["Cloudinary:ApiSecret"]
            );

            _cloudinary = new Cloudinary(acc);
        }

        public async Task<string> UploadPhotoAsync(IFormFile file)
        {
            if (file == null || file.Length == 0)
            {
                throw new ArgumentException("No file uploaded");
            }

            // Validate file type
            var allowedTypes = new[] { "image/jpeg", "image/png", "image/gif" };
            if (!allowedTypes.Contains(file.ContentType.ToLower()))
            {
                throw new ArgumentException("Invalid file type");
            }

            // Validate file size (max 5MB)
            if (file.Length > 5 * 1024 * 1024)
            {
                throw new ArgumentException("File size too large");
            }

            using var stream = file.OpenReadStream();
            var uploadParams = new ImageUploadParams
            {
                File = new FileDescription(file.FileName, stream),
                Folder = "posts", // organize files in folders
                UseFilename = false, // don't use original filename
                UniqueFilename = true, // ensure unique names
                Transformation = new Transformation()
                    .Quality("auto") // automatic quality optimization
                    .FetchFormat("auto") // automatic format optimization
            };

            var uploadResult = await _cloudinary.UploadAsync(uploadParams);

            if (uploadResult.Error != null)
            {
                throw new Exception(uploadResult.Error.Message);
            }

            return uploadResult.SecureUrl.ToString();
        }

        public async Task DeletePhotoAsync(string publicId)
        {
            if (string.IsNullOrEmpty(publicId))
                return;

            var deleteParams = new DeletionParams(publicId);
            var result = await _cloudinary.DestroyAsync(deleteParams);

            if (result.Error != null)
            {
                throw new Exception(result.Error.Message);
            }
        }
    }

}
