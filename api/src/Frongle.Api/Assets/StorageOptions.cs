namespace Frongle.Api.Assets;

/// <summary>Where the API keeps image files. Production uses Amazon S3, and the local cluster uses MinIO.</summary>
public sealed class StorageOptions
{
    /// <summary>The bucket that holds the images.</summary>
    public string Bucket { get; set; } = "";

    /// <summary>
    /// The address of an S3-compatible server such as MinIO. Browsers use this address too, so it must be reachable from them.
    /// Leave it empty to use Amazon S3.
    /// </summary>
    public string? ServiceUrl { get; set; }

    /// <summary>The AWS region of the bucket. It is needed for Amazon S3 only.</summary>
    public string? Region { get; set; }

    /// <summary>The access key for an S3-compatible server. Leave it empty on AWS, where the pod's role gives access.</summary>
    public string? AccessKey { get; set; }

    /// <summary>The secret key for an S3-compatible server. Leave it empty on AWS.</summary>
    public string? SecretKey { get; set; }
}
