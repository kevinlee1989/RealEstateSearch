var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

// A minimal test page: serves wwwroot/index.html and app.js, nothing else.
// All data comes from RealEstateSearch.Api, which the browser calls directly (see CORS there).
app.UseDefaultFiles();
app.UseStaticFiles();

app.Run();
