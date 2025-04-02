// --- START FILE: Program.cs ---
using JpkWebGenerator.Services; // Poprawny namespace dla Twoich klas
using Microsoft.AspNetCore.Hosting; // Dla IWebHostEnvironment
using Microsoft.Extensions.DependencyInjection; // Dla AddSingleton itp.
using Microsoft.Extensions.Hosting; // Dla IHostEnvironment
using OfficeOpenXml;
using System.IO; // Dla Path, Directory

// Ustawienie licencji EPPlus (musi by� wykonane przed u�yciem klas EPPlus)
// Umieszczamy to przed budowaniem WebApplication, aby by�o pewne, �e wykona si� raz na starcie.
OfficeOpenXml.ExcelPackage.License.SetNonCommercialOrganization("Projekt JPK_WB");
Console.WriteLine("Ustawiono kontekst licencji EPPlus."); // Logowanie dla pewno�ci

var builder = WebApplication.CreateBuilder(args);

// --- Rejestracja Serwis�w w Kontenerze DI ---
Console.WriteLine("Rejestrowanie serwis�w...");

builder.Services.AddRazorPages(); // Dodaje obs�ug� Razor Pages
builder.Services.AddAntiforgery(); // Dodaje ochron� przed atakami CSRF (wa�ne w formularzach POST)

// Pobranie connection stringa z konfiguracji (appsettings.json)
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrEmpty(connectionString))
{
    // Krytyczny b��d - aplikacja nie mo�e dzia�a� bez po��czenia z baz�
    throw new InvalidOperationException("Connection string 'DefaultConnection' nie zosta� znaleziony w konfiguracji (appsettings.json).");
}
Console.WriteLine("Odczytano Connection String.");

// Rejestracja w�asnych serwis�w z odpowiednim cyklem �ycia
// AddScoped - instancja tworzona raz na ��danie HTTP
builder.Services.AddScoped<FileReader>();
builder.Services.AddScoped<DatabaseWriter>(provider => new DatabaseWriter(connectionString));
// IWebHostEnvironment jest zazwyczaj rejestrowane domy�lnie, ale AddSingleton jest bezpieczne
builder.Services.AddSingleton<IWebHostEnvironment>(builder.Environment);

Console.WriteLine("Serwisy zarejestrowane.");


// --- Budowanie Aplikacji ---
var app = builder.Build();


// --- Konfiguracja Potoku Przetwarzania ��da� HTTP ---
Console.WriteLine($"Konfigurowanie potoku HTTP dla �rodowiska: {app.Environment.EnvironmentName}");

// W �rodowisku innym ni� deweloperskie u�yj strony b��d�w i HSTS
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error"); // Przekierowuje na stron� /Error w razie nieobs�u�onego wyj�tku
    // HSTS (Strict Transport Security) wymusza HTTPS po pierwszym udanym po��czeniu
    app.UseHsts();
    Console.WriteLine("Skonfigurowano obs�ug� b��d�w produkcyjnych i HSTS.");
}
else
{
    // W trybie deweloperskim pokazujemy bardziej szczeg�ow� stron� b��du
    app.UseDeveloperExceptionPage();
    Console.WriteLine("Skonfigurowano stron� b��d�w deweloperskich.");
}

app.UseHttpsRedirection(); // Przekierowuje ��dania HTTP na HTTPS
app.UseStaticFiles(); // Umo�liwia serwowanie plik�w statycznych z folderu wwwroot (CSS, JS, obrazy)

app.UseRouting(); // W��cza mechanizm routingu (kierowania ��da� do odpowiednich stron/handler�w)

app.UseAuthorization(); // W��cza mechanizm autoryzacji (nawet je�li nie jest teraz u�ywany)

app.MapRazorPages(); // Mapuje routing dla stron Razor Pages (np. ��danie do /Index trafia do Index.cshtml)
Console.WriteLine("Potok HTTP skonfigurowany.");


// --- Uruchomienie Aplikacji ---
Console.WriteLine("Uruchamianie aplikacji...");
app.Run(); // Uruchamia serwer webowy i nas�uchuje na ��dania
// --- END FILE: Program.cs ---