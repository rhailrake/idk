using Idk.Bot.Configuration;
using Idk.Bot.Hosting;
using Microsoft.Extensions.Hosting;

var options = BotOptions.FromEnvironment();
var builder = Host.CreateApplicationBuilder(args);

builder.ConfigureIdkLogging(options);
builder.Services.AddIdkBot(options);

await builder.Build().RunAsync();
