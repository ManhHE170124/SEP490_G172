using System;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Reflection;
using System.Security.Claims;
using System.Text;
using System.Threading;
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
using Microsoft.IdentityModel.Tokens;
using Moq;
using Xunit;

namespace Keytietkiem.UnitTests.Controllers
{
    public class AuthorizationTests
    {
        #region Helpers

        private static IOptions<JwtConfig> CreateJwtOptions()
        {
            var cfg = new JwtConfig
            {
                SecretKey = "THIS_IS_A_TEST_SECRET_KEY_1234567890",
                Issuer = "TestIssuer",
                Audience = "TestAudience",
                ExpiryInMinutes = 30,
                RefreshTokenExpiryInDays = 7
            };

            return new OptionsWrapper<JwtConfig>(cfg);
        }

        private static IOptions<ClientConfig> CreateClientOptions()
        {
            var cfg = new ClientConfig
            {
                ClientUrl = "https://test.local",
                ResetLinkExpiryInMinutes = 60
            };

            return new OptionsWrapper<ClientConfig>(cfg);
        }

        private static IMemoryCache CreateMemoryCache()
            => new MemoryCache(new MemoryCacheOptions());

        private static (AccountService service,
                        KeytietkiemDbContext db,
                        IMemoryCache cache,
                        Mock<IClock> clockMock,
                        JwtConfig jwtConfig)
            CreateService(string databaseName)
        {
            var options = new DbContextOptionsBuilder<KeytietkiemDbContext>()
                .UseInMemoryDatabase(databaseName)
                .Options;

            var context = new KeytietkiemDbContext(options);

            var accountRepository = new GenericRepository<Account>(context);
            var userRepository = new GenericRepository<User>(context);

            var clockMock = new Mock<IClock>();
            clockMock.SetupGet(c => c.UtcNow)
                     .Returns(new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc));

            var cache = CreateMemoryCache();
            var emailServiceMock = new Mock<IEmailService>();

            var jwtOptions = CreateJwtOptions();
            var clientOptions = CreateClientOptions();

            var service = new AccountService(
                context,
                accountRepository,
                userRepository,
                clockMock.Object,
                jwtOptions,
                clientOptions,
                cache,
                emailServiceMock.Object);

            return (service, context, cache, clockMock, jwtOptions.Value);
        }

        private static string CreateJwtToken(
            JwtConfig config,
            Claim[] claims,
            DateTime expires,
            bool useDifferentKey = false)
        {
            var keyString = useDifferentKey
                ? "OTHER_TEST_SECRET_KEY_0987654321"
                : config.SecretKey;

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(keyString));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: config.Issuer,
                audience: config.Audience,
                claims: claims,
                expires: expires,
                signingCredentials: creds);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        private static ClaimsPrincipal InvokeValidateToken(AccountService service, string token)
        {
            var method = typeof(AccountService).GetMethod(
                "ValidateToken",
                BindingFlags.NonPublic | BindingFlags.Instance);

            Assert.NotNull(method);

            return (ClaimsPrincipal)method!.Invoke(service, new object[] { token })!;
        }

        private static byte[] InvokeHashPassword(AccountService service, string password)
        {
            var method = typeof(AccountService).GetMethod(
                "HashPassword",
                BindingFlags.NonPublic | BindingFlags.Static);

            Assert.NotNull(method);

            return (byte[])method!.Invoke(null, new object[] { password })!;
        }

        private static bool InvokeVerifyPassword(AccountService service, string password, byte[] hash)
        {
            var method = typeof(AccountService).GetMethod(
                "VerifyPassword",
                BindingFlags.NonPublic | BindingFlags.Static);

            Assert.NotNull(method);

            return (bool)method!.Invoke(null, new object[] { password, hash })!;
        }

        #endregion

        // AUTH01 – token hợp lệ
        [Fact]
        public void AUTH01_ValidateToken_ValidJwt_ReturnsPrincipal()
        {
            var (service, _, _, _, jwtConfig) =
                CreateService(nameof(AUTH01_ValidateToken_ValidJwt_ReturnsPrincipal));

            // dùng thời gian thực, không dùng clockMock (để tránh bị coi là expired)
            var now = DateTime.UtcNow;
            var accountId = Guid.NewGuid();

            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
                new Claim("AccountId", accountId.ToString())
            };

            // cho expire sau 10 phút tính từ hiện tại => chắc chắn chưa hết hạn
            var token = CreateJwtToken(jwtConfig, claims, now.AddMinutes(10));

            var principal = InvokeValidateToken(service, token);

            Assert.NotNull(principal);
            Assert.Equal(accountId.ToString(), principal.FindFirst("AccountId")!.Value);
        }

        // AUTH02 – token không phải JWT hợp lệ về cấu trúc
        [Fact]
        public void AUTH02_ValidateToken_InvalidString_ThrowsException()
        {
            var (service, _, _, _, _) =
                CreateService(nameof(AUTH02_ValidateToken_InvalidString_ThrowsException));

            Assert.ThrowsAny<Exception>(() =>
                InvokeValidateToken(service, "this-is-not-a-jwt-token"));
        }

        // AUTH03 – tampered signature
        [Fact]
        public void AUTH03_ValidateToken_TamperedSignature_ThrowsSecurityTokenException()
        {
            var (service, _, _, _, jwtConfig) =
                CreateService(nameof(AUTH03_ValidateToken_TamperedSignature_ThrowsSecurityTokenException));

            var now = DateTime.UtcNow;
            var accountId = Guid.NewGuid();

            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
                new Claim("AccountId", accountId.ToString())
            };

            // ký bằng secret khác -> signature không hợp lệ
            var token = CreateJwtToken(jwtConfig, claims, now.AddMinutes(10), useDifferentKey: true);

            var ex = Assert.ThrowsAny<Exception>(() =>
                InvokeValidateToken(service, token));

            var baseEx = ex.GetBaseException();
            Assert.IsAssignableFrom<SecurityTokenException>(baseEx);
        }

        // AUTH04 – token hết hạn
        [Fact]
        public void AUTH04_ValidateToken_ExpiredToken_ThrowsSecurityTokenExpiredException()
        {
            var (service, _, _, _, jwtConfig) =
                CreateService(nameof(AUTH04_ValidateToken_ExpiredToken_ThrowsSecurityTokenExpiredException));

            var now = DateTime.UtcNow;
            var accountId = Guid.NewGuid();

            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
                new Claim("AccountId", accountId.ToString())
            };

            // expire trong quá khứ
            var token = CreateJwtToken(jwtConfig, claims, now.AddMinutes(-1));

            var ex = Assert.ThrowsAny<Exception>(() =>
                InvokeValidateToken(service, token));

            var baseEx = ex.GetBaseException();
            Assert.IsType<SecurityTokenExpiredException>(baseEx);
        }

        // AUTH05 – GetProfile account tồn tại
        [Fact]
        public async Task AUTH05_GetProfile_ExistingAccount_ReturnsProfile()
        {
            var (service, db, _, clockMock, _) =
                CreateService(nameof(AUTH05_GetProfile_ExistingAccount_ReturnsProfile));

            var user = new User
            {
                UserId = Guid.NewGuid(),
                Email = "profile@test.com",
                Status = "Active",
                CreatedAt = clockMock.Object.UtcNow
            };

            var account = new Account
            {
                AccountId = Guid.NewGuid(),
                Username = "profileUser",
                UserId = user.UserId,
                PasswordHash = Array.Empty<byte>(),
                CreatedAt = clockMock.Object.UtcNow,
                User = user
            };

            db.Users.Add(user);
            db.Accounts.Add(account);
            await db.SaveChangesAsync();

            var profile = await service.GetProfileAsync(account.AccountId, CancellationToken.None);

            Assert.NotNull(profile);
            Assert.Equal(account.AccountId, profile.AccountId);
            Assert.Equal(user.Email, profile.Email);
            Assert.Equal(account.Username, profile.Username);
        }

        // AUTH06 – GetProfile account không tồn tại
        [Fact]
        public async Task AUTH06_GetProfile_AccountMissing_ThrowsInvalidOperationException()
        {
            var (service, _, _, _, _) =
                CreateService(nameof(AUTH06_GetProfile_AccountMissing_ThrowsInvalidOperationException));

            var nonExistingId = Guid.NewGuid();

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.GetProfileAsync(nonExistingId, CancellationToken.None));
        }

        // AUTH07 – ChangePassword current password đúng
        [Fact]
        public async Task AUTH07_ChangePassword_CurrentPasswordMatches_Succeeds()
        {
            var (service, db, _, clockMock, _) =
                CreateService(nameof(AUTH07_ChangePassword_CurrentPasswordMatches_Succeeds));

            const string oldPassword = "OldP@ssw0rd!";
            const string newPassword = "NewP@ssw0rd!";

            var user = new User
            {
                UserId = Guid.NewGuid(),
                Email = "changepw@test.com",
                Status = "Active",
                CreatedAt = clockMock.Object.UtcNow
            };

            var accountId = Guid.NewGuid();
            var hash = InvokeHashPassword(service, oldPassword);

            var account = new Account
            {
                AccountId = accountId,
                Username = "changepwUser",
                UserId = user.UserId,
                PasswordHash = hash,
                FailedLoginCount = 0,
                CreatedAt = clockMock.Object.UtcNow,
                User = user
            };

            db.Users.Add(user);
            db.Accounts.Add(account);
            await db.SaveChangesAsync();

            var dto = new ChangePasswordDto
            {
                CurrentPassword = oldPassword,
                NewPassword = newPassword
            };

            await service.ChangePasswordAsync(accountId, dto, CancellationToken.None);

            var updated = await db.Accounts.FindAsync(accountId);
            Assert.NotNull(updated);
            Assert.True(InvokeVerifyPassword(service, newPassword, updated!.PasswordHash!));
        }

        // AUTH08 – ChangePassword current password sai
        [Fact]
        public async Task AUTH08_ChangePassword_CurrentPasswordDoesNotMatch_ThrowsUnauthorized()
        {
            var (service, db, _, clockMock, _) =
                CreateService(nameof(AUTH08_ChangePassword_CurrentPasswordDoesNotMatch_ThrowsUnauthorized));

            const string realPassword = "RealP@ssw0rd!";
            const string wrongPassword = "WrongP@ssw0rd!";

            var user = new User
            {
                UserId = Guid.NewGuid(),
                Email = "changepw2@test.com",
                Status = "Active",
                CreatedAt = clockMock.Object.UtcNow
            };

            var accountId = Guid.NewGuid();
            var hash = InvokeHashPassword(service, realPassword);

            var account = new Account
            {
                AccountId = accountId,
                Username = "changepwUser2",
                UserId = user.UserId,
                PasswordHash = hash,
                FailedLoginCount = 0,
                CreatedAt = clockMock.Object.UtcNow,
                User = user
            };

            db.Users.Add(user);
            db.Accounts.Add(account);
            await db.SaveChangesAsync();

            var dto = new ChangePasswordDto
            {
                CurrentPassword = wrongPassword,
                NewPassword = "AnyNewP@ss1!"
            };

            await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
                service.ChangePasswordAsync(accountId, dto, CancellationToken.None));
        }
    }
}
