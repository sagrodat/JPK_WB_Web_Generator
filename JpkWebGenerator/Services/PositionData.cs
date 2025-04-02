using System;

namespace JpkWebGenerator.Services // Upewnij się, że namespace pasuje
{
    public class PositionData
    {
        public string? NrRachunku { get; set; }
        public DateTime? Data { get; set; }
        public string? Kontrahent { get; set; }
        public string? NrRachunkuKontrahenta { get; set; }
        public string? Tytul { get; set; }
        public decimal? Kwota { get; set; }
        public decimal? SaldoKoncowe { get; set; }
        public int HeaderId { get; set; }
    }
}