using System.Diagnostics.CodeAnalysis;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Keytietkiem.DTOs;
using Keytietkiem.DTOs.Enums;
using Keytietkiem.Infrastructure;
using Keytietkiem.Models;
using Keytietkiem.Options;
using Keytietkiem.Repositories;
using Keytietkiem.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Keytietkiem.Services;

public class AccountService : IAccountService
{
    private const string OtpCacheKeyPrefix = "OTP_";
    private const string VerificationTokenCacheKeyPrefix = "VERIFY_TOKEN_";
    private const string ResetTokenCacheKeyPrefix = "RESET_TOKEN_";
    private const string RevokedTokenCacheKeyPrefix = "REVOKED_TOKEN_";
    private const int OtpExpiryMinutes = 5;
    private const int VerificationTokenExpiryMinutes = 30;
    private const string Admin = "admin";
    private const string AccountNotFoundMessage = "Tài khoản không tồn tại";
    private readonly IGenericRepository<Account> _accountRepository;
    private readonly IMemoryCache _cache;
    private readonly IClock _clock;
    private readonly KeytietkiemDbContext _context;
    private readonly IEmailService _emailService;
    private readonly JwtConfig _jwtConfig;
    private readonly ClientConfig _clientConfig;
    private readonly IGenericRepository<User> _userRepository;

    [SuppressMessage("Major Code Smell", "S107:Methods should not have too many parameters",
        Justification = "Constructor injection requires these dependencies")]
    public AccountService(
        KeytietkiemDbContext context,
        IGenericRepository<Account> accountRepository,
        IGenericRepository<User> userRepository,
        IClock clock,
        IOptions<JwtConfig> jwtOptions,
        IOptions<ClientConfig> clientOptions,
        IMemoryCache cache,
        IEmailService emailService)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _accountRepository = accountRepository ?? throw new ArgumentNullException(nameof(accountRepository));
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _jwtConfig = jwtOptions?.Value ?? throw new ArgumentNullException(nameof(jwtOptions));
        _clientConfig = clientOptions?.Value ?? throw new ArgumentNullException(nameof(clientOptions));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _emailService = emailService ?? throw new ArgumentNullException(nameof(emailService));
    }

    public async Task<LoginResponseDto> RegisterAsync(RegisterDto registerDto,
        CancellationToken cancellationToken = default)
    {
        // Verify the verification token from OTP verification
        var verificationKey = $"{VerificationTokenCacheKeyPrefix}{registerDto.Email}";
        if (!_cache.TryGetValue(verificationKey, out string? storedToken) ||
            storedToken != registerDto.VerificationToken)
            throw new UnauthorizedAccessException(
                "Token xác thực không hợp lệ hoặc đã hết hạn. Vui lòng xác thực OTP lại");

        // Validate username uniqueness
        if (await IsUsernameExistsAsync(registerDto.Username, cancellationToken))
            throw new InvalidOperationException("Username đã tồn tại");

        // Validate email uniqueness
        if (await IsEmailExistsAsync(registerDto.Email, cancellationToken))
            throw new InvalidOperationException("Email đã được đăng ký");

        // Get Customer role
        var customerRole = await _context.Roles
            .FirstOrDefaultAsync(r => r.RoleId == "Customer", cancellationToken);

        if (customerRole == null)
            throw new InvalidOperationException("Customer role không tồn tại. Vui lòng kiểm tra cấu hình hệ thống");

        // Check if a temp user exists with this email
        var existingTempUser = await _userRepository.FirstOrDefaultAsync(
            u => u.Email == registerDto.Email && u.IsTemp,
            cancellationToken);

        User user;
        bool isNewUser = false;

        if (existingTempUser != null)
        {
            // Update existing temp user to active user
            user = existingTempUser;
            user.FirstName = registerDto.FirstName;
            user.LastName = registerDto.LastName;
            user.FullName = $"{registerDto.FirstName} {registerDto.LastName}";
            user.Phone = registerDto.Phone;
            user.Address = registerDto.Address;
            user.Status = "Active";
            user.IsTemp = false;
            user.EmailVerified = true; // Email verified via OTP
            user.UpdatedAt = _clock.UtcNow;

            _userRepository.Update(user);
        }
        else
        {
            // Create new User entity
            user = new User
            {
                UserId = Guid.NewGuid(),
                FirstName = registerDto.FirstName,
                LastName = registerDto.LastName,
                FullName = $"{registerDto.FirstName} {registerDto.LastName}",
                Email = registerDto.Email,
                Phone = registerDto.Phone,
                Address = registerDto.Address,
                Status = "Active",
                EmailVerified = true, // Email verified via OTP
                CreatedAt = _clock.UtcNow
            };
            isNewUser = true;
        }

        // Add Customer role to user
        user.Roles.Add(customerRole);

        // Create Account entity
        var account = new Account
        {
            AccountId = Guid.NewGuid(),
            Username = registerDto.Username,
            PasswordHash = HashPassword(registerDto.Password),
            UserId = user.UserId,
            FailedLoginCount = 0,
            CreatedAt = _clock.UtcNow
        };

        // Save to database using transaction
        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            if (isNewUser)
            {
                await _userRepository.AddAsync(user, cancellationToken);
            }
            // If updating existing temp user, Update was already called above

            await _accountRepository.AddAsync(account, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        // Remove verification token from cache
        _cache.Remove(verificationKey);

        // Load user with roles for token generation
        var userWithRoles = await LoadUserWithRolesAsync(user.UserId, cancellationToken);

        // Generate tokens
        return GenerateLoginResponse(account, userWithRoles);
    }

    public async Task<LoginResponseDto> LoginAsync(LoginDto loginDto, CancellationToken cancellationToken = default)
    {
        // Find account by username with user and roles
        var account = await _context.Accounts
            .Include(a => a.User)
            .ThenInclude(u => u.Roles)
            .FirstOrDefaultAsync(a => a.Username == loginDto.Username, cancellationToken);

        if (account == null) throw new UnauthorizedAccessException("Username hoặc password không chính xác");

        // Check if account is locked
        if (account.LockedUntil.HasValue && account.LockedUntil.Value > _clock.UtcNow)
        {
            var remainingMinutes = Math.Ceiling((account.LockedUntil.Value - _clock.UtcNow).TotalMinutes);
            throw new UnauthorizedAccessException(
                $"Tài khoản đang bị khóa. Vui lòng thử lại sau {remainingMinutes} phút");
        }

        // Check if user is active
        if (account.User.Status != "Active") throw new UnauthorizedAccessException("Tài khoản đã bị vô hiệu hóa");

        // Verify password
        if (!VerifyPassword(loginDto.Password, account.PasswordHash))
        {
            // Increment failed login count
            account.FailedLoginCount++;

            // Lock account after 5 failed attempts within 15 minutes
            if (account.FailedLoginCount >= 5)
            {
                account.LockedUntil = _clock.UtcNow.AddMinutes(15);
                account.FailedLoginCount = 0;
            }

            _accountRepository.Update(account);
            await _context.SaveChangesAsync(cancellationToken);

            throw new UnauthorizedAccessException("Username hoặc password không chính xác");
        }

        // Reset failed login count and update last login
        account.FailedLoginCount = 0;
        account.LastLoginAt = _clock.UtcNow;
        account.LockedUntil = null;

        _accountRepository.Update(account);
        await _context.SaveChangesAsync(cancellationToken);

        // Generate tokens
        return GenerateLoginResponse(account, account.User);
    }

    public async Task<LoginResponseDto> RefreshTokenAsync(RefreshTokenDto refreshTokenDto,
        CancellationToken cancellationToken = default)
    {
        // For this implementation, we'll use a simple approach
        // In production, you should store refresh tokens in database with expiry
        var principal = ValidateToken(refreshTokenDto.RefreshToken);
        var userIdClaim = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (userIdClaim == null || !Guid.TryParse(userIdClaim, out var userId))
            throw new UnauthorizedAccessException("Refresh token không hợp lệ");

        // Load user with account and roles
        var user = await LoadUserWithRolesAsync(userId, cancellationToken);
        var account = await _accountRepository.FirstOrDefaultAsync(a => a.UserId == userId, cancellationToken);

        if (account == null || user.Status != "Active")
            throw new UnauthorizedAccessException("Tài khoản không tồn tại hoặc đã bị vô hiệu hóa");

        // Generate new tokens
        return GenerateLoginResponse(account, user);
    }

    public async Task<AccountProfileDto> GetProfileAsync(Guid accountId, CancellationToken cancellationToken = default)
    {
        var account = await _context.Accounts
            .Include(a => a.User)
            .ThenInclude(u => u.Roles)
            .FirstOrDefaultAsync(a => a.AccountId == accountId, cancellationToken);

        if (account == null)
            throw new InvalidOperationException(AccountNotFoundMessage);

        var user = account.User;
        var roles = user.Roles.Select(r => r.RoleId).ToList();

        return new AccountProfileDto
        {
            UserId = user.UserId,
            AccountId = account.AccountId,
            Username = account.Username,
            Email = user.Email,
            FullName = user.FullName ?? string.Empty,
            Phone = user.Phone,
            Address = user.Address,
            AvatarUrl = user.AvatarUrl,
            Status = user.Status,
            Roles = roles
        };
    }

    public async Task<AccountProfileDto> UpdateProfileAsync(Guid accountId, UpdateAccountProfileDto updateDto,
        CancellationToken cancellationToken = default)
    {
        var account = await _context.Accounts
            .Include(a => a.User)
            .ThenInclude(u => u.Roles)
            .FirstOrDefaultAsync(a => a.AccountId == accountId, cancellationToken);

        if (account == null)
            throw new InvalidOperationException(AccountNotFoundMessage);

        var user = account.User ?? throw new InvalidOperationException("Người dùng không tồn tại");

        var trimmedFullName = updateDto.FullName?.Trim() ?? string.Empty;
        user.FullName = trimmedFullName;

        if (!string.IsNullOrWhiteSpace(trimmedFullName))
        {
            var nameParts = trimmedFullName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (nameParts.Length == 1)
            {
                user.FirstName = nameParts[0];
                user.LastName = nameParts[0];
            }
            else
            {
                user.FirstName = nameParts[^1];
                user.LastName = string.Join(' ', nameParts[..^1]);
            }
        }

        user.Phone = updateDto.Phone;
        user.Address = updateDto.Address;
        user.AvatarUrl = updateDto.AvatarUrl;
        user.UpdatedAt = _clock.UtcNow;

        _userRepository.Update(user);
        await _context.SaveChangesAsync(cancellationToken);

        var roles = user.Roles.Select(r => r.RoleId).ToList();

        return new AccountProfileDto
        {
            UserId = user.UserId,
            AccountId = account.AccountId,
            Username = account.Username,
            Email = user.Email,
            FullName = user.FullName ?? string.Empty,
            Phone = user.Phone,
            Address = user.Address,
            AvatarUrl = user.AvatarUrl,
            Status = user.Status,
            Roles = roles
        };
    }

    public async Task<User?> GetUserAsync(string email, CancellationToken cancellationToken = default)
    {
        return await _userRepository.FirstOrDefaultAsync(a => a.Email == email, cancellationToken);
    }

    public async Task ChangePasswordAsync(Guid accountId, ChangePasswordDto changePasswordDto,
        CancellationToken cancellationToken = default)
    {
        var account = await _accountRepository.GetByIdAsync(accountId, cancellationToken);

        if (account == null) throw new InvalidOperationException(AccountNotFoundMessage);

        // Verify current password
        if (!VerifyPassword(changePasswordDto.CurrentPassword, account.PasswordHash))
            throw new UnauthorizedAccessException("Mật khẩu hiện tại không chính xác");

        // Update password
        account.PasswordHash = HashPassword(changePasswordDto.NewPassword);
        account.UpdatedAt = _clock.UtcNow;

        _accountRepository.Update(account);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> IsUsernameExistsAsync(string username, CancellationToken cancellationToken = default)
    {
        return await _accountRepository.AnyAsync(a => a.Username == username, cancellationToken);
    }

    public async Task<bool> IsEmailExistsAsync(string email, CancellationToken cancellationToken = default)
    {
        return await _userRepository.AnyAsync(u => u.Email == email && !u.IsTemp, cancellationToken);
    }

    public async Task<User> CreateTempUserAsync(string email, CancellationToken cancellationToken = default)
    {
        // Check if temp user already exists
        var existingTempUser = await _userRepository.FirstOrDefaultAsync(
            u => u.Email == email && u.IsTemp,
            cancellationToken);

        if (existingTempUser != null)
        {
            return existingTempUser;
        }

        // Create new temp user
        var tempUser = new User
        {
            UserId = Guid.NewGuid(),
            Email = email,
            Status = "Temp",
            IsTemp = true,
            EmailVerified = false,
            CreatedAt = _clock.UtcNow
        };

        await _userRepository.AddAsync(tempUser, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        return tempUser;
    }

    public async Task<string> SendOtpAsync(SendOtpDto sendOtpDto, CancellationToken cancellationToken = default)
    {
        // Generate 6-digit OTP
        var otp = GenerateOtp();

        // Store OTP in cache with expiry
        var cacheKey = $"{OtpCacheKeyPrefix}{sendOtpDto.Email}";
        var cacheOptions = new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(OtpExpiryMinutes)
        };
        _cache.Set(cacheKey, otp, cacheOptions);

        // Send OTP via email
        await _emailService.SendOtpEmailAsync(sendOtpDto.Email, otp, cancellationToken);

        return $"Mã OTP đã được gửi đến {sendOtpDto.Email}. Mã có hiệu lực trong {OtpExpiryMinutes} phút.";
    }

    public async Task<OtpVerificationResponseDto> VerifyOtpAsync(VerifyOtpDto verifyOtpDto,
        CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask; // Satisfy async requirement

        // Get OTP from cache
        var cacheKey = $"{OtpCacheKeyPrefix}{verifyOtpDto.Email}";
        if (!_cache.TryGetValue(cacheKey, out string? storedOtp))
            return new OtpVerificationResponseDto
            {
                IsVerified = false,
                Message = "Mã OTP không tồn tại hoặc đã hết hạn"
            };

        // Verify OTP
        if (storedOtp != verifyOtpDto.Otp)
            return new OtpVerificationResponseDto
            {
                IsVerified = false,
                Message = "Mã OTP không chính xác"
            };

        // Remove OTP from cache
        _cache.Remove(cacheKey);

        // Generate verification token
        var verificationToken = GenerateVerificationToken();

        // Store verification token in cache
        var verificationKey = $"{VerificationTokenCacheKeyPrefix}{verifyOtpDto.Email}";
        var verificationCacheOptions = new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(VerificationTokenExpiryMinutes)
        };
        _cache.Set(verificationKey, verificationToken, verificationCacheOptions);

        return new OtpVerificationResponseDto
        {
            IsVerified = true,
            Message = "Xác thực OTP thành công",
            VerificationToken = verificationToken
        };
    }

    public async Task<string> ForgotPasswordAsync(ForgotPasswordDto forgotPasswordDto,
        CancellationToken cancellationToken = default)
    {
        // Check if user exists
        var user = await _userRepository.FirstOrDefaultAsync(u => u.Email == forgotPasswordDto.Email,
            cancellationToken);

        if (user == null)
            throw new InvalidOperationException("Email không tồn tại trong hệ thống");

        // Generate secure reset token
        var resetToken = GenerateVerificationToken();

        // Store reset token in cache with expiry
        var cacheKey = $"{ResetTokenCacheKeyPrefix}{resetToken}";
        var cacheOptions = new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(_clientConfig.ResetLinkExpiryInMinutes)
        };
        _cache.Set(cacheKey, forgotPasswordDto.Email, cacheOptions);

        // Send password reset link via email
        await _emailService.SendPasswordResetEmailAsync(forgotPasswordDto.Email, resetToken, cancellationToken);

        return
            $"Link đặt lại mật khẩu đã được gửi đến {forgotPasswordDto.Email}. Link có hiệu lực trong {_clientConfig.ResetLinkExpiryInMinutes} phút.";
    }

    public async Task ResetPasswordAsync(ResetPasswordDto resetPasswordDto,
        CancellationToken cancellationToken = default)
    {
        // Get email from cache using reset token
        var cacheKey = $"{ResetTokenCacheKeyPrefix}{resetPasswordDto.Token}";
        if (!_cache.TryGetValue(cacheKey, out string? email))
            throw new UnauthorizedAccessException("Token đặt lại mật khẩu không hợp lệ hoặc đã hết hạn");

        // Find user by email
        var user = await _userRepository.FirstOrDefaultAsync(u => u.Email == email,
            cancellationToken);

        if (user == null)
            throw new InvalidOperationException("Người dùng không tồn tại");

        // Find account by user ID
        var account = await _accountRepository.FirstOrDefaultAsync(a => a.UserId == user.UserId,
            cancellationToken);

        if (account == null)
            throw new InvalidOperationException(AccountNotFoundMessage);

        // Update password
        account.PasswordHash = HashPassword(resetPasswordDto.NewPassword);
        account.UpdatedAt = _clock.UtcNow;

        // Reset failed login count and unlock account
        account.FailedLoginCount = 0;
        account.LockedUntil = null;

        _accountRepository.Update(account);
        await _context.SaveChangesAsync(cancellationToken);

        // Remove reset token from cache
        _cache.Remove(cacheKey);
    }

    public async Task RevokeTokenAsync(RevokeTokenDto revokeTokenDto, CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask; // Satisfy async requirement

        // Validate both tokens
        try
        {
            _ = ValidateToken(revokeTokenDto.AccessToken);
            _ = ValidateToken(revokeTokenDto.RefreshToken);

            // Get token JTI (unique identifier) from access token
            var accessTokenHandler = new JwtSecurityTokenHandler();
            var accessJwtToken = accessTokenHandler.ReadJwtToken(revokeTokenDto.AccessToken);
            var accessTokenId = accessJwtToken.Id ?? accessJwtToken.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Jti)?.Value;

            var refreshTokenHandler = new JwtSecurityTokenHandler();
            var refreshJwtToken = refreshTokenHandler.ReadJwtToken(revokeTokenDto.RefreshToken);
            var refreshTokenId = refreshJwtToken.Id ?? refreshJwtToken.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Jti)?.Value;

            // Store revoked tokens in cache until their expiry
            if (!string.IsNullOrEmpty(accessTokenId))
            {
                var accessExpiry = accessJwtToken.ValidTo;
                if (accessExpiry > _clock.UtcNow)
                {
                    var cacheOptions = new MemoryCacheEntryOptions
                    {
                        AbsoluteExpiration = accessExpiry
                    };
                    _cache.Set($"{RevokedTokenCacheKeyPrefix}{accessTokenId}", true, cacheOptions);
                }
            }

            if (!string.IsNullOrEmpty(refreshTokenId))
            {
                var refreshExpiry = refreshJwtToken.ValidTo;
                if (refreshExpiry > _clock.UtcNow)
                {
                    var cacheOptions = new MemoryCacheEntryOptions
                    {
                        AbsoluteExpiration = refreshExpiry
                    };
                    _cache.Set($"{RevokedTokenCacheKeyPrefix}{refreshTokenId}", true, cacheOptions);
                }
            }
        }
        catch (Exception)
        {
            // If token validation fails, it's already invalid, so we can ignore
            // This handles the case where tokens are expired or malformed
        }
    }

    public async Task SeedDataAsync(CancellationToken cancellationToken = default)
    {
        var admin = await _accountRepository.FirstOrDefaultAsync(x =>
            x.Username == Admin, cancellationToken);

        if (admin != null) return;

        var role = await _context.Roles.FirstOrDefaultAsync(x => x.RoleId == Admin,
            cancellationToken: cancellationToken);

        if (role == null)
        {
            return;
        }

        var firstName = "Admin";
        var lastName = "Owner";
        var user = new User
        {
            EmailVerified = true,
            FirstName = firstName,
            LastName = lastName,
            FullName = $"{firstName} {lastName}",
            Email = "admin@keytietkiem.com",
            Status = nameof(UserStatus.Active),
            UserId = Guid.NewGuid(),
            CreatedAt = _clock.UtcNow,
        };
        user.Roles.Add(role);

        admin = new Account
        {
            Username = Admin,
            PasswordHash = HashPassword("admin"),
            AccountId = Guid.NewGuid(),
            UserId = user.UserId,
            FailedLoginCount = 0,
            CreatedAt = _clock.UtcNow,
        };

        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            await _userRepository.AddAsync(user, cancellationToken);
            await _accountRepository.AddAsync(admin, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    // Private helper methods

    private async Task<User> LoadUserWithRolesAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await _context.Users
            .Include(u => u.Roles)
            .FirstOrDefaultAsync(u => u.UserId == userId, cancellationToken);

        if (user == null) throw new InvalidOperationException("Người dùng không tồn tại");

        return user;
    }

    private LoginResponseDto GenerateLoginResponse(Account account, User user)
    {
        var roles = user.Roles.Select(r => r.RoleId).ToList();
        var expiresAt = _clock.UtcNow.AddMinutes(_jwtConfig.ExpiryInMinutes);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.UserId.ToString()),
            new(ClaimTypes.Name, account.Username),
            new(ClaimTypes.Email, user.Email),
            new("AccountId", account.AccountId.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        // Add role claims
        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));

        var accessToken = GenerateJwtToken(claims, expiresAt);

        // Generate new claims for refresh token with different JTI
        var refreshClaims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.UserId.ToString()),
            new(ClaimTypes.Name, account.Username),
            new(ClaimTypes.Email, user.Email),
            new("AccountId", account.AccountId.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };
        refreshClaims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));

        var refreshTokenExpiresAt = _clock.UtcNow.AddDays(_jwtConfig.RefreshTokenExpiryInDays);
        var refreshToken = GenerateJwtToken(refreshClaims, refreshTokenExpiresAt);

        return new LoginResponseDto
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            ExpiresAt = expiresAt,
            User = new UserInfoDto
            {
                UserId = user.UserId,
                AccountId = account.AccountId,
                Username = account.Username,
                Email = user.Email,
                FullName = user.FullName ?? string.Empty,
                Phone = user.Phone,
                Address = user.Address,
                AvatarUrl = user.AvatarUrl,
                Status = user.Status,
                Roles = roles,
                SupportPriorityLevel = user.SupportPriorityLevel
            }
        };
    }

    private string GenerateJwtToken(IEnumerable<Claim> claims, DateTime expiresAt)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtConfig.SecretKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            _jwtConfig.Issuer,
            _jwtConfig.Audience,
            claims,
            expires: expiresAt,
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private ClaimsPrincipal ValidateToken(string token)
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        var key = Encoding.UTF8.GetBytes(_jwtConfig.SecretKey);

        var validationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = _jwtConfig.Issuer,
            ValidAudience = _jwtConfig.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(key),
            ClockSkew = TimeSpan.Zero
        };

        return tokenHandler.ValidateToken(token, validationParameters, out _);
    }

    private static byte[] HashPassword(string password)
    {
        // Using PBKDF2 with HMACSHA256
        const int iterations = 100000;
        const int keySize = 32;

        using var rng = RandomNumberGenerator.Create();
        var salt = new byte[16];
        rng.GetBytes(salt);

        using var pbkdf2 = new Rfc2898DeriveBytes(password, salt, iterations, HashAlgorithmName.SHA256);
        var hash = pbkdf2.GetBytes(keySize);

        // Combine salt and hash
        var result = new byte[salt.Length + hash.Length];
        Buffer.BlockCopy(salt, 0, result, 0, salt.Length);
        Buffer.BlockCopy(hash, 0, result, salt.Length, hash.Length);

        return result;
    }

    private static bool VerifyPassword(string password, byte[]? storedHash)
    {
        if (storedHash == null || storedHash.Length != 48) // 16 bytes salt + 32 bytes hash
            return false;

        const int iterations = 100000;
        const int keySize = 32;

        // Extract salt
        var salt = new byte[16];
        Buffer.BlockCopy(storedHash, 0, salt, 0, 16);

        // Hash input password
        using var pbkdf2 = new Rfc2898DeriveBytes(password, salt, iterations, HashAlgorithmName.SHA256);
        var hash = pbkdf2.GetBytes(keySize);

        // Compare hashes
        for (var i = 0; i < keySize; i++)
            if (storedHash[i + 16] != hash[i])
                return false;

        return true;
    }

    private static string GenerateOtp()
    {
        // Generate a 6-digit OTP
        using var rng = RandomNumberGenerator.Create();
        var bytes = new byte[4];
        rng.GetBytes(bytes);
        var number = BitConverter.ToUInt32(bytes, 0) % 1000000;
        return number.ToString("D6");
    }

    private static string GenerateVerificationToken()
    {
        // Generate a secure random verification token
        using var rng = RandomNumberGenerator.Create();
        var bytes = new byte[32];
        rng.GetBytes(bytes);
        return Convert.ToBase64String(bytes);
    }
}
