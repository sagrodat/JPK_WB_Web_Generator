// --- START FILE: DatabaseWriter.cs ---
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;

namespace JpkWebGenerator.Services
{
    /// <summary>
    /// Odpowiada za interakcje z bazą danych MS SQL Server.
    /// Zawiera logikę tworzenia/czyszczenia tabel, zapisu danych nagłówka i pozycji,
    /// wywoływania procedur walidacji i generowania XML oraz pobierania błędów walidacji.
    /// </summary>
    public class DatabaseWriter
    {
        /// <summary>
        /// Ciąg połączenia do bazy danych, przekazywany przy tworzeniu instancji.
        /// </summary>
        private readonly string _connectionString;

        /// <summary>
        /// Inicjalizuje nową instancję klasy <see cref="DatabaseWriter"/>.
        /// </summary>
        /// <param name="connectionString">Ciąg połączenia do bazy danych MS SQL Server.</param>
        public DatabaseWriter(string connectionString)
        {
            _connectionString = connectionString;
        }


        /// <summary>
        /// Sprawdza istnienie wymaganych tabel (Headers, Positions, ValidationErrors) w bazie danych.
        /// Jeśli tabela nie istnieje, tworzy ją zgodnie z wymaganą strukturą (bez kolumny ImportTimestamp).
        /// Jeśli tabela ValidationErrors istnieje, ale brakuje kolumny HeaderId, dodaje ją.
        /// </summary>
        /// <exception cref="SqlException">Rzucany w przypadku błędu SQL podczas sprawdzania/tworzenia tabel.</exception>
        public async Task EnsureTablesExistAsync()
        {
            // Definicja SQL tworząca/modyfikująca tabele BEZ ImportTimestamp
            string sql = @"
            IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = 'dbo' AND TABLE_NAME = 'Headers') BEGIN
                CREATE TABLE dbo.Headers (
                    HeaderId INT IDENTITY(1,1) PRIMARY KEY, NIP VARCHAR(10) NULL, REGON VARCHAR(14) NULL, NazwaFirmy NVARCHAR(255) NULL,
                    KodKraju CHAR(2) NULL, Wojewodztwo NVARCHAR(100) NULL, Powiat NVARCHAR(100) NULL, Gmina NVARCHAR(100) NULL,
                    Ulica NVARCHAR(100) NULL, NrDomu VARCHAR(10) NULL, NrLokalu VARCHAR(10) NULL, Miejscowosc NVARCHAR(100) NULL,
                    KodPocztowy VARCHAR(6) NULL, Poczta NVARCHAR(100) NULL, NumerRachunku VARCHAR(34) NULL, DataOd DATE NULL,
                    DataDo DATE NULL, KodWaluty CHAR(3) NULL, KodUrzedu VARCHAR(4) NULL, SaldoPoczatkowe DECIMAL(18, 2) NULL
                ); PRINT 'Tabela Headers została utworzona.'; END;

            IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = 'dbo' AND TABLE_NAME = 'Positions') BEGIN
                CREATE TABLE dbo.Positions (
                    PositionId INT IDENTITY(1,1) PRIMARY KEY, NrRachunku VARCHAR(34) NULL, Data DATE NULL, Kontrahent NVARCHAR(255) NULL,
                    NrRachunkuKontrahenta VARCHAR(34) NULL, Tytul NVARCHAR(255) NULL, Kwota DECIMAL(18, 2) NULL,
                    SaldoKoncowe DECIMAL(18, 2) NULL, HeaderId INT NOT NULL -- Dodano HeaderId
                ); PRINT 'Tabela Positions została utworzona.'; END;

            IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = 'dbo' AND TABLE_NAME = 'ValidationErrors') BEGIN
                 CREATE TABLE dbo.ValidationErrors ( ErrorId INT IDENTITY(1,1) PRIMARY KEY, HeaderId INT NULL, TableName NVARCHAR(128) NOT NULL,
                     RecordId INT NULL, ColumnName NVARCHAR(128) NULL, ErrorCode NVARCHAR(50) NOT NULL, ErrorMessage NVARCHAR(MAX) NOT NULL,
                     ErrorTimestamp DATETIME2 DEFAULT(SYSDATETIME()) ); PRINT 'Tabela ValidationErrors została utworzona.'; END
            ELSE IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = 'dbo' AND TABLE_NAME = 'ValidationErrors' AND COLUMN_NAME = 'HeaderId') BEGIN
                 ALTER TABLE dbo.ValidationErrors ADD HeaderId INT NULL; PRINT 'Dodano kolumnę HeaderId do tabeli ValidationErrors.'; END;
            ";
            using var connection = new SqlConnection(_connectionString);
            using var command = new SqlCommand(sql, connection);
            try
            {
                await connection.OpenAsync();
                await command.ExecuteNonQueryAsync();
                Console.WriteLine("Sprawdzono/utworzono/zmodyfikowano tabele.");
            }
            catch (SqlException ex)
            {
                 Console.ForegroundColor = ConsoleColor.Red;
                 Console.WriteLine($"Błąd SQL podczas EnsureTablesExistAsync: {ex.Message}");
                 if (ex.Errors.Count > 0) { Console.WriteLine($"Szczegóły błędów SQL ({ex.Errors.Count}):"); foreach (SqlError error in ex.Errors) { Console.WriteLine($"  - Błąd nr {error.Number}, Stan {error.State}, Linia {error.LineNumber}: {error.Message}"); } }
                 Console.ResetColor();
                 throw;
             }
        }

        /// <summary>
        /// Wstawia pojedynczy rekord nagłówka do tabeli dbo.Headers i zwraca jego ID.
        /// </summary>
        /// <param name="header">Obiekt HeaderData z danymi do wstawienia.</param>
        /// <param name="openingBalance">Przechwycone saldo początkowe do zapisania w kolumnie SaldoPoczatkowe.</param>
        /// <returns>ID (HeaderId) nowo wstawionego rekordu nagłówka.</returns>
        /// <exception cref="InvalidOperationException">Rzucany, gdy nie uda się pobrać ID po wstawieniu.</exception>
        /// <exception cref="SqlException">Rzucany w przypadku błędu SQL podczas operacji INSERT.</exception>
        public async Task<int> InsertHeaderDataAsync(HeaderData header, decimal? openingBalance)
        {
            // SQL INSERT bez ImportTimestamp, zwraca ID za pomocą SCOPE_IDENTITY()
            string sql = @"
                INSERT INTO dbo.Headers ( NIP, REGON, NazwaFirmy, KodKraju, Wojewodztwo, Powiat, Gmina, Ulica, NrDomu, NrLokalu, Miejscowosc, KodPocztowy, Poczta, NumerRachunku, DataOd, DataDo, KodWaluty, KodUrzedu, SaldoPoczatkowe )
                VALUES ( @NIP, @REGON, @NazwaFirmy, @KodKraju, @Wojewodztwo, @Powiat, @Gmina, @Ulica, @NrDomu, @NrLokalu, @Miejscowosc, @KodPocztowy, @Poczta, @NumerRachunku, @DataOd, @DataDo, @KodWaluty, @KodUrzedu, @SaldoPoczatkowe );
                SELECT SCOPE_IDENTITY();";
            using var connection = new SqlConnection(_connectionString);
            using var command = new SqlCommand(sql, connection);

            // Parametryzacja zapytania
            command.Parameters.AddWithValue("@NIP", (object)header.NIP ?? DBNull.Value);
            command.Parameters.AddWithValue("@REGON", (object)header.REGON ?? DBNull.Value);
            command.Parameters.AddWithValue("@NazwaFirmy", (object)header.NazwaFirmy ?? DBNull.Value);
            command.Parameters.AddWithValue("@KodKraju", (object)header.KodKraju ?? DBNull.Value);
            command.Parameters.AddWithValue("@Wojewodztwo", (object)header.Wojewodztwo ?? DBNull.Value);
            command.Parameters.AddWithValue("@Powiat", (object)header.Powiat ?? DBNull.Value);
            command.Parameters.AddWithValue("@Gmina", (object)header.Gmina ?? DBNull.Value);
            command.Parameters.AddWithValue("@Ulica", (object)header.Ulica ?? DBNull.Value);
            command.Parameters.AddWithValue("@NrDomu", (object)header.NrDomu ?? DBNull.Value);
            command.Parameters.AddWithValue("@NrLokalu", (object)header.NrLokalu ?? DBNull.Value);
            command.Parameters.AddWithValue("@Miejscowosc", (object)header.Miejscowosc ?? DBNull.Value);
            command.Parameters.AddWithValue("@KodPocztowy", (object)header.KodPocztowy ?? DBNull.Value);
            command.Parameters.AddWithValue("@Poczta", (object)header.Poczta ?? DBNull.Value);
            command.Parameters.AddWithValue("@NumerRachunku", (object)header.NumerRachunku ?? DBNull.Value);
            command.Parameters.AddWithValue("@DataOd", (object)header.DataOd ?? DBNull.Value);
            command.Parameters.AddWithValue("@DataDo", (object)header.DataDo ?? DBNull.Value);
            command.Parameters.AddWithValue("@KodWaluty", (object)header.KodWaluty ?? DBNull.Value);
            command.Parameters.AddWithValue("@KodUrzedu", (object)header.KodUrzedu ?? DBNull.Value);
            command.Parameters.AddWithValue("@SaldoPoczatkowe", (object)openingBalance ?? DBNull.Value);

            try
            {
                await connection.OpenAsync();
                var result = await command.ExecuteScalarAsync(); // Oczekujemy zwróconego ID
                Console.WriteLine("Zapisano dane nagłówka do bazy.");
                if (result != null && result != DBNull.Value)
                {
                    // SCOPE_IDENTITY() zwraca decimal, konwertujemy na int
                    return Convert.ToInt32(result);
                }
                else
                {
                    // To nie powinno się zdarzyć przy poprawnym IDENTITY, ale dla bezpieczeństwa
                    throw new InvalidOperationException("Nie udało się uzyskać ID wstawionego nagłówka.");
                }
            }
            catch (SqlException ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Błąd SQL podczas wstawiania danych nagłówka: {ex.Message}");
                if (ex.Errors.Count > 0) { Console.WriteLine($"Szczegóły błędów SQL ({ex.Errors.Count}):"); foreach (SqlError error in ex.Errors) { Console.WriteLine($"  - Błąd nr {error.Number}, Stan {error.State}, Linia {error.LineNumber}: {error.Message}"); } }
                Console.ResetColor();
                throw;
            }
        }

        /// <summary>
        /// Wstawia listę pozycji (transakcji) do tabeli dbo.Positions za pomocą SqlBulkCopy.
        /// Filtruje wiersze bez kwoty (traktowane jako saldo początkowe).
        /// Używa HeaderId do powiązania z nagłówkiem.
        /// </summary>
        /// <param name="allPositions">Pełna lista obiektów PositionData wczytanych z plików (może zawierać wiersz salda).</param>
        /// <exception cref="SqlException">Rzucany w przypadku błędu SQL podczas operacji SqlBulkCopy.</exception>
        public async Task InsertPositionDataBulkAsync(List<PositionData> allPositions)
        {
            if (allPositions == null || !allPositions.Any()) { Console.WriteLine("Brak danych pozycji do zapisania."); return; }

            // Filtrowanie: Bierzemy tylko wiersze, które mają kwotę (czyli nie są saldem początkowym)
            var transactionsToInsert = allPositions.Where(p => p.Kwota != null).ToList();
            if (!transactionsToInsert.Any()) { Console.WriteLine("Brak transakcji (pozycji z kwotą) do zapisania."); return; }

            Console.WriteLine($"Przygotowywanie {transactionsToInsert.Count} transakcji do zapisu...");

            // Tworzenie DataTable jako źródła dla SqlBulkCopy
            var dataTable = new DataTable("PositionsType");
            dataTable.Columns.Add("NrRachunku", typeof(string));
            dataTable.Columns.Add("Data", typeof(DateTime));
            dataTable.Columns.Add("Kontrahent", typeof(string));
            dataTable.Columns.Add("NrRachunkuKontrahenta", typeof(string));
            dataTable.Columns.Add("Tytul", typeof(string));
            dataTable.Columns.Add("Kwota", typeof(decimal));
            dataTable.Columns.Add("SaldoKoncowe", typeof(decimal));
            dataTable.Columns.Add("HeaderId", typeof(int)); // Dodano HeaderId

            foreach (var pos in transactionsToInsert)
            {
                dataTable.Rows.Add(
                   (object)pos.NrRachunku ?? DBNull.Value,
                   pos.Data, // Zakładamy, że Data nie jest null po filtracji w FileReader
                   (object)pos.Kontrahent ?? DBNull.Value,
                   (object)pos.NrRachunkuKontrahenta ?? DBNull.Value,
                   (object)pos.Tytul ?? DBNull.Value,
                   pos.Kwota, // Kwota nie jest null po filtrowaniu .Where()
                   (object)pos.SaldoKoncowe ?? DBNull.Value, // Saldo może być null? Poprawione na AllowDBNull=true w tabeli
                   pos.HeaderId // Przekazujemy HeaderId
               );
            }

            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();
            using var bulkCopy = new SqlBulkCopy(connection);
            bulkCopy.DestinationTableName = "dbo.Positions";

            // Mapowania kolumn - teraz z HeaderId
            bulkCopy.ColumnMappings.Add("NrRachunku", "NrRachunku");
            bulkCopy.ColumnMappings.Add("Data", "Data");
            bulkCopy.ColumnMappings.Add("Kontrahent", "Kontrahent");
            bulkCopy.ColumnMappings.Add("NrRachunkuKontrahenta", "NrRachunkuKontrahenta");
            bulkCopy.ColumnMappings.Add("Tytul", "Tytul");
            bulkCopy.ColumnMappings.Add("Kwota", "Kwota");
            bulkCopy.ColumnMappings.Add("SaldoKoncowe", "SaldoKoncowe");
            bulkCopy.ColumnMappings.Add("HeaderId", "HeaderId");

             try
             {
                 await bulkCopy.WriteToServerAsync(dataTable);
                 Console.WriteLine($"Zapisano {transactionsToInsert.Count} transakcji do bazy.");
             }
             catch (SqlException ex)
             {
                  Console.ForegroundColor = ConsoleColor.Red;
                  Console.WriteLine($"Błąd SQL podczas masowego wstawiania danych pozycji (SqlBulkCopy): {ex.Message}");
                  if (ex.Errors.Count > 0) {
                       Console.WriteLine($"Szczegóły błędów SQL ({ex.Errors.Count}):");
                       foreach (SqlError error in ex.Errors) { Console.WriteLine($"  - Błąd nr {error.Number}, Stan {error.State}, Linia {error.LineNumber}: {error.Message}"); }
                  }
                  Console.ResetColor();
                  throw;
             }
        }

        /// <summary>
        /// Wywołuje procedurę składowaną dbo.usp_ValidateImportData w bazie danych.
        /// </summary>
        /// <param name="headerId">ID nagłówka (z tabeli dbo.Headers), dla którego ma być przeprowadzona walidacja.</param>
        /// <returns>Liczba znalezionych błędów walidacji lub -1 w przypadku błędu wykonania procedury SQL.</returns>
        /// <exception cref="SqlException">Rzucany w przypadku błędu połączenia lub wykonania procedury.</exception>
        public async Task<int> ValidateImportAsync(int headerId)
        {
            Console.WriteLine($"Wywoływanie procedury walidującej dla HeaderId = {headerId}...");
            int errorCount = -1;
            using (SqlConnection validationConnection = new SqlConnection(_connectionString))
            using (SqlCommand validationCommand = new SqlCommand("dbo.usp_ValidateImportData", validationConnection))
            {
                validationCommand.CommandType = CommandType.StoredProcedure;
                validationCommand.Parameters.Add(new SqlParameter("@HeaderId", SqlDbType.Int) { Value = headerId });
                try
                {
                    await validationConnection.OpenAsync();
                    var result = await validationCommand.ExecuteScalarAsync(); // Procedura zwraca liczbę błędów
                    if (result != null && result != DBNull.Value) { errorCount = Convert.ToInt32(result); }
                }
                catch (SqlException ex)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"Błąd SQL podczas wywoływania procedury walidującej: {ex.Message}");
                    if (ex.Errors.Count > 0) { Console.WriteLine($"Szczegóły błędów SQL ({ex.Errors.Count}):"); foreach (SqlError error in ex.Errors) { Console.WriteLine($"  - Błąd nr {error.Number}, Stan {error.State}, Linia {error.LineNumber}: {error.Message}"); } }
                    Console.ResetColor();
                    throw; // Rzuć wyjątek dalej
                }
            }
            return errorCount;
        }

        /// <summary>
        /// Pobiera listę błędów walidacji dla danego nagłówka z tabeli dbo.ValidationErrors.
        /// </summary>
        /// <param name="headerId">ID nagłówka (z tabeli dbo.Headers), dla którego pobierane są błędy.</param>
        /// <returns>Lista obiektów ValidationError.</returns>
        /// <exception cref="SqlException">Rzucany w przypadku błędu połączenia lub wykonania zapytania SELECT.</exception>
        public async Task<List<ValidationError>> GetValidationErrorsAsync(int headerId)
        {
            var errors = new List<ValidationError>();
            // Zapytanie pobierające błędy dla danego HeaderId
            string sql = @"SELECT ErrorId, HeaderId, TableName, RecordId, ColumnName, ErrorCode, ErrorMessage, ErrorTimestamp FROM dbo.ValidationErrors WHERE HeaderId = @HeaderId ORDER BY ErrorId;";
            Console.WriteLine($"Pobieranie błędów walidacji dla HeaderId = {headerId}...");
            using var connection = new SqlConnection(_connectionString);
            using var command = new SqlCommand(sql, connection);
            command.Parameters.Add(new SqlParameter("@HeaderId", SqlDbType.Int) { Value = headerId });
            try
            {
                await connection.OpenAsync();
                using (var reader = await command.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        // Mapowanie danych z readera na obiekt ValidationError
                        var error = new ValidationError
                        {
                            ErrorId = reader.GetInt32(reader.GetOrdinal("ErrorId")),
                            HeaderId = reader.IsDBNull(reader.GetOrdinal("HeaderId")) ? (int?)null : reader.GetInt32(reader.GetOrdinal("HeaderId")),
                            TableName = reader.GetString(reader.GetOrdinal("TableName")),
                            RecordId = reader.IsDBNull(reader.GetOrdinal("RecordId")) ? (int?)null : reader.GetInt32(reader.GetOrdinal("RecordId")),
                            ColumnName = reader.IsDBNull(reader.GetOrdinal("ColumnName")) ? null : reader.GetString(reader.GetOrdinal("ColumnName")),
                            ErrorCode = reader.GetString(reader.GetOrdinal("ErrorCode")),
                            ErrorMessage = reader.GetString(reader.GetOrdinal("ErrorMessage")),
                            ErrorTimestamp = reader.GetDateTime(reader.GetOrdinal("ErrorTimestamp"))
                        };
                        errors.Add(error);
                    }
                }
                Console.WriteLine($"Pobrano {errors.Count} błędów walidacji.");
            }
            catch (SqlException ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Błąd SQL podczas pobierania błędów walidacji dla HeaderId={headerId}: {ex.Message}");
                if (ex.Errors.Count > 0) { Console.WriteLine($"Szczegóły błędów SQL ({ex.Errors.Count}):"); foreach (SqlError error in ex.Errors) { Console.WriteLine($"  - Błąd nr {error.Number}, Stan {error.State}, Linia {error.LineNumber}: {error.Message}"); } }
                Console.ResetColor();
                throw;
            }
            return errors;
        }

        /// <summary>
        /// Wywołuje procedurę składowaną dbo.usp_GenerateJPK_WB_XML w bazie danych.
        /// Przed wywołaniem sprawdza, czy walidacja dla danego nagłówka zakończyła się sukcesem.
        /// </summary>
        /// <param name="headerId">ID nagłówka (z tabeli dbo.Headers), dla którego ma być wygenerowany XML.</param>
        /// <returns>Ciąg znaków zawierający wygenerowany XML lub null, jeśli walidacja się nie powiodła lub procedura SQL zwróciła błąd/null.</returns>
        /// <exception cref="SqlException">Rzucany w przypadku błędu połączenia lub wykonania procedury SQL.</exception>
        public async Task<string?> GenerateXmlAsync(int headerId)
        {
            Console.WriteLine($"Wywoływanie procedury generującej XML dla HeaderId = {headerId}...");
            string? generatedXml = null;

            // Sprawdzenie wyniku poprzedniej walidacji przed generowaniem XML
            int errorCheck = await ValidateImportAsync(headerId);
            if (errorCheck != 0)
            {
                Console.WriteLine($"Walidacja dla HeaderId={headerId} zwróciła {errorCheck} błędów. Przerywanie generowania XML.");
                return null; // Zwracamy null, aby zasygnalizować problem
            }

            using (SqlConnection connection = new SqlConnection(_connectionString))
            using (SqlCommand command = new SqlCommand("dbo.usp_GenerateJPK_WB_XML", connection))
            {
                command.CommandType = CommandType.StoredProcedure;
                command.Parameters.Add(new SqlParameter("@HeaderId", SqlDbType.Int) { Value = headerId });
                try
                {
                    await connection.OpenAsync();
                    var result = await command.ExecuteScalarAsync(); // Oczekujemy XML jako string
                    if (result != null && result != DBNull.Value)
                    {
                        generatedXml = result.ToString();
                        // Sprawdzenie, czy procedura nie zwróciła wewnętrznego błędu jako XML
                         if(generatedXml != null && generatedXml.StartsWith("<Error")) {
                              Console.ForegroundColor = ConsoleColor.Red;
                              Console.WriteLine($"Procedura SQL usp_GenerateJPK_WB_XML zwróciła błąd: {generatedXml}");
                              Console.ResetColor();
                              return null;
                         }
                         Console.WriteLine($"Pomyślnie pobrano XML z SQL Server dla HeaderId={headerId}.");
                    }
                    else { Console.WriteLine($"Procedura SQL usp_GenerateJPK_WB_XML nie zwróciła wyniku dla HeaderId={headerId}."); }
                }
                catch (SqlException ex)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"Błąd SQL podczas generowania XML dla HeaderId={headerId}: {ex.Message}");
                    if (ex.Errors.Count > 0) { Console.WriteLine($"Szczegóły błędów SQL ({ex.Errors.Count}):"); foreach (SqlError error in ex.Errors) { Console.WriteLine($"  - Błąd nr {error.Number}, Stan {error.State}, Linia {error.LineNumber}: {error.Message}"); } }
                    Console.ResetColor();
                    throw; // Rzuć wyjątek, aby obsłużyć go wyżej
                }
            }
            return generatedXml;
        }
    }
}
// --- END FILE: DatabaseWriter.cs ---