using JpkWebGenerator.Services; // Poprawny namespace!
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace JpkWebGenerator.Pages // Poprawny namespace!
{
    public class ValidationErrorsModel : PageModel
    {
        private readonly DatabaseWriter _dbWriter;

        // Lista b��d�w do wy�wietlenia na stronie
        public List<ValidationError> Errors { get; set; } = new List<ValidationError>();

        // W�a�ciwo�� do powi�zania parametru z URL (?headerId=X lub /X)
        [BindProperty(SupportsGet = true)]
        public int HeaderId { get; set; }

        public ValidationErrorsModel(DatabaseWriter dbWriter)
        {
            _dbWriter = dbWriter;
        }

        // Metoda wywo�ywana przy ��daniu GET strony
        public async Task OnGetAsync() // Usuni�to parametr, bo mamy BindProperty
        {
            if (HeaderId > 0)
            {
                Errors = await _dbWriter.GetValidationErrorsAsync(HeaderId);
            }
            // Je�li HeaderId = 0 lub nie podano, lista Errors pozostanie pusta
        }
    }
}