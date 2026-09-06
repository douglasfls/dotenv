using DotEnv;

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddDotEnv(".env", (configuration) =>
{
    // configuration.Decryptor = new MyDecryptor();
}).AddEnvironmentVariables();

builder.Services.AddOpenApi();

var app = builder.Build();

app.UseHttpsRedirection();

app.MapGet("/", (IConfiguration configuration) =>
    {
        return configuration.GetValue<string>("with_equal_sign");
    });

app.Run();

public class MyDecryptor : IDecryptor
{
    public string Decrypt(string publicKey, string privateKey, string value)
    {
        throw new NotImplementedException();
    }
}