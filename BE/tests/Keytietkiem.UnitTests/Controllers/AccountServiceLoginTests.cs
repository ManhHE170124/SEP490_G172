using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Keytietkiem.DTOs;
using Keytietkiem.Infrastructure;
using Keytietkiem.Models;
using Keytietkiem.Options;
using Keytietkiem.Repositories;
using Keytietkiem.Services;
using Keytietkiem.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Keytietkiem.UnitTests.Controllers
{
    /// <summary>
    /// Test matrix map 1-1 với sheet LOGIN01..LOGIN10.
    /// </summary>
    public class AccountServiceLoginTests : IDisposable
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

        public AccountServiceLoginTests()
        {
            var dbOptions = new DbContextOptionsBuilder<KeytietkiemDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            _context = new KeytietkiemDbContext(dbOptions);
            _accountRepo = new GenericRepository<Account>(_context);
            _userRepo = new GenericRepository<User>(_context);
            _cache = new MemoryCache(new MemoryCacheOptions());

            _nowUtc = new DateTime(2025, 1, 1, 12, 0, 0, DateTimeKind.Utc);
            _clockMock = new Mock<IClock>();
            _clockMock.SetupGet(c => c.UtcNow).Returns(() => _nowUtc);

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

        private AccountService CreateService()
        {
            return new AccountService(
                _context,
                _accountRepo,
                _userRepo,
                _clockMock.Object,
                _jwtOptions,
                _clientOptions,
                _cache,
                _emailServiceMock.Object);
        }

        /// <summary>
        /// Tạo account + user + role Customer trong InMemory DB.
        /// </summary>
        private async Task<Account> SeedAccountAsync(
            string username,
            string passwordPlain,
            string userStatus = "Active",
            int failedLoginCount = 0,
            DateTime? lockedUntil = null)
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
                FailedLoginCount = failedLoginCount,
                LockedUntil = lockedUntil,
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

        // ===================== LOGIN01 =====================

        [Fact]
        public async Task Login01_ValidCredentials_ReturnsTokens()
        {
            var service = CreateService();
            var username = "john";
            var password = "P@ssw0rd!";
            await SeedAccountAsync(username, password);

            var dto = new LoginDto { Username = username, Password = password };

            var response = await service.LoginAsync(dto);

            Assert.NotNull(response);
            Assert.False(string.IsNullOrEmpty(response.AccessToken));
            Assert.False(string.IsNullOrEmpty(response.RefreshToken));
            Assert.Equal(username, response.User.Username);

            var accountInDb = _context.Accounts.Single(a => a.Username == username);
            Assert.Equal(0, accountInDb.FailedLoginCount);
            Assert.Null(accountInDb.LockedUntil);
            Assert.Equal(_nowUtc, accountInDb.LastLoginAt);
        }

        // ===================== LOGIN02 =====================

        [Fact]
        public void Login02_UsernameEmptyOrWhitespace_FailsValidation()
        {
            var dto = new LoginDto
            {
                Username = "   ",     // hoặc string.Empty
                Password = "Valid123!"
            };

            var results = ValidateModel(dto);

            Assert.Contains(results, r => r.MemberNames.Contains(nameof(LoginDto.Username)));
        }

        // ===================== LOGIN03 =====================

        [Fact]
        public void Login03_PasswordEmptyOrWhitespace_FailsValidation()
        {
            var dto = new LoginDto
            {
                Username = "validuser",
                Password = "   "
            };

            var results = ValidateModel(dto);

            Assert.Contains(results, r => r.MemberNames.Contains(nameof(LoginDto.Password)));
        }

        // ===================== LOGIN04 =====================

        [Fact]
        public async Task Login04_AccountDoesNotExist_ThrowsUnauthorized()
        {
            var service = CreateService();
            var dto = new LoginDto { Username = "does-not-exist", Password = "any" };

            var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(
                () => service.LoginAsync(dto));

            Assert.Contains("Username hoặc password không chính xác", ex.Message);
        }

        // ===================== LOGIN05 =====================

        [Fact]
        public async Task Login05_WrongPassword_IncrementsFailedLoginCount()
        {
            var service = CreateService();
            var username = "user_wrong_pw";
            var correctPassword = "Correct@123";
            await SeedAccountAsync(
                username,
                correctPassword,
                userStatus: "Active",
                failedLoginCount: 2);

            var dto = new LoginDto { Username = username, Password = "WrongPassword!" };

            var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(
                () => service.LoginAsync(dto));

            Assert.Contains("Username hoặc password không chính xác", ex.Message);

            var accountInDb = _context.Accounts.Single(a => a.Username == username);
            Assert.Equal(3, accountInDb.FailedLoginCount);   // 2 + 1
            Assert.Null(accountInDb.LockedUntil);
        }

        // ===================== LOGIN06 =====================

        [Fact]
        public async Task Login06_WrongPassword_FifthAttempt_LocksAccount()
        {
            var service = CreateService();
            var username = "user_lock_after_5";
            var correctPassword = "Correct@123";
            await SeedAccountAsync(
                username,
                correctPassword,
                userStatus: "Active",
                failedLoginCount: 4);

            var dto = new LoginDto { Username = username, Password = "WrongPassword!" };

            var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(
                () => service.LoginAsync(dto));

            Assert.Contains("Username hoặc password không chính xác", ex.Message);

            var accountInDb = _context.Accounts.Single(a => a.Username == username);
            Assert.Equal(0, accountInDb.FailedLoginCount); // reset về 0 khi đã lock
            Assert.NotNull(accountInDb.LockedUntil);
            Assert.Equal(_nowUtc.AddMinutes(15), accountInDb.LockedUntil);
        }

        // ===================== LOGIN07 =====================

        [Fact]
        public async Task Login07_AccountAlreadyLocked_ThrowsUnauthorized()
        {
            var service = CreateService();
            var username = "locked_user";
            var password = "P@ssw0rd!";
            await SeedAccountAsync(
                username,
                password,
                userStatus: "Active",
                failedLoginCount: 0,
                lockedUntil: _nowUtc.AddMinutes(5));

            var dto = new LoginDto { Username = username, Password = password };

            var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(
                () => service.LoginAsync(dto));

            Assert.Contains("Tài khoản đang bị khóa", ex.Message);
        }

        // ===================== LOGIN08 =====================

        [Fact]
        public async Task Login08_DisabledUser_ThrowsUnauthorized()
        {
            var service = CreateService();
            var username = "disabled_user";
            var password = "P@ssw0rd!";
            await SeedAccountAsync(
                username,
                password,
                userStatus: "Disabled",
                failedLoginCount: 0,
                lockedUntil: null);

            var dto = new LoginDto { Username = username, Password = password };

            var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(
                () => service.LoginAsync(dto));

            Assert.Contains("Tài khoản đã bị vô hiệu hóa", ex.Message);
        }

        // ===================== LOGIN09 =====================

        [Fact]
        public async Task Login09_LockExpired_AllowsLoginAgain()
        {
            var service = CreateService();
            var username = "lock_expired_user";
            var password = "P@ssw0rd!";

            // Từng bị khóa nhưng hết hạn trước thời điểm _nowUtc
            await SeedAccountAsync(
                username,
                password,
                userStatus: "Active",
                failedLoginCount: 0,
                lockedUntil: _nowUtc.AddMinutes(-5));

            var dto = new LoginDto { Username = username, Password = password };

            var response = await service.LoginAsync(dto);

            Assert.NotNull(response);
            var accountInDb = _context.Accounts.Single(a => a.Username == username);
            Assert.Equal(0, accountInDb.FailedLoginCount);
            Assert.Null(accountInDb.LockedUntil);
            Assert.Equal(_nowUtc, accountInDb.LastLoginAt);
        }

        // ===================== LOGIN10 =====================

        [Fact]
        public async Task Login10_SuccessAfterPreviousFailures_ResetsFailedLoginCount()
        {
            var service = CreateService();
            var username = "user_success_after_fail";
            var password = "P@ssw0rd!";

            await SeedAccountAsync(
                username,
                password,
                userStatus: "Active",
                failedLoginCount: 3,
                lockedUntil: null);

            var dto = new LoginDto { Username = username, Password = password };

            var response = await service.LoginAsync(dto);

            Assert.NotNull(response);

            var accountInDb = _context.Accounts.Single(a => a.Username == username);
            Assert.Equal(0, accountInDb.FailedLoginCount);
            Assert.Null(accountInDb.LockedUntil);
            Assert.Equal(_nowUtc, accountInDb.LastLoginAt);
        }

        public void Dispose()
        {
            _context.Dispose();
            _cache.Dispose();
        }
    }
}
