using Keytietkiem.Controllers;
using Keytietkiem.DTOs;
using Keytietkiem.Infrastructure;
using Keytietkiem.Services;
using Keytietkiem.Services.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Keytietkiem.UnitTests.Controllers
{
    /// <summary>
    /// Unit tests cho chức năng Update Supplier
    /// 10 test case: USup001 - USup010
    /// </summary>
    public class SupplierUpdateTests
    {
        private readonly Mock<ISupplierService> _serviceMock;
        private readonly Mock<IAuditLogger> _auditLoggerMock;
        private readonly SupplierController _controller;

        public SupplierUpdateTests()
        {
            _serviceMock = new Mock<ISupplierService>();
            _auditLoggerMock = new Mock<IAuditLogger>();

            _controller = new SupplierController(
                _serviceMock.Object,
                _auditLoggerMock.Object);

            // Giả lập user đã đăng nhập + account active + email verified + role hợp lệ
            var httpContext = new DefaultHttpContext();
            httpContext.User = new ClaimsPrincipal(
                new ClaimsIdentity(new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
                    new Claim(ClaimTypes.Email, "staff@example.com"),
                }, "TestAuth"));

            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = httpContext
            };
        }

        #region Helpers

        private static UpdateSupplierDto BuildValidUpdateDto(int? idOverride = null)
        {
            return new UpdateSupplierDto
            {
                SupplierId = idOverride ?? 1,
                Name = "Updated Supplier",
                ContactEmail = "updated@example.com",
                ContactPhone = "+84901234567",
                LicenseTerms = "New license terms content",
                Notes = "Some updated notes"
            };
        }

        private static bool ValidateDto(UpdateSupplierDto dto, out List<ValidationResult> results)
        {
            var ctx = new ValidationContext(dto);
            results = new List<ValidationResult>();
            return Validator.TryValidateObject(dto, ctx, results, validateAllProperties: true);
        }

        #endregion

        // USup001 - Normal: update thành công
        [Fact]
        public async Task USup001_UpdateSupplier_Succeeds_WithValidData()
        {
            var id = 10;
            var dto = BuildValidUpdateDto(id);

            var updated = new SupplierResponseDto
            {
                SupplierId = id,
                Name = dto.Name,
                ContactEmail = dto.ContactEmail,
                ContactPhone = dto.ContactPhone,
                LicenseTerms = dto.LicenseTerms,
                Notes = dto.Notes,
                CreatedAt = DateTime.UtcNow.AddMonths(-1),
                Status = "Active",
                ActiveProductCount = 3,
                TotalProductKeyCount = 20
            };

            _serviceMock
                .Setup(s => s.UpdateSupplierAsync(
                    It.IsAny<UpdateSupplierDto>(),
                    It.IsAny<Guid>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(updated);

            var result = await _controller.UpdateSupplier(id, dto);

            var ok = Assert.IsType<OkObjectResult>(result);
            var response = Assert.IsType<SupplierResponseDto>(ok.Value);

            Assert.Equal(updated.SupplierId, response.SupplierId);
            Assert.Equal(updated.Name, response.Name);
            Assert.Equal(updated.ContactEmail, response.ContactEmail);

            _serviceMock.Verify(s => s.UpdateSupplierAsync(
                    It.Is<UpdateSupplierDto>(d => d.SupplierId == id),
                    It.IsAny<Guid>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()),
                Times.Once);

            _auditLoggerMock.Verify(a => a.LogAsync(
                    It.IsAny<HttpContext>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<object>(),
                    It.IsAny<object>()),
                Times.Once);
        }

        // USup002 - Abnormal: ID URL != ID body
        [Fact]
        public async Task USup002_UpdateSupplier_ReturnsBadRequest_WhenIdMismatch()
        {
            var routeId = 10;
            var bodyId = 11;
            var dto = BuildValidUpdateDto(bodyId);

            var result = await _controller.UpdateSupplier(routeId, dto);

            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            var value = badRequest.Value;
            Assert.NotNull(value);

            var messageProp = value!.GetType().GetProperty("message");
            Assert.NotNull(messageProp);
            var message = (string?)messageProp!.GetValue(value);
            Assert.Equal("ID trong URL và body không khớp", message);

            _serviceMock.Verify(s => s.UpdateSupplierAsync(
                    It.IsAny<UpdateSupplierDto>(),
                    It.IsAny<Guid>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()),
                Times.Never);

            _auditLoggerMock.Verify(a => a.LogAsync(
                    It.IsAny<HttpContext>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<object>(),
                    It.IsAny<object>()),
                Times.Never);
        }

        // USup003 - Abnormal (service): Supplier không tồn tại
        [Fact]
        public async Task USup003_UpdateSupplier_ThrowsInvalidOperation_WhenSupplierNotFound()
        {
            var id = 10;
            var dto = BuildValidUpdateDto(id);

            _serviceMock
                .Setup(s => s.UpdateSupplierAsync(
                    It.IsAny<UpdateSupplierDto>(),
                    It.IsAny<Guid>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("Nhà cung cấp không tồn tại"));

            await Assert.ThrowsAsync<InvalidOperationException>(() => _controller.UpdateSupplier(id, dto));

            _auditLoggerMock.Verify(a => a.LogAsync(
                    It.IsAny<HttpContext>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<object>(),
                    It.IsAny<object>()),
                Times.Never);
        }

        // USup004 - Abnormal (service): Tên nhà cung cấp trùng
        [Fact]
        public async Task USup004_UpdateSupplier_ThrowsInvalidOperation_WhenDuplicateName()
        {
            var id = 10;
            var dto = BuildValidUpdateDto(id);

            _serviceMock
                .Setup(s => s.UpdateSupplierAsync(
                    It.IsAny<UpdateSupplierDto>(),
                    It.IsAny<Guid>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException($"Nhà cung cấp với tên '{dto.Name}' đã tồn tại"));

            await Assert.ThrowsAsync<InvalidOperationException>(() => _controller.UpdateSupplier(id, dto));

            _auditLoggerMock.Verify(a => a.LogAsync(
                    It.IsAny<HttpContext>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<object>(),
                    It.IsAny<object>()),
                Times.Never);
        }

        // USup005 - Abnormal (service): LicenseTerms < 10 ký tự
        [Fact]
        public async Task USup005_UpdateSupplier_ThrowsInvalidOperation_WhenLicenseTermsTooShort()
        {
            var id = 10;
            var dto = BuildValidUpdateDto(id);
            dto.LicenseTerms = "TooShort"; // length < 10

            _serviceMock
                .Setup(s => s.UpdateSupplierAsync(
                    It.IsAny<UpdateSupplierDto>(),
                    It.IsAny<Guid>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("Điều khoản giấy phép phải có ít nhất 10 ký tự"));

            await Assert.ThrowsAsync<InvalidOperationException>(() => _controller.UpdateSupplier(id, dto));

            _auditLoggerMock.Verify(a => a.LogAsync(
                    It.IsAny<HttpContext>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<object>(),
                    It.IsAny<object>()),
                Times.Never);
        }

        // ===== DTO validation (input conditions) =====

        // USup006 - Abnormal: thiếu tên
        [Fact]
        public void USup006_UpdateSupplierDto_FailsValidation_WhenNameMissing()
        {
            var dto = BuildValidUpdateDto(1);
            dto.Name = string.Empty;

            var isValid = ValidateDto(dto, out var results);

            Assert.False(isValid);
            Assert.Contains(results, r => r.MemberNames.Contains(nameof(UpdateSupplierDto.Name)));
        }

        // USup007 - Abnormal: email sai
        [Fact]
        public void USup007_UpdateSupplierDto_FailsValidation_WhenEmailInvalid()
        {
            var dto = BuildValidUpdateDto(1);
            dto.ContactEmail = "not-an-email";

            var isValid = ValidateDto(dto, out var results);

            Assert.False(isValid);
            Assert.Contains(results, r => r.MemberNames.Contains(nameof(UpdateSupplierDto.ContactEmail)));
        }

        // USup008 - Abnormal: phone quá dài
        [Fact]
        public void USup008_UpdateSupplierDto_FailsValidation_WhenPhoneTooLong()
        {
            var dto = BuildValidUpdateDto(1);
            dto.ContactPhone = new string('1', 33); // > 32

            var isValid = ValidateDto(dto, out var results);

            Assert.False(isValid);
            Assert.Contains(results, r => r.MemberNames.Contains(nameof(UpdateSupplierDto.ContactPhone)));
        }

        // USup009 - Abnormal: LicenseTerms > 500
        [Fact]
        public void USup009_UpdateSupplierDto_FailsValidation_WhenLicenseTermsTooLong()
        {
            var dto = BuildValidUpdateDto(1);
            dto.LicenseTerms = new string('L', 501); // > 500

            var isValid = ValidateDto(dto, out var results);

            Assert.False(isValid);
            Assert.Contains(results, r => r.MemberNames.Contains(nameof(UpdateSupplierDto.LicenseTerms)));
        }

        // USup010 - Boundary: giá trị ở biên trên vẫn hợp lệ
        [Fact]
        public void USup010_UpdateSupplierDto_PassesValidation_WhenAtBoundaryValues()
        {
            var dto = BuildValidUpdateDto(1);
            dto.Name = new string('N', 100);                     // max 100
            dto.ContactEmail = new string('e', 244) + "@x.com";  // tổng 254
            dto.ContactPhone = new string('1', 32);              // max 32
            dto.LicenseTerms = new string('L', 500);             // max 500
            dto.Notes = new string('n', 1000);                   // max 1000

            var isValid = ValidateDto(dto, out var results);

            Assert.True(isValid);
            Assert.Empty(results);
        }
    }
}
