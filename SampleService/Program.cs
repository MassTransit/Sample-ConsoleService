using System;
using System.Threading.Tasks;
using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SampleService.Contracts;

namespace SampleService
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var builder = new HostBuilder()
                .ConfigureAppConfiguration((hostingContext, config) =>
                {
                    config.AddJsonFile("appsettings.json", optional: true);
                    config.AddEnvironmentVariables();

                    if (args != null)
                        config.AddCommandLine(args);
                })
                .ConfigureServices((hostContext, services) =>
                {
                    services.Configure<AppConfig>(hostContext.Configuration.GetSection("AppConfig"));

                    services.AddMassTransit(cfg =>
                    {
                        cfg.AddConsumer<TimeConsumer>();
                        cfg.AddBus(ConfigureBus);
                        cfg.AddRequestClient<IsItTime>();
                    });

                    services.AddSingleton<IHostedService, MassTransitConsoleHostedService>();
                    services.AddSingleton<IHostedService, CheckTheTimeService>();
                })
                .ConfigureLogging((hostingContext, logging) =>
                {
                    logging.AddConfiguration(hostingContext.Configuration.GetSection("Logging"));
                    logging.AddConsole();
                });

            await builder.RunConsoleAsync();
        }

        static IBusControl ConfigureBus(IServiceProvider provider)
        {
            var options = provider.GetRequiredService<IOptions<AppConfig>>().Value;

            return Bus.Factory.CreateUsingRabbitMq(cfg =>
            {
                var host = cfg.Host(options.Host, options.VirtualHost, h =>
                {
                    h.Username(options.Username);
                    h.Password(options.Password);
                });

                cfg.ConfigureEndpoints(provider);
            });
        }
    }
}