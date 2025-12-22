using System;
using System.IO;
using System.Threading.Tasks;
using Keytietkiem.Controllers;
using Keytietkiem.DTOs.Post;
using Keytietkiem.Models;
using Keytietkiem.Services;
using Keytietkiem.UnitTests.Helpers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace Keytietkiem.UnitTests.Controllers
{
    public class PostImagesController_UploadTests
    {
        private static PostImagesController CreateController(
            string databaseName,
            Mock<IPhotoService>? photoServiceMock = null)
        {
            var options = new DbContextOptionsBuilder<KeytietkiemDbContext>()
                .UseInMemoryDatabase(databaseName)
                .Options;

            var factory = new TestDbContextFactory(options);
            var photoService = photoServiceMock?.Object ?? MockPhotoService.CreateMock().SetupUploadSuccess().Object;

            return new PostImagesController(factory.CreateDbContext(), photoService);
        }

        private static IFormFile CreateMockFormFile(string fileName, string contentType, long length)
        {
            var fileMock = new Mock<IFormFile>();
            var content = "test image content";
            var ms = new MemoryStream();
            var writer = new StreamWriter(ms);
            writer.Write(content);
            writer.Flush();
            ms.Position = 0;

            fileMock.Setup(f => f.FileName).Returns(fileName);
            fileMock.Setup(f => f.ContentType).Returns(contentType);
            fileMock.Setup(f => f.Length).Returns(length);
            fileMock.Setup(f => f.OpenReadStream()).Returns(ms);

            return fileMock.Object;
        }

        [Fact]
        public async Task UploadImage_ShouldReturnOk_WhenValidImageFile()
        {
            var photoServiceMock = MockPhotoService.CreateMock();
            photoServiceMock.SetupUploadSuccess("https://res.cloudinary.com/test/image/upload/test.jpg");

            var controller = CreateController("UploadImage_Valid", photoServiceMock);

            var request = new ImageUploadRequest
            {
                File = CreateMockFormFile("test.jpg", "image/jpeg", 1024)
            };

            var result = await controller.UploadImage(request);

            var ok = Assert.IsType<OkObjectResult>(result);
            var value = ok.Value;
            var pathProp = value?.GetType().GetProperty("path");
            var path = pathProp?.GetValue(value)?.ToString();
            Assert.NotNull(path);
            Assert.Contains("cloudinary.com", path);
        }

        [Fact]
        public async Task UploadImage_ShouldReturnBadRequest_WhenPhotoServiceThrowsArgumentException()
        {
            var photoServiceMock = MockPhotoService.CreateMock();
            photoServiceMock.SetupUploadThrows(new ArgumentException("No file uploaded"));

            var controller = CreateController("UploadImage_NoFile", photoServiceMock);

            var request = new ImageUploadRequest
            {
                File = CreateMockFormFile("test.jpg", "image/jpeg", 0)
            };

            var result = await controller.UploadImage(request);

            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            Assert.NotNull(badRequest.Value);
        }

        [Fact]
        public async Task UploadImage_ShouldReturnStatusCode500_WhenPhotoServiceThrowsException()
        {
            var photoServiceMock = MockPhotoService.CreateMock();
            photoServiceMock.SetupUploadThrows(new Exception("Upload failed"));

            var controller = CreateController("UploadImage_Exception", photoServiceMock);

            var request = new ImageUploadRequest
            {
                File = CreateMockFormFile("test.jpg", "image/jpeg", 1024)
            };

            var result = await controller.UploadImage(request);

            var statusCode = Assert.IsType<ObjectResult>(result);
            Assert.Equal(500, statusCode.StatusCode);
        }
    }
}

