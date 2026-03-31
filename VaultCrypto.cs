using System;
using System.Security.Cryptography;
using System.Text;

namespace PassNotes;

public static class VaultCrypto
{
    private static readonly byte[] Magic = Encoding.ASCII.GetBytes("PNV1");

    public static byte[] Encrypt(string password, byte[] plaintext)
    {
        byte[] salt = RandomNumberGenerator.GetBytes(16);
        byte[] nonce = RandomNumberGenerator.GetBytes(12);

        using var kdf = new Rfc2898DeriveBytes(password, salt, 120_000, HashAlgorithmName.SHA256);
        byte[] key = kdf.GetBytes(32);

        byte[] ciphertext = new byte[plaintext.Length];
        byte[] tag = new byte[16];

        try
        {
            using var aes = new AesGcm(key, 16);
            aes.Encrypt(nonce, plaintext, ciphertext, tag);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
        }

        byte[] outBytes = new byte[Magic.Length + salt.Length + nonce.Length + tag.Length + ciphertext.Length];
        int o = 0;
        Buffer.BlockCopy(Magic, 0, outBytes, o, Magic.Length); o += Magic.Length;
        Buffer.BlockCopy(salt, 0, outBytes, o, salt.Length); o += salt.Length;
        Buffer.BlockCopy(nonce, 0, outBytes, o, nonce.Length); o += nonce.Length;
        Buffer.BlockCopy(tag, 0, outBytes, o, tag.Length); o += tag.Length;
        Buffer.BlockCopy(ciphertext, 0, outBytes, o, ciphertext.Length);

        CryptographicOperations.ZeroMemory(ciphertext);
        return outBytes;
    }

    public static byte[] Decrypt(string password, byte[] blob)
    {
        if (blob.Length < 4 + 16 + 12 + 16) throw new CryptographicException("Invalid vault file.");

        for (int i = 0; i < Magic.Length; i++)
            if (blob[i] != Magic[i]) throw new CryptographicException("Invalid vault file.");

        int o = Magic.Length;

        byte[] salt = new byte[16];
        Buffer.BlockCopy(blob, o, salt, 0, salt.Length); o += salt.Length;

        byte[] nonce = new byte[12];
        Buffer.BlockCopy(blob, o, nonce, 0, nonce.Length); o += nonce.Length;

        byte[] tag = new byte[16];
        Buffer.BlockCopy(blob, o, tag, 0, tag.Length); o += tag.Length;

        int ctLen = blob.Length - o;
        byte[] ciphertext = new byte[ctLen];
        Buffer.BlockCopy(blob, o, ciphertext, 0, ctLen);

        using var kdf = new Rfc2898DeriveBytes(password, salt, 120_000, HashAlgorithmName.SHA256);
        byte[] key = kdf.GetBytes(32);

        byte[] plaintext = new byte[ctLen];

        try
        {
            using var aes = new AesGcm(key, 16);
            aes.Decrypt(nonce, ciphertext, tag, plaintext);
            return plaintext;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
            CryptographicOperations.ZeroMemory(ciphertext);
        }
    }
}
