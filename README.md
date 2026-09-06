# DotEnv for .NET

A .NET configuration provider for dotenv files created and encrypted by [dotenvx](https://dotenvx.com/).

## Usage

```csharp
using DotEnv;

var builder = WebApplication.CreateBuilder(args);
builder.Configuration
    .AddDotEnv()                 // .env, then .env.{DOTNET_ENVIRONMENT}
    .AddEnvironmentVariables(); // deployment variables take final precedence
```

Encrypted values are decrypted automatically. Keep the matching dotenvx private key in `.env.keys` for local development, or provide `DOTENV_PRIVATE_KEY` through your deployment environment.

```dotenv
DOTENV_PUBLIC_KEY="03..."
API_KEY="encrypted:..."
```

Environment-specific files use dotenvx key names such as `DOTENV_PRIVATE_KEY_PRODUCTION` for `.env.production`.

## Options

```csharp
builder.Configuration.AddDotEnv(".env", options =>
{
    options.EnvironmentName = builder.Environment.EnvironmentName;
    options.KeysFilePath = "/run/secrets/.env.keys";
    options.Optional = false;
    options.SetEnvironmentVariables = true;
    options.Overload = false;
});
```

`Overload` has the same meaning as dotenvx's `--overload`: when `false`, existing process environment values are preserved. The configuration provider still contains the parsed file values, so add `.AddEnvironmentVariables()` afterward when deployment variables should override configuration too.

The parser supports quoted and multiline values, inline comments, `export`, equals signs in values, and dotenvx variable expansion including `${NAME:-default}`. You can parse text directly with `DotEnvParser.Parse(source)`.

Command substitution and external secret URI resolution (`$(command)`, `bw://`, and `op://`) are intentionally not executed by this in-process configuration provider.
