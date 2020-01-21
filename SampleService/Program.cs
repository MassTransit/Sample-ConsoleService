using System;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
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
        public static AppConfig AppConfig { get; set; }

        static async Task Main(string[] args)
        {
            var isService = !(Debugger.IsAttached || args.Contains("--console"));

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

                    services.AddHostedService<MassTransitConsoleHostedService>();
                    services.AddHostedService<CheckTheTimeService>();
                })
                .ConfigureLogging((hostingContext, logging) =>
                {
                    logging.AddConfiguration(hostingContext.Configuration.GetSection("Logging"));
                    logging.AddConsole();
                });

            if (isService)
            {
                await builder.UseWindowsService().Build().RunAsync();
                //await builder.UseSystemd().Build().RunAsync(); // For Linux, replace the nuget package: "Microsoft.Extensions.Hosting.WindowsServices" with "Microsoft.Extensions.Hosting.Systemd", and then use this line instead
            }
            else
            {
                await builder.RunConsoleAsync();
            }
        }

        static IBusControl ConfigureBus(IServiceProvider provider)
        {
            AppConfig = provider.GetRequiredService<IOptions<AppConfig>>().Value;

            X509Certificate2 x509Certificate2 = null;

            X509Store store = new X509Store(StoreName.My, StoreLocation.LocalMachine);
            store.Open(OpenFlags.ReadOnly);

            try
            {
                X509Certificate2Collection certificatesInStore = store.Certificates;

                x509Certificate2 = certificatesInStore.OfType<X509Certificate2>()
                    .FirstOrDefault(cert => cert.Thumbprint?.ToLower() == AppConfig.SSLThumbprint?.ToLower());
            }
            finally
            {
                store.Close();
            }

            return Bus.Factory.CreateUsingRabbitMq(cfg =>
            {
                var host = cfg.Host(AppConfig.Host, AppConfig.VirtualHost, h =>
                {
                    h.Username(AppConfig.Username);
                    h.Password(AppConfig.Password);

                    if (AppConfig.SSLActive)
                    {
                        h.UseSsl(ssl =>
                        {
                            ssl.ServerName = Dns.GetHostName();
                            ssl.AllowPolicyErrors(SslPolicyErrors.RemoteCertificateNameMismatch);
                            ssl.Certificate = x509Certificate2;
                            ssl.Protocol = SslProtocols.Tls12;
                            ssl.CertificateSelectionCallback = CertificateSelectionCallback;
                        });
                    }
                });

                cfg.ConfigureEndpoints(provider);
            });
        }

        private static X509Certificate CertificateSelectionCallback(object sender, string targethost, X509CertificateCollection localcertificates, X509Certificate remotecertificate, string[] acceptableissuers)
        {
            var serverCertificate = localcertificates.OfType<X509Certificate2>()
                                    .FirstOrDefault(cert => cert.Thumbprint.ToLower() == AppConfig.SSLThumbprint.ToLower());

            return serverCertificate ?? throw new Exception("Wrong certificate");
        }
    }
}