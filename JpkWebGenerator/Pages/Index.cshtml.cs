// --- START FILE: Index.cshtml.cs ---
using JpkWebGenerator.Services; 
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.ComponentModel.DataAnnotations;
using System.IO;
using Microsoft.Extensions.Hosting;
using System.Text;
using System.Xml.Linq;
using System.Xml;
using Microsoft.Data.SqlClient;
using System.Data;
using System.Xml.Schema;
using System.Collections.Generic;
using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace JpkWebGenerator.Pages 
{
    /// <summary>
    /// Model logiki (code-behind) dla strony głównej aplikacji (Index.cshtml).
    /// Odpowiada za obsługę żądań GET i POST, w tym przetwarzanie wysłanych plików,
    /// interakcję z serwisami FileReader i DatabaseWriter, zarządzanie stanem (TempData)
    /// oraz inicjowanie pobierania wygenerowanego pliku XML.
    /// </summary>
    public class IndexModel : PageModel
    {
        private readonly FileReader _fileReader;
        private readonly DatabaseWriter _dbWriter;
        // IWebHostEnvironment używane do określania ścieżek na serwerze
        private readonly IWebHostEnvironment _environment;

        /// <summary>
        /// Właściwość powiązana z kontrolką input typu 'file' dla pliku nagłówkowego w formularzu POST.
        /// Oznaczona jako wymagana za pomocą atrybutu [Required].
        /// </summary>
        [BindProperty]
        [Required(ErrorMessage = "Plik nagłówkowy jest wymagany.")]
        [Display(Name = "Plik nagłówkowy")]
        public IFormFile HeaderFile { get; set; } = null!; // Inicjalizacja null-forgiving, walidacja ModelState sprawdzi Required

        /// <summary>
        /// Właściwość powiązana z kontrolką input typu 'file' (multiple) dla plików pozycji w formularzu POST.
        /// Oznaczona jako wymagana (co najmniej jeden plik).
        /// </summary>
        [BindProperty]
        [Required(ErrorMessage = "Co najmniej jeden plik pozycji jest wymagany.")]
        [Display(Name = "Plik(i) pozycji")]
        public List<IFormFile> PositionFiles { get; set; }

        /// <summary>
        /// Przechowuje komunikat statusu lub błędu do wyświetlenia użytkownikowi po zakończeniu operacji POST
        /// i przekierowaniu z powrotem na stronę GET. Atrybut [TempData] zapewnia przetrwanie danych przez jedno przekierowanie.
        /// </summary>
        [TempData]
        public string? StatusMessage { get; set; }

        /// <summary>
        /// Przechowuje ID rekordu nagłówka (z tabeli dbo.Headers) po udanym zapisie do bazy danych.
        /// Używane do przekazania ID do handlera pobierania XML lub strony wyświetlania błędów.
        /// Atrybut [TempData] zapewnia przetrwanie danych przez jedno przekierowanie.
        /// </summary>
        [TempData]
        public int? ProcessedHeaderId { get; set; }

        /// <summary>
        /// Przechowuje liczbę błędów znalezionych podczas walidacji SQL (lub XSD) dla ostatniego przetworzenia.
        /// Wartość 0 oznacza sukces, > 0 oznacza błędy, < 0 (np. -1) oznacza błąd wykonania walidacji/generowania.
        /// Atrybut [TempData] zapewnia przetrwanie danych przez jedno przekierowanie.
        /// </summary>
        [TempData]
        public int? ValidationErrorCount { get; set; }

        /// <summary>
        /// Konstruktor klasy IndexModel. Wstrzykuje wymagane serwisy (FileReader, DatabaseWriter, IWebHostEnvironment)
        /// skonfigurowane w kontenerze Dependency Injection (w Program.cs).
        /// </summary>
        /// <param name="fileReader">Serwis odpowiedzialny za odczyt i parsowanie plików wejściowych.</param>
        /// <param name="dbWriter">Serwis odpowiedzialny za operacje na bazie danych (zapis, walidacja, generowanie XML).</param>
        /// <param name="environment">Serwis dostarczający informacji o środowisku hostingu aplikacji webowej (np. ścieżki).</param>
        public IndexModel(FileReader fileReader, DatabaseWriter dbWriter, IWebHostEnvironment environment)
        {
            _fileReader = fileReader;
            _dbWriter = dbWriter;
            _environment = environment;
            PositionFiles = new List<IFormFile>(); // Zainicjuj listę, aby uniknąć potencjalnych błędów NullReferenceException
        }

        /// <summary>
        /// Metoda obsługująca żądanie HTTP GET (np. gdy użytkownik wchodzi na stronę po raz pierwszy lub po przekierowaniu).
        /// Obecnie nie wykonuje żadnych specjalnych akcji inicjalizujących.
        /// </summary>
        public void OnGet()
        {
            // Można by tu dodać logikę np. do wyświetlania historii poprzednich importów,
            // lub do czyszczenia TempData, jeśli nie chcemy, aby komunikaty/ID były widoczne po odświeżeniu strony.
        }

        /// <summary>
        /// Metoda asynchronicznie obsługująca żądanie HTTP POST wysłane z formularza na stronie Index.
        /// Realizuje główny przepływ pracy aplikacji: odbiera pliki, zapisuje je tymczasowo,
        /// przetwarza dane (odczyt, zapis do bazy), wywołuje walidację SQL,
        /// ustawia wyniki w TempData do wyświetlenia po przekierowaniu i sprząta pliki tymczasowe.
        /// </summary>
        /// <returns>Obiekt <see cref="IActionResult"/>, zazwyczaj przekierowanie (RedirectToPage) z powrotem do strony Index (wzorzec PRG).</returns>
        public async Task<IActionResult> OnPostAsync()
        {
            // Podstawowa walidacja atrybutów [Required] dla właściwości modelu
            if (!ModelState.IsValid) { return Page(); }
            // Dodatkowe, jawne sprawdzenie czy lista plików pozycji nie jest pusta
            if (PositionFiles == null || !PositionFiles.Any()) { ModelState.AddModelError("PositionFiles", "Należy wybrać co najmniej jeden plik pozycji."); return Page(); }

            List<string> tempFilePaths = new List<string>(); // Przechowuje ścieżki do WSZYSTKICH zapisanych plików tymczasowych
            int headerId = 0; // ID zapisanego nagłówka
            int errorCount = -1; // Wynik walidacji SQL
            // Ustalenie ścieżki do bezpiecznego folderu na tymczasowe pliki (poza wwwroot)
            var uploadsFolderPath = Path.Combine(_environment.ContentRootPath, "TempUploads");
            Directory.CreateDirectory(uploadsFolderPath); // Utwórz folder, jeśli nie istnieje
            string? headerTempPath = null; // Ścieżka do tymczasowego pliku nagłówka

            try
            {
                // --- Krok 1: Zapis Plików Tymczasowych na Serwerze ---
                Console.WriteLine("Zapisywanie plików tymczasowych...");
                var headerFileName = $"{Guid.NewGuid()}_{Path.GetFileName(HeaderFile.FileName)}"; // Unikalna nazwa pliku
                headerTempPath = Path.Combine(uploadsFolderPath, headerFileName);
                using (var stream = new FileStream(headerTempPath, FileMode.Create)) { await HeaderFile.CopyToAsync(stream); }
                tempFilePaths.Add(headerTempPath); // Dodaj do listy do posprzątania
                Console.WriteLine($"Zapisano nagłówek jako: {headerTempPath}");

                var positionTempPaths = new List<string>(); // Lista ścieżek tylko do plików pozycji
                foreach (var positionFile in PositionFiles)
                {
                    if (positionFile != null && positionFile.Length > 0) // Sprawdź czy plik nie jest pusty
                    {
                        var positionFileName = $"{Guid.NewGuid()}_{Path.GetFileName(positionFile.FileName)}";
                        var positionTempPath = Path.Combine(uploadsFolderPath, positionFileName);
                        using (var stream = new FileStream(positionTempPath, FileMode.Create)) { await positionFile.CopyToAsync(stream); }
                        tempFilePaths.Add(positionTempPath);     // Dodaj do ogólnej listy sprzątania
                        positionTempPaths.Add(positionTempPath); // Dodaj do listy dla FileReader
                        Console.WriteLine($"Zapisano pozycję jako: {positionTempPath}");
                    }
                }
                if (!positionTempPaths.Any()) { throw new InvalidOperationException("Nie udało się zapisać żadnych plików pozycji."); }

                // --- Krok 2: Przetwarzanie Danych (Odczyt z plików, Zapis do Bazy) ---
                Console.WriteLine("\nRozpoczynanie przetwarzania danych...");
                await _dbWriter.EnsureTablesExistAsync(); // Sprawdź/utwórz/zaktualizuj strukturę tabel
                // await _dbWriter.ClearDataTablesAsync(); // Wykomentowane - nie czyścimy danych przy każdym żądaniu

                // Odczyt danych z plików tymczasowych
                HeaderData headerData = _fileReader.ReadHeaderFile(headerTempPath);
                List<PositionData> positionData = _fileReader.ReadPositionFiles(positionTempPaths);
                Console.WriteLine("Wczytywanie plików zakończone.");

                // Przechwycenie salda początkowego (jeśli występuje jako wiersz bez kwoty)
                Console.WriteLine("Próba znalezienia salda początkowego...");
                decimal? openingBalance = positionData
                    .Where(p => p.Kwota == null && p.Data != null && p.SaldoKoncowe != null) // Kryteria wiersza salda
                    .OrderBy(p => p.Data).FirstOrDefault()?.SaldoKoncowe; // Weź pierwsze pasujące
                if (openingBalance != null) { Console.WriteLine($"Znaleziono potencjalne saldo początkowe: {openingBalance}"); }
                else { Console.WriteLine("Nie znaleziono wiersza salda początkowego."); }

                // Zapis do bazy danych
                Console.WriteLine("\nRozpoczynanie zapisu do bazy danych...");
                headerId = await _dbWriter.InsertHeaderDataAsync(headerData, openingBalance); // Zapis nagłówka zwraca ID
                Console.WriteLine($"Rekord nagłówka zapisany z ID: {headerId}");

                // Przypisanie uzyskanego ID nagłówka do każdej pozycji przed zapisem masowym
                Console.WriteLine($"Przypisywanie HeaderId={headerId} do {positionData.Count} pozycji...");
                foreach (var pos in positionData) { pos.HeaderId = headerId; }

                // Masowy zapis transakcji (metoda filtruje wiersz salda)
                await _dbWriter.InsertPositionDataBulkAsync(positionData);
                Console.WriteLine("Zapis do bazy danych zakończony.");

                // --- Krok 3: Wywołanie Walidacji w SQL ---
                Console.WriteLine("\nRozpoczynanie walidacji danych w bazie SQL...");
                errorCount = await _dbWriter.ValidateImportAsync(headerId); // Wywołanie procedury SQL
                Console.WriteLine($"Walidacja zakończona. Znaleziono błędów: {errorCount}");

                // --- Ustawienie Wyniku w TempData dla Widoku ---
                ProcessedHeaderId = headerId;
                ValidationErrorCount = errorCount;
                // Ustawienie komunikatu na podstawie wyniku walidacji SQL
                if (errorCount == 0) { StatusMessage = $"Przetwarzanie i walidacja zakończone pomyślnie. Możesz teraz wygenerować plik XML."; }
                else if (errorCount > 0) { StatusMessage = $"Przetwarzanie zakończone, ale znaleziono {errorCount} błędów walidacji."; }
                else { if (string.IsNullOrEmpty(StatusMessage)) StatusMessage = $"Wystąpił nieoczekiwany problem podczas walidacji."; } // Na wypadek błędu SQL w ValidateImportAsync
            }
            catch (Exception ex) // Ogólna obsługa błędów całego procesu
            {
                StatusMessage = $"Wystąpił krytyczny błąd: {ex.Message}";
                Console.WriteLine($"KRYTYCZNY BŁĄD w OnPostAsync: {ex}");
                ProcessedHeaderId = null; // Wyczyść ID, bo proces nie powiódł się
                ValidationErrorCount = -1; // Oznacz jako błąd
            }
            finally // Ten blok wykona się ZAWSZE, aby posprzątać pliki
            {
                Console.WriteLine("\nSprzątanie plików tymczasowych...");
                foreach (var path in tempFilePaths)
                {
                    try { if (System.IO.File.Exists(path)) { System.IO.File.Delete(path); Console.WriteLine($"Usunięto: {Path.GetFileName(path)}"); } }
                    catch (IOException ioEx) { Console.WriteLine($"OSTRZEŻENIE: Nie można usunąć pliku {Path.GetFileName(path)}. Błąd: {ioEx.Message}"); }
                    catch (Exception cleanupEx) { Console.WriteLine($"OSTRZEŻENIE: Nieoczekiwany błąd usuwania pliku {Path.GetFileName(path)}: {cleanupEx.Message}"); }
                }
                Console.WriteLine("Sprzątanie plików tymczasowych zakończone.");
            }

            // Zawsze przekierowujemy z powrotem do strony Index (GET), aby uniknąć ponownego POST przy odświeżeniu
            return RedirectToPage();
        }


        /// <summary>
        /// Metoda asynchronicznie obsługująca żądanie GET wysłane przez link "Generuj i Pobierz XML" (page handler "DownloadXml").
        /// Odpowiada za wywołanie procedury SQL generującej XML, sformatowanie wyniku,
        /// walidację XSD i zwrócenie go jako plik do pobrania przez przeglądarkę.
        /// </summary>
        /// <param name="headerId">ID nagłówka (z tabeli dbo.Headers), dla którego ma być wygenerowany plik XML. Przekazywane w URL.</param>
        /// <returns>Wynik typu <see cref="FileResult"/> (plik do pobrania) lub <see cref="RedirectToPageResult"/> w przypadku błędu.</returns>
        public async Task<IActionResult> OnGetDownloadXmlAsync(int headerId)
        {
            // Podstawowe sprawdzenie poprawności przekazanego ID
            if (headerId <= 0) { TempData["StatusMessage"] = "Błąd: Nieprawidłowe ID nagłówka."; return RedirectToPage(); }

            string? generatedXml = null;
            try
            {
                // Pobierz XML wygenerowany przez procedurę SQL
                generatedXml = await _dbWriter.GenerateXmlAsync(headerId);
            }
            catch (Exception ex) // Błąd podczas komunikacji z bazą lub wykonania GenerateXmlAsync
            {
                StatusMessage = $"Wystąpił błąd podczas pobierania XML z bazy danych: {ex.Message}";
                Console.WriteLine($"BŁĄD w OnGetDownloadXmlAsync podczas wywołania GenerateXmlAsync dla HeaderId={headerId}: {ex}");
                TempData["ProcessedHeaderId"] = headerId; TempData["ValidationErrorCount"] = -1;
                return RedirectToPage();
            }

            // Sprawdź, czy procedura SQL zwróciła poprawny XML
            if (!string.IsNullOrEmpty(generatedXml) && !generatedXml.TrimStart().StartsWith("<Error", StringComparison.OrdinalIgnoreCase))
            {
                var fileName = $"jpk_wb_{headerId}_{DateTime.Now:yyyyMMddHHmmss}.xml";
                try
                {
                    // --- Walidacja XSD wygenerowanego XML ---
                    Console.WriteLine($"Rozpoczynanie walidacji XSD dla HeaderId={headerId}...");
                    List<string> xsdValidationErrors = new List<string>();
                    XDocument docToValidate = XDocument.Parse(generatedXml);

                    string schemaFolderPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Schemas");
                    if (!Directory.Exists(schemaFolderPath)) { throw new DirectoryNotFoundException($"Folder ze schematami XSD ({schemaFolderPath}) nie istnieje."); }


                    string jpkWbSchemaFile = "Schemat_JPK_WB(1)_v1-0.xsd";
                    string strukturyDanychSchemaFile = "StrukturyDanych_v4-0E.xsd";
                    string kodyCechKrajowSchemaFile = "KodyCechKrajow_v3-0E.xsd";
                    string elementarneTypySchemaFile = "ElementarneTypyDanych_v4-0E.xsd"; 
                    string kodyUrzedowSkarbowychSchemaFile = "KodyUrzedowSkarbowych_v4-0E.xsd"; 
                    string kodyKrajowSchemaFile = "KodyKrajow_v4-1E.xsd";

                    XmlSchemaSet schemas = new XmlSchemaSet();
                    Console.WriteLine($"Ładowanie schematów z folderu: {schemaFolderPath}");
                    // Dodajemy wszystkie potrzebne schematy do zestawu
                    schemas.Add(null, Path.Combine(schemaFolderPath, elementarneTypySchemaFile));
                    schemas.Add(null, Path.Combine(schemaFolderPath, kodyUrzedowSkarbowychSchemaFile));
                    schemas.Add(null, Path.Combine(schemaFolderPath, kodyKrajowSchemaFile));
                    schemas.Add(null, Path.Combine(schemaFolderPath, kodyCechKrajowSchemaFile));
                    schemas.Add(null, Path.Combine(schemaFolderPath, strukturyDanychSchemaFile));
                    schemas.Add(null, Path.Combine(schemaFolderPath, jpkWbSchemaFile)); // Główny schemat

                    Console.WriteLine("Kompilowanie zestawu schematów...");
                    schemas.Compile(); // Sprawdzenie spójności załadowanych schematów
                    Console.WriteLine("Kompilacja zakończona.");

                    // Handler do zbierania błędów walidacji XSD
                    ValidationEventHandler eventHandler = (sender, e) => {
                        string errorMsg = $"[XSD {e.Severity}] Linia {e.Exception?.LineNumber}, Poz: {e.Exception?.LinePosition}: {e.Message}";
                        xsdValidationErrors.Add(errorMsg);
                        Console.WriteLine(errorMsg); // Logowanie błędu na serwerze
                    };

                    // Walidacja dokumentu XML względem załadowanych schematów
                    Console.WriteLine("Uruchamianie walidacji dokumentu XML...");
                    docToValidate.Validate(schemas, eventHandler);

                    // Sprawdzenie wyniku walidacji XSD
                    if (xsdValidationErrors.Any())
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($"Walidacja XSD NIE powiodła się dla HeaderId={headerId}. Błędy: {xsdValidationErrors.Count}");
                        Console.ResetColor();
                        TempData["StatusMessage"] = $"Wygenerowany XML nie przeszedł walidacji XSD ({xsdValidationErrors.Count} błędów). Plik nie pobrany. Sprawdź logi serwera.";
                        TempData["ProcessedHeaderId"] = headerId; TempData["ValidationErrorCount"] = xsdValidationErrors.Count;
                        return RedirectToPage(); // Przekieruj z powrotem z komunikatem
                    }
                    else
                    {
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine($"Walidacja XSD powiodła się dla HeaderId={headerId}.");
                        Console.ResetColor();
                        // Przejdź do formatowania i zwrócenia pliku
                    }
                    // --- KONIEC WALIDACJI XSD ---

                    // --- Formatowanie i Zwrócenie Pliku ---
                    // Używamy XDocument, który już mamy sparsowane
                    XmlWriterSettings settings = new XmlWriterSettings { Encoding = Encoding.UTF8, OmitXmlDeclaration = false, Indent = true, Async = true };
                    using (var memoryStream = new System.IO.MemoryStream())
                    {
                        using (XmlWriter writer = XmlWriter.Create(memoryStream, settings)) { await docToValidate.SaveAsync(writer, default); }
                        var fileBytes = memoryStream.ToArray();
                        Console.WriteLine($"Zwracanie sformatowanego pliku XML: {fileName}");
                        // Zwracamy bajty jako plik do pobrania
                        return File(fileBytes, "application/xml", fileName);
                    }
                }
                catch (FileNotFoundException fnfEx) // Błąd - nie znaleziono któregoś pliku XSD
                {
                    StatusMessage = $"Błąd krytyczny: Nie znaleziono pliku schematu XSD: {Path.GetFileName(fnfEx.FileName)}. Sprawdź folder 'Schemas'.";
                    Console.WriteLine($"KRYTYCZNY BŁĄD walidacji XSD: {fnfEx}");
                    return RedirectToPage();
                }
                catch (XmlSchemaException schemaEx) // Błąd w strukturze lub kompilacji schematów XSD
                {
                    StatusMessage = $"Błąd krytyczny schematu XSD: {schemaEx.Message} (Linia: {schemaEx.LineNumber}, Poz: {schemaEx.LinePosition}).";
                    Console.WriteLine($"KRYTYCZNY BŁĄD walidacji XSD: {schemaEx}");
                    return RedirectToPage();
                }
                catch (Exception ex) // Inne błędy (np. parsowania XML zwróconego z SQL, zapisu do MemoryStream)
                {
                    StatusMessage = $"Wystąpił błąd podczas walidacji XSD lub przygotowywania pliku XML: {ex.Message}";
                    Console.WriteLine($"BŁĄD w OnGetDownloadXmlAsync (XSD/Zapis) dla HeaderId={headerId}: {ex}");
                    return RedirectToPage();
                }
            }
            else
            {
                // Procedura SQL nie zwróciła XML lub był to jej wewnętrzny błąd <Error>
                StatusMessage = $"Nie udało się wygenerować danych XML dla nagłówka ID={headerId} (brak danych lub błąd SQL).";
                TempData["ProcessedHeaderId"] = headerId;
                TempData["ValidationErrorCount"] = await _dbWriter.ValidateImportAsync(headerId); // Pobierz liczbę błędów SQL
                return RedirectToPage();
            }
        } // Koniec OnGetDownloadXmlAsync

    } // Koniec klasy IndexModel
} // Koniec namespace
  // --- END FILE: Index.cshtml.cs ---