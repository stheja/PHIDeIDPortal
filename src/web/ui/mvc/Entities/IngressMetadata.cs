using System;
using System.Text.Json.Serialization;

namespace PHIDeIDPortal.Ingress.Entities
{
    /// <summary>
    /// Lean metadata document for the SRE ingress path.
    /// Partition key is AuthorOid.
    /// </summary>
    public class IngressMetadata
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = default!;

        [JsonPropertyName("FileName")]
        public string FileName { get; set; } = default!;

        [JsonPropertyName("BlobName")]
        public string BlobName { get; set; } = default!;

        [JsonPropertyName("ContainerName")]
        public string ContainerName { get; set; } = default!;

        /// <summary>
        /// Partition key (immutable Entra ID 'oid').
        /// </summary>
        [JsonPropertyName("AuthorOid")]
        public string AuthorOid { get; set; } = default!;

        [JsonPropertyName("UploadTimestamp")]
        public DateTime UploadTimestamp { get; set; }

        /// <summary>
        /// Convenience field. Not used for authorization decisions.
        /// </summary>
        [JsonPropertyName("Uri")]
        public string? Uri { get; set; }
    }
}
