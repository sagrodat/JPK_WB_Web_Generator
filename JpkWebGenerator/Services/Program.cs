// --- START FILE: Program.cs ---
using JpkWebGenerator.Services; // Poprawny namespace dla Twoich klas
using Microsoft.AspNetCore.Hosting; // Dla IWebHostEnvironment
using Microsoft.Extensions.DependencyInjection; // Dla AddSingleton itp.
using Microsoft.Extensions.Hosting; // Dla IHostEnvironment
using OfficeOpenXml;
using System.IO; // Dla Path, Directory

// Ustawienie licencji EPPlus (musi byæ wykonane przed u¿yciem klas EPPlus)
// Umieszczamy to przed budowaniem WebApplication, aby by³o pewne, ¿e wykona siê raz na starcie.
OfficeOpenXml.ExcelPackage.License.SetNonCommercialOrganization("Projekt JPK_WB");
Console.WriteLine("Ustawiono kontekst licencji EPPlus."); // Logowanie dla pewnoœci

var builder = WebApplication.CreateBuilder(args);

// --- Rejestracja Serwisów w Kontenerze DI ---
Console.WriteLine("Rejestrowanie serwisów...");

builder.Services.AddRazorPages(); // Dodaje obs³ugê Razor Pages
builder.Services.AddAntiforgery(); // Dodaje ochronê przed atakami CSRF (wa¿ne w formularzach POST)

// Pobranie connection stringa z konfiguracji (appsettings.json)
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrEmpty(connectionString))
{
    // Krytyczny b³¹d - aplikacja nie mo¿e dzia³aæ bez po³¹czenia z baz¹
    throw new InvalidOperationException("Connection string 'DefaultConnection' nie zosta³ znaleziony w konfiguracji (appsettings.json).");
}
Console.WriteLine("Odczytano Connection String.");

// Rejestracja w³asnych serwisów z odpowiednim cyklem ¿ycia
// AddScoped - instancja tworzona raz na ¿¹danie HTTP
builder.Services.AddScoped<FileReader>();
builder.Services.AddScoped<DatabaseWriter>(provider => new DatabaseWriter(connectionString));
// IWebHostEnvironment jest zazwyczaj rejestrowane domyœlnie, ale AddSingleton jest bezpieczne
builder.Services.AddSingleton<IWebHostEnvironment>(builder.Environment);

Console.WriteLine("Serwisy zarejestrowane.");


// --- Budowanie Aplikacji ---
var app = builder.Build();


// --- Konfiguracja Potoku Przetwarzania ¯¹dañ HTTP ---
Console.WriteLine($"Konfigurowanie potoku HTTP dla œrodowiska: {app.Environment.EnvironmentName}");

// W œrodowisku innym ni¿ deweloperskie u¿yj strony b³êdów i HSTS
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error"); // Przekierowuje na stronê /Error w razie nieobs³u¿onego wyj¹tku
    // HSTS (Strict Transport Security) wymusza HTTPS po pierwszym udanym po³¹czeniu
    app.UseHsts();
    Console.WriteLine("Skonfigurowano obs³ugê b³êdów produkcyjnych i HSTS.");
}
else
{
    // W trybie deweloperskim pokazujemy bardziej szczegó³ow¹ stronê b³êdu
    app.UseDeveloperExceptionPage();
    Console.WriteLine("Skonfigurowano stronê b³êdów deweloperskich.");
}

app.UseHttpsRedirection(); // Przekierowuje ¿¹dania HTTP na HTTPS
app.UseStaticFiles(); // Umo¿liwia serwowanie plików statycznych z folderu wwwroot (CSS, JS, obrazy)

app.UseRouting(); // W³¹cza mechanizm routingu (kierowania ¿¹dañ do odpowiednich stron/handlerów)

app.UseAuthorization(); // W³¹cza mechanizm autoryzacji (nawet jeœli nie jest teraz u¿ywany)

app.MapRazorPages(); // Mapuje routing dla stron Razor Pages (np. ¿¹danie do /Index trafia do Index.cshtml)
Console.WriteLine("Potok HTTP skonfigurowany.");


// --- Uruchomienie Aplikacji ---
Console.WriteLine("Uruchamianie aplikacji...");
app.Run(); // Uruchamia serwer webowy i nas³uchuje na ¿¹dania
// --- END FILE: Program.cs ---