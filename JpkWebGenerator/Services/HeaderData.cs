using System;

namespace JpkWebGenerator.Services // Upewnij się, że namespace pasuje
{
    public class HeaderData
    {
        public string? NIP { get; set; }
        public string? REGON { get; set; }
        public string? NazwaFirmy { get; set; }
        public string? KodKraju { get; set; } = "PL";
        public string? Wojewodztwo { get; set; }
        public string? Powiat { get; set; }
        public string? Gmina { get; set; }
        public string? Ulica { get; set; }
        public string? NrDomu { get; set; }
        public string? NrLokalu { get; set; }
        public string? Miejscowosc { get; set; }
        public string? KodPocztowy { get; set; }
        public string? Poczta { get; set; }
        public string? NumerRachunku { get; set; }
        public DateTime? DataOd { get; set; }
        public DateTime? DataDo { get; set; }
        public string? KodWaluty { get; set; } = "PLN";
        public string? KodUrzedu { get; set; }
        // Usunięto: public DateTime ImportTimestamp { get; set; }
        // SaldoPoczatkowe jest teraz przekazywane osobno
    }
}