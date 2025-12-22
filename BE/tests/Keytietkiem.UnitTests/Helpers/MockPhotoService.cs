using System.Threading.Tasks;
using Keytietkiem.Services;
using Microsoft.AspNetCore.Http;
using Moq;

namespace Keytietkiem.UnitTests.Helpers
{
    public static class MockPhotoService
    {
        public static Mock<IPhotoService> CreateMock()
        {
            var mock = new Mock<IPhotoService>();
            return mock;
        }

        public static Mock<IPhotoService> SetupUploadSuccess(this Mock<IPhotoService> mock, string returnUrl = "https://res.cloudinary.com/test/image/upload/test.jpg")
        {
            mock.Setup(x => x.UploadPhotoAsync(It.IsAny<IFormFile>()))
                .ReturnsAsync(returnUrl);
            return mock;
        }

        public static Mock<IPhotoService> SetupUploadThrows(this Mock<IPhotoService> mock, Exception exception)
        {
            mock.Setup(x => x.UploadPhotoAsync(It.IsAny<IFormFile>()))
                .ThrowsAsync(exception);
            return mock;
        }

        public static Mock<IPhotoService> SetupDeleteSuccess(this Mock<IPhotoService> mock)
        {
            mock.Setup(x => x.DeletePhotoAsync(It.IsAny<string>()))
                .Returns(Task.CompletedTask);
            return mock;
        }
    }
}

