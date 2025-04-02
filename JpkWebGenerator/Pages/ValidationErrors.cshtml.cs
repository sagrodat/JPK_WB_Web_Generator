using JpkWebGenerator.Services; // Poprawny namespace!
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace JpkWebGenerator.Pages // Poprawny namespace!
{
    public class ValidationErrorsModel : PageModel
    {
        private readonly DatabaseWriter _dbWriter;

        // Lista b³êdów do wyœwietlenia na stronie
        public List<ValidationError> Errors { get; set; } = new List<ValidationError>();

        // W³aœciwoœæ do powi¹zania parametru z URL (?headerId=X lub /X)
        [BindProperty(SupportsGet = true)]
        public int HeaderId { get; set; }

        public ValidationErrorsModel(DatabaseWriter dbWriter)
        {
            _dbWriter = dbWriter;
        }

        // Metoda wywo³ywana przy ¿¹daniu GET strony
        public async Task OnGetAsync() // Usuniêto parametr, bo mamy BindProperty
        {
            if (HeaderId > 0)
            {
                Errors = await _dbWriter.GetValidationErrorsAsync(HeaderId);
            }
            // Jeœli HeaderId = 0 lub nie podano, lista Errors pozostanie pusta
        }
    }
}