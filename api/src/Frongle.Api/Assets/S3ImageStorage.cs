using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Options;

namespace Frongle.Api.Assets;

/// <summary>Creates pre-signed links to an S3 bucket. The API signs the links on its own and never touches the files.</summary>
public sealed class S3ImageStorage : IImageStorage, IDisposable
{
    private static readonly TimeSpan UploadWindow = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan ReadWindow = TimeSpan.FromHours(1);

    private readonly AmazonS3Client _client;
    private readonly string _bucket;
    private readonly Protocol _protocol;

    /// <summary>Creates the storage for the bucket in the options.</summary>
    /// <param name="options">Where the bucket is and how to sign for it.</param>
    public S3ImageStorage(IOptions<StorageOptions> options)
    {
        _client = CreateClient(options.Value);
        _bucket = options.Value.Bucket;
        // A local S3 server such as MinIO often has no TLS, and a link must use the same scheme as the server.
        _protocol = options.Value.ServiceUrl?.StartsWith("http://", StringComparison.OrdinalIgnoreCase) == true ? Protocol.HTTP : Protocol.HTTPS;
    }

    /// <summary>Opens a client for the server in the options.</summary>
    /// <param name="options">Where the server is and how to sign for it.</param>
    /// <returns>A client that the caller must dispose.</returns>
    public static AmazonS3Client CreateClient(StorageOptions options)
    {
        var onCustomServer = !string.IsNullOrEmpty(options.ServiceUrl);
        var config = new AmazonS3Config { ForcePathStyle = onCustomServer };
        if (onCustomServer) config.ServiceURL = options.ServiceUrl;
        else if (!string.IsNullOrEmpty(options.Region)) config.RegionEndpoint = RegionEndpoint.GetBySystemName(options.Region);

        return string.IsNullOrEmpty(options.AccessKey)
            ? new AmazonS3Client(config)
            : new AmazonS3Client(new BasicAWSCredentials(options.AccessKey, options.SecretKey), config);
    }

    /// <inheritdoc />
    public string CreateUploadUrl(string key, string contentType) => Sign(key, HttpVerb.PUT, UploadWindow, contentType);

    /// <inheritdoc />
    public string CreateReadUrl(string key) => Sign(key, HttpVerb.GET, ReadWindow, null);

    /// <inheritdoc />
    public void Dispose() => _client.Dispose();

    /// <summary>Makes a pre-signed link to a file in the bucket.</summary>
    /// <param name="key">Where the file is in the bucket.</param>
    /// <param name="verb">What the link allows: PUT to upload or GET to read.</param>
    /// <param name="validFor">How long the link works.</param>
    /// <param name="contentType">The media type that an upload must declare, or <see langword="null"/> for a read.</param>
    /// <returns>The signed URL.</returns>
    private string Sign(string key, HttpVerb verb, TimeSpan validFor, string? contentType) =>
        _client.GetPreSignedURL(new GetPreSignedUrlRequest
        {
            BucketName = _bucket,
            Key = key,
            Verb = verb,
            Protocol = _protocol,
            Expires = DateTime.UtcNow + validFor,
            ContentType = contentType,
        });
}
