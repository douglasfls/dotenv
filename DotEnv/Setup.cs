using Microsoft.Extensions.Configuration;

namespace DotEnv;

/// <summary>Provides dotenvx-compatible configuration extensions.</summary>
public static class Setup
{
    /// <summary>Controls how dotenv files are loaded.</summary>
    public sealed class DotEnvOptions
    {
        /// <summary>Gets or sets the decryptor used for <c>encrypted:</c> values.</summary>
        public IDecryptor Decryptor { get; set; } = new DotEnvxDefaultDecryptor();
        /// <summary>Gets or sets an explicit dotenvx public key.</summary>
        public string? PublicKey { get; set; }
        /// <summary>Gets or sets an explicit dotenvx private key.</summary>
        public string? PrivateKey { get; set; }
        /// <summary>Gets or sets the path to the dotenvx keys file.</summary>
        public string? KeysFilePath { get; set; }
        /// <summary>Gets or sets the environment name used to load a sibling environment file.</summary>
        public string? EnvironmentName { get; set; } = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
            ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
        /// <summary>Gets or sets whether a missing base file is allowed.</summary>
        public bool Optional { get; set; } = true;
        /// <summary>Gets or sets whether values are also written to the process environment.</summary>
        public bool SetEnvironmentVariables { get; set; } = true;
        /// <summary>Gets or sets whether dotenv values replace existing process values.</summary>
        public bool Overload { get; set; }
    }

    /// <summary>Adds a dotenvx-compatible file to the configuration builder.</summary>
    /// <param name="configuration">The configuration builder.</param>
    /// <param name="filePath">The base dotenv file path.</param>
    /// <param name="configure">An optional options callback.</param>
    /// <returns>The original configuration builder.</returns>
    public static IConfigurationBuilder AddDotEnv(this IConfigurationBuilder configuration, string filePath = ".env", Action<DotEnvOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        var options = new DotEnvOptions();
        configure?.Invoke(options);
        configuration.Add(new DotEnvConfigurationSource(Path.GetFullPath(filePath), options));
        return configuration;
    }

    private sealed class DotEnvConfigurationSource(string path, DotEnvOptions options) : IConfigurationSource
    {
        public IConfigurationProvider Build(IConfigurationBuilder builder) => new DotEnvConfigurationProvider(path, options);
    }

    private sealed class DotEnvConfigurationProvider(string path, DotEnvOptions options) : ConfigurationProvider
    {
        public override void Load()
        {
            if (!File.Exists(path) && !options.Optional)
                throw new FileNotFoundException("The dotenv file was not found.", path);
            var values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            LoadFile(path, values);
            if (!string.IsNullOrWhiteSpace(options.EnvironmentName))
                LoadFile($"{path}.{options.EnvironmentName}", values);
            Data = values;
            if (!options.SetEnvironmentVariables) return;
            foreach (var pair in values)
                if (options.Overload || Environment.GetEnvironmentVariable(pair.Key) is null)
                    Environment.SetEnvironmentVariable(pair.Key, pair.Value);
        }

        private void LoadFile(string dotenvPath, Dictionary<string, string?> destination)
        {
            if (!File.Exists(dotenvPath)) return;
            var parsed = DotEnvParser.Parse(File.ReadAllText(dotenvPath), destination);
            var keyName = GetPrivateKeyName(dotenvPath);
            var keysPath = options.KeysFilePath ?? Path.Combine(Path.GetDirectoryName(path)!, ".env.keys");
            var keys = File.Exists(keysPath) ? DotEnvParser.Parse(File.ReadAllText(keysPath)) : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var privateKey = options.PrivateKey ?? Environment.GetEnvironmentVariable(keyName) ?? keys.GetValueOrDefault(keyName);
            var publicKey = options.PublicKey ?? parsed.GetValueOrDefault(keyName.Replace("PRIVATE", "PUBLIC", StringComparison.Ordinal)) ?? string.Empty;
            foreach (var pair in parsed)
            {
                var value = pair.Value;
                if (value.StartsWith("encrypted:", StringComparison.Ordinal))
                {
                    if (string.IsNullOrWhiteSpace(privateKey))
                        throw new InvalidOperationException($"Encrypted value '{pair.Key}' requires {keyName}.");
                    value = options.Decryptor.Decrypt(publicKey, privateKey, value);
                }
                destination[pair.Key] = value;
            }
        }

        private static string GetPrivateKeyName(string dotenvPath)
        {
            var name = Path.GetFileName(dotenvPath);
            if (name.Equals(".env", StringComparison.OrdinalIgnoreCase)) return "DOTENV_PRIVATE_KEY";
            const string prefix = ".env.";
            var suffix = name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ? name[prefix.Length..] : name;
            var normalized = new string(suffix.Select(c => char.IsLetterOrDigit(c) ? char.ToUpperInvariant(c) : '_').ToArray());
            return $"DOTENV_PRIVATE_KEY_{normalized}";
        }
    }
}
