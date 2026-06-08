using ApiSign.AspNetCore.Abstractions;
using ApiSign.AspNetCore.Models;

namespace ApiSign.SampleWeb.Infrastructure;

public sealed class SampleAppSecretProvider(IConfiguration configuration) : IAppSecretProvider
{
    private readonly IConfiguration _configuration = configuration;

    public Task<AppSecretInfo?> GetAppSecretAsync(string appId)
    {
        var secret = _configuration[$"AppSecrets:{appId}:SecretKey"];
        if (string.IsNullOrWhiteSpace(secret))
        {
            return Task.FromResult<AppSecretInfo?>(null);
        }

        var enabled = _configuration.GetValue($"AppSecrets:{appId}:IsEnabled", true);
        return Task.FromResult<AppSecretInfo?>(new AppSecretInfo
        {
            AppId = appId,
            SecretKey = secret,
            IsEnabled = enabled,
            Algorithm = SignAlgorithm.HMACSHA256,
        });
    }
}