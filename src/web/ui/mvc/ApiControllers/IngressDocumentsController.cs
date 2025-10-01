using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using PHIDeIDPortal.Ingress.Entities;
using PHIDeIDPortal.Ingress.Services;

namespace PHIDeIDPortal.ApiControllers
{
    [ApiController]
    [Authorize]
    [Route("api/ingress")]
    public class IngressDocumentsController : ControllerBase
    {
        private readonly IConfiguration _cfg;
        private readonly string _ingressContainer;

        public IngressDocumentsController(IConfiguration cfg)
        {
            _cfg = cfg;
            _ingressContainer = _cfg["StorageAccount:IngressContainer"]
                ?? throw new ArgumentNullException("StorageAccount:IngressContainer");
        }

        private string GetAuthorOid() =>
            User.FindFirst("oid")?.Value ?? throw new InvalidOperationException("User 'oid' claim not found.");

        [HttpPost("upload")]
        [RequestSizeLimit(268435456)] // 256 MiB
        public async Task<IActionResult> Upload([FromForm] IFormFile file, CancellationToken ct)
        {
            if (file == null || file.Length == 0) return BadRequest("No file uploaded.");

            var authorOid = GetAuthorOid();
            var safeName = IngressBlobStorage.SanitizeFileName(file.FileName);
            var blobName = $"{DateTime.UtcNow:yyyy/MM/dd}/{Guid.NewGuid()}-{safeName}";

            var storage = new IngressBlobStorage(_cfg);
            var uri = await storage.UploadAsync(file, _ingressContainer, blobName, ct);

            var meta = new IngressMetadata
            {
                Id = Guid.NewGuid().ToString("N"),
                FileName = file.FileName,
                BlobName = blobName,
                ContainerName = _ingressContainer,
                AuthorOid = authorOid,
                UploadTimestamp = DateTime.UtcNow,
                Uri = uri
            };

            await using var cosmos = new IngressCosmosService(_cfg);
            await cosmos.UpsertAsync(meta, ct);

            return Ok(new { id = meta.Id, message = "Uploaded." });
        }

        [HttpGet("list")]
        public async Task<IActionResult> List([FromQuery] string? continuationToken, CancellationToken ct)
        {
            var authorOid = GetAuthorOid();
            await using var cosmos = new IngressCosmosService(_cfg);
            var page = await cosmos.GetByAuthorAsync(authorOid, continuationToken, 50, ct);
            return Ok(page);
        }

        [HttpGet("download")]
        public async Task<IActionResult> Download([FromQuery] string id, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(id)) return BadRequest("id required.");

            var authorOid = GetAuthorOid();
            await using var cosmos = new IngressCosmosService(_cfg);
            var meta = await cosmos.GetByIdAndAuthorAsync(id, authorOid, ct);

            if (meta is null) return NotFound("Not found or access denied.");

            var storage = new IngressBlobStorage(_cfg);
            var (stream, contentType) = await storage.OpenReadAsync(meta.ContainerName, meta.BlobName, ct);

            return File(stream, contentType, meta.FileName);
        }
    }
}
