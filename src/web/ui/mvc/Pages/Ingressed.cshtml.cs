using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Configuration;
using PHIDeIDPortal.Ingress.Entities;
using PHIDeIDPortal.Ingress.Models;
using PHIDeIDPortal.Ingress.Services;

namespace PHIDeIDPortal.Pages
{
    [Authorize]
    public class IngressedModel : PageModel
    {
        private readonly IConfiguration _cfg;
        public List<IngressMetadata> Results { get; private set; } = new();
        public string? NextContinuationToken { get; private set; }

        public IngressedModel(IConfiguration cfg) => _cfg = cfg;

        public async Task OnGetAsync(string? continuationToken, CancellationToken ct)
        {
            var oid = User.FindFirst("oid")?.Value;
            if (string.IsNullOrEmpty(oid)) return;

            await using var cosmos = new IngressCosmosService(_cfg);
            var page = await cosmos.GetByAuthorAsync(oid, continuationToken, 50, ct);
            Results = page.Items;
            NextContinuationToken = page.ContinuationToken;
        }
    }
}
