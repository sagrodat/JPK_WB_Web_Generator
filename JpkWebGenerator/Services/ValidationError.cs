// --- START FILE: ValidationError.cs ---
using System;

namespace JpkWebGenerator.Services 
{
    /// <summary>
    /// Reprezentuje pojedynczy błąd walidacji zapisany w tabeli dbo.ValidationErrors.
    /// </summary>
    public class ValidationError
    {
        /// <summary>
        /// Unikalny identyfikator błędu w bazie danych.
        /// </summary>
        public int ErrorId { get; set; }

        /// <summary>
        /// Identyfikator rekordu nagłówka (z tabeli dbo.Headers), którego dotyczy błąd.
        /// </summary>
        public int? HeaderId { get; set; }

        /// <summary>
        /// Nazwa tabeli, w której wystąpił błąd ('Headers' lub 'Positions').
        /// </summary>
        public string TableName { get; set; } = string.Empty;

        /// <summary>
        /// Identyfikator rekordu (np. PositionId), którego dotyczy błąd (jeśli dotyczy konkretnego wiersza).
        /// Może być null dla błędów dotyczących całego nagłówka.
        /// </summary>
        public int? RecordId { get; set; }

        /// <summary>
        /// Nazwa kolumny, w której wykryto błąd (jeśli dotyczy konkretnej kolumny).
        /// </summary>
        public string? ColumnName { get; set; }

        /// <summary>
        /// Krótki kod identyfikujący rodzaj błędu (np. 'NIP_MISSING', 'BALANCE_INCONSISTENT').
        /// </summary>
        public string ErrorCode { get; set; } = string.Empty;

        /// <summary>
        /// Czytelny dla człowieka opis błędu walidacji.
        /// </summary>
        public string ErrorMessage { get; set; } = string.Empty;

        /// <summary>
        /// Data i czas zarejestrowania błędu w bazie danych.
        /// </summary>
        public DateTime ErrorTimestamp { get; set; }
    }
}
// --- END FILE: ValidationError.cs ---