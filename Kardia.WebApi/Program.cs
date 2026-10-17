using Kardia.WebApi;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddKardia(builder.Configuration, builder.Environment);

var app = builder.Build();
app.UseKardia();
await app.RunAsync();
