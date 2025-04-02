// --- START FILE: HeaderData.cs ---
using System;

namespace JpkWebGenerator.Services 
{
    /// <summary>
    /// Reprezentuje dane wczytane z pliku nagłówkowego JPK_WB.
    /// Zawiera informacje identyfikujące podmiot, jego adres, numer rachunku
    /// oraz okres i inne metadane raportu JPK_WB.
    /// </summary>
    public class HeaderData
    {
        /// <summary>
        /// Numer Identyfikacji Podatkowej (NIP) podmiotu (10 cyfr).
        /// </summary>
        public string? NIP { get; set; }

        /// <summary>
        /// Numer REGON podmiotu (9 lub 14 cyfr). Opcjonalny w niektórych strukturach JPK.
        /// </summary>
        public string? REGON { get; set; }

        /// <summary>
        /// Pełna nazwa firmy lub imię i nazwisko osoby fizycznej.
        /// </summary>
        public string? NazwaFirmy { get; set; }

        /// <summary>
        /// Dwuliterowy kod kraju (ISO 3166-1 alpha-2) siedziby/zamieszkania podmiotu. Domyślnie "PL".
        /// </summary>
        public string? KodKraju { get; set; } = "PL";

        /// <summary>
        /// Województwo z adresu podmiotu (dla adresu polskiego).
        /// </summary>
        public string? Wojewodztwo { get; set; }

        /// <summary>
        /// Powiat z adresu podmiotu (dla adresu polskiego).
        /// </summary>
        public string? Powiat { get; set; }

        /// <summary>
        /// Gmina z adresu podmiotu (dla adresu polskiego).
        /// </summary>
        public string? Gmina { get; set; }

        /// <summary>
        /// Ulica z adresu podmiotu (opcjonalna).
        /// </summary>
        public string? Ulica { get; set; }

        /// <summary>
        /// Numer domu/budynku z adresu podmiotu.
        /// </summary>
        public string? NrDomu { get; set; }

        /// <summary>
        /// Numer lokalu z adresu podmiotu (opcjonalny).
        /// </summary>
        public string? NrLokalu { get; set; }

        /// <summary>
        /// Miejscowość z adresu podmiotu.
        /// </summary>
        public string? Miejscowosc { get; set; }

        /// <summary>
        /// Kod pocztowy z adresu podmiotu (np. "NN-NNN" dla Polski).
        /// </summary>
        public string? KodPocztowy { get; set; }

        /// <summary>
        /// Nazwa urzędu pocztowego (poczta) właściwego dla adresu podmiotu.
        /// </summary>
        public string? Poczta { get; set; }

        /// <summary>
        /// Numer rachunku bankowego w formacie IBAN, którego dotyczy wyciąg.
        /// </summary>
        public string? NumerRachunku { get; set; }

        /// <summary>
        /// Data początkowa okresu, za który generowany jest raport JPK_WB (włącznie).
        /// </summary>
        public DateTime? DataOd { get; set; }

        /// <summary>
        /// Data końcowa okresu, za który generowany jest raport JPK_WB (włącznie).
        /// </summary>
        public DateTime? DataDo { get; set; }

        /// <summary>
        /// Trzyliterowy kod waluty rachunku (ISO 4217). Domyślnie "PLN".
        /// </summary>
        public string? KodWaluty { get; set; } = "PLN";

        /// <summary>
        /// Czterocyfrowy kod urzędu skarbowego właściwego dla podmiotu.
        /// </summary>
        public string? KodUrzedu { get; set; }

        // Saldo początkowe jest pobierane z pliku pozycji lub przekazywane osobno,
        // dlatego zostało usunięte z tego modelu (lub dodane w DatabaseWriter).
        // public decimal SaldoPoczatkowe { get; set; }
    }
}
// --- END FILE: HeaderData.cs ---