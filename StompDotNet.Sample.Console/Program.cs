using System.Threading.Tasks;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace StompDotNet.Sample.Console
{

    public static class Program
    {

        public static Task Main(string[] args) => new HostBuilder()
            .ConfigureServices(s => s.AddSingleton<IHostedService, SampleService>())
            .RunConsoleAsync();

    }

}
