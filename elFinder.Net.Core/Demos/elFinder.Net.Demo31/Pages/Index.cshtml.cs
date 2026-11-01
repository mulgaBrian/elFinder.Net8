using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;

namespace elFinder.Net.Demo8.Pages
{
  public class IndexModel(ILogger<IndexModel> logger) : PageModel
  {
    private readonly ILogger<IndexModel> _logger = logger;

    public void OnGet()
    {

    }
  }
}
