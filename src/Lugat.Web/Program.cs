using Lugat.Web;
using Lugat.Web.Services;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// Timeouts are set per call in SessionService (uploads and AI checks need longer than ordinary calls).
builder.Services.AddScoped(_ => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress), Timeout = Timeout.InfiniteTimeSpan });
builder.Services.AddScoped<BrowserInterop>();
builder.Services.AddScoped<SessionService>();
builder.Services.AddScoped<PushDevice>();

await builder.Build().RunAsync();
