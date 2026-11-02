using Njord;

var builder = NjordHost.Configure(WebApplication.CreateBuilder(args));
var app = NjordHost.Configure(builder.Build());

await app.RunAsync();
