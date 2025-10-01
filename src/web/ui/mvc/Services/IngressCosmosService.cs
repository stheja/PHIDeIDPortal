using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using Azure.Identity;
using PHIDeIDPortal.Ingress.Entities;
using PHIDeIDPortal.Ingress.Models;

namespace PHIDeIDPortal.Ingress.Services
{
    /// <summary>
    /// Self-contained Cosmos access using Managed Identity via DefaultAzureCredential.
    /// No DI registration required.
    /// </summary>
    public sealed class IngressCosmosService : IAsyncDisposable
    {
        private readonly CosmosClient _client;
        private readonly Container _container;

        public IngressCosmosService(IConfiguration configuration)
        {
            var cfg = configuration.GetSection("CosmosDbIngress");
            var endpoint = cfg["AccountEndpoint"] ?? throw new ArgumentNullException("CosmosDbIngress:AccountEndpoint");
            var dbId     = cfg["DatabaseId"]      ?? throw new ArgumentNullException("CosmosDbIngress:DatabaseId");
            var cId      = cfg["ContainerId"]     ?? throw new ArgumentNullException("CosmosDbIngress:ContainerId");

            var credential = new DefaultAzureCredential();
            _client = new CosmosClient(endpoint, credential, new CosmosClientOptions
            {
                SerializerOptions = new CosmosSerializationOptions
                {
                    PropertyNamingPolicy = CosmosPropertyNamingPolicy.Default
                }
            });
            _container = _client.GetContainer(dbId, cId);
        }

        public async Task<PagedResult<IngressMetadata>> GetByAuthorAsync(
            string authorOid,
            string? continuationToken,
            int pageSize = 50,
            CancellationToken ct = default)
        {
            var query = new QueryDefinition("SELECT * FROM c WHERE c.AuthorOid = @oid ORDER BY c.UploadTimestamp DESC")
                .WithParameter("@oid", authorOid);

            var opts = new QueryRequestOptions { MaxItemCount = pageSize, PartitionKey = new PartitionKey(authorOid) };
            var iterator = _container.GetItemQueryIterator<IngressMetadata>(query, continuationToken, opts);
            var page = await iterator.ReadNextAsync(ct);

            return new PagedResult<IngressMetadata>
            {
                Items = page.ToList(),
                ContinuationToken = page.ContinuationToken
            };
        }

        public async Task<IngressMetadata?> GetByIdAndAuthorAsync(string id, string authorOid, CancellationToken ct = default)
        {
            try
            {
                var resp = await _container.ReadItemAsync<IngressMetadata>(id, new PartitionKey(authorOid), cancellationToken: ct);
                return resp.Resource;
            }
            catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return null;
            }
        }

        public Task<ItemResponse<IngressMetadata>> UpsertAsync(IngressMetadata doc, CancellationToken ct = default)
            => _container.UpsertItemAsync(doc, new PartitionKey(doc.AuthorOid), cancellationToken: ct);

        public async ValueTask DisposeAsync()
        {
            _client?.Dispose();
            await Task.CompletedTask;
        }
    }
}
