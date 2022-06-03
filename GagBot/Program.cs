using GagBot;
using GagBot.Services;
using Telegram.Bot;

var builder = WebApplication.CreateBuilder(args);

var services = builder.Services;
var configuration = builder.Configuration;

var botConfig = configuration.GetSection("BotConfiguration").Get<BotConfiguration>();

services.AddHostedService<ConfigureWebhook>();
services.AddHttpClient("tgwebhook")
    .AddTypedClient<ITelegramBotClient>(httpClient => new TelegramBotClient(botConfig.Token!, httpClient));
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
