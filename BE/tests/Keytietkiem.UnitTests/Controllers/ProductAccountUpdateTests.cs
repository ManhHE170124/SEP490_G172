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
    /// Unit tests cho chức năng Update Product Account
    /// Mapping với decision table UA001 - UA008.
    /// </summary>
    public class ProductAccountUpdateTests
    {
        private readonly Mock<IProductAccountService> _serviceMock;
        private readonly Mock<IAuditLogger> _auditLoggerMock;
        private readonly ProductAccountController _controller;

        public ProductAccountUpdateTests()
        {
            _serviceMock = new Mock<IProductAccountService>();
            _auditLoggerMock = new Mock<IAuditLogger>();

            _controller = new ProductAccountController(
                _serviceMock.Object,
                _auditLoggerMock.Object);

            // Giả lập user login (có AccountId và NameIdentifier)
            var httpContext = new DefaultHttpContext();
            httpContext.User = new ClaimsPrincipal(
                new ClaimsIdentity(new[]
                {
                    new Claim("AccountId", Guid.NewGuid().ToString()),
                    new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString())
                }, "TestAuth"));

            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = httpContext
            };
        }

        #region Helpers

        private static UpdateProductAccountDto BuildValidUpdateDto(Guid? idOverride = null)
        {
            return new UpdateProductAccountDto
            {
                ProductAccountId = idOverride ?? Guid.NewGuid(),
                AccountEmail = "updated@example.com",
                AccountUsername = "updateduser",
                AccountPassword = "NewStrongPassword#123",
                MaxUsers = 10,
                Status = "Active",
                ExpiryDate = DateTime.UtcNow.Date.AddMonths(1),
                Notes = "Updated notes"
            };
        }

        private static bool ValidateDto(UpdateProductAccountDto dto, out List<ValidationResult> results)
        {
            var ctx = new ValidationContext(dto);
            results = new List<ValidationResult>();
            return Validator.TryValidateObject(dto, ctx, results, validateAllProperties: true);
        }

        #endregion

        // UA001 - Normal: update thành công với dữ liệu hợp lệ
        [Fact]
        public async Task UA001_UpdateProductAccount_Succeeds_WithValidData()
        {
            // Arrange
            var id = Guid.NewGuid();
            var updateDto = BuildValidUpdateDto(id);

            var existingAccount = new ProductAccountResponseDto
            {
                ProductAccountId = id,
                ProductId = Guid.NewGuid(),
                VariantId = Guid.NewGuid(),
                ProductName = "Old name",
                VariantTitle = "Old variant",
                AccountEmail = "old@example.com",
                AccountUsername = "olduser",
                AccountPassword = "oldPassword",
                MaxUsers = 5,
                CurrentUsers = 1,
                Status = "Active",
                CogsPrice = 10,
                SellPrice = 100,
                CreatedAt = DateTime.UtcNow.AddMonths(-1),
                CreatedBy = Guid.NewGuid(),
                UpdatedAt = DateTime.UtcNow.AddDays(-1),
                UpdatedBy = Guid.NewGuid(),
                Customers = new List<ProductAccountCustomerDto>()
            };

            var updatedResponse = new ProductAccountResponseDto
            {
                ProductAccountId = id,
                ProductId = existingAccount.ProductId,
                VariantId = existingAccount.VariantId,
                ProductName = "New name",
                VariantTitle = "New variant",
                AccountEmail = updateDto.AccountEmail!,
                AccountUsername = updateDto.AccountUsername,
                AccountPassword = updateDto.AccountPassword ?? existingAccount.AccountPassword,
                MaxUsers = updateDto.MaxUsers ?? existingAccount.MaxUsers,
                CurrentUsers = existingAccount.CurrentUsers,
                Status = updateDto.Status ?? existingAccount.Status,
                CogsPrice = existingAccount.CogsPrice,
                SellPrice = existingAccount.SellPrice,
                CreatedAt = existingAccount.CreatedAt,
                CreatedBy = existingAccount.CreatedBy,
                UpdatedAt = DateTime.UtcNow,
                UpdatedBy = Guid.NewGuid(),
                Customers = existingAccount.Customers
            };

            _serviceMock
                .Setup(s => s.GetByIdAsync(
                    id,
                    false,
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(existingAccount);

            _serviceMock
                .Setup(s => s.CheckAccountEmailOrUsernameExists(
                    existingAccount.VariantId,
                    updateDto.AccountEmail,
                    updateDto.AccountUsername,
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync((id, false));

            _serviceMock
                .Setup(s => s.UpdateAsync(
                    updateDto,
                    It.IsAny<Guid>(),
                    It.IsAny<Guid>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(updatedResponse);

            // Act
            var result = await _controller.Update(id, updateDto);

            // Assert
            var ok = Assert.IsType<OkObjectResult>(result);
            var response = Assert.IsType<ProductAccountResponseDto>(ok.Value);

            Assert.Equal(updatedResponse.ProductAccountId, response.ProductAccountId);
            Assert.Equal(updatedResponse.AccountEmail, response.AccountEmail);
            Assert.Equal(updatedResponse.AccountUsername, response.AccountUsername);

            _serviceMock.Verify(s => s.GetByIdAsync(
                    id,
                    false,
                    It.IsAny<CancellationToken>()),
                Times.Once);

            _serviceMock.Verify(s => s.UpdateAsync(
                    updateDto,
                    It.IsAny<Guid>(),
                    It.IsAny<Guid>(),
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }

        // UA002 - Abnormal: id trên route và trong body không khớp
        [Fact]
        public async Task UA002_UpdateProductAccount_ReturnsBadRequest_WhenIdMismatch()
        {
            // Arrange
            var routeId = Guid.NewGuid();
            var bodyId = Guid.NewGuid(); // khác routeId
            var updateDto = BuildValidUpdateDto(bodyId);

            // Act
            var result = await _controller.Update(routeId, updateDto);

            // Assert
            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Equal("ID không khớp", badRequest.Value);

            _serviceMock.Verify(s => s.GetByIdAsync(
                    It.IsAny<Guid>(),
                    It.IsAny<bool>(),
                    It.IsAny<CancellationToken>()),
                Times.Never);

            _serviceMock.Verify(s => s.UpdateAsync(
                    It.IsAny<UpdateProductAccountDto>(),
                    It.IsAny<Guid>(),
                    It.IsAny<Guid>(),
                    It.IsAny<CancellationToken>()),
                Times.Never);
        }

        // UA003 - Abnormal: trùng email/username với account khác
        [Fact]
        public async Task UA003_UpdateProductAccount_ReturnsBadRequest_WhenDuplicateEmailOrUsername()
        {
            // Arrange
            var id = Guid.NewGuid();
            var updateDto = BuildValidUpdateDto(id);

            var existingAccount = new ProductAccountResponseDto
            {
                ProductAccountId = id,
                ProductId = Guid.NewGuid(),
                VariantId = Guid.NewGuid(),
                ProductName = "Old name",
                VariantTitle = "Old variant",
                AccountEmail = "old@example.com",
                AccountUsername = "olduser",
                AccountPassword = "oldPassword",
                MaxUsers = 5,
                CurrentUsers = 1,
                Status = "Active",
                CogsPrice = 10,
                SellPrice = 100,
                CreatedAt = DateTime.UtcNow.AddMonths(-1),
                CreatedBy = Guid.NewGuid(),
                UpdatedAt = DateTime.UtcNow.AddDays(-1),
                UpdatedBy = Guid.NewGuid(),
                Customers = new List<ProductAccountCustomerDto>()
            };

            _serviceMock
                .Setup(s => s.GetByIdAsync(
                    id,
                    false,
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(existingAccount);

            // Giả lập trùng với account khác (Item1 != id, Item2 = true)
            _serviceMock
                .Setup(s => s.CheckAccountEmailOrUsernameExists(
                    existingAccount.VariantId,
                    updateDto.AccountEmail,
                    updateDto.AccountUsername,
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync((Guid.NewGuid(), true));

            // Act
            var result = await _controller.Update(id, updateDto);

            // Assert
            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            var value = badRequest.Value;

            Assert.NotNull(value);
            var messageProp = value!.GetType().GetProperty("message");
            Assert.NotNull(messageProp);
            var message = (string?)messageProp!.GetValue(value);
            Assert.Equal("Tên đăng nhập hoặc email đã tồn tại", message);

            _serviceMock.Verify(s => s.UpdateAsync(
                    It.IsAny<UpdateProductAccountDto>(),
                    It.IsAny<Guid>(),
                    It.IsAny<Guid>(),
                    It.IsAny<CancellationToken>()),
                Times.Never);
        }

        // UA004 - Abnormal: service throw ValidationException (capacity, business rule, no changes,...)
        [Fact]
        public async Task UA004_UpdateProductAccount_ReturnsBadRequest_WhenValidationExceptionThrown()
        {
            // Arrange
            var id = Guid.NewGuid();
            var updateDto = BuildValidUpdateDto(id);

            var existingAccount = new ProductAccountResponseDto
            {
                ProductAccountId = id,
                ProductId = Guid.NewGuid(),
                VariantId = Guid.NewGuid(),
                ProductName = "Old name",
                VariantTitle = "Old variant",
                AccountEmail = "old@example.com",
                AccountUsername = "olduser",
                AccountPassword = "oldPassword",
                MaxUsers = 5,
                CurrentUsers = 1,
                Status = "Active",
                CogsPrice = 10,
                SellPrice = 100,
                CreatedAt = DateTime.UtcNow.AddMonths(-1),
                CreatedBy = Guid.NewGuid(),
                UpdatedAt = DateTime.UtcNow.AddDays(-1),
                UpdatedBy = Guid.NewGuid(),
                Customers = new List<ProductAccountCustomerDto>()
            };

            _serviceMock
                .Setup(s => s.GetByIdAsync(
                    id,
                    false,
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(existingAccount);

            _serviceMock
                .Setup(s => s.CheckAccountEmailOrUsernameExists(
                    existingAccount.VariantId,
                    updateDto.AccountEmail,
                    updateDto.AccountUsername,
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync((id, false));

            _serviceMock
                .Setup(s => s.UpdateAsync(
                    updateDto,
                    It.IsAny<Guid>(),
                    It.IsAny<Guid>(),
                    It.IsAny<CancellationToken>()))
                .ThrowsAsync(new ValidationException("Business rule violated"));

            // Act
            var result = await _controller.Update(id, updateDto);

            // Assert
            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            var value = badRequest.Value;

            Assert.NotNull(value);
            var messageProp = value!.GetType().GetProperty("message");
            Assert.NotNull(messageProp);
            var message = (string?)messageProp!.GetValue(value);
            Assert.Equal("Business rule violated", message);
        }

        // ===== DTO validation tests =====

        // UA005 - Abnormal: email sai format
        [Fact]
        public void UA005_UpdateProductAccount_FailsValidation_WhenEmailInvalid()
        {
            var dto = BuildValidUpdateDto(Guid.NewGuid());
            dto.AccountEmail = "not-an-email";

            var isValid = ValidateDto(dto, out var results);

            Assert.False(isValid);
            Assert.Contains(results, r => r.MemberNames.Contains(nameof(UpdateProductAccountDto.AccountEmail)));
        }

        // UA006 - Abnormal: MaxUsers ngoài khoảng [1,100] (capacity quá nhỏ)
        [Fact]
        public void UA006_UpdateProductAccount_FailsValidation_WhenMaxUsersOutOfRange()
        {
            var dto = BuildValidUpdateDto(Guid.NewGuid());
            dto.MaxUsers = 0;

            var isValid = ValidateDto(dto, out var results);

            Assert.False(isValid);
            Assert.Contains(results, r => r.MemberNames.Contains(nameof(UpdateProductAccountDto.MaxUsers)));
        }

        // UA007 - Abnormal: Username dài hơn 100 ký tự
        [Fact]
        public void UA007_UpdateProductAccount_FailsValidation_WhenUsernameTooLong()
        {
            var dto = BuildValidUpdateDto(Guid.NewGuid());
            dto.AccountUsername = new string('x', 101);

            var isValid = ValidateDto(dto, out var results);

            Assert.False(isValid);
            Assert.Contains(results, r => r.MemberNames.Contains(nameof(UpdateProductAccountDto.AccountUsername)));
        }

        // UA008 - Boundary: giá trị biên vẫn hợp lệ (MaxUsers = 100, Username 100 ký tự, Notes 1000 ký tự)
        [Fact]
        public void UA008_UpdateProductAccount_PassesValidation_WhenBoundaryValuesAreUsed()
        {
            var dto = BuildValidUpdateDto(Guid.NewGuid());
            dto.MaxUsers = 100;
            dto.AccountUsername = new string('x', 100);
            dto.Notes = new string('n', 1000);

            var isValid = ValidateDto(dto, out var results);

            Assert.True(isValid);
            Assert.Empty(results);
        }
    }
}
