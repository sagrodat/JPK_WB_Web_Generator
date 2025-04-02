using JpkWebGenerator.Services; // <-- Upewnij siê, ¿e to poprawny namespace dla Twoich klas!
using Microsoft.Data.SqlClient;
using OfficeOpenXml;

// Zak³adana przestrzeñ nazw projektu: JpkWebGenerator
// Zak³adane umieszczenie klas FileReader/DatabaseWriter: folder Services

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages();

// Ustawienie licencji EPPlus
OfficeOpenXml.ExcelPackage.License.SetNonCommercialOrganization("Projekt JPK_WB");

// Pobranie connection stringa z konfiguracji (appsettings.json)
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
// Sprawdzenie czy connection string zosta³ wczytany
if (string.IsNullOrEmpty(connectionString))
{
    // Mo¿na tu rzuciæ wyj¹tkiem lub ustawiæ domyœlny dla lokalnego dev
    // Dla bezpieczeñstwa rzucimy wyj¹tkiem, jeœli nie ma go w konfiguracji
    throw new InvalidOperationException("Connection string 'DefaultConnection' not found in configuration.");
}


// Rejestracja serwisów z POPRAWNYMI PRZESTRZENIAMI NAZW
// U¿ywamy pe³nych nazw typów lub upewniamy siê, ¿e using na górze jest poprawny
builder.Services.AddScoped<JpkWebGenerator.Services.FileReader>();
builder.Services.AddScoped<JpkWebGenerator.Services.DatabaseWriter>(provider => new JpkWebGenerator.Services.DatabaseWriter(connectionString));


var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization(); // Nawet jeœli nie u¿ywamy teraz, warto zostawiæ

app.MapRazorPages(); // Mapuje strony Razor Pages

app.Run();