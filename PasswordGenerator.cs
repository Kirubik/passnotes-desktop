using System;
using System.Security.Cryptography;
using System.Text;

namespace PassNotes;

public static class PasswordGenerator
{
    private const string Alphabet = "abcdefghijkmnopqrstuvwxyzABCDEFGHJKLMNPQRSTUVWXYZ23456789!@#$%^&*()-_=+[]{};:,.?";

    public const int MinLength = 3;
    public const int MaxLength = 32;

    public static string Generate(int length = 16)
    {
        if (length < MinLength || length > MaxLength)
            throw new ArgumentOutOfRangeException(nameof(length), $"Password length must be between {MinLength} and {MaxLength}.");

        var bytes = RandomNumberGenerator.GetBytes(length);
        var sb = new StringBuilder(length);
        for (var i = 0; i < length; i++)
            sb.Append(Alphabet[bytes[i] % Alphabet.Length]);
        return sb.ToString();
    }
}
