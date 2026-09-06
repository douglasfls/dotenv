using Microsoft.Extensions.Configuration;

namespace DotEnv.Tests;

public sealed class DotEnvTests
{
    [Fact]
    public void ParserSupportsDotenvSyntaxAndExpansion()
    {
        const string source = """
            export HOST=example.com # comment
            URL="https://${HOST}/api?a=b"
            LITERAL='${HOST}'
            MULTILINE="first
            second"
            DEFAULT=${MISSING:-fallback}
            """;
        var values = DotEnvParser.Parse(source);
        Assert.Equal("example.com", values["HOST"]);
        Assert.Equal("https://example.com/api?a=b", values["URL"]);
        Assert.Equal("${HOST}", values["LITERAL"]);
        Assert.Equal("first\nsecond", values["MULTILINE"]);
        Assert.Equal("fallback", values["DEFAULT"]);
    }

    [Fact]
    public void DecryptorReadsPublishedDotenvxVector()
    {
        const string encrypted = "encrypted:BEiK4SwVe4rznIrZKW7qcnEBxo6Qxy0gv8xXiPN90N1jjhwkyVqzgoszZEI00tEeRWHsfQyJbCVy11qOEkkmERWbvOBzHve8/7WmVwyo5Z3HIOhlI45C+zmFgqmS2zMw9GPzHd9e";
        const string privateKey = "a4547dcd9d3429615a3649bb79e87edb62ee6a74b007075e9141ae44f5fb412c";
        var plaintext = new DotEnvxDefaultDecryptor().Decrypt(string.Empty, privateKey, encrypted);
        Assert.Equal("World", plaintext);
    }

    [Fact]
    public void ProviderLoadsEnvironmentOverrideWithoutReplacingExistingProcessValue()
    {
        var directory = Directory.CreateTempSubdirectory("dotenv-tests-");
        var variableName = $"DOTENV_TEST_{Guid.NewGuid():N}";
        try
        {
            File.WriteAllText(Path.Combine(directory.FullName, ".env"), $"{variableName}=base\nONLY_BASE=yes");
            File.WriteAllText(Path.Combine(directory.FullName, ".env.Test"), $"{variableName}=override");
            Environment.SetEnvironmentVariable(variableName, "process");
            var configuration = new ConfigurationBuilder().AddDotEnv(Path.Combine(directory.FullName, ".env"), o => o.EnvironmentName = "Test").Build();
            Assert.Equal("override", configuration[variableName]);
            Assert.Equal("yes", configuration["ONLY_BASE"]);
            Assert.Equal("process", Environment.GetEnvironmentVariable(variableName));
        }
        finally
        {
            Environment.SetEnvironmentVariable(variableName, null);
            directory.Delete(true);
        }
    }

    [Fact]
    public void ProviderDecryptsFileUsingDotenvKeys()
    {
        const string encrypted = "encrypted:BEiK4SwVe4rznIrZKW7qcnEBxo6Qxy0gv8xXiPN90N1jjhwkyVqzgoszZEI00tEeRWHsfQyJbCVy11qOEkkmERWbvOBzHve8/7WmVwyo5Z3HIOhlI45C+zmFgqmS2zMw9GPzHd9e";
        const string privateKey = "a4547dcd9d3429615a3649bb79e87edb62ee6a74b007075e9141ae44f5fb412c";
        var directory = Directory.CreateTempSubdirectory("dotenv-tests-");
        try
        {
            File.WriteAllText(Path.Combine(directory.FullName, ".env"), $"HELLO=\"{encrypted}\"");
            File.WriteAllText(Path.Combine(directory.FullName, ".env.keys"), $"DOTENV_PRIVATE_KEY={privateKey}");
            var configuration = new ConfigurationBuilder()
                .AddDotEnv(Path.Combine(directory.FullName, ".env"), o => o.SetEnvironmentVariables = false)
                .Build();
            Assert.Equal("World", configuration["HELLO"]);
        }
        finally
        {
            directory.Delete(true);
        }
    }
}
