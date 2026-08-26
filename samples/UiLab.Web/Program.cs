using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

namespace UiLab.Web;

public static class Program
{
    public static async Task Main(string[] args)
    {
        WebAssemblyHostBuilder builder = WebAssemblyHostBuilder.CreateDefault(args);
        builder.RootComponents.Add<UiLabRoot>("#app");
        await builder.Build().RunAsync();
    }
}
