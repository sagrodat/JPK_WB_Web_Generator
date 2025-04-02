using JpkWebGenerator.Services; // <-- Upewnij si�, �e to poprawny namespace dla Twoich klas!
using Microsoft.Data.SqlClient;
using OfficeOpenXml;

// Zak�adana przestrze� nazw projektu: JpkWebGenerator
// Zak�adane umieszczenie klas FileReader/DatabaseWriter: folder Services

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages();

// Ustawienie licencji EPPlus
OfficeOpenXml.ExcelPackage.License.SetNonCommercialOrganization("Projekt JPK_WB");

// Pobranie connection stringa z konfiguracji (appsettings.json)
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
// Sprawdzenie czy connection string zosta� wczytany
if (string.IsNullOrEmpty(connectionString))
{
    // Mo�na tu rzuci� wyj�tkiem lub ustawi� domy�lny dla lokalnego dev
    // Dla bezpiecze�stwa rzucimy wyj�tkiem, je�li nie ma go w konfiguracji
    throw new InvalidOperationException("Connection string 'DefaultConnection' not found in configuration.");
}


// Rejestracja serwis�w z POPRAWNYMI PRZESTRZENIAMI NAZW
// U�ywamy pe�nych nazw typ�w lub upewniamy si�, �e using na g�rze jest poprawny
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

app.UseAuthorization(); // Nawet je�li nie u�ywamy teraz, warto zostawi�

app.MapRazorPages(); // Mapuje strony Razor Pages

app.Run();