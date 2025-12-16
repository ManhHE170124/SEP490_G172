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
using System.Threading;                 // <== THÊM DÒNG NÀY
using System.Threading.Tasks;
using Xunit;

namespace Keytietkiem.UnitTests.Controllers
{
    /// <summary>
    /// Unit tests cho chức năng Create Product Account
    /// Mapping với decision table CPA001 - CPA008.
    /// </summary>
    public class ProductAccountCreateTests
    {
        private readonly Mock<IProductAccountService> _serviceMock;
        private readonly Mock<IAuditLogger> _auditLoggerMock;
        private readonly ProductAccountController _controller;

        public ProductAccountCreateTests()
        {
            _serviceMock = new Mock<IProductAccountService>();
            _auditLoggerMock = new Mock<IAuditLogger>();

            _controller = new ProductAccountController(
                _serviceMock.Object,
                _auditLoggerMock.Object);

            // Giả lập user login (có AccountId)
            var httpContext = new DefaultHttpContext();
            httpContext.User = new ClaimsPrincipal(
                new ClaimsIdentity(new[]
                {
                    new Claim("AccountId", Guid.NewGuid().ToString())
                }, "TestAuth"));

            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = httpContext
            };
        }

        #region Helper

        private static CreateProductAccountDto BuildValidCreateDto()
        {
            return new CreateProductAccountDto
            {
                VariantId = Guid.NewGuid(),
                AccountEmail = "valid@example.com",
                AccountUsername = "validuser",
                AccountPassword = "StrongPassword#123",
                MaxUsers = 5,
                CogsPrice = 10,
                StartDate = DateTime.UtcNow.Date,
                Notes = "Test account"
            };
        }

        private static bool ValidateDto(
            CreateProductAccountDto dto,
            out List<ValidationResult> validationResults)
        {
            var ctx = new ValidationContext(dto);
            validationResults = new List<ValidationResult>();
            return Validator.TryValidateObject(dto, ctx, validationResults, validateAllProperties: true);
        }

        #endregion

        // CPA001 - Normal: data hợp lệ -> tạo thành công
        [Fact]
        public async Task CPA001_CreateProductAccount_Succeeds_WithValidData()
        {
            // Arrange
            var createDto = BuildValidCreateDto();

            var createdResponse = new ProductAccountResponseDto
            {
                ProductAccountId = Guid.NewGuid(),
                ProductId = Guid.NewGuid(),
                VariantId = createDto.VariantId,
                ProductName = "Test Product",
                VariantTitle = "1 Month",
                AccountEmail = createDto.AccountEmail,
                AccountUsername = createDto.AccountUsername,
                AccountPassword = createDto.AccountPassword,
                MaxUsers = createDto.MaxUsers,
                Status = "Active",
                CogsPrice = createDto.CogsPrice ?? 0,
                SellPrice = 100,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = Guid.NewGuid(),
                UpdatedAt = DateTime.UtcNow,
                UpdatedBy = null
            };

            // Không có trùng email/username
            _serviceMock
                .Setup(s => s.CheckAccountEmailOrUsernameExists(
                    createDto.VariantId,
                    createDto.AccountEmail,
                    createDto.AccountUsername,
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(((Guid?)null, false));

            _serviceMock
                .Setup(s => s.CreateAsync(
                    It.IsAny<CreateProductAccountDto>(),
                    It.IsAny<Guid>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(createdResponse);

            // Act
            var result = await _controller.Create(createDto);

            // Assert
            var createdAt = Assert.IsType<CreatedAtActionResult>(result);
            Assert.Equal(nameof(ProductAccountController.GetById), createdAt.ActionName);

            var response = Assert.IsType<ProductAccountResponseDto>(createdAt.Value);
            Assert.Equal(createDto.AccountEmail, response.AccountEmail);
            Assert.Equal(createDto.VariantId, response.VariantId);

            _serviceMock.Verify(s => s.CheckAccountEmailOrUsernameExists(
                    createDto.VariantId,
                    createDto.AccountEmail,
                    createDto.AccountUsername,
                    It.IsAny<CancellationToken>()),
                Times.Once);

            _serviceMock.Verify(s => s.CreateAsync(
                    It.IsAny<CreateProductAccountDto>(),
                    It.IsAny<Guid>(),
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }

        // CPA002 - Abnormal: email/username đã tồn tại -> BadRequest, không gọi CreateAsync
        [Fact]
        public async Task CPA002_CreateProductAccount_ReturnsBadRequest_WhenDuplicateEmailOrUsername()
        {
            // Arrange
            var createDto = BuildValidCreateDto();
            var existingId = Guid.NewGuid();

            _serviceMock
                .Setup(s => s.CheckAccountEmailOrUsernameExists(
                    createDto.VariantId,
                    createDto.AccountEmail,
                    createDto.AccountUsername,
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync((existingId, true));

            // Act
            var result = await _controller.Create(createDto);

            // Assert
            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            Assert.NotNull(badRequest.Value);

            var messageProp = badRequest.Value!.GetType().GetProperty("message");
            Assert.NotNull(messageProp);
            var message = (string?)messageProp!.GetValue(badRequest.Value);
            Assert.Equal("Tên đăng nhập hoặc email đã tồn tại", message);

            _serviceMock.Verify(s => s.CreateAsync(
                    It.IsAny<CreateProductAccountDto>(),
                    It.IsAny<Guid>(),
                    It.IsAny<CancellationToken>()),
                Times.Never);
        }

        // CPA003 - Abnormal: thiếu các field bắt buộc
        // CPA003 - Abnormal: thiếu các field bắt buộc
        [Fact]
        public void CPA003_CreateProductAccount_FailsValidation_WhenRequiredFieldsMissing()
        {
            // Arrange: thiếu Email, Password, MaxUsers < 1
            // VariantId vẫn là Guid.Empty nhưng [Required] với value-type không bắt lỗi
            var dto = new CreateProductAccountDto
            {
                VariantId = Guid.Empty,
                MaxUsers = 0,
                StartDate = DateTime.UtcNow
            };

            // Act
            var isValid = ValidateDto(dto, out var results);

            // Assert
            Assert.False(isValid);

            // 3 lỗi chắc chắn phải có
            Assert.Contains(results, r => r.MemberNames.Contains(nameof(CreateProductAccountDto.AccountEmail)));
            Assert.Contains(results, r => r.MemberNames.Contains(nameof(CreateProductAccountDto.AccountPassword)));
            Assert.Contains(results, r => r.MemberNames.Contains(nameof(CreateProductAccountDto.MaxUsers)));
        }


        // CPA004 - Abnormal: email sai format
        [Fact]
        public void CPA004_CreateProductAccount_FailsValidation_WhenEmailInvalid()
        {
            var dto = BuildValidCreateDto();
            dto.AccountEmail = "not-an-email";

            var isValid = ValidateDto(dto, out var results);

            Assert.False(isValid);
            Assert.Contains(results, r => r.MemberNames.Contains(nameof(CreateProductAccountDto.AccountEmail)));
        }

        // CPA005 - Abnormal: MaxUsers ngoài khoảng [1,100]
        [Fact]
        public void CPA005_CreateProductAccount_FailsValidation_WhenMaxUsersOutOfRange()
        {
            var dto = BuildValidCreateDto();
            dto.MaxUsers = 0;   // < 1

            var isValid = ValidateDto(dto, out var results);

            Assert.False(isValid);
            Assert.Contains(results, r => r.MemberNames.Contains(nameof(CreateProductAccountDto.MaxUsers)));
        }

        // CPA006 - Abnormal: Username > 100 ký tự
        [Fact]
        public void CPA006_CreateProductAccount_FailsValidation_WhenUsernameTooLong()
        {
            var dto = BuildValidCreateDto();
            dto.AccountUsername = new string('x', 101);

            var isValid = ValidateDto(dto, out var results);

            Assert.False(isValid);
            Assert.Contains(results, r => r.MemberNames.Contains(nameof(CreateProductAccountDto.AccountUsername)));
        }

        // CPA007 - Boundary: Username = 100 ký tự (giới hạn trên) -> hợp lệ
        [Fact]
        public void CPA007_CreateProductAccount_PassesValidation_WhenUsernameAtMaxLengthBoundary()
        {
            var dto = BuildValidCreateDto();
            dto.AccountUsername = new string('x', 100);

            var isValid = ValidateDto(dto, out var results);

            Assert.True(isValid);
            Assert.DoesNotContain(results, r => r.MemberNames.Contains(nameof(CreateProductAccountDto.AccountUsername)));
        }

        // CPA008 - Boundary: MaxUsers = 100, CogsPrice = 0 -> hợp lệ
        [Fact]
        public void CPA008_CreateProductAccount_PassesValidation_WhenAtMaxBoundaryValues()
        {
            var dto = BuildValidCreateDto();
            dto.MaxUsers = 100;
            dto.CogsPrice = 0;

            var isValid = ValidateDto(dto, out var results);

            Assert.True(isValid);
            Assert.DoesNotContain(results, r => r.MemberNames.Contains(nameof(CreateProductAccountDto.MaxUsers)));
            Assert.DoesNotContain(results, r => r.MemberNames.Contains(nameof(CreateProductAccountDto.CogsPrice)));
        }
    }
}
