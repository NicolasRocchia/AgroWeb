using WebAgroConnect.Configs;
using Microsoft.Extensions.Options;
using WebApplication1.Logic;

var builder = WebApplication.CreateBuilder(args);


// Add services to the container.
builder.Services.AddControllersWithViews();

builder.Services.Configure<ApiOptions>(builder.Configuration.GetSection("Api"));

builder.Services.AddHttpClient("AgroApi", (sp, client) =>
{
    var opt = sp.GetRequiredService<IOptions<ApiOptions>>().Value;
    client.BaseAddress = new Uri(opt.BaseUrl);
    client.Timeout = TimeSpan.FromSeconds(60);
})
.AddHttpMessageHandler<ApiAuthHandler>();


var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
