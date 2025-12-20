using System.Threading.Tasks;
using Keytietkiem.Services;
using Microsoft.AspNetCore.Http;
using Moq;

namespace Keytietkiem.UnitTests.Helpers
{
    public static class MockAuditLogger
    {
        public static Mock<IAuditLogger> CreateMock()
        {
            var mock = new Mock<IAuditLogger>();
            mock.Setup(x => x.LogAsync(
                It.IsAny<HttpContext>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<object>(),
                It.IsAny<object>()))
                .Returns(Task.CompletedTask);
            return mock;
        }
    }
}

