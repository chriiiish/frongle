using Frongle.Api.Assets;

namespace Frongle.Api.Tests;

/// <summary>Stands in for S3 in the endpoint tests. Its URLs name the key, so a test can check where an image would go.</summary>
public sealed class FakeImageStorage : IImageStorage
{
    public string CreateUploadUrl(string key, string contentType) => $"https://storage.test/upload/{key}?type={contentType}";

    public string CreateReadUrl(string key) => $"https://storage.test/read/{key}";
}
