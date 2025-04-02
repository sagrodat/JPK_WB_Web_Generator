using JpkWebGenerator.Services; // <-- Upewnij się, że poprawny namespace!
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.ComponentModel.DataAnnotations;
using System.IO;
using Microsoft.Extensions.Hosting;
using System.Text; // Potrzebne dla Encoding.UTF8
using System.Xml.Linq; // Potrzebne dla XDocument
using System.Xml; // Potrzebne dla XmlWriter

namespace JpkWebGenerator.Pages // <-- Upewnij się, że poprawny namespace!
{
    public class IndexModel : PageModel
    {
        private readonly FileReader _fileReader;
        private readonly DatabaseWriter _dbWriter;
        private readonly IWebHostEnvironment _environment;

        [BindProperty]
        [Required(ErrorMessage = "Plik nagłówkowy jest wymagany.")]
        [Display(Name = "Plik nagłówkowy")]
        public IFormFile HeaderFile { get; set; }

        [BindProperty]
        [Required(ErrorMessage = "Co najmniej jeden plik pozycji jest wymagany.")]
        [Display(Name = "Plik(i) pozycji")]
        public List<IFormFile> PositionFiles { get; set; }

        [TempData]
        public string? StatusMessage { get; set; }

        [TempData]
        public int? ProcessedHeaderId { get; set; }

        [TempData]
        public int? ValidationErrorCount { get; set; }


        public IndexModel(FileReader fileReader, DatabaseWriter dbWriter, IWebHostEnvironment environment)
        {
            _fileReader = fileReader;
            _dbWriter = dbWriter;
            _environment = environment;
            PositionFiles = new List<IFormFile>();
        }

        public void OnGet()
        {
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid) { return Page(); }
            if (PositionFiles == null || !PositionFiles.Any()) { ModelState.AddModelError("PositionFiles", "Należy wybrać co najmniej jeden plik pozycji."); return Page(); }

            List<string> tempFilePaths = new List<string>();
            int headerId = 0;
            int errorCount = -1;
            var uploadsFolderPath = Path.Combine(_environment.WebRootPath, "temp_uploads");
            Directory.CreateDirectory(uploadsFolderPath);
            string? headerTempPath = null;

            try
            {
                // --- Zapis Plików Tymczasowych ---
                Console.WriteLine("Zapisywanie plików tymczasowych...");
                var headerFileName = $"{Guid.NewGuid()}_{HeaderFile.FileName}"; // Deklaracja headerFileName
                headerTempPath = Path.Combine(uploadsFolderPath, headerFileName);
                using (var stream = new FileStream(headerTempPath, FileMode.Create)) { await HeaderFile.CopyToAsync(stream); }
                tempFilePaths.Add(headerTempPath);
                Console.WriteLine($"Zapisano nagłówek jako: {headerTempPath}");
                var positionTempPaths = new List<string>();
                // Zapis plików pozycji - UZUPEŁNIONA PĘTLA
                foreach (var positionFile in PositionFiles)
                {
                    if (positionFile != null && positionFile.Length > 0) // Dodatkowe sprawdzenie pliku
                    {
                        var positionFileName = $"{Guid.NewGuid()}_{Path.GetFileName(positionFile.FileName)}";
                        var positionTempPath = Path.Combine(uploadsFolderPath, positionFileName);
                        using (var stream = new FileStream(positionTempPath, FileMode.Create))
                        {
                            await positionFile.CopyToAsync(stream);
                        }
                        tempFilePaths.Add(positionTempPath);     // Dodajemy do ogólnej listy do sprzątania
                        positionTempPaths.Add(positionTempPath); // Dodajemy do listy tylko dla pozycji
                        Console.WriteLine($"Zapisano pozycję jako: {positionTempPath}");
                    }
                }

                // --- Wywołanie Logiki Przetwarzania ---
                Console.WriteLine("\nRozpoczynanie przetwarzania danych...");
                await _dbWriter.EnsureTablesExistAsync();
               // await _dbWriter.ClearDataTablesAsync(); // CZYSZCZENIE
                HeaderData headerData = _fileReader.ReadHeaderFile(headerTempPath);
                List<PositionData> positionData = _fileReader.ReadPositionFiles(positionTempPaths);
                Console.WriteLine("Wczytywanie plików zakończone.");
                decimal? openingBalance = positionData.Where(p => p.Kwota == null && p.Data != null && p.SaldoKoncowe != null).OrderBy(p => p.Data).FirstOrDefault()?.SaldoKoncowe;
                if (openingBalance != null) { Console.WriteLine($"Znaleziono saldo początkowe: {openingBalance}"); } else { Console.WriteLine("Nie znaleziono wiersza salda początkowego."); }
                Console.WriteLine("\nRozpoczynanie zapisu do bazy danych...");
                headerId = await _dbWriter.InsertHeaderDataAsync(headerData, openingBalance);
                Console.WriteLine($"Rekord nagłówka zapisany z ID: {headerId}");
                Console.WriteLine($"Przypisywanie HeaderId={headerId} do {positionData.Count} pozycji...");
                foreach (var pos in positionData)
                {
                    pos.HeaderId = headerId;
                }

                await _dbWriter.InsertPositionDataBulkAsync(positionData);
                Console.WriteLine("Zapis do bazy danych zakończony.");

                // --- Wywołanie Walidacji w SQL ---
                Console.WriteLine("\nRozpoczynanie walidacji danych w bazie SQL...");
                errorCount = await _dbWriter.ValidateImportAsync(headerId); // Użycie nowej metody z DatabaseWriter
                Console.WriteLine($"Walidacja zakończona. Znaleziono błędów: {errorCount}");

                // --- Ustawienie Wyniku w TempData ---
                ProcessedHeaderId = headerId;
                ValidationErrorCount = errorCount;
                if (errorCount == 0) { StatusMessage = $"Przetwarzanie i walidacja zakończone pomyślnie. Możesz teraz wygenerować plik XML."; }
                else if (errorCount > 0) { StatusMessage = $"Przetwarzanie zakończone, ale znaleziono {errorCount} błędów walidacji."; }
                else { if (string.IsNullOrEmpty(StatusMessage)) StatusMessage = $"Wystąpił nieoczekiwany problem podczas walidacji."; }
            }
            catch (Exception ex) { StatusMessage = $"Wystąpił krytyczny błąd: {ex.Message}"; Console.WriteLine($"KRYTYCZNY BŁĄD w OnPostAsync: {ex}"); ProcessedHeaderId = null; ValidationErrorCount = -1; }
            finally { /* ... kod sprzątania plików tymczasowych ... */ }

            return RedirectToPage();
        }

        // ==================================================
        // === NOWY HANDLER DO POBIERANIA PLIKU XML ===
        // ==================================================
        public async Task<IActionResult> OnGetDownloadXmlAsync(int headerId)
        {
            // Podstawowe sprawdzenie ID
            if (headerId <= 0)
            {
                TempData["StatusMessage"] = "Błąd: Nieprawidłowe ID nagłówka do wygenerowania XML.";
                return RedirectToPage();
            }

            // Opcjonalne: Ponowne sprawdzenie walidacji (można dodać później dla pewności)
            // int currentErrorCount = await _dbWriter.ValidateImportAsync(headerId);
            // if (currentErrorCount != 0) { ... }

            string? generatedXml = null;
            try
            {
                // Krok 1: Pobierz XML z procedury SQL
                generatedXml = await _dbWriter.GenerateXmlAsync(headerId);
            }
            catch (Exception ex)
            {
                StatusMessage = $"Wystąpił błąd podczas pobierania XML z bazy danych: {ex.Message}";
                Console.WriteLine($"BŁĄD w OnGetDownloadXmlAsync podczas wywołania GenerateXmlAsync: {ex}");
                return RedirectToPage(); // Wróć do strony głównej z komunikatem błędu
            }


            if (!string.IsNullOrEmpty(generatedXml))
            {
                // Sukces - mamy XML z bazy danych, teraz go sformatujmy i zwróćmy
                var fileName = $"jpk_wb_{headerId}_{DateTime.Now:yyyyMMddHHmmss}.xml";

                try
                {
                    // Krok 2: Sparsuj XML do XDocument, aby móc go zapisać z formatowaniem
                    XDocument doc = XDocument.Parse(generatedXml);

                    // Krok 3: Przygotuj ustawienia zapisu XML z wcięciami i deklaracją
                    XmlWriterSettings settings = new XmlWriterSettings
                    {
                        Encoding = System.Text.Encoding.UTF8,     // Kodowanie UTF-8
                        OmitXmlDeclaration = false,              // Dołącz deklarację <?xml ... ?>
                        Indent = true,                           // Włącz wcięcia (formatowanie)
                        Async = true                             // Pozwól na operacje asynchroniczne
                    };

                    // Krok 4: Zapisz sformatowany XML do strumienia w pamięci
                    var memoryStream = new System.IO.MemoryStream();
                    using (XmlWriter writer = XmlWriter.Create(memoryStream, settings))
                    {
                        await doc.SaveAsync(writer, default); // Zapisz asynchronicznie do strumienia w pamięci
                    } // using zamyka writer, ale memoryStream nadal zawiera dane

                    // Krok 5: Pobierz sformatowane dane jako tablicę bajtów ze strumienia pamięci
         
                    var fileBytes = memoryStream.ToArray();

                    // Krok 6: Zwróć sformatowane dane jako plik do pobrania
                    return File(fileBytes, "application/xml", fileName);
                }
                catch (Exception formatEx) // Złap błędy parsowania lub zapisu XML
                {
                    StatusMessage = $"Wystąpił błąd podczas formatowania lub przygotowywania pliku XML: {formatEx.Message}";
                    Console.WriteLine($"BŁĄD w OnGetDownloadXmlAsync podczas formatowania/zapisu XML: {formatEx}");
                    return RedirectToPage();
                }
            }
            else
            {
                // Procedura SQL nie zwróciła XML lub zwróciła błąd (obsłużone już w GenerateXmlAsync)
                StatusMessage = $"Nie udało się wygenerować danych XML dla nagłówka ID={headerId}.";
                return RedirectToPage();
            }
        }
    }
}