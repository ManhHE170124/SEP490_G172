using CloudinaryDotNet.Actions;
using CloudinaryDotNet;

namespace Keytietkiem.Services
{
    /// <summary>
    /// Service interface for photo operations using Cloudinary.
    /// </summary>
    public interface IPhotoService
    {
        /// <summary>
        /// Uploads a photo to Cloudinary.
        /// </summary>
        /// <param name="file">The file to upload.</param>
        /// <returns>The secure URL of the uploaded photo.</returns>
        Task<string> UploadPhotoAsync(IFormFile file);
        
        /// <summary>
        /// Deletes a photo from Cloudinary.
        /// </summary>
        /// <param name="publicId">The public ID of the photo to delete.</param>
        Task DeletePhotoAsync(string publicId);
    }
    
    /// <summary>
    /// Service implementation for photo operations using Cloudinary cloud storage.
    /// </summary>
    public class CloudinaryService : IPhotoService
    {

        private readonly Cloudinary _cloudinary;

        /// <summary>
        /// Initializes a new instance of the <see cref="CloudinaryService"/> class.
        /// </summary>
        /// <param name="configuration">The configuration containing Cloudinary credentials.</param>
        public CloudinaryService(IConfiguration configuration)
        {
            var acc = new Account(
                configuration["Cloudinary:CLOUD_NAME"],
                configuration["Cloudinary:CLOUD_API_KEY"],
                configuration["Cloudinary:CLOUD_API_SECRETS"]
            );

            _cloudinary = new Cloudinary(acc);
        }

        /// <summary>
        /// Uploads a photo to Cloudinary with validation and optimization.
        /// </summary>
        /// <param name="file">The file to upload (JPEG, PNG, or GIF, max 5MB).</param>
        /// <returns>The secure URL of the uploaded photo.</returns>
        /// <exception cref="ArgumentException">Thrown when file is invalid or exceeds size limit.</exception>
        /// <exception cref="Exception">Thrown when upload fails.</exception>
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

        /// <summary>
        /// Deletes a photo from Cloudinary.
        /// </summary>
        /// <param name="publicId">The public ID of the photo to delete.</param>
        /// <exception cref="Exception">Thrown when deletion fails.</exception>
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
