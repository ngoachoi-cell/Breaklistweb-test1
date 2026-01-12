using OfficeOpenXml;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();

// ---- EPPlus 8+ licensing (MUST be set before ExcelPackage is used) ----
ExcelPackage.License.SetNonCommercialPersonal("Achoi");
// For commercial use, replace with valid license key.
// ----------------------------------------------------------------------

builder.Services.AddSingleton<BreaklistWeb.Services.BreaklistStore>();
builder.Services.AddSingleton<BreaklistWeb.Services.ShiftReportParser>();

var app = builder.Build();

// Ensure App_Data exists
var dataDir = Path.Combine(app.Environment.ContentRootPath, "App_Data");
Directory.CreateDirectory(dataDir);

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Breaklist}/{action=Upload}/{id?}");

app.Run();