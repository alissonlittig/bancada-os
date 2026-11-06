using Bancada;
using Bancada.Services;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddSingleton<IStorage, BrowserStorage>();
builder.Services.AddSingleton<OrderStore>(sp => new OrderStore(sp.GetRequiredService<IStorage>()));

await builder.Build().RunAsync();
