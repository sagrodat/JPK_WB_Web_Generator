using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using CsvHelper;
using CsvHelper.Configuration;
using OfficeOpenXml;

namespace JpkWebGenerator.Services // Upewnij się, że namespace pasuje do Twojego projektu
{
    public class FileReader
    {
        public FileReader()
        {
            // Upewnij się, że licencja EPPlus jest ustawiona w Program.cs
        }

        // --- Metody publiczne ---
        public HeaderData ReadHeaderFile(string filePath)
        {
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

        public List<PositionData> ReadPositionFiles(List<string> filePaths)
        {
            var allPositions = new List<PositionData>();
            Console.WriteLine($"Wczytywanie plików pozycji ({filePaths.Count}):");
            foreach (string filePath in filePaths)
            {
                Console.WriteLine($"  - {filePath}");
                string extension = Path.GetExtension(filePath).ToLowerInvariant();
                List<PositionData> positions;
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
            Console.WriteLine($"Łącznie wczytano {allPositions.Count} pozycji.");
            return allPositions;
        }


        // --- Metody prywatne (Excel) ---
        private HeaderData ReadHeaderFromExcel(string filePath)
        {
            // Użyj using, aby upewnić się, że zasoby są zwolnione
            using var package = new ExcelPackage(new FileInfo(filePath));
            var worksheet = package.Workbook.Worksheets.FirstOrDefault();
            if (worksheet == null) throw new InvalidDataException("Plik Excel nagłówkowy jest pusty lub nie zawiera arkuszy.");

            if (worksheet.Dimension == null || worksheet.Dimension.Rows < 2)
                throw new InvalidDataException("Arkusz nagłówkowy nie zawiera wystarczającej liczby wierszy (oczekiwano nagłówków w wierszu 1 i danych w wierszu 2).");

            var headers = Enumerable.Range(1, worksheet.Dimension.End.Column)
                                    .Select(col => worksheet.Cells[1, col].Text.Trim())
                                    .ToList();

            int GetColIndex(string name)
            {
                int index = headers.FindIndex(h => h.Equals(name, StringComparison.OrdinalIgnoreCase)); // Ignoruj wielkość liter
                if (index == -1) Console.WriteLine($"Ostrzeżenie: Nie znaleziono kolumny '{name}' w pliku nagłówka Excel.");
                return index + 1; // EPPlus indeksuje od 1
            }

            var headerData = new HeaderData();
            try
            {
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
                Console.WriteLine($"Błąd podczas parsowania danych nagłówka z Excel: {ex.Message}");
                Console.ResetColor();
                throw new InvalidDataException("Nie udało się sparsować danych nagłówka z pliku Excel.", ex);
            }
            return headerData;
        }

        private List<PositionData> ReadPositionsFromExcel(string filePath)
        {
            var positions = new List<PositionData>();
            using var package = new ExcelPackage(new FileInfo(filePath));
            var worksheet = package.Workbook.Worksheets.FirstOrDefault();
            if (worksheet == null)
            {
                Console.WriteLine($"Ostrzeżenie: Plik Excel pozycji '{filePath}' jest pusty lub nie zawiera arkuszy. Pomijanie pliku.");
                return positions;
            }

            if (worksheet.Dimension == null || worksheet.Dimension.Rows < 2)
            {
                Console.WriteLine($"Ostrzeżenie: Arkusz pozycji w pliku '{filePath}' nie zawiera danych. Pomijanie pliku.");
                return positions;
            }

            var headers = Enumerable.Range(1, worksheet.Dimension.End.Column)
                                    .Select(col => worksheet.Cells[1, col].Text.Trim())
                                    .ToList();

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
                    var position = new PositionData
                    {
                        NrRachunku = worksheet.Cells[row, GetColIndex("NrRachunku")]?.Text?.Replace(" ", ""),
                        Data = GetExcelDate(worksheet.Cells[row, GetColIndex("Data")]),
                        Kontrahent = worksheet.Cells[row, GetColIndex("Kontrahent", required: false)]?.Text,
                        NrRachunkuKontrahenta = worksheet.Cells[row, GetColIndex("NrRachunkuKontrahenta", required: false)]?.Text?.Replace(" ", ""),
                        Tytul = worksheet.Cells[row, GetColIndex("Tytul", required: false)]?.Text,
                        Kwota = GetExcelDecimal(worksheet.Cells[row, GetColIndex("Kwota")]),
                        SaldoKoncowe = GetExcelDecimal(worksheet.Cells[row, GetColIndex("SaldoKoncowe")])
                    };
                    // Prosta walidacja - pomiń wiersze bez kluczowych danych (np. daty lub kwoty) // na razie kwote pomijamy bo bedzie trzeba jakos wczytac saldo poczatkowe!
                    if (position.Data == null)
                    {
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.WriteLine($"Ostrzeżenie: Pomijanie wiersza {row} w pliku '{filePath}' z powodu braku daty lub kwoty.");
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
                }
            }
            return positions;
        }

        private DateTime? GetExcelDate(ExcelRange cell)
        { /* Implementacja jak w poprzedniej wersji DataLoader */
            if (cell?.Value == null) return null;
            if (cell.Value is DateTime dt) return dt;
            if (cell.Value is double oaDate) return DateTime.FromOADate(oaDate);
            if (DateTime.TryParse(cell.Text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out DateTime parsedDate)) return parsedDate;
            if (DateTime.TryParse(cell.Text, CultureInfo.GetCultureInfo("pl-PL"), DateTimeStyles.AssumeLocal, out parsedDate)) return parsedDate;
            Console.WriteLine($"Ostrzeżenie: Nie można sparsować daty z komórki '{cell.Address}', wartość: '{cell.Text}'");
            return null;
        }
        private decimal? GetExcelDecimal(ExcelRange cell)
        { /* Implementacja jak w poprzedniej wersji DataLoader */
            if (cell?.Value == null) return null;
            if (cell.Value is decimal dec) return dec;
            if (cell.Value is double dbl) return (decimal)dbl;
            if (cell.Value is int i) return i;
            if (cell.Value is long l) return l;
            string? textValue = cell.Text?.Trim();
            if (string.IsNullOrWhiteSpace(textValue)) return null;
            if (decimal.TryParse(textValue, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal resultInv)) return resultInv;
            if (decimal.TryParse(textValue, NumberStyles.Any, CultureInfo.GetCultureInfo("pl-PL"), out decimal resultPl)) return resultPl;
            Console.WriteLine($"Ostrzeżenie: Nie można sparsować wartości decimal z komórki '{cell.Address}', wartość: '{cell.Text}'");
            return null;
        }

        // --- Metody prywatne (CSV) ---
        private HeaderData ReadHeaderFromCsv(string filePath)
        { /* Implementacja jak w poprzedniej wersji DataLoader */
            try
            {
                using var reader = new StreamReader(filePath);
                string firstLine = reader.ReadLine() ?? "";
                reader.BaseStream.Position = 0; reader.DiscardBufferedData();
                char separator = firstLine.Contains(';') ? ';' : ',';
                var config = new CsvConfiguration(CultureInfo.GetCultureInfo("pl-PL")) { Delimiter = separator.ToString(), HeaderValidated = null, MissingFieldFound = null, PrepareHeaderForMatch = args => args.Header.Trim().ToLower() };
                using var csv = new CsvReader(reader, config);
                // Zakładamy, że plik CSV nagłówkowy ma tylko jeden wiersz danych po nagłówku
                if (!csv.Read() || !csv.ReadHeader()) throw new InvalidDataException("Plik CSV nagłówka nie zawiera nagłówka lub danych.");
                if (!csv.Read()) throw new InvalidDataException("Plik CSV nagłówka nie zawiera wiersza danych.");

                var header = csv.GetRecord<HeaderData>();
                if (header != null)
                {
                    if (string.IsNullOrWhiteSpace(header.KodKraju)) header.KodKraju = "PL";
                    if (string.IsNullOrWhiteSpace(header.KodWaluty)) header.KodWaluty = "PLN";
                    if (header.NumerRachunku != null) header.NumerRachunku = header.NumerRachunku.Replace(" ", "");
                }
                else
                {
                    throw new InvalidDataException("Nie udało się odczytać rekordu nagłówka z pliku CSV.");
                }
                return header;
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Błąd podczas czytania pliku nagłówka CSV '{filePath}': {ex.Message}");
                Console.ResetColor();
                throw new InvalidDataException($"Nie udało się odczytać pliku nagłówka CSV: {filePath}", ex);
            }
        }
        private List<PositionData> ReadPositionsFromCsv(string filePath)
        { /* Implementacja jak w poprzedniej wersji DataLoader */
            try
            {
                using var reader = new StreamReader(filePath);
                string firstLine = reader.ReadLine() ?? "";
                reader.BaseStream.Position = 0; reader.DiscardBufferedData();
                char separator = firstLine.Contains(';') ? ';' : ',';
                var config = new CsvConfiguration(CultureInfo.GetCultureInfo("pl-PL")) { Delimiter = separator.ToString(), HeaderValidated = null, MissingFieldFound = null, PrepareHeaderForMatch = args => args.Header.Trim().ToLower() };
                using var csv = new CsvReader(reader, config);
                var records = new List<PositionData>();
                csv.Read(); csv.ReadHeader(); // Czytaj nagłówki
                while (csv.Read())
                {
                    try
                    {
                        var record = csv.GetRecord<PositionData>();
                        if (record != null)
                        {
                            if (record.NrRachunku != null) record.NrRachunku = record.NrRachunku.Replace(" ", "");
                            if (record.NrRachunkuKontrahenta != null) record.NrRachunkuKontrahenta = record.NrRachunkuKontrahenta.Replace(" ", "");
                            // Prosta walidacja - pomiń wiersze bez kluczowych danych
                            if (record.Data == null)
                            {
                                Console.ForegroundColor = ConsoleColor.Yellow;
                                Console.WriteLine($"Ostrzeżenie: Pomijanie wiersza {csv.Context.Parser.Row} w pliku CSV '{filePath}' z powodu braku daty lub kwoty.");
                                Console.ResetColor();
                                continue;
                            }
                            records.Add(record);
                        }
                    }
                    catch (CsvHelperException ex)
                    {
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.WriteLine($"Ostrzeżenie: Błąd podczas parsowania wiersza {csv.Context.Parser.Row} w pliku pozycji CSV '{filePath}': {ex.Message}. Pomijanie wiersza.");
                        Console.ResetColor();
                    }
                }
                return records;
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Błąd podczas czytania pliku pozycji CSV '{filePath}': {ex.Message}");
                Console.ResetColor();
                throw new InvalidDataException($"Nie udało się odczytać pliku pozycji CSV: {filePath}", ex);
            }
        }
    }
}