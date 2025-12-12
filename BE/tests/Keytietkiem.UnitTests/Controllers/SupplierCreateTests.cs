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
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Keytietkiem.UnitTests.Controllers
{
    /// <summary>
    /// Unit tests cho chức năng Create Supplier
    /// Mapping với decision table Create Supplier (CSup001–CSup006).
    /// </summary>
    public class SupplierCreateTests
    {
        private readonly Mock<ISupplierService> _serviceMock;
        private readonly Mock<IAuditLogger> _auditLoggerMock;
        private readonly SupplierController _controller;

        public SupplierCreateTests()
        {
            _serviceMock = new Mock<ISupplierService>();
            _auditLoggerMock = new Mock<IAuditLogger>();

            _controller = new SupplierController(
                _serviceMock.Object,
                _auditLoggerMock.Object);

            // Giả lập user đã đăng nhập, account active, có quyền
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

        private static CreateSupplierDto BuildValidCreateDto()
        {
            return new CreateSupplierDto
            {
                Name = "New Supplier",
                ContactEmail = "supplier@example.com",
                ContactPhone = "+84901234567",
                LicenseTerms = "Valid license terms content",
                Notes = "Some notes"
            };
        }

        private static bool ValidateDto(CreateSupplierDto dto, out List<ValidationResult> results)
        {
            var ctx = new ValidationContext(dto);
            results = new List<ValidationResult>();
            return Validator.TryValidateObject(dto, ctx, results, validateAllProperties: true);
        }

        #endregion

        // CSup001 - Normal:
        // Logged in + Active account + Has permissions
        // Valid required fields + email format + unique name
        // Click Save → Create supplier thành công, trả CreatedAtAction, audit log được ghi.
        [Fact]
        public async Task CSup001_CreateSupplier_Succeeds_WithValidData()
        {
            // Arrange
            var createDto = BuildValidCreateDto();

            var createdSupplier = new SupplierResponseDto
            {
                SupplierId = 10,
                Name = createDto.Name,
                ContactEmail = createDto.ContactEmail,
                ContactPhone = createDto.ContactPhone,
                LicenseTerms = createDto.LicenseTerms,
                Notes = createDto.Notes,
                CreatedAt = DateTime.UtcNow,
                Status = "Active",
                ActiveProductCount = 0,
                TotalProductKeyCount = 0
            };

            _serviceMock
                .Setup(s => s.CreateSupplierAsync(
                    It.IsAny<CreateSupplierDto>(),
                    It.IsAny<Guid>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(createdSupplier);

            // Act
            var result = await _controller.CreateSupplier(createDto);

            // Assert
            var createdAt = Assert.IsType<CreatedAtActionResult>(result);
            Assert.Equal(nameof(SupplierController.GetSupplierById), createdAt.ActionName);

            var response = Assert.IsType<SupplierResponseDto>(createdAt.Value);
            Assert.Equal(createdSupplier.SupplierId, response.SupplierId);
            Assert.Equal(createDto.Name, response.Name);

            _serviceMock.Verify(s => s.CreateSupplierAsync(
                    It.Is<CreateSupplierDto>(d => d.Name == createDto.Name),
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

        // CSup002 - Abnormal:
        // Thiếu field bắt buộc (Name) → validate input lỗi.
        [Fact]
        public void CSup002_CreateSupplier_FailsValidation_WhenNameMissing()
        {
            // Arrange: Name trống
            var dto = BuildValidCreateDto();
            dto.Name = string.Empty;

            // Act
            var isValid = ValidateDto(dto, out var results);

            // Assert
            Assert.False(isValid);
            Assert.Contains(results, r => r.MemberNames.Contains(nameof(CreateSupplierDto.Name)));
        }

        // CSup003 - Abnormal:
        // Email format không hợp lệ → validate input lỗi.
        [Fact]
        public void CSup003_CreateSupplier_FailsValidation_WhenEmailInvalid()
        {
            var dto = BuildValidCreateDto();
            dto.ContactEmail = "not-an-email";

            var isValid = ValidateDto(dto, out var results);

            Assert.False(isValid);
            Assert.Contains(results, r => r.MemberNames.Contains(nameof(CreateSupplierDto.ContactEmail)));
        }

        // CSup004 - Abnormal:
        // Số điện thoại dài quá giới hạn → validate input lỗi.
        [Fact]
        public void CSup004_CreateSupplier_FailsValidation_WhenPhoneTooLong()
        {
            var dto = BuildValidCreateDto();
            dto.ContactPhone = new string('1', 33); // > 32

            var isValid = ValidateDto(dto, out var results);

            Assert.False(isValid);
            Assert.Contains(results, r => r.MemberNames.Contains(nameof(CreateSupplierDto.ContactPhone)));
        }

        // CSup005 - Abnormal (business rule):
        // Tên nhà cung cấp không unique / LicenseTerms không hợp lệ, service ném InvalidOperationException
        // → controller bubble exception (middleware chịu trách nhiệm mapping ra HTTP 4xx/5xx).
        [Fact]
        public async Task CSup005_CreateSupplier_ThrowsInvalidOperation_WhenBusinessRuleViolated()
        {
            var dto = BuildValidCreateDto();

            _serviceMock
                .Setup(s => s.CreateSupplierAsync(
                    It.IsAny<CreateSupplierDto>(),
                    It.IsAny<Guid>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("Nhà cung cấp với tên này đã tồn tại"));

            await Assert.ThrowsAsync<InvalidOperationException>(() => _controller.CreateSupplier(dto));

            _auditLoggerMock.Verify(a => a.LogAsync(
                    It.IsAny<HttpContext>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<object>(),
                    It.IsAny<object>()),
                Times.Never);
        }

        // CSup006 - Boundary:
        // Name = 100 ký tự, Notes = 1000 ký tự (max length) + email/phone hợp lệ
        // → DTO vẫn hợp lệ.
        [Fact]
        public void CSup006_CreateSupplier_PassesValidation_WhenAtBoundaryLengths()
        {
            var dto = BuildValidCreateDto();
            dto.Name = new string('N', 100);          // max 100
            dto.Notes = new string('n', 1000);        // max 1000
            dto.ContactEmail = "valid@example.com";   // trong giới hạn
            dto.ContactPhone = "+84901234567";

            var isValid = ValidateDto(dto, out var results);

            Assert.True(isValid);
            Assert.Empty(results);
        }
    }
}
