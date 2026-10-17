using Kardia.Website;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddKardiaWebsite(builder.Configuration);

var app = builder.Build();
app.UseKardiaWebsite();
await app.RunAsync();
