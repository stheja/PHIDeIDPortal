using System.Collections.Generic;

namespace PHIDeIDPortal.Ingress.Models
{
    public class PagedResult<T>
    {
        public List<T> Items { get; set; } = new();
        public string? ContinuationToken { get; set; }
    }
}
