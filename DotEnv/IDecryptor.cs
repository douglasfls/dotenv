namespace DotEnv;

/// <summary>Decrypts a dotenvx encrypted value.</summary>
public interface IDecryptor
{
    /// <summary>Decrypts an encrypted dotenv value.</summary>
    /// <param name="publicKey">The public key declared by the dotenv file.</param>
    /// <param name="privateKey">The matching private key.</param>
    /// <param name="value">The encrypted value.</param>
    /// <returns>The plaintext value.</returns>
    string Decrypt(string publicKey, string privateKey, string value);
}
