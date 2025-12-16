using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Keytietkiem.Controllers;
using Keytietkiem.DTOs;
using Keytietkiem.Infrastructure;
using Keytietkiem.Models;
using Keytietkiem.Options;
using Keytietkiem.Repositories;
using Keytietkiem.Services;
using Keytietkiem.Services.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Keytietkiem.UnitTests.Controllers
{
    /// <summary>
    /// Test matrix ChangePassword map 1-1 với sheet CHGPW01..CHGPW08.
    /// </summary>
    public class AccountServiceChangePasswordTests : IDisposable
    {
        private readonly KeytietkiemDbContext _context;
        private readonly GenericRepository<Account> _accountRepo;
        private readonly GenericRepository<User> _userRepo;
        private readonly IMemoryCache _cache;
        private readonly Mock<IClock> _clockMock;
        private readonly Mock<IEmailService> _emailServiceMock;
        private readonly IOptions<JwtConfig> _jwtOptions;
        private readonly IOptions<ClientConfig> _clientOptions;
        private readonly DateTime _nowUtc;

        public AccountServiceChangePasswordTests()
        {
            var dbOptions = new DbContextOptionsBuilder<KeytietkiemDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            _context = new KeytietkiemDbContext(dbOptions);
            _accountRepo = new GenericRepository<Account>(_context);
            _userRepo = new GenericRepository<User>(_context);
            _cache = new MemoryCache(new MemoryCacheOptions());

            // Giờ giả cho IClock
            _nowUtc = new DateTime(2025, 1, 1, 12, 0, 0, DateTimeKind.Utc);
            _clockMock = new Mock<IClock>();
            _clockMock.SetupGet(c => c.UtcNow).Returns(() => _nowUtc);

            // Jwt + Client options (giống file option thật)
            _jwtOptions = Microsoft.Extensions.Options.Options.Create(new JwtConfig
            {
                SecretKey = "THIS_IS_TEST_SECRET_KEY_1234567890",
                Issuer = "Keytietkiem.Tests",
                Audience = "keytietkiem.Client",
                ExpiryInMinutes = 15,
                RefreshTokenExpiryInDays = 7
            });

            _clientOptions = Microsoft.Extensions.Options.Options.Create(new ClientConfig
            {
                ClientUrl = "https://test-client.keytietkiem.com",
                ResetLinkExpiryInMinutes = 30
            });

            _emailServiceMock = new Mock<IEmailService>();
        }

        private AccountService CreateService(IGenericRepository<Account>? customAccountRepo = null)
        {
            var accRepo = customAccountRepo ?? _accountRepo;
            return new AccountService(
                _context,
                accRepo,
                _userRepo,
                _clockMock.Object,
                _jwtOptions,
                _clientOptions,
                _cache,
                _emailServiceMock.Object);
        }

        /// <summary>
        /// Tạo sẵn 1 account + user + role Customer trong InMemory DB.
        /// </summary>
        private async Task<Account> SeedAccountAsync(
            string username,
            string passwordPlain,
            string userStatus = "Active")
        {
            var role = new Role
            {
                RoleId = "Customer",
                Name = "Customer",
                IsSystem = false,
                IsActive = true,
                CreatedAt = _nowUtc
            };

            var user = new User
            {
                UserId = Guid.NewGuid(),
                Email = $"{username}@test.com",
                Status = userStatus,
                EmailVerified = true,
                CreatedAt = _nowUtc,
                SupportPriorityLevel = 0,
                TotalProductSpend = 0m,
                IsTemp = false
            };

            user.Roles.Add(role);

            var account = new Account
            {
                AccountId = Guid.NewGuid(),
                Username = username,
                PasswordHash = HashPasswordForTest(passwordPlain),
                UserId = user.UserId,
                FailedLoginCount = 0,
                LockedUntil = null,
                CreatedAt = _nowUtc
            };

            _context.Roles.Add(role);
            _context.Users.Add(user);
            _context.Accounts.Add(account);
            await _context.SaveChangesAsync();

            return account;
        }

        /// <summary>
        /// Hash password giống hệt AccountService.HashPassword.
        /// </summary>
        private static byte[] HashPasswordForTest(string password)
        {
            const int iterations = 100000;
            const int keySize = 32;

            using var rng = RandomNumberGenerator.Create();
            var salt = new byte[16];
            rng.GetBytes(salt);

            using var pbkdf2 = new Rfc2898DeriveBytes(password, salt, iterations, HashAlgorithmName.SHA256);
            var hash = pbkdf2.GetBytes(keySize);

            var result = new byte[salt.Length + hash.Length];
            Buffer.BlockCopy(salt, 0, result, 0, salt.Length);
            Buffer.BlockCopy(hash, 0, result, salt.Length, hash.Length);

            return result;
        }

        private static IList<ValidationResult> ValidateModel(object model)
        {
            var ctx = new ValidationContext(model);
            var results = new List<ValidationResult>();
            Validator.TryValidateObject(model, ctx, results, validateAllProperties: true);
            return results;
        }

        // ================= DTO validation (400 BadRequest trong sheet) =================

        // CHGPW02 - Thiếu CurrentPassword
        [Fact]
        public void ChangePassword02_CurrentPasswordMissing_FailsValidation()
        {
            var dto = new ChangePasswordDto
            {
                CurrentPassword = "",
                NewPassword = "NewPass123!"
            };

            var results = ValidateModel(dto);

            Assert.Contains(results, r => r.MemberNames.Contains(nameof(ChangePasswordDto.CurrentPassword)));
        }

        // CHGPW03 - Thiếu / rỗng NewPassword
        [Fact]
        public void ChangePassword03_NewPasswordMissing_FailsValidation()
        {
            var dto = new ChangePasswordDto
            {
                CurrentPassword = "OldPass123!",
                NewPassword = ""
            };

            var results = ValidateModel(dto);

            Assert.Contains(results, r => r.MemberNames.Contains(nameof(ChangePasswordDto.NewPassword)));
        }

        // CHGPW04 - NewPassword quá ngắn (< 6 ký tự)
        [Fact]
        public void ChangePassword04_NewPasswordTooShort_FailsValidation()
        {
            var dto = new ChangePasswordDto
            {
                CurrentPassword = "OldPass123!",
                NewPassword = "123" // dưới min 6
            };

            var results = ValidateModel(dto);

            Assert.Contains(results, r => r.MemberNames.Contains(nameof(ChangePasswordDto.NewPassword)));
        }

        // ================= Service tests (ChangePasswordAsync) =================

        // CHGPW01 - Happy path: account tồn tại, current đúng, lưu OK
        [Fact]
        public async Task ChangePassword01_ValidRequest_UpdatesPasswordAndUpdatedAt()
        {
            var service = CreateService();
            var username = "changepw_user_ok";
            var currentPassword = "OldPass123!";
            var newPassword = "NewPass123!";

            var account = await SeedAccountAsync(username, currentPassword);
            var oldHash = (byte[])account.PasswordHash.Clone();

            var dto = new ChangePasswordDto
            {
                CurrentPassword = currentPassword,
                NewPassword = newPassword
            };

            await service.ChangePasswordAsync(account.AccountId, dto);

            var updated = _context.Accounts.Single(a => a.AccountId == account.AccountId);
            Assert.NotNull(updated.PasswordHash);
            Assert.NotEqual(oldHash, updated.PasswordHash); // password đã đổi
            Assert.Equal(_nowUtc, updated.UpdatedAt);       // UpdatedAt = _clock.UtcNow
        }

        // CHGPW05 - Account không tồn tại
        [Fact]
        public async Task ChangePassword05_AccountNotFound_ThrowsInvalidOperation()
        {
            var service = CreateService();

            var dto = new ChangePasswordDto
            {
                CurrentPassword = "anything",
                NewPassword = "NewPass123!"
            };

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.ChangePasswordAsync(Guid.NewGuid(), dto));

            Assert.Contains("Tài khoản không tồn tại", ex.Message);
        }

        // CHGPW06 - CurrentPassword sai
        [Fact]
        public async Task ChangePassword06_CurrentPasswordWrong_ThrowsUnauthorized()
        {
            var service = CreateService();
            var username = "changepw_user_wrong";
            var correctPassword = "Correct123!";
            var wrongPassword = "Wrong123!";
            var newPassword = "NewPass123!";

            var account = await SeedAccountAsync(username, correctPassword);

            var dto = new ChangePasswordDto
            {
                CurrentPassword = wrongPassword,
                NewPassword = newPassword
            };

            var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(
                () => service.ChangePasswordAsync(account.AccountId, dto));

            Assert.Contains("Mật khẩu hiện tại không chính xác", ex.Message);
        }

        // CHGPW07 - Lỗi bất ngờ khi repository.Update / SaveChanges
        private class FailingAccountRepository : GenericRepository<Account>
        {
            public FailingAccountRepository(KeytietkiemDbContext context) : base(context) { }

            public override void Update(Account entity)
            {
                throw new DbUpdateException("Simulated DB failure", innerException: null);
            }
        }

        [Fact]
        public async Task ChangePassword07_RepositoryUpdateThrows_PropagatesException()
        {
            var username = "changepw_db_error";
            var currentPassword = "OldPass123!";
            var newPassword = "NewPass123!";

            var account = await SeedAccountAsync(username, currentPassword);

            var failingRepo = new FailingAccountRepository(_context);
            var service = CreateService(failingRepo);

            var dto = new ChangePasswordDto
            {
                CurrentPassword = currentPassword,
                NewPassword = newPassword
            };

            await Assert.ThrowsAsync<DbUpdateException>(
                () => service.ChangePasswordAsync(account.AccountId, dto));
        }

        // ================= Controller test (HTTP 200 + log) =================

        // CHGPW08 - Controller trả 200 OK + "Đổi mật khẩu thành công" khi service OK
        [Fact]
        public async Task ChangePassword08_Controller_Success_ReturnsOk()
        {
            var accountId = Guid.NewGuid();

            var accountServiceMock = new Mock<IAccountService>();
            accountServiceMock
                .Setup(s => s.ChangePasswordAsync(accountId, It.IsAny<ChangePasswordDto>(),
                    It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            var auditLoggerMock = new Mock<IAuditLogger>();
            auditLoggerMock
                .Setup(a => a.LogAsync(
                    It.IsAny<HttpContext>(),
                    "ChangePassword",
                    "Account",
                    accountId.ToString(),
                    null,
                    null))
                .Returns(Task.CompletedTask);

            var controller = new AccountController(accountServiceMock.Object, auditLoggerMock.Object);

            // giả JWT có claim AccountId
            var user = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
        new Claim("AccountId", accountId.ToString())
    }, "TestAuth"));

            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = user }
            };

            var dto = new ChangePasswordDto
            {
                CurrentPassword = "OldPass123!",
                NewPassword = "NewPass123!"
            };

            var result = await controller.ChangePassword(dto);

            var okResult = Assert.IsType<OkObjectResult>(result);
            Assert.NotNull(okResult.Value);

            // Lấy message theo cả 2 kiểu: string hoặc object có property "message"
            string? message;
            if (okResult.Value is string s)
            {
                message = s;
            }
            else
            {
                var prop = okResult.Value.GetType().GetProperty("message");
                Assert.NotNull(prop); // chắc chắn phải có nếu dùng kiểu { message = "..." }
                message = prop.GetValue(okResult.Value) as string;
            }

            Assert.Equal("Đổi mật khẩu thành công", message);

            // Service & audit được gọi đúng 1 lần
            accountServiceMock.Verify(
                s => s.ChangePasswordAsync(accountId, It.IsAny<ChangePasswordDto>(), It.IsAny<CancellationToken>()),
                Times.Once);

            auditLoggerMock.Verify(
                a => a.LogAsync(
                    It.IsAny<HttpContext>(),
                    "ChangePassword",
                    "Account",
                    accountId.ToString(),
                    null,
                    null),
                Times.Once);
        }


        public void Dispose()
        {
            _context.Dispose();
            _cache.Dispose();
        }
    }
}
