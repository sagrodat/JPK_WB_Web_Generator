// --- START FILE: FileReader.cs ---
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using CsvHelper;
using CsvHelper.Configuration;
using OfficeOpenXml; // EPPlus

namespace JpkWebGenerator.Services 
{
    /// <summary>
    /// Odpowiada za wczytywanie i parsowanie danych z plików wejściowych (Excel .xlsx lub CSV)
    /// dla nagłówka i pozycji JPK_WB. Obsługuje różne formaty plików i podstawowe błędy odczytu.
    /// </summary>
    public class FileReader
    {
        /// <summary>
        /// Inicjalizuje nową instancję klasy <see cref="FileReader"/>.
        /// Wymaga globalnego ustawienia licencji EPPlus przy starcie aplikacji.
        /// </summary>
        public FileReader()
        {
            // Upewnij się, że licencja EPPlus jest ustawiona w Program.cs
        }

        // --- Metody publiczne ---

        /// <summary>
        /// Wczytuje i parsuje dane z pojedynczego pliku nagłówkowego.
        /// Automatycznie wykrywa format (.xlsx lub .csv) na podstawie rozszerzenia pliku.
        /// </summary>
        /// <param name="filePath">Pełna ścieżka do pliku nagłówkowego.</param>
        /// <returns>Obiekt <see cref="HeaderData"/> zawierający sparsowane dane nagłówka.</returns>
        /// <exception cref="ArgumentException">Rzucany, gdy rozszerzenie pliku jest nieobsługiwane.</exception>
        /// <exception cref="InvalidDataException">Rzucany, gdy plik jest pusty, nieprawidłowo sformatowany lub wystąpił błąd parsowania.</exception>
        /// <exception cref="FileNotFoundException">Rzucany, gdy plik pod podaną ścieżką nie istnieje.</exception>
        public HeaderData ReadHeaderFile(string filePath)
        {
            if (!File.Exists(filePath)) throw new FileNotFoundException($"Plik nagłówka nie został znaleziony: {filePath}");

            Console.WriteLine($"Wczytywanie pliku nagłówkowego: {filePath}...");
            string extension = Path.GetExtension(filePath).ToLowerInvariant();
            if (extension == ".xlsx")
            {
                return ReadHeaderFromExcel(filePath);
            }
            else if (extension == ".csv")
            {
                return ReadHeaderFromCsv(filePath);
            }
            else
            {
                throw new ArgumentException($"Nieobsługiwany format pliku nagłówkowego: {extension}. Obsługiwane formaty: .xlsx, .csv");
            }
        }

        /// <summary>
        /// Wczytuje i parsuje dane z jednego lub wielu plików pozycji.
        /// Automatycznie wykrywa format (.xlsx lub .csv) dla każdego pliku i agreguje wyniki.
        /// Pomija pliki o nieobsługiwanym rozszerzeniu (z ostrzeżeniem w konsoli).
        /// </summary>
        /// <param name="filePaths">Lista pełnych ścieżek do plików pozycji.</param>
        /// <returns>Lista obiektów <see cref="PositionData"/> zawierająca sparsowane dane ze wszystkich poprawnie odczytanych plików.</returns>
        public List<PositionData> ReadPositionFiles(List<string> filePaths)
        {
            var allPositions = new List<PositionData>();
            Console.WriteLine($"Wczytywanie plików pozycji ({filePaths.Count}):");
            foreach (string filePath in filePaths)
            {
                if (!File.Exists(filePath))
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine($"Ostrzeżenie: Plik pozycji '{filePath}' nie istnieje. Pomijanie.");
                    Console.ResetColor();
                    continue;
                }

                Console.WriteLine($"  - {filePath}");
                string extension = Path.GetExtension(filePath).ToLowerInvariant();
                List<PositionData> positions = new List<PositionData>(); // Inicjalizuj pustą listę
                try
                {
                    if (extension == ".xlsx")
                    {
                        positions = ReadPositionsFromExcel(filePath);
                    }
                    else if (extension == ".csv")
                    {
                        positions = ReadPositionsFromCsv(filePath);
                    }
                    else
                    {
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.WriteLine($"Ostrzeżenie: Nieobsługiwany format pliku pozycji '{filePath}'. Pomijanie.");
                        Console.ResetColor();
                        continue; // Pomiń nieznany plik
                    }
                    allPositions.AddRange(positions);
                    Console.WriteLine($"    Wczytano {positions.Count} pozycji.");
                }
                catch (Exception ex) // Łapanie błędów odczytu pojedynczego pliku
                {
                     Console.ForegroundColor = ConsoleColor.Red;
                     Console.WriteLine($"BŁĄD podczas wczytywania pliku pozycji '{filePath}': {ex.Message}. Pomijanie tego pliku.");
                     Console.ResetColor();
                     // Kontynuuj z następnym plikiem
                }
            }
            Console.WriteLine($"Łącznie wczytano {allPositions.Count} pozycji ze wszystkich plików.");
            return allPositions;
        }


        // --- Metody prywatne (Excel) ---

        /// <summary>
        /// Wewnętrzna metoda wczytująca dane nagłówka z pliku Excel (.xlsx).
        /// Zakłada, że nagłówki kolumn znajdują się w pierwszym wierszu, a dane w drugim.
        /// </summary>
        private HeaderData ReadHeaderFromExcel(string filePath)
        {
            using var package = new ExcelPackage(new FileInfo(filePath));
            var worksheet = package.Workbook.Worksheets.FirstOrDefault();
            if (worksheet == null) throw new InvalidDataException($"Plik Excel ({Path.GetFileName(filePath)}) jest pusty lub nie zawiera arkuszy.");
            if (worksheet.Dimension == null || worksheet.Dimension.Rows < 2) throw new InvalidDataException($"Arkusz w pliku ({Path.GetFileName(filePath)}) nie zawiera danych (wiersz 2).");

            // Mapowanie nagłówków Excel na indeksy kolumn (ignoruje wielkość liter)
            var headers = Enumerable.Range(1, worksheet.Dimension.End.Column)
                                    .Select(col => worksheet.Cells[1, col].Text.Trim())
                                    .ToList();
            int GetColIndex(string name) {
                int index = headers.FindIndex(h => h.Equals(name, StringComparison.OrdinalIgnoreCase));
                if (index == -1) Console.WriteLine($"Ostrzeżenie: Nie znaleziono kolumny '{name}' w pliku nagłówka Excel: {Path.GetFileName(filePath)}");
                return index + 1;
            }

            var headerData = new HeaderData();
            try
            {
                // Odczyt poszczególnych pól z drugiego wiersza
                headerData.NIP = worksheet.Cells[2, GetColIndex("NIP")]?.Text;
                headerData.REGON = worksheet.Cells[2, GetColIndex("REGON")]?.Text;
                headerData.NazwaFirmy = worksheet.Cells[2, GetColIndex("NazwaFirmy")]?.Text;
                headerData.KodKraju = worksheet.Cells[2, GetColIndex("KodKraju")]?.Text ?? "PL";
                headerData.Wojewodztwo = worksheet.Cells[2, GetColIndex("Wojewodztwo")]?.Text;
                headerData.Powiat = worksheet.Cells[2, GetColIndex("Powiat")]?.Text;
                headerData.Gmina = worksheet.Cells[2, GetColIndex("Gmina")]?.Text;
                headerData.Ulica = worksheet.Cells[2, GetColIndex("Ulica")]?.Text;
                headerData.NrDomu = worksheet.Cells[2, GetColIndex("NrDomu")]?.Text;
                headerData.NrLokalu = worksheet.Cells[2, GetColIndex("NrLokalu")]?.Text;
                headerData.Miejscowosc = worksheet.Cells[2, GetColIndex("Miejscowosc")]?.Text;
                headerData.KodPocztowy = worksheet.Cells[2, GetColIndex("KodPocztowy")]?.Text;
                headerData.Poczta = worksheet.Cells[2, GetColIndex("Poczta")]?.Text;
                headerData.NumerRachunku = worksheet.Cells[2, GetColIndex("NumerRachunku")]?.Text?.Replace(" ", "");
                headerData.DataOd = GetExcelDate(worksheet.Cells[2, GetColIndex("DataOd")]);
                headerData.DataDo = GetExcelDate(worksheet.Cells[2, GetColIndex("DataDo")]);
                headerData.KodWaluty = worksheet.Cells[2, GetColIndex("KodWaluty")]?.Text ?? "PLN";
                headerData.KodUrzedu = worksheet.Cells[2, GetColIndex("KodUrzedu")]?.Text;
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Błąd podczas parsowania danych nagłówka z Excel ({Path.GetFileName(filePath)}): {ex.Message}");
                Console.ResetColor();
                throw new InvalidDataException($"Nie udało się sparsować danych nagłówka z pliku Excel: {Path.GetFileName(filePath)}", ex);
            }
            return headerData;
        }

        /// <summary>
        /// Wewnętrzna metoda wczytująca dane pozycji z pliku Excel (.xlsx).
        /// Zakłada, że nagłówki kolumn są w pierwszym wierszu, a dane zaczynają się od drugiego.
        /// Pomija wiersze, dla których nie udało się sparsować daty.
        /// </summary>
        private List<PositionData> ReadPositionsFromExcel(string filePath)
        {
            var positions = new List<PositionData>();
            using var package = new ExcelPackage(new FileInfo(filePath));
            var worksheet = package.Workbook.Worksheets.FirstOrDefault();
            if (worksheet == null) { Console.WriteLine($"Ostrzeżenie: Plik Excel pozycji '{filePath}' jest pusty lub nie zawiera arkuszy. Pomijanie."); return positions; }
            if (worksheet.Dimension == null || worksheet.Dimension.Rows < 2) { Console.WriteLine($"Ostrzeżenie: Arkusz pozycji w pliku '{filePath}' nie zawiera danych. Pomijanie."); return positions; }

            var headers = Enumerable.Range(1, worksheet.Dimension.End.Column).Select(col => worksheet.Cells[1, col].Text.Trim()).ToList();

            int GetColIndex(string name, bool required = true)
            {
                int index = headers.FindIndex(h => h.Equals(name, StringComparison.OrdinalIgnoreCase));
                if (required && index == -1) throw new InvalidDataException($"Brak wymaganej kolumny '{name}' w pliku pozycji Excel: {filePath}");
                if (index == -1) Console.WriteLine($"Ostrzeżenie: Nie znaleziono kolumny '{name}' w pliku pozycji Excel: {filePath}");
                return index + 1;
            }

            for (int row = 2; row <= worksheet.Dimension.End.Row; row++)
            {
                try
                {
                    // Odczyt danych wiersza
                    var position = new PositionData {
                        NrRachunku = worksheet.Cells[row, GetColIndex("NrRachunku")]?.Text?.Replace(" ", ""),
                        Data = GetExcelDate(worksheet.Cells[row, GetColIndex("Data")]),
                        Kontrahent = worksheet.Cells[row, GetColIndex("Kontrahent", required: false)]?.Text,
                        NrRachunkuKontrahenta = worksheet.Cells[row, GetColIndex("NrRachunkuKontrahenta", required: false)]?.Text?.Replace(" ", ""),
                        Tytul = worksheet.Cells[row, GetColIndex("Tytul", required: false)]?.Text,
                        Kwota = GetExcelDecimal(worksheet.Cells[row, GetColIndex("Kwota")]),
                        SaldoKoncowe = GetExcelDecimal(worksheet.Cells[row, GetColIndex("SaldoKoncowe")])
                        // HeaderId zostanie ustawione później w Program.cs/Index.cshtml.cs
                    };

                    // Pomijamy wiersze bez daty (kluczowe dla JPK)
                    if (position.Data == null)
                    {
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.WriteLine($"Ostrzeżenie: Pomijanie wiersza {row} w pliku '{filePath}' z powodu braku daty.");
                        Console.ResetColor();
                        continue;
                    }
                    positions.Add(position);
                }
                catch (Exception ex)
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine($"Ostrzeżenie: Błąd podczas parsowania wiersza {row} w pliku pozycji Excel '{filePath}': {ex.Message}. Pomijanie wiersza.");
                    Console.ResetColor();
                    // Kontynuujemy przetwarzanie następnych wierszy
                }
            }
            return positions;
        }

        /// <summary>
        /// Pomocnicza metoda do bezpiecznego parsowania daty z komórki Excel. Obsługuje formaty liczbowe i tekstowe.
        /// </summary>
        /// <param name="cell">Komórka ExcelRange do sparsowania.</param>
        /// <returns>Data jako DateTime? lub null, jeśli parsowanie się nie powiodło.</returns>
        private DateTime? GetExcelDate(ExcelRange? cell)
        {
             if (cell?.Value == null) return null;
             try
             {
                 if (cell.Value is DateTime dt) return dt;
                 if (cell.Value is double oaDate) return DateTime.FromOADate(oaDate);
                 // Próba parsowania jako tekst (z różnymi kulturami dla pewności)
                 if (DateTime.TryParse(cell.Text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out DateTime parsedDate)) return parsedDate.ToLocalTime();
                 if (DateTime.TryParse(cell.Text, CultureInfo.GetCultureInfo("pl-PL"), DateTimeStyles.None, out parsedDate)) return parsedDate; // Spróbuj z polską kulturą
                 if (DateTime.TryParse(cell.Text, out parsedDate)) return parsedDate; // Domyślna kultura
             }
             catch (Exception ex) // Złap potencjalne błędy konwersji
             {
                  Console.WriteLine($"Ostrzeżenie: Błąd konwersji daty z komórki '{cell.Address}', wartość: '{cell.Text}', błąd: {ex.Message}");
                  return null;
             }
             Console.WriteLine($"Ostrzeżenie: Nie można sparsować daty z komórki '{cell.Address}', wartość: '{cell.Text}'");
             return null;
        }

        /// <summary>
        /// Pomocnicza metoda do bezpiecznego parsowania liczby (decimal) z komórki Excel.
        /// Uwzględnia różne typy liczbowe oraz separatory '.' i ','.
        /// </summary>
        /// <param name="cell">Komórka ExcelRange do sparsowania.</param>
        /// <returns>Liczba jako decimal? lub null, jeśli parsowanie się nie powiodło.</returns>
        private decimal? GetExcelDecimal(ExcelRange? cell)
        {
             if (cell?.Value == null) return null;
             try
             {
                 if (cell.Value is decimal dec) return dec;
                 if (cell.Value is double dbl) return (decimal)dbl; // Bezpośrednia konwersja double na decimal może tracić precyzję
                 if (cell.Value is int i) return i;
                 if (cell.Value is long l) return l;

                 // Próba parsowania jako tekst
                 string? textValue = cell.Text?.Trim();
                 if (string.IsNullOrWhiteSpace(textValue)) return null;

                 // Najpierw spróbuj z kropką (kultura niezmienna)
                 if (decimal.TryParse(textValue, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal resultInv)) return resultInv;
                 // Potem spróbuj z przecinkiem (kultura polska)
                 if (decimal.TryParse(textValue, NumberStyles.Any, CultureInfo.GetCultureInfo("pl-PL"), out decimal resultPl)) return resultPl;
                 // Spróbuj z domyślną kulturą systemu
                 if (decimal.TryParse(textValue, NumberStyles.Any, CultureInfo.CurrentCulture, out decimal resultCurr)) return resultCurr;

             }
             catch(Exception ex)
             {
                   Console.WriteLine($"Ostrzeżenie: Błąd konwersji liczby decimal z komórki '{cell.Address}', wartość: '{cell.Text}', błąd: {ex.Message}");
                   return null;
             }

             Console.WriteLine($"Ostrzeżenie: Nie można sparsować wartości decimal z komórki '{cell.Address}', wartość: '{cell.Text}'");
             return null;
        }


        // --- Metody prywatne (CSV) ---

        /// <summary>
        /// Wewnętrzna metoda wczytująca dane nagłówka z pliku CSV.
        /// Automatycznie wykrywa separator (',' lub ';'). Zakłada nagłówki w pierwszym wierszu, dane w drugim.
        /// Używa polskiej kultury do parsowania dat i liczb.
        /// </summary>
        private HeaderData ReadHeaderFromCsv(string filePath)
        {
            try
            {
                using var reader = new StreamReader(filePath);
                // Proste wykrywanie separatora na podstawie pierwszej linii
                string firstLine = reader.ReadLine() ?? "";
                reader.BaseStream.Position = 0; reader.DiscardBufferedData();
                char separator = firstLine.Contains(';') ? ';' : ',';

                var config = new CsvConfiguration(CultureInfo.GetCultureInfo("pl-PL"))
                {
                    Delimiter = separator.ToString(),
                    HeaderValidated = null, // Nie sprawdzaj nagłówków w CsvHelper
                    MissingFieldFound = null, // Nie rzucaj błędu jeśli brakuje kolumny
                    PrepareHeaderForMatch = args => args.Header.Trim().ToLower() // Dopasuj nagłówki case-insensitive
                };
                using var csv = new CsvReader(reader, config);

                if (!csv.Read() || !csv.ReadHeader()) throw new InvalidDataException($"Plik CSV nagłówka ({Path.GetFileName(filePath)}) nie zawiera nagłówka lub danych.");
                if (!csv.Read()) throw new InvalidDataException($"Plik CSV nagłówka ({Path.GetFileName(filePath)}) nie zawiera wiersza danych.");

                var header = csv.GetRecord<HeaderData>(); // Mapowanie po nazwach kolumn (case-insensitive)
                if (header != null)
                {
                    // Domyślne wartości i czyszczenie, jeśli CsvHelper nie ustawił lub dane są puste
                    if (string.IsNullOrWhiteSpace(header.KodKraju)) header.KodKraju = "PL";
                    if (string.IsNullOrWhiteSpace(header.KodWaluty)) header.KodWaluty = "PLN";
                    if (header.NumerRachunku != null) header.NumerRachunku = header.NumerRachunku.Replace(" ", "");
                } else { throw new InvalidDataException($"Nie udało się odczytać rekordu nagłówka z pliku CSV: {Path.GetFileName(filePath)}"); }
                return header;
            }
            catch (Exception ex) // Łapanie ogólnych błędów (np. I/O) oraz błędów CsvHelper
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Błąd podczas czytania pliku nagłówka CSV '{filePath}': {ex.Message}");
                Console.ResetColor();
                throw new InvalidDataException($"Nie udało się odczytać pliku nagłówka CSV: {filePath}", ex);
            }
        }

        /// <summary>
        /// Wewnętrzna metoda wczytująca dane pozycji z pliku CSV.
        /// Automatycznie wykrywa separator (',' lub ';'). Zakłada nagłówki w pierwszym wierszu.
        /// Używa polskiej kultury. Pomija wiersze bez daty. Obsługuje błędy parsowania pojedynczych wierszy.
        /// </summary>
        private List<PositionData> ReadPositionsFromCsv(string filePath)
        {
           var records = new List<PositionData>();
           try
           {
               using var reader = new StreamReader(filePath);
               string firstLine = reader.ReadLine() ?? "";
               reader.BaseStream.Position = 0; reader.DiscardBufferedData();
               char separator = firstLine.Contains(';') ? ';' : ',';

               var config = new CsvConfiguration(CultureInfo.GetCultureInfo("pl-PL")) { /* ... jak poprzednio ... */ };
               using var csv = new CsvReader(reader, config);
               if (!csv.Read() || !csv.ReadHeader()) {
                    Console.WriteLine($"Ostrzeżenie: Plik CSV pozycji '{filePath}' nie zawiera nagłówka lub danych. Pomijanie.");
                    return records; // Zwróć pustą listę
               }

               while (csv.Read())
               {
                   try
                   {
                       var record = csv.GetRecord<PositionData>();
                       if (record != null)
                       {
                           // Czyszczenie numerów rachunków
                           if (record.NrRachunku != null) record.NrRachunku = record.NrRachunku.Replace(" ", "");
                           if (record.NrRachunkuKontrahenta != null) record.NrRachunkuKontrahenta = record.NrRachunkuKontrahenta.Replace(" ", "");
                           // Walidacja - pomijamy wiersze bez daty
                           if (record.Data == null)
                           {
                               Console.ForegroundColor = ConsoleColor.Yellow;
                               Console.WriteLine($"Ostrzeżenie: Pomijanie wiersza {csv.Context.Parser.Row} w pliku CSV '{filePath}' z powodu braku daty.");
                               Console.ResetColor();
                               continue;
                           }
                           // HeaderId zostanie ustawione później
                           records.Add(record);
                       }
                   }
                   catch (CsvHelperException csvEx) // Błąd parsowania wiersza przez CsvHelper
                   {
                       Console.ForegroundColor = ConsoleColor.Yellow;
                       Console.WriteLine($"Ostrzeżenie: Błąd parsowania CsvHelper wiersza {csvEx.Context?.Parser?.Row ?? -1} pliku '{filePath}': {csvEx.Message}. Pomijanie wiersza.");
                       Console.ResetColor();
                   }
               } // koniec while
               return records;
           }
           catch (Exception ex) // Inne błędy (np. I/O)
           {
               Console.ForegroundColor = ConsoleColor.Red;
               Console.WriteLine($"Błąd podczas czytania pliku pozycji CSV '{filePath}': {ex.Message}");
               Console.ResetColor();
               throw new InvalidDataException($"Nie udało się odczytać pliku pozycji CSV: {filePath}", ex);
           }
        } // koniec ReadPositionsFromCsv

    } // koniec klasy FileReader
} // koniec namespace
// --- END FILE: FileReader.cs ---