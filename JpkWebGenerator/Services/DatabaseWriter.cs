using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;

namespace JpkWebGenerator.Services // Upewnij się, że namespace pasuje
{
    public class DatabaseWriter
    {
        private readonly string _connectionString;

        public DatabaseWriter(string connectionString)
        {
            _connectionString = connectionString;
        }

        // Metoda do czyszczenia tabel (z pełnym catch)
        public async Task ClearDataTablesAsync()
        {
            string sql = @"
                IF EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = 'dbo' AND TABLE_NAME = 'ValidationErrors') BEGIN DELETE FROM dbo.ValidationErrors; PRINT 'Usunięto dane z tabeli ValidationErrors.'; END ELSE PRINT 'Tabela ValidationErrors nie istnieje, pomijanie czyszczenia.';
                IF EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = 'dbo' AND TABLE_NAME = 'Positions') BEGIN DELETE FROM dbo.Positions; PRINT 'Usunięto dane z tabeli Positions.'; END ELSE PRINT 'Tabela Positions nie istnieje, pomijanie czyszczenia.';
                IF EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = 'dbo' AND TABLE_NAME = 'Headers') BEGIN DELETE FROM dbo.Headers; PRINT 'Usunięto dane z tabeli Headers.'; END ELSE PRINT 'Tabela Headers nie istnieje, pomijanie czyszczenia.';
            ";
            Console.WriteLine("Czyszczenie tabel danych (Headers, Positions, ValidationErrors) za pomocą DELETE...");
            using var connection = new SqlConnection(_connectionString);
            using var command = new SqlCommand(sql, connection);
            try
            {
                await connection.OpenAsync();
                await command.ExecuteNonQueryAsync();
                Console.WriteLine("Tabele zostały wyczyszczone (DELETE).");
            }
            catch (SqlException ex) // Uzupełniony catch
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Błąd SQL podczas czyszczenia tabel (DELETE): {ex.Message}");
                // Wypisz szczegóły błędów SQL, jeśli są dostępne
                if (ex.Errors.Count > 0)
                {
                    Console.WriteLine($"Szczegóły błędów SQL ({ex.Errors.Count}):");
                    foreach (SqlError error in ex.Errors)
                    {
                        Console.WriteLine($"  - Błąd nr {error.Number}, Stan {error.State}, Poziom {error.Class}, Linia {error.LineNumber}: {error.Message}");
                    }
                }
                Console.ResetColor();
                throw; // Rzuć wyjątek dalej, aby zatrzymać aplikację w razie problemu
            }
        }

        // Metoda tworząca/sprawdzająca tabele (z pełnym catch)
        public async Task EnsureTablesExistAsync()
        {
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
                    NrRachunkuKontrahenta VARCHAR(34) NULL, Tytul NVARCHAR(255) NULL, Kwota DECIMAL(18, 2) NULL, SaldoKoncowe DECIMAL(18, 2) NULL
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
            catch (SqlException ex) // Uzupełniony catch
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Błąd SQL podczas EnsureTablesExistAsync: {ex.Message}");
                if (ex.Errors.Count > 0) { Console.WriteLine($"Szczegóły błędów SQL ({ex.Errors.Count}):"); foreach (SqlError error in ex.Errors) { Console.WriteLine($"  - Błąd nr {error.Number}, Stan {error.State}, Linia {error.LineNumber}: {error.Message}"); } }
                Console.ResetColor();
                throw;
            }
        }

        // Metoda wstawiająca nagłówek (z pełnym catch)
        public async Task<int> InsertHeaderDataAsync(HeaderData header, decimal? openingBalance)
        {
            string sql = @"
                INSERT INTO dbo.Headers ( NIP, REGON, NazwaFirmy, KodKraju, Wojewodztwo, Powiat, Gmina, Ulica, NrDomu, NrLokalu, Miejscowosc, KodPocztowy, Poczta, NumerRachunku, DataOd, DataDo, KodWaluty, KodUrzedu, SaldoPoczatkowe )
                VALUES ( @NIP, @REGON, @NazwaFirmy, @KodKraju, @Wojewodztwo, @Powiat, @Gmina, @Ulica, @NrDomu, @NrLokalu, @Miejscowosc, @KodPocztowy, @Poczta, @NumerRachunku, @DataOd, @DataDo, @KodWaluty, @KodUrzedu, @SaldoPoczatkowe );
                SELECT SCOPE_IDENTITY();";
            using var connection = new SqlConnection(_connectionString);
            using var command = new SqlCommand(sql, connection);
            // Dodawanie parametrów (bez zmian)
            command.Parameters.AddWithValue("@NIP", (object)header.NIP ?? DBNull.Value); command.Parameters.AddWithValue("@REGON", (object)header.REGON ?? DBNull.Value); command.Parameters.AddWithValue("@NazwaFirmy", (object)header.NazwaFirmy ?? DBNull.Value); command.Parameters.AddWithValue("@KodKraju", (object)header.KodKraju ?? DBNull.Value); command.Parameters.AddWithValue("@Wojewodztwo", (object)header.Wojewodztwo ?? DBNull.Value); command.Parameters.AddWithValue("@Powiat", (object)header.Powiat ?? DBNull.Value); command.Parameters.AddWithValue("@Gmina", (object)header.Gmina ?? DBNull.Value); command.Parameters.AddWithValue("@Ulica", (object)header.Ulica ?? DBNull.Value); command.Parameters.AddWithValue("@NrDomu", (object)header.NrDomu ?? DBNull.Value); command.Parameters.AddWithValue("@NrLokalu", (object)header.NrLokalu ?? DBNull.Value); command.Parameters.AddWithValue("@Miejscowosc", (object)header.Miejscowosc ?? DBNull.Value); command.Parameters.AddWithValue("@KodPocztowy", (object)header.KodPocztowy ?? DBNull.Value); command.Parameters.AddWithValue("@Poczta", (object)header.Poczta ?? DBNull.Value); command.Parameters.AddWithValue("@NumerRachunku", (object)header.NumerRachunku ?? DBNull.Value); command.Parameters.AddWithValue("@DataOd", (object)header.DataOd ?? DBNull.Value); command.Parameters.AddWithValue("@DataDo", (object)header.DataDo ?? DBNull.Value); command.Parameters.AddWithValue("@KodWaluty", (object)header.KodWaluty ?? DBNull.Value); command.Parameters.AddWithValue("@KodUrzedu", (object)header.KodUrzedu ?? DBNull.Value); command.Parameters.AddWithValue("@SaldoPoczatkowe", (object)openingBalance ?? DBNull.Value);
            try
            {
                await connection.OpenAsync();
                var result = await command.ExecuteScalarAsync();
                Console.WriteLine("Zapisano dane nagłówka do bazy.");
                if (result != null && result != DBNull.Value) { return Convert.ToInt32(result); }
                else { throw new InvalidOperationException("Nie udało się uzyskać ID wstawionego nagłówka."); }
            }
            catch (SqlException ex) // Uzupełniony catch
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Błąd SQL podczas wstawiania danych nagłówka: {ex.Message}");
                if (ex.Errors.Count > 0) { Console.WriteLine($"Szczegóły błędów SQL ({ex.Errors.Count}):"); foreach (SqlError error in ex.Errors) { Console.WriteLine($"  - Błąd nr {error.Number}, Stan {error.State}, Linia {error.LineNumber}: {error.Message}"); } }
                Console.ResetColor();
                throw;
            }
        }

        // Metoda wstawiająca pozycje (z pełnym catch)
        public async Task InsertPositionDataBulkAsync(List<PositionData> allPositions)
        {
            if (allPositions == null || !allPositions.Any()) { Console.WriteLine("Brak danych pozycji do zapisania."); return; }
            var transactionsToInsert = allPositions.Where(p => p.Kwota != null).ToList();
            if (!transactionsToInsert.Any()) { Console.WriteLine("Brak transakcji (pozycji z kwotą) do zapisania."); return; }
            Console.WriteLine($"Przygotowywanie {transactionsToInsert.Count} transakcji do zapisu...");
            var dataTable = new DataTable("PositionsType");
            // Definicja DataTable (bez zmian)
            dataTable.Columns.Add("NrRachunku", typeof(string)); dataTable.Columns.Add("Data", typeof(DateTime)); dataTable.Columns.Add("Kontrahent", typeof(string)); dataTable.Columns.Add("NrRachunkuKontrahenta", typeof(string)); dataTable.Columns.Add("Tytul", typeof(string)); dataTable.Columns.Add("Kwota", typeof(decimal)); dataTable.Columns.Add("SaldoKoncowe", typeof(decimal));
            foreach (var pos in transactionsToInsert) { dataTable.Rows.Add((object)pos.NrRachunku ?? DBNull.Value, pos.Data, (object)pos.Kontrahent ?? DBNull.Value, (object)pos.NrRachunkuKontrahenta ?? DBNull.Value, (object)pos.Tytul ?? DBNull.Value, pos.Kwota, (object)pos.SaldoKoncowe ?? DBNull.Value); }
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();
            using var bulkCopy = new SqlBulkCopy(connection);
            bulkCopy.DestinationTableName = "dbo.Positions";
            // Mapowania kolumn (bez zmian)
            bulkCopy.ColumnMappings.Add("NrRachunku", "NrRachunku"); bulkCopy.ColumnMappings.Add("Data", "Data"); bulkCopy.ColumnMappings.Add("Kontrahent", "Kontrahent"); bulkCopy.ColumnMappings.Add("NrRachunkuKontrahenta", "NrRachunkuKontrahenta"); bulkCopy.ColumnMappings.Add("Tytul", "Tytul"); bulkCopy.ColumnMappings.Add("Kwota", "Kwota"); bulkCopy.ColumnMappings.Add("SaldoKoncowe", "SaldoKoncowe");
            try
            {
                await bulkCopy.WriteToServerAsync(dataTable);
                Console.WriteLine($"Zapisano {transactionsToInsert.Count} transakcji do bazy.");
            }
            catch (SqlException ex) // Uzupełniony catch dla BulkCopy
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Błąd SQL podczas masowego wstawiania danych pozycji (SqlBulkCopy): {ex.Message}");
                // SqlBulkCopy może mieć bardziej szczegółowe błędy w kolekcji Errors
                if (ex.Errors.Count > 0)
                {
                    Console.WriteLine($"Szczegóły błędów SQL ({ex.Errors.Count}):");
                    foreach (SqlError error in ex.Errors)
                    {
                        Console.WriteLine($"  - Błąd nr {error.Number}, Stan {error.State}, Linia {error.LineNumber}: {error.Message}");
                    }
                }
                Console.ResetColor();
                throw;
            }
        }

        public async Task<int> ValidateImportAsync(int headerId)
        {
            Console.WriteLine($"Wywoływanie procedury walidującej dla HeaderId = {headerId}...");
            int errorCount = -1; // Domyślnie błąd

            using (SqlConnection validationConnection = new SqlConnection(_connectionString)) // Używamy _connectionString z klasy
            using (SqlCommand validationCommand = new SqlCommand("dbo.usp_ValidateImportData", validationConnection))
            {
                validationCommand.CommandType = CommandType.StoredProcedure;
                validationCommand.Parameters.Add(new SqlParameter("@HeaderId", SqlDbType.Int) { Value = headerId });

                try
                {
                    await validationConnection.OpenAsync();
                    var result = await validationCommand.ExecuteScalarAsync();
                    if (result != null && result != DBNull.Value)
                    {
                        errorCount = Convert.ToInt32(result); // Zapisz faktyczną liczbę błędów
                    }
                    // Nie wypisujemy wyniku tutaj, zrobimy to w Program.cs/Index.cshtml.cs
                }
                catch (SqlException ex)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"Błąd SQL podczas wywoływania procedury walidującej: {ex.Message}");
                    // Wypisz szczegóły błędów SQL
                    if (ex.Errors.Count > 0)
                    {
                        Console.WriteLine($"Szczegóły błędów SQL ({ex.Errors.Count}):");
                        foreach (SqlError error in ex.Errors)
                        {
                            Console.WriteLine($"  - Błąd nr {error.Number}, Stan {error.State}, Linia {error.LineNumber}: {error.Message}");
                        }
                    }
                    Console.ResetColor();
                    // Rzuć wyjątek dalej, aby główny kod wiedział o problemie
                    throw;
                }
            } // using validationCommand, validationConnection

            return errorCount; // Zwróć liczbę błędów
        }

        // NOWA METODA do dodania w klasie DatabaseWriter
        public async Task<List<ValidationError>> GetValidationErrorsAsync(int headerId)
        {
            var errors = new List<ValidationError>();
            string sql = @"
            SELECT ErrorId, HeaderId, TableName, RecordId, ColumnName, ErrorCode, ErrorMessage, ErrorTimestamp
            FROM dbo.ValidationErrors
            WHERE HeaderId = @HeaderId
            ORDER BY ErrorId;
        ";

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
                Console.ResetColor();
                // Można rzucić wyjątek dalej lub zwrócić pustą listę / null
                throw;
            }
            return errors;
        }

        // NOWA METODA do dodania w klasie DatabaseWriter
        public async Task<string?> GenerateXmlAsync(int headerId)
        {
            Console.WriteLine($"Wywoływanie procedury generującej XML dla HeaderId = {headerId}...");
            string? generatedXml = null;

            // Najpierw sprawdźmy ponownie, czy nie ma błędów walidacji dla tego ID
            // (Dobra praktyka, choć teoretycznie nie powinno się tu dostać, jeśli są błędy)
            int errorCheck = await ValidateImportAsync(headerId); // Używamy istniejącej metody
            if (errorCheck != 0)
            {
                Console.WriteLine($"Walidacja dla HeaderId={headerId} zwróciła {errorCheck} błędów. Przerywanie generowania XML.");
                // Można rzucić wyjątek lub zwrócić null/specjalny string błędu
                return "<Error>Validation previously failed or returned errors.</Error>";
            }

            using (SqlConnection connection = new SqlConnection(_connectionString))
            using (SqlCommand command = new SqlCommand("dbo.usp_GenerateJPK_WB_XML", connection))
            {
                command.CommandType = CommandType.StoredProcedure;
                command.Parameters.Add(new SqlParameter("@HeaderId", SqlDbType.Int) { Value = headerId });

                try
                {
                    await connection.OpenAsync();
                    // Procedura zwraca XML jako pojedynczą wartość
                    var result = await command.ExecuteScalarAsync();
                    if (result != null && result != DBNull.Value)
                    {
                        generatedXml = result.ToString(); // Wynik typu XML jest mapowany na string

                        // Sprawdźmy, czy procedura SQL nie zwróciła swojego znacznika błędu
                        if (generatedXml != null && generatedXml.StartsWith("<Error>"))
                        {
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.WriteLine($"Procedura SQL usp_GenerateJPK_WB_XML zwróciła błąd: {generatedXml}");
                            Console.ResetColor();
                            return null; // Zwracamy null, aby C# wiedział, że coś poszło nie tak
                        }
                        Console.WriteLine($"Pomyślnie pobrano XML z SQL Server dla HeaderId={headerId}.");
                    }
                    else
                    {
                        Console.WriteLine($"Procedura SQL usp_GenerateJPK_WB_XML nie zwróciła wyniku dla HeaderId={headerId}.");
                    }
                }
                catch (SqlException ex)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"Błąd SQL podczas generowania XML dla HeaderId={headerId}: {ex.Message}");
                    if (ex.Errors.Count > 0) { Console.WriteLine($"Szczegóły błędów SQL ({ex.Errors.Count}):"); foreach (SqlError error in ex.Errors) { Console.WriteLine($"  - Błąd nr {error.Number}, Stan {error.State}, Linia {error.LineNumber}: {error.Message}"); } }
                    Console.ResetColor();
                    // Rzucamy wyjątek dalej lub zwracamy null, aby zasygnalizować problem
                    throw; // lub return null;
                }
            } // using command, connection

            return generatedXml;
        }


    }
}