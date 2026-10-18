using Njord;

var builder = WebApplication.CreateBuilder(args);

await NjordHost.Create(builder).RunAsync();
