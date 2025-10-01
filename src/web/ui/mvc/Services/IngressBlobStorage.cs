using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Azure.Identity;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace PHIDeIDPortal.Ingress.Services
{
    /// <summary>
    /// Self-contained blob operations using Managed Identity.
    /// </summary>
    public sealed class IngressBlobStorage
    {
        private readonly BlobServiceClient _blobServiceClient;

        public IngressBlobStorage(IConfiguration configuration)
        {
            var accountName = configuration["StorageAccount:AccountName"] 
                ?? throw new ArgumentNullException("StorageAccount:AccountName");
            var serviceUri = new Uri($"https://{accountName}.blob.core.windows.net");
            _blobServiceClient = new BlobServiceClient(serviceUri, new DefaultAzureCredential());
        }

        public static string SanitizeFileName(string fileName)
            => Regex.Replace(Path.GetFileName(fileName), @"[^a-zA-Z0-9_\-\.]", "_");

        public async Task<string> UploadAsync(IFormFile file, string containerName, string blobName, CancellationToken ct)
        {
            var container = _blobServiceClient.GetBlobContainerClient(containerName);
            await container.CreateIfNotExistsAsync(PublicAccessType.None, cancellationToken: ct);

            var blob = container.GetBlobClient(blobName);
            var headers = new BlobHttpHeaders { ContentType = string.IsNullOrWhiteSpace(file.ContentType) ? "application/octet-stream" : file.ContentType };

            using var stream = file.OpenReadStream();
            await blob.UploadAsync(stream, new BlobUploadOptions { HttpHeaders = headers }, ct);
            return blob.Uri.ToString();
        }

        public async Task<(Stream Stream, string ContentType)> OpenReadAsync(string containerName, string blobName, CancellationToken ct)
        {
            var container = _blobServiceClient.GetBlobContainerClient(containerName);
            var blob = container.GetBlobClient(blobName);
            var resp = await blob.DownloadStreamingAsync(cancellationToken: ct);
            var contentType = resp.Value.Details.ContentType ?? "application/octet-stream";
            return (resp.Value.Content, contentType);
        }
    }
}
