using DtoOrm.Portal.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();

// The portal talks to the School API exclusively over HTTP through a typed client. Pages depend on
// the ISchoolApiClient abstraction, never on the concrete implementation or the API project itself.
var apiBaseUrl = builder.Configuration["SchoolApi:BaseUrl"]
    ?? throw new InvalidOperationException("SchoolApi:BaseUrl is required.");

builder.Services.AddHttpClient<ISchoolApiClient, SchoolApiClient>(client =>
{
    client.BaseAddress = new Uri(apiBaseUrl);
    client.Timeout = TimeSpan.FromSeconds(15);
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
}

app.UseStaticFiles();
app.UseRouting();
app.MapRazorPages();

app.Run();
