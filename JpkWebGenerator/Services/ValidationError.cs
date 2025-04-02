using System;

namespace JpkWebGenerator.Services // Użyj poprawnego namespace dla Twojego projektu
{
    // Klasa reprezentująca pojedynczy błąd walidacji z tabeli dbo.ValidationErrors
    public class ValidationError
    {
        public int ErrorId { get; set; }
        public int? HeaderId { get; set; } // Może być null dla błędów globalnych (chociaż teraz używamy)
        public string TableName { get; set; } = string.Empty;
        public int? RecordId { get; set; } // Np. PositionId
        public string? ColumnName { get; set; }
        public string ErrorCode { get; set; } = string.Empty;
        public string ErrorMessage { get; set; } = string.Empty;
        public DateTime ErrorTimestamp { get; set; }
    }
}