using System;
using System.Threading.Tasks;
using Keytietkiem.Controllers;
using Keytietkiem.DTOs.Post;
using Keytietkiem.Models;
using Keytietkiem.Services;
using Keytietkiem.UnitTests.Helpers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace Keytietkiem.UnitTests.Controllers
{
    public class PostImagesController_DeleteTests
    {
        private static PostImagesController CreateController(
            string databaseName,
            Mock<IPhotoService>? photoServiceMock = null)
        {
            var options = new DbContextOptionsBuilder<KeytietkiemDbContext>()
                .UseInMemoryDatabase(databaseName)
                .Options;

            var factory = new TestDbContextFactory(options);
            var photoService = photoServiceMock?.Object ?? MockPhotoService.CreateMock().SetupDeleteSuccess().Object;

            return new PostImagesController(factory.CreateDbContext(), photoService);
        }

        [Fact]
        public async Task DeleteImage_ShouldReturnOk_WhenValidPublicId()
        {
            var photoServiceMock = MockPhotoService.CreateMock();
            photoServiceMock.SetupDeleteSuccess();

            var controller = CreateController("DeleteImage_Valid", photoServiceMock);

            var request = new ImageDeleteRequest
            {
                PublicId = "posts/test-image-123"
            };

            var result = await controller.DeleteImage(request);

            var ok = Assert.IsType<OkObjectResult>(result);
            var value = ok.Value;
            var messageProp = value?.GetType().GetProperty("message");
            var message = messageProp?.GetValue(value)?.ToString();
            Assert.NotNull(message);
            Assert.Contains("deleted successfully", message);
        }

        [Fact]
        public async Task DeleteImage_ShouldReturnBadRequest_WhenPublicIdIsEmpty()
        {
            var photoServiceMock = MockPhotoService.CreateMock();
            photoServiceMock.SetupDeleteSuccess();

            var controller = CreateController("DeleteImage_EmptyPublicId", photoServiceMock);

            var request = new ImageDeleteRequest
            {
                PublicId = ""
            };

            var result = await controller.DeleteImage(request);

            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            var value = badRequest.Value;
            var messageProp = value?.GetType().GetProperty("message");
            var message = messageProp?.GetValue(value)?.ToString();
            Assert.NotNull(message);
            Assert.Contains("PublicId is required", message);
        }

        [Fact]
        public async Task DeleteImage_ShouldReturnBadRequest_WhenPublicIdIsNull()
        {
            var photoServiceMock = MockPhotoService.CreateMock();
            photoServiceMock.SetupDeleteSuccess();

            var controller = CreateController("DeleteImage_NullPublicId", photoServiceMock);

            var request = new ImageDeleteRequest
            {
                PublicId = null!
            };

            var result = await controller.DeleteImage(request);

            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            var value = badRequest.Value;
            var messageProp = value?.GetType().GetProperty("message");
            var message = messageProp?.GetValue(value)?.ToString();
            Assert.NotNull(message);
            Assert.Contains("PublicId is required", message);
        }

        [Fact]
        public async Task DeleteImage_ShouldReturnBadRequest_WhenPublicIdIsWhitespace()
        {
            var photoServiceMock = MockPhotoService.CreateMock();
            photoServiceMock.SetupDeleteSuccess();

            var controller = CreateController("DeleteImage_WhitespacePublicId", photoServiceMock);

            var request = new ImageDeleteRequest
            {
                PublicId = "   "
            };

            var result = await controller.DeleteImage(request);

            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            var value = badRequest.Value;
            var messageProp = value?.GetType().GetProperty("message");
            var message = messageProp?.GetValue(value)?.ToString();
            Assert.NotNull(message);
            Assert.Contains("PublicId is required", message);
        }

        [Fact]
        public async Task DeleteImage_ShouldReturnStatusCode500_WhenPhotoServiceThrowsException()
        {
            var photoServiceMock = MockPhotoService.CreateMock();
            photoServiceMock.Setup(x => x.DeletePhotoAsync(It.IsAny<string>()))
                .ThrowsAsync(new Exception("Delete failed"));

            var controller = CreateController("DeleteImage_Exception", photoServiceMock);

            var request = new ImageDeleteRequest
            {
                PublicId = "posts/test-image-123"
            };

            var result = await controller.DeleteImage(request);

            var statusCode = Assert.IsType<ObjectResult>(result);
            Assert.Equal(500, statusCode.StatusCode);
            var value = statusCode.Value;
            var messageProp = value?.GetType().GetProperty("message");
            var message = messageProp?.GetValue(value)?.ToString();
            Assert.NotNull(message);
            Assert.Contains("Error deleting image", message);
        }
    }
}

