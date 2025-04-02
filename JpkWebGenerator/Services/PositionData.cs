// --- START FILE: PositionData.cs ---
using System;

namespace JpkWebGenerator.Services
{
    /// <summary>
    /// Reprezentuje dane pojedynczej operacji (wiersza) wczytane z pliku pozycji JPK_WB.
    /// Zawiera również identyfikator nagłówka (HeaderId) do powiązania danych w bazie.
    /// </summary>
    public class PositionData
    {
        /// <summary>
        /// Numer rachunku bankowego (IBAN), zgodny z nagłówkiem raportu.
        /// </summary>
        public string? NrRachunku { get; set; }

        /// <summary>
        /// Data wykonania operacji bankowej. Może być null w specjalnym wierszu salda początkowego.
        /// </summary>
        public DateTime? Data { get; set; }

        /// <summary>
        /// Nazwa lub dane identyfikujące kontrahenta operacji (stronę przeciwną).
        /// Może zawierać np. "Saldo początkowe".
        /// </summary>
        public string? Kontrahent { get; set; }

        /// <summary>
        /// Numer rachunku (IBAN) kontrahenta, jeśli jest dostępny i dotyczy.
        /// </summary>
        public string? NrRachunkuKontrahenta { get; set; }

        /// <summary>
        /// Tytuł lub opis operacji bankowej (np. numer faktury, cel przelewu).
        /// </summary>
        public string? Tytul { get; set; }

        /// <summary>
        /// Kwota operacji. Dodatnia dla uznań (wpływów na konto), ujemna dla obciążeń (wydatków).
        /// Powinna być null tylko w specjalnym wierszu reprezentującym saldo początkowe.
        /// </summary>
        public decimal? Kwota { get; set; }

        /// <summary>
        /// Saldo na rachunku bankowym *po* zaksięgowaniu danej operacji.
        /// Dla wiersza salda początkowego reprezentuje to właśnie saldo początkowe.
        /// </summary>
        public decimal? SaldoKoncowe { get; set; }

        /// <summary>
        /// Identyfikator nagłówka (z tabeli dbo.Headers), do którego należy ta pozycja.
        /// Klucz obcy używany do powiązania danych w bazie danych.
        /// </summary>
        public int HeaderId { get; set; }
    }
}
// --- END FILE: PositionData.cs ---