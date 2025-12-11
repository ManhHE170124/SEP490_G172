using Keytietkiem.Controllers;
using Keytietkiem.DTOs;
using Keytietkiem.Infrastructure;
using Keytietkiem.Services;
using Keytietkiem.Services.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Keytietkiem.UnitTests.Controllers
{
    /// <summary>
    /// Unit tests cho CreateProductReport (Create Error Key)
    /// Mapping sơ bộ theo decision table CEK01–CEK10.
    /// </summary>
    public class ProductReportCreateErrorKeyTests
    {
        private readonly Mock<IProductReportService> _productReportServiceMock;
        private readonly Mock<IAuditLogger> _auditLoggerMock;
        private readonly ProductReportController _controller;

        public ProductReportCreateErrorKeyTests()
        {
            _productReportServiceMock = new Mock<IProductReportService>(MockBehavior.Strict);
            _auditLoggerMock = new Mock<IAuditLogger>(MockBehavior.Strict);

            _controller = new ProductReportController(
                _productReportServiceMock.Object,
                _auditLoggerMock.Object);

            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            };
        }

        /// <summary>
        /// Build dto hợp lệ cho case lỗi key.
        /// Chỉ dùng các field mà ProductReportService cần.
        /// </summary>
        private static CreateProductReportDto BuildValidKeyErrorDto(bool includeOptionalFields = false)
        {
            return new CreateProductReportDto
            {
                UserId = Guid.NewGuid(),
                ProductVariantId = Guid.NewGuid(),
                ProductKeyId = Guid.NewGuid(),
                ProductAccountId = null,
                Name = includeOptionalFields ? "Lỗi key - đầy đủ thông tin" : "Lỗi key",
                Description = includeOptionalFields
                    ? "Key không kích hoạt được trên máy khách, có đính kèm thông tin chi tiết."
                    : "Key không kích hoạt được."
            };
        }

        private void SetupAuditLogger(Guid reportId)
        {
            _auditLoggerMock
                .Setup(a => a.LogAsync(
                    It.IsAny<HttpContext>(),
                    "Create",
                    "ProductReport",
                    reportId.ToString(),
                    It.IsAny<object>(),
                    It.IsAny<object>()))
                .Returns(Task.CompletedTask);
        }

        // CEK01 - Normal: chỉ field bắt buộc
        [Fact]
        public async Task CEK01_CreateKeyError_Succeeds_WithRequiredFieldsOnly()
        {
            var dto = BuildValidKeyErrorDto(includeOptionalFields: false);
            var reportId = Guid.NewGuid();

            var createdReport = new ProductReportResponseDto
            {
                Id = reportId,
                Name = dto.Name,
                Description = dto.Description,
                ProductVariantId = dto.ProductVariantId,
                ProductKeyId = dto.ProductKeyId,
                ProductAccountId = dto.ProductAccountId,
                UserId = dto.UserId!.Value,
                Status = "Pending"
            };

            _productReportServiceMock
                .Setup(s => s.CreateProductReportAsync(
                    It.IsAny<CreateProductReportDto>(),
                    It.IsAny<Guid>(),
                    It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(createdReport));

            SetupAuditLogger(reportId);

            var result = await _controller.CreateProductReport(dto);

            var createdAt = Assert.IsType<CreatedAtActionResult>(result);
            Assert.Equal(nameof(ProductReportController.GetProductReportById), createdAt.ActionName);

            var response = Assert.IsType<ProductReportResponseDto>(createdAt.Value);
            Assert.Equal(reportId, response.Id);
            Assert.Equal(dto.Name, response.Name);

            _productReportServiceMock.Verify(s => s.CreateProductReportAsync(
                    It.Is<CreateProductReportDto>(d => d.ProductVariantId == dto.ProductVariantId),
                    dto.UserId!.Value,
                    It.IsAny<CancellationToken>()),
                Times.Once);

            _auditLoggerMock.Verify(a => a.LogAsync(
                    It.IsAny<HttpContext>(),
                    "Create",
                    "ProductReport",
                    reportId.ToString(),
                    It.IsAny<object>(),
                    It.IsAny<object>()),
                Times.Once);

            _productReportServiceMock.VerifyNoOtherCalls();
            _auditLoggerMock.VerifyNoOtherCalls();
        }

        // CEK02 - Normal: có thêm optional fields
        [Fact]
        public async Task CEK02_CreateKeyError_Succeeds_WithOptionalFieldsFilled()
        {
            var dto = BuildValidKeyErrorDto(includeOptionalFields: true);
            var reportId = Guid.NewGuid();

            var createdReport = new ProductReportResponseDto
            {
                Id = reportId,
                Name = dto.Name,
                Description = dto.Description,
                ProductVariantId = dto.ProductVariantId,
                ProductKeyId = dto.ProductKeyId,
                ProductAccountId = dto.ProductAccountId,
                UserId = dto.UserId!.Value,
                Status = "Pending"
            };

            _productReportServiceMock
                .Setup(s => s.CreateProductReportAsync(
                    It.IsAny<CreateProductReportDto>(),
                    It.IsAny<Guid>(),
                    It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(createdReport));

            SetupAuditLogger(reportId);

            var result = await _controller.CreateProductReport(dto);

            var createdAt = Assert.IsType<CreatedAtActionResult>(result);
            var response = Assert.IsType<ProductReportResponseDto>(createdAt.Value);
            Assert.Equal(reportId, response.Id);
            Assert.Equal(dto.Description, response.Description);

            _productReportServiceMock.Verify(s => s.CreateProductReportAsync(
                    It.Is<CreateProductReportDto>(d => d.ProductKeyId == dto.ProductKeyId),
                    dto.UserId!.Value,
                    It.IsAny<CancellationToken>()),
                Times.Once);

            _auditLoggerMock.Verify(a => a.LogAsync(
                    It.IsAny<HttpContext>(),
                    "Create",
                    "ProductReport",
                    reportId.ToString(),
                    It.IsAny<object>(),
                    It.IsAny<object>()),
                Times.Once);

            _productReportServiceMock.VerifyNoOtherCalls();
            _auditLoggerMock.VerifyNoOtherCalls();
        }

        // CEK03 - Abnormal: thiếu field bắt buộc (service ném ArgumentException)
        [Fact]
        public async Task CEK03_CreateKeyError_ThrowsArgumentException_WhenRequiredFieldsMissing()
        {
            var dto = new CreateProductReportDto
            {
                UserId = Guid.NewGuid()
                // Cố tình không set ProductVariantId, ProductKeyId, Name, Description
            };

            _productReportServiceMock
                .Setup(s => s.CreateProductReportAsync(
                    It.IsAny<CreateProductReportDto>(),
                    It.IsAny<Guid>(),
                    It.IsAny<CancellationToken>()))
                .ThrowsAsync(new ArgumentException("Required fields missing"));

            var ex = await Assert.ThrowsAsync<ArgumentException>(() => _controller.CreateProductReport(dto));
            Assert.Equal("Required fields missing", ex.Message);

            _productReportServiceMock.Verify(s => s.CreateProductReportAsync(
                    It.IsAny<CreateProductReportDto>(),
                    It.IsAny<Guid>(),
                    It.IsAny<CancellationToken>()),
                Times.Once);

            _auditLoggerMock.VerifyNoOtherCalls();
        }

        // CEK04 - Abnormal: variant không tồn tại
        [Fact]
        public async Task CEK04_CreateKeyError_ThrowsInvalidOperation_WhenVariantDoesNotExist()
        {
            var dto = BuildValidKeyErrorDto();

            _productReportServiceMock
                .Setup(s => s.CreateProductReportAsync(
                    It.IsAny<CreateProductReportDto>(),
                    It.IsAny<Guid>(),
                    It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("Biến thể sản phẩm không tồn tại"));

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => _controller.CreateProductReport(dto));
            Assert.Equal("Biến thể sản phẩm không tồn tại", ex.Message);

            _productReportServiceMock.Verify(s => s.CreateProductReportAsync(
                    It.IsAny<CreateProductReportDto>(),
                    It.IsAny<Guid>(),
                    It.IsAny<CancellationToken>()),
                Times.Once);

            _auditLoggerMock.VerifyNoOtherCalls();
        }

        // CEK05 - Abnormal: không cung cấp ProductKeyId và ProductAccountId
        [Fact]
        public async Task CEK05_CreateKeyError_ThrowsInvalidOperation_WhenNoKeyOrAccountProvided()
        {
            var dto = BuildValidKeyErrorDto();
            dto.ProductKeyId = null;
            dto.ProductAccountId = null;

            _productReportServiceMock
                .Setup(s => s.CreateProductReportAsync(
                    It.IsAny<CreateProductReportDto>(),
                    It.IsAny<Guid>(),
                    It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("Phải cung cấp ít nhất một trong ProductKeyId hoặc ProductAccountId"));

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => _controller.CreateProductReport(dto));
            Assert.Equal("Phải cung cấp ít nhất một trong ProductKeyId hoặc ProductAccountId", ex.Message);

            _productReportServiceMock.Verify(s => s.CreateProductReportAsync(
                    It.IsAny<CreateProductReportDto>(),
                    It.IsAny<Guid>(),
                    It.IsAny<CancellationToken>()),
                Times.Once);

            _auditLoggerMock.VerifyNoOtherCalls();
        }

        // CEK06 - Abnormal: Product key không tồn tại
        [Fact]
        public async Task CEK06_CreateKeyError_ThrowsInvalidOperation_WhenProductKeyDoesNotExist()
        {
            var dto = BuildValidKeyErrorDto();

            _productReportServiceMock
                .Setup(s => s.CreateProductReportAsync(
                    It.IsAny<CreateProductReportDto>(),
                    It.IsAny<Guid>(),
                    It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("Product key không tồn tại"));

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => _controller.CreateProductReport(dto));
            Assert.Equal("Product key không tồn tại", ex.Message);

            _productReportServiceMock.Verify(s => s.CreateProductReportAsync(
                    It.IsAny<CreateProductReportDto>(),
                    It.IsAny<Guid>(),
                    It.IsAny<CancellationToken>()),
                Times.Once);

            _auditLoggerMock.VerifyNoOtherCalls();
        }

        // CEK07 - Abnormal: Product account không tồn tại
        [Fact]
        public async Task CEK07_CreateKeyError_ThrowsInvalidOperation_WhenProductAccountDoesNotExist()
        {
            var dto = BuildValidKeyErrorDto();
            dto.ProductAccountId = Guid.NewGuid();

            _productReportServiceMock
                .Setup(s => s.CreateProductReportAsync(
                    It.IsAny<CreateProductReportDto>(),
                    It.IsAny<Guid>(),
                    It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("Tài khoản sản phẩm không tồn tại"));

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => _controller.CreateProductReport(dto));
            Assert.Equal("Tài khoản sản phẩm không tồn tại", ex.Message);

            _productReportServiceMock.Verify(s => s.CreateProductReportAsync(
                    It.IsAny<CreateProductReportDto>(),
                    It.IsAny<Guid>(),
                    It.IsAny<CancellationToken>()),
                Times.Once);

            _auditLoggerMock.VerifyNoOtherCalls();
        }

        // CEK08 - Abnormal: thiếu Reporter (UserId)
        [Fact]
        public async Task CEK08_CreateKeyError_ThrowsInvalidOperation_WhenReporterMissing()
        {
            var dto = BuildValidKeyErrorDto();
            dto.UserId = null;

            // Controller sẽ ném InvalidOperationException từ dto.UserId!.Value
            await Assert.ThrowsAsync<InvalidOperationException>(() => _controller.CreateProductReport(dto));

            _productReportServiceMock.VerifyNoOtherCalls();
            _auditLoggerMock.VerifyNoOtherCalls();
        }

        // CEK09 - Cancel: bấm Cancel trên UI -> không gọi API
        [Fact]
        public void CEK09_CancelCreateKeyError_DoesNotCallService()
        {
            _productReportServiceMock.VerifyNoOtherCalls();
            _auditLoggerMock.VerifyNoOtherCalls();
        }

        // CEK10 - Cancel (Boundary): form đã nhập dữ liệu nhưng vẫn Cancel
        [Fact]
        public void CEK10_CancelCreateKeyError_WithFilledForm_DoesNotCallService()
        {
            _productReportServiceMock.VerifyNoOtherCalls();
            _auditLoggerMock.VerifyNoOtherCalls();
        }
    }
}
