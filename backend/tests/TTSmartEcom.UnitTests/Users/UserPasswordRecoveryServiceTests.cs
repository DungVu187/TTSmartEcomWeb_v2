using TTSmartEcom.Application.Abstractions.Users;
using TTSmartEcom.Application.Users;

namespace TTSmartEcom.UnitTests.Users;

public sealed class UserPasswordRecoveryServiceTests
{
    private static readonly DateTimeOffset Now = new(2030, 1, 2, 3, 4, 5, TimeSpan.Zero);

    [Fact]
    public async Task RequestReset_NormalizesPhone_PersistsSixDigitOtp_AndSendsMaskedEmail()
    {
        FakeUserRepository repository = new()
        {
            RecoveryUser = new PasswordRecoveryUser("user-1", "0987654321", "customer@example.com", "Customer"),
        };
        FakeEmailSender email = new(PasswordResetEmailDeliveryStatus.Delivered);
        UserPasswordRecoveryService service = CreateService(repository, email);

        PasswordResetRequestResult result = await service.RequestResetAsync(
            "+84 987.654.321",
            CancellationToken.None);

        Assert.Equal(PasswordResetRequestStatus.Success, result.Status);
        Assert.Equal("0987654321", repository.LastIdentifier);
        Assert.Equal("0987654321", result.Phone);
        Assert.Equal("cu***@example.com", result.MaskedEmail);
        Assert.Matches("^[0-9]{6}$", repository.StoredOtp);
        Assert.Equal(Now.AddMinutes(5), repository.StoredExpiresAt);
        Assert.NotNull(email.LastMessage);
        Assert.Equal(repository.StoredOtp, email.LastMessage.Otp);
        Assert.Equal(TimeSpan.FromMinutes(5), email.LastMessage.ValidFor);
    }

    [Fact]
    public async Task RequestReset_WhenEmailMissing_DoesNotPersistOrCallProvider()
    {
        FakeUserRepository repository = new()
        {
            RecoveryUser = new PasswordRecoveryUser("user-1", "0987654321", null, "Customer"),
        };
        FakeEmailSender email = new(PasswordResetEmailDeliveryStatus.Delivered);
        UserPasswordRecoveryService service = CreateService(repository, email);

        PasswordResetRequestResult result = await service.RequestResetAsync(
            "0987654321",
            CancellationToken.None);

        Assert.Equal(PasswordResetRequestStatus.EmailMissing, result.Status);
        Assert.Null(repository.StoredOtp);
        Assert.Null(email.LastMessage);
    }

    [Fact]
    public async Task RequestReset_WhenProviderUnavailable_ClearsPersistedOtp_AndDoesNotReportSuccess()
    {
        FakeUserRepository repository = new()
        {
            RecoveryUser = new PasswordRecoveryUser("user-1", "0987654321", "customer@example.com", "Customer"),
        };
        FakeEmailSender email = new(PasswordResetEmailDeliveryStatus.Unavailable);
        UserPasswordRecoveryService service = CreateService(repository, email);

        PasswordResetRequestResult result = await service.RequestResetAsync(
            "CUSTOMER@EXAMPLE.COM",
            CancellationToken.None);

        Assert.Equal(PasswordResetRequestStatus.ProviderUnavailable, result.Status);
        Assert.Equal("customer@example.com", repository.LastIdentifier);
        Assert.Equal(repository.StoredOtp, repository.ClearedOtp);
    }

    [Fact]
    public async Task Reset_WithValidOtp_HashesPasswordAndRequestsAtomicTokenRotationAndOtpClear()
    {
        FakeUserRepository repository = new()
        {
            RecoveryUser = new PasswordRecoveryUser("user-1", "0987654321", "customer@example.com", "Customer"),
            ResetAccepted = true,
        };
        FakePasswordHasher hasher = new();
        UserPasswordRecoveryService service = CreateService(
            repository,
            new FakeEmailSender(PasswordResetEmailDeliveryStatus.Delivered),
            hasher);

        PasswordResetResult result = await service.ResetAsync(
            "CUSTOMER@EXAMPLE.COM",
            "123456",
            "newPassword123",
            CancellationToken.None);

        Assert.Equal(PasswordResetStatus.Success, result.Status);
        Assert.Equal("newPassword123", hasher.LastPlaintext);
        Assert.Equal("hash:newPassword123", repository.NewPasswordHash);
        Assert.Equal("123456", repository.ResetOtp);
        Assert.Equal(Now, repository.ResetNow);
        Assert.Equal(Now, repository.PasswordChangedAt);
        Assert.Matches("^[0-9a-f]{64}$", repository.ReplacementLoginToken);
    }

    [Theory]
    [InlineData("12345")]
    [InlineData("1234567")]
    [InlineData("12A456")]
    public async Task Reset_WithMalformedOtp_RejectsBeforeHashingOrPersistence(string otp)
    {
        FakeUserRepository repository = new()
        {
            RecoveryUser = new PasswordRecoveryUser("user-1", "0987654321", "customer@example.com", "Customer"),
        };
        FakePasswordHasher hasher = new();
        UserPasswordRecoveryService service = CreateService(
            repository,
            new FakeEmailSender(PasswordResetEmailDeliveryStatus.Delivered),
            hasher);

        PasswordResetResult result = await service.ResetAsync(
            "0987654321",
            otp,
            "newPassword123",
            CancellationToken.None);

        Assert.Equal(PasswordResetStatus.OtpInvalid, result.Status);
        Assert.Null(hasher.LastPlaintext);
        Assert.Null(repository.ResetOtp);
    }

    [Fact]
    public async Task Reset_WhenAtomicOtpCheckFails_ReturnsOtpInvalid()
    {
        FakeUserRepository repository = new()
        {
            RecoveryUser = new PasswordRecoveryUser("user-1", "0987654321", "customer@example.com", "Customer"),
            ResetAccepted = false,
        };
        UserPasswordRecoveryService service = CreateService(
            repository,
            new FakeEmailSender(PasswordResetEmailDeliveryStatus.Delivered));

        PasswordResetResult result = await service.ResetAsync(
            "0987654321",
            "123456",
            "newPassword123",
            CancellationToken.None);

        Assert.Equal(PasswordResetStatus.OtpInvalid, result.Status);
    }

    private static UserPasswordRecoveryService CreateService(
        FakeUserRepository repository,
        FakeEmailSender email,
        FakePasswordHasher? hasher = null) =>
        new(repository, email, hasher ?? new FakePasswordHasher(), new FixedTimeProvider(Now));

    private sealed class FakeUserRepository : IUserRepository
    {
        public PasswordRecoveryUser? RecoveryUser { get; init; }
        public bool ResetAccepted { get; init; }
        public string? LastIdentifier { get; private set; }
        public string? StoredOtp { get; private set; }
        public DateTimeOffset? StoredExpiresAt { get; private set; }
        public string? ClearedOtp { get; private set; }
        public string? ResetOtp { get; private set; }
        public DateTimeOffset? ResetNow { get; private set; }
        public string? NewPasswordHash { get; private set; }
        public string? ReplacementLoginToken { get; private set; }
        public DateTimeOffset? PasswordChangedAt { get; private set; }

        public Task<PasswordRecoveryUser?> FindForPasswordRecoveryAsync(
            string identifier,
            CancellationToken cancellationToken)
        {
            LastIdentifier = identifier;
            return Task.FromResult(RecoveryUser);
        }

        public Task<bool> StorePasswordResetOtpAsync(
            string userId,
            string otp,
            DateTimeOffset expiresAt,
            CancellationToken cancellationToken)
        {
            StoredOtp = otp;
            StoredExpiresAt = expiresAt;
            return Task.FromResult(true);
        }

        public Task<bool> ClearPasswordResetOtpAsync(
            string userId,
            string expectedOtp,
            CancellationToken cancellationToken)
        {
            ClearedOtp = expectedOtp;
            return Task.FromResult(true);
        }

        public Task<bool> ResetPasswordWithOtpAsync(
            string userId,
            string expectedOtp,
            DateTimeOffset now,
            string passwordHash,
            string replacementLoginToken,
            DateTimeOffset passwordChangedAt,
            CancellationToken cancellationToken)
        {
            ResetOtp = expectedOtp;
            ResetNow = now;
            NewPasswordHash = passwordHash;
            ReplacementLoginToken = replacementLoginToken;
            PasswordChangedAt = passwordChangedAt;
            return Task.FromResult(ResetAccepted);
        }

        public Task<UserRecord?> FindByLoginAsync(string identifier, CancellationToken cancellationToken) =>
            Task.FromResult<UserRecord?>(null);

        public Task<UserRecord?> ConsumeAutologinTokenAsync(
            string token,
            string replacementToken,
            CancellationToken cancellationToken) => Task.FromResult<UserRecord?>(null);

        public Task<TTSmartEcom.Application.Abstractions.Authentication.UserIdentitySnapshot?> FindIdentityAsync(
            string userId,
            CancellationToken cancellationToken) =>
            Task.FromResult<TTSmartEcom.Application.Abstractions.Authentication.UserIdentitySnapshot?>(null);
    }

    private sealed class FakeEmailSender(PasswordResetEmailDeliveryStatus deliveryStatus) : IPasswordResetEmailSender
    {
        public PasswordResetEmailMessage? LastMessage { get; private set; }

        public Task<PasswordResetEmailDeliveryStatus> SendAsync(
            PasswordResetEmailMessage message,
            CancellationToken cancellationToken)
        {
            LastMessage = message;
            return Task.FromResult(deliveryStatus);
        }
    }

    private sealed class FakePasswordHasher : IPasswordHashWriter
    {
        public string? LastPlaintext { get; private set; }

        public string Hash(string password)
        {
            LastPlaintext = password;
            return $"hash:{password}";
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset value) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => value;
    }
}
