using System.Threading.Tasks;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace StompDotNet.Sample.Console
{

    public static class Program
    {

        public static Task Main(string[] args) => Host.CreateDefaultBuilder(args)
            .ConfigureServices(s => s.AddSingleton<IHostedService, SampleService>())
            .ConfigureLogging(logging => logging.ClearProviders().AddConsole().SetMinimumLevel(LogLevel.Debug))
            .RunConsoleAsync();

    }

}
