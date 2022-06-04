using System.Text.RegularExpressions;
using GagBot;
using GagBot.Services;
using Telegram.Bot;

var builder = WebApplication.CreateBuilder(args);

var services = builder.Services;
var configuration = builder.Configuration;

var botConfig = configuration.GetSection("BotConfiguration").Get<BotConfiguration>();

var dotEnvPath = Path.Combine(configuration.GetValue<string>(WebHostDefaults.ContentRootKey), ".env");
ReadAndSetEnvironmentVariables(dotEnvPath);

botConfig.Token = Environment.GetEnvironmentVariable("BOT_TOKEN");

services.AddTransient<BotConfiguration>(_ => botConfig);

services.AddHostedService<ConfigureWebhook>();

services.AddHttpClient("tgwebhook")
    .AddTypedClient<ITelegramBotClient>(httpClient => new TelegramBotClient(botConfig.Token!, httpClient));

services.AddScoped<HandleUpdateService>();

services.AddControllers().AddNewtonsoftJson();

var app = builder.Build();

app.UseRouting();

app.UseCors();

app.UseEndpoints(endpoints =>
{
    var token = botConfig.Token;
    endpoints.MapControllerRoute(name: "tgwebhook", 
                                pattern: $"bot/{token}",
                                new {controller = "Webhook", action = "Post"});

    endpoints.MapControllers();
});

app.Run();

void ReadAndSetEnvironmentVariables(string dotEnvPath)
{
    var variables = File.ReadAllLines(dotEnvPath)
        .Where(line => Regex.IsMatch(line, @"^[a-zA-Z][a-zA-Z0-9]*_?[a-zA-Z]*=.+$"));

    foreach (var variable in variables)
    { 
        string[] data = variable.Split('=');

        (string variableName, string variableValue) = (data[0], data[1]);

        Environment.SetEnvironmentVariable(variableName, variableValue);
    }
}