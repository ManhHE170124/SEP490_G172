using System;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Keytietkiem.DTOs;
using Keytietkiem.Infrastructure;
using Keytietkiem.Models;
using Keytietkiem.Options;
using Keytietkiem.Repositories;
using Keytietkiem.Services;
using Keytietkiem.Services.Interfaces;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Keytietkiem.UnitTests.Controllers
{
    public class AccountServiceRegisterTests
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

        private static RegisterDto CreateValidRegisterDto(
            string email = "user@test.com",
            string username = "user01",
            string verificationToken = "VALID_TOKEN")
        {
            return new RegisterDto
            {
                Email = email,
                VerificationToken = verificationToken,
                Username = username,
                Password = "P@ssw0rd!",
                FirstName = "Test",
                LastName = "User",
                Phone = "0123456789",
                Address = "Ha Noi"
            };
        }

        private static void SeedCustomerRole(KeytietkiemDbContext context)
        {
            context.Roles.Add(new Role
            {
                RoleId = "Customer",
                Name = "Customer",
                IsActive = true,
                IsSystem = true,
                CreatedAt = DateTime.UtcNow
            });
            context.SaveChanges();
        }

        private static string GetVerificationKey(string email)
            => $"VERIFY_TOKEN_{email}";

        private static (AccountService service,
                        KeytietkiemDbContext db,
                        IMemoryCache cache,
                        Mock<IClock> clockMock)
            CreateService(string databaseName,
                          IGenericRepository<Account>? accountRepoOverride = null,
                          IGenericRepository<User>? userRepoOverride = null)
        {
            // Dùng SQLite in-memory để hỗ trợ transaction
            var connection = new SqliteConnection("Filename=:memory:");
            connection.Open();

            var options = new DbContextOptionsBuilder<KeytietkiemDbContext>()
                .UseSqlite(connection)
                .Options;

            var context = new KeytietkiemDbContext(options);
            context.Database.EnsureCreated();

            var accountRepository =
                accountRepoOverride ?? new GenericRepository<Account>(context);
            var userRepository =
                userRepoOverride ?? new GenericRepository<User>(context);

            var clockMock = new Mock<IClock>();
            clockMock.SetupGet(c => c.UtcNow)
                     .Returns(new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc));

            var cache = CreateMemoryCache();
            var emailServiceMock = new Mock<IEmailService>();

            var service = new AccountService(
                context,
                accountRepository,
                userRepository,
                clockMock.Object,
                CreateJwtOptions(),
                CreateClientOptions(),
                cache,
                emailServiceMock.Object);

            return (service, context, cache, clockMock);
        }

        #endregion

        // REG01 – New user, token OK
        [Fact]
        public async Task REG01_Register_NewUser_Success()
        {
            var (service, db, cache, _) =
                CreateService(nameof(REG01_Register_NewUser_Success));

            SeedCustomerRole(db);

            var dto = CreateValidRegisterDto();
            cache.Set(GetVerificationKey(dto.Email), dto.VerificationToken);

            var result = await service.RegisterAsync(dto, CancellationToken.None);

            Assert.NotNull(result);
            Assert.False(string.IsNullOrWhiteSpace(result.AccessToken));
            Assert.False(string.IsNullOrWhiteSpace(result.RefreshToken));

            var user = db.Users
                .Include(u => u.Roles)
                .Single(u => u.Email == dto.Email);

            Assert.Equal("Active", user.Status);
            Assert.True(user.EmailVerified);
            Assert.False(user.IsTemp);
            Assert.Contains(user.Roles, r => r.RoleId == "Customer");

            var account = db.Accounts.Single(a => a.Username == dto.Username);
            Assert.Equal(user.UserId, account.UserId);

            Assert.False(cache.TryGetValue(GetVerificationKey(dto.Email), out _));
        }

        // REG02 – Có temp user, được convert thành user chính thức
        [Fact]
        public async Task REG02_Register_ExistingTempUser_IsConvertedAndReused()
        {
            var (service, db, cache, clockMock) =
                CreateService(nameof(REG02_Register_ExistingTempUser_IsConvertedAndReused));

            SeedCustomerRole(db);

            var tempUser = new User
            {
                UserId = Guid.NewGuid(),
                Email = "user2@test.com",
                Status = "Active",
                IsTemp = true,
                EmailVerified = false,
                CreatedAt = clockMock.Object.UtcNow
            };
            db.Users.Add(tempUser);
            db.SaveChanges();

            var originalUserId = tempUser.UserId;

            // Detach tempUser để tránh conflict 2 instance cùng key
            db.Entry(tempUser).State = EntityState.Detached;

            var dto = CreateValidRegisterDto(
                email: tempUser.Email,
                username: "user02");

            cache.Set(GetVerificationKey(dto.Email), dto.VerificationToken);

            var result = await service.RegisterAsync(dto, CancellationToken.None);

            Assert.NotNull(result);

            var user = db.Users
                .Include(u => u.Roles)
                .Single(u => u.Email == dto.Email);

            Assert.Equal(originalUserId, user.UserId);
            Assert.False(user.IsTemp);
            Assert.True(user.EmailVerified);
            Assert.Equal("Active", user.Status);
            Assert.Equal(dto.Phone, user.Phone);
            Assert.Equal(dto.Address, user.Address);
            Assert.Contains(user.Roles, r => r.RoleId == "Customer");

            var account = db.Accounts.Single(a => a.Username == dto.Username);
            Assert.Equal(user.UserId, account.UserId);
        }

        // REG03 – token missing/expired
        [Fact]
        public async Task REG03_Register_TokenMissing_ThrowsUnauthorized()
        {
            var (service, db, cache, _) =
                CreateService(nameof(REG03_Register_TokenMissing_ThrowsUnauthorized));

            SeedCustomerRole(db);

            var dto = CreateValidRegisterDto();

            await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
                service.RegisterAsync(dto, CancellationToken.None));

            Assert.Empty(db.Users);
            Assert.Empty(db.Accounts);
        }

        // REG04 – token mismatch
        [Fact]
        public async Task REG04_Register_TokenMismatch_ThrowsUnauthorized()
        {
            var (service, db, cache, _) =
                CreateService(nameof(REG04_Register_TokenMismatch_ThrowsUnauthorized));

            SeedCustomerRole(db);

            var dto = CreateValidRegisterDto();
            cache.Set(GetVerificationKey(dto.Email), "OTHER_TOKEN");

            await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
                service.RegisterAsync(dto, CancellationToken.None));

            Assert.Empty(db.Users);
            Assert.Empty(db.Accounts);
        }

        // REG05 – username đã tồn tại
        [Fact]
        public async Task REG05_Register_UsernameAlreadyExists_ThrowsInvalidOperation()
        {
            var (service, db, cache, clockMock) =
                CreateService(nameof(REG05_Register_UsernameAlreadyExists_ThrowsInvalidOperation));

            SeedCustomerRole(db);

            var existingUser = new User
            {
                UserId = Guid.NewGuid(),
                Email = "existing@test.com",
                Status = "Active",
                CreatedAt = clockMock.Object.UtcNow
            };
            db.Users.Add(existingUser);

            db.Accounts.Add(new Account
            {
                AccountId = Guid.NewGuid(),
                Username = "dupUser",
                UserId = existingUser.UserId,
                PasswordHash = Array.Empty<byte>(),
                FailedLoginCount = 0,
                CreatedAt = clockMock.Object.UtcNow
            });
            db.SaveChanges();

            var dto = CreateValidRegisterDto(
                email: "newuser@test.com",
                username: "dupUser");

            cache.Set(GetVerificationKey(dto.Email), dto.VerificationToken);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.RegisterAsync(dto, CancellationToken.None));
        }

        // REG06 – email đã tồn tại (non-temp)
        [Fact]
        public async Task REG06_Register_EmailAlreadyExists_ThrowsInvalidOperation()
        {
            var (service, db, cache, clockMock) =
                CreateService(nameof(REG06_Register_EmailAlreadyExists_ThrowsInvalidOperation));

            SeedCustomerRole(db);

            var existingUser = new User
            {
                UserId = Guid.NewGuid(),
                Email = "dupEmail@test.com",
                Status = "Active",
                IsTemp = false,
                EmailVerified = true,
                CreatedAt = clockMock.Object.UtcNow
            };
            db.Users.Add(existingUser);
            db.SaveChanges();

            var dto = CreateValidRegisterDto(
                email: existingUser.Email,
                username: "newUserName");

            cache.Set(GetVerificationKey(dto.Email), dto.VerificationToken);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.RegisterAsync(dto, CancellationToken.None));
        }

        // REG07 – thiếu Customer role
        [Fact]
        public async Task REG07_Register_CustomerRoleMissing_ThrowsInvalidOperation()
        {
            var (service, db, cache, _) =
                CreateService(nameof(REG07_Register_CustomerRoleMissing_ThrowsInvalidOperation));

            var dto = CreateValidRegisterDto();
            cache.Set(GetVerificationKey(dto.Email), dto.VerificationToken);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.RegisterAsync(dto, CancellationToken.None));

            Assert.Empty(db.Users);
            Assert.Empty(db.Accounts);
        }

        // REG08 – lỗi DB trong transaction (Add Account fail)
        [Fact]
        public async Task REG08_Register_DbFailureDuringTransaction_RethrowsException()
        {
            var connection = new SqliteConnection("Filename=:memory:");
            connection.Open();

            var options = new DbContextOptionsBuilder<KeytietkiemDbContext>()
                .UseSqlite(connection)
                .Options;

            var context = new KeytietkiemDbContext(options);
            context.Database.EnsureCreated();
            SeedCustomerRole(context);

            var userRepo = new GenericRepository<User>(context);

            var accountRepoMock = new Mock<IGenericRepository<Account>>();

            accountRepoMock
                .Setup(r => r.AnyAsync(
                    It.IsAny<Expression<Func<Account, bool>>>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);

            accountRepoMock
                .Setup(r => r.AddAsync(It.IsAny<Account>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new DbUpdateException("Simulated DB failure", (Exception?)null));

            var clockMock = new Mock<IClock>();
            clockMock.SetupGet(c => c.UtcNow).Returns(DateTime.UtcNow);

            var cache = CreateMemoryCache();
            var emailServiceMock = new Mock<IEmailService>();

            var service = new AccountService(
                context,
                accountRepoMock.Object,
                userRepo,
                clockMock.Object,
                CreateJwtOptions(),
                CreateClientOptions(),
                cache,
                emailServiceMock.Object);

            var dto = CreateValidRegisterDto();
            cache.Set(GetVerificationKey(dto.Email), dto.VerificationToken);

            await Assert.ThrowsAsync<DbUpdateException>(() =>
                service.RegisterAsync(dto, CancellationToken.None));
        }

        // REG09 – boundary: password hash & role Customer
        [Fact]
        public async Task REG09_Register_PasswordIsHashed_AndCustomerRoleInResponse()
        {
            var (service, db, cache, _) =
                CreateService(nameof(REG09_Register_PasswordIsHashed_AndCustomerRoleInResponse));

            SeedCustomerRole(db);

            var dto = CreateValidRegisterDto();
            cache.Set(GetVerificationKey(dto.Email), dto.VerificationToken);

            var result = await service.RegisterAsync(dto, CancellationToken.None);

            var account = db.Accounts.Single(a => a.Username == dto.Username);

            Assert.NotNull(account.PasswordHash);
            Assert.NotEmpty(account.PasswordHash);
            Assert.NotEqual(dto.Password.Length, account.PasswordHash!.Length);

            Assert.Contains("Customer", result.User.Roles);
        }
    }
}
