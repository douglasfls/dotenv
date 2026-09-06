
using System.Security.Cryptography;
using System.Text;
using Org.BouncyCastle.Asn1.Sec;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Modes;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Math;

namespace DotEnv;

/// <summary>Decrypts dotenvx ECIES payloads using secp256k1 and AES-256-GCM.</summary>
public sealed class DotEnvxDefaultDecryptor : IDecryptor
{
    // eciesjs, used by dotenvx, emits an uncompressed ephemeral key and a 16-byte nonce.
    private const int PublicKeyLength = 65;
    private const int NonceLength = 16;
    private const int TagLength = 16;

    /// <inheritdoc />
    public string Decrypt(string dotEnvPublicKey, string privateKey, string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(privateKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        var payload = Convert.FromBase64String(RemoveEncryptedPrefix(value));

        if (payload.Length <= PublicKeyLength + NonceLength + TagLength)
            throw new CryptographicException("Invalid dotenvx encrypted payload.");

        var ephemeralPublicKey = payload.AsSpan(0, PublicKeyLength);
        var nonce = payload.AsSpan(PublicKeyLength, NonceLength);
        var tagAndCiphertext = payload.AsSpan(PublicKeyLength + NonceLength);
        var aesKey = DeriveSharedKey(privateKey, ephemeralPublicKey);

        try
        {
            // .NET's AesGcm accepts only 12-byte nonces; dotenvx/eciesjs uses 16.
#pragma warning disable CS0618 // Required for the 16-byte dotenvx GCM nonce.
            var cipher = new GcmBlockCipher(new AesEngine());
#pragma warning restore CS0618
            cipher.Init(false, new AeadParameters(new KeyParameter(aesKey), TagLength * 8, nonce.ToArray()));
            // eciesjs stores tag || ciphertext, while BouncyCastle consumes ciphertext || tag.
            var input = new byte[tagAndCiphertext.Length];
            tagAndCiphertext[TagLength..].CopyTo(input);
            tagAndCiphertext[..TagLength].CopyTo(input.AsSpan(tagAndCiphertext.Length - TagLength));
            var plaintext = new byte[cipher.GetOutputSize(input.Length)];
            var length = cipher.ProcessBytes(input, 0, input.Length, plaintext, 0);
            length += cipher.DoFinal(plaintext, length);
            return Encoding.UTF8.GetString(plaintext, 0, length);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(aesKey);
        }
    }

    private static byte[] DeriveSharedKey(string privateKeyHex, ReadOnlySpan<byte> ephemeralPublicKey)
    {
        var curve = SecNamedCurves.GetByName("secp256k1");
        var domain = new ECDomainParameters(curve.Curve, curve.G, curve.N, curve.H);
        var privateKeyBytes = Convert.FromHexString(privateKeyHex);

        try
        {
            var d = new BigInteger(1, privateKeyBytes);
            var publicPoint = curve.Curve.DecodePoint(ephemeralPublicKey.ToArray());
            var sharedPoint = publicPoint.Multiply(d).Normalize().GetEncoded(false);
            var master = new byte[ephemeralPublicKey.Length + sharedPoint.Length];
            ephemeralPublicKey.CopyTo(master);
            sharedPoint.CopyTo(master, ephemeralPublicKey.Length);
            try
            {
                var key = new byte[32];
                HKDF.DeriveKey(HashAlgorithmName.SHA256, master, key, ReadOnlySpan<byte>.Empty, ReadOnlySpan<byte>.Empty);
                return key;
            }
            finally
            {
                CryptographicOperations.ZeroMemory(sharedPoint);
                CryptographicOperations.ZeroMemory(master);
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(privateKeyBytes);
        }
    }

    private static string RemoveEncryptedPrefix(string value)
    {
        const string prefix = "encrypted:";

        return value.StartsWith(
            prefix,
            StringComparison.Ordinal)
            ? value[prefix.Length..]
            : value;
    }
}
