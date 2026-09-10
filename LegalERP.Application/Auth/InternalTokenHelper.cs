using System;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace LegalERP.Application.Auth;

public static class InternalTokenHelper
{
    private static readonly byte[] SecretKey = Encoding.UTF8.GetBytes("LegalERP-SuperSecretInternalKey-2026-LawFirmSecurityKey-987654321");

    public static string GenerateToken(CurrentUserDto user)
    {
        var payload = JsonSerializer.Serialize(new TokenPayload(
            user.Id,
            user.FullName,
            user.Email,
            user.Role,
            DateTimeOffset.UtcNow.AddDays(30).ToUnixTimeSeconds()
        ));

        var payloadBytes = Encoding.UTF8.GetBytes(payload);
        var payloadBase64 = Convert.ToBase64String(payloadBytes);

        using var hmac = new HMACSHA256(SecretKey);
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payloadBase64));
        var signatureBase64 = Convert.ToBase64String(hash);

        return $"{payloadBase64}.{signatureBase64}";
    }

    public static CurrentUserDto? ValidateToken(string token)
    {
        if (string.IsNullOrWhiteSpace(token)) return null;

        var parts = token.Split('.');
        if (parts.Length != 2) return null;

        var payloadBase64 = parts[0];
        var signatureBase64 = parts[1];

        using var hmac = new HMACSHA256(SecretKey);
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payloadBase64));
        var expectedSignature = Convert.ToBase64String(hash);

        if (!CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(signatureBase64),
            Encoding.UTF8.GetBytes(expectedSignature)))
        {
            return null;
        }

        try
        {
            var payloadJson = Encoding.UTF8.GetString(Convert.FromBase64String(payloadBase64));
            var data = JsonSerializer.Deserialize<TokenPayload>(payloadJson);
            if (data == null) return null;

            if (DateTimeOffset.UtcNow.ToUnixTimeSeconds() > data.Exp)
                return null; // Expired

            return new CurrentUserDto(data.Id, data.FullName, data.Email, data.Role, null);
        }
        catch
        {
            return null;
        }
    }

    private record TokenPayload(Guid Id, string FullName, string Email, string Role, long Exp);
}
