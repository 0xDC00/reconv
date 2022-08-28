using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
//using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
//using Microsoft.Extensions.Logging;
using System;
//using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace EvRw
{
    public partial class Program
    {
        internal static ExR.Format.Logger Log = new ExR.Format.Logger(nameof(EvRw));
        internal static ExR.Format.LogListener Listener = new ExR.XTermLogListener(true,
            ExR.Format.LogLevel.Info | ExR.Format.LogLevel.Warning | ExR.Format.LogLevel.Error | ExR.Format.LogLevel.Fatal | ExR.Format.LogLevel.Debug);

        public static async Task Main(string[] args)
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance); // More encoding
            Listener.Subscribe(Log);

            var builder = WebAssemblyHostBuilder.CreateDefault(args);
            builder.RootComponents.Add<App>("#app");

            builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });
            //builder.Services.AddBlazorDownloadFile();
            await builder.Build().RunAsync();
        }
    }
}
