using System.Net;
using Frongle.Api.Assets;
using Microsoft.Extensions.Options;
using Testcontainers.Minio;

namespace Frongle.Api.Tests;

/// <summary>Proves that the URLs from <see cref="S3ImageStorage"/> work against an S3 server, which here is MinIO.</summary>
public sealed class ImageStorageTests : IAsyncLifetime, IDisposable
{
    private const string Bucket = "frongle-test";
    // A digest, not a tag, so that an upstream image update cannot change or break CI without a change here.
    private readonly MinioContainer _minio = new MinioBuilder("cgr.dev/chainguard/minio@sha256:0f95aa412a12351a95bb43c3b54b66440eb0aa022bb3f3458942678a489e915b").Build();
    private S3ImageStorage _storage = null!;

    public async Task InitializeAsync()
    {
        await _minio.StartAsync();
        var options = new StorageOptions
        {
            Bucket = Bucket,
            ServiceUrl = _minio.GetConnectionString(),
            AccessKey = _minio.GetAccessKey(),
            SecretKey = _minio.GetSecretKey(),
        };
        _storage = new S3ImageStorage(Options.Create(options));
        using var admin = S3ImageStorage.CreateClient(options);
        await admin.PutBucketAsync(Bucket);
    }

    public async Task DisposeAsync() => await _minio.DisposeAsync();

    public void Dispose() => _storage.Dispose();

    [Fact]
    public async Task An_image_that_was_uploaded_to_the_upload_url_can_be_read_from_the_read_url()
    {
        var picture = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 1, 2, 3 };
        using var http = new HttpClient();

        using var upload = new ByteArrayContent(picture);
        upload.Headers.ContentType = new("image/jpeg");
        var put = await http.PutAsync(_storage.CreateUploadUrl("acme/asset/event/image", "image/jpeg"), upload);
        var read = await http.GetAsync(_storage.CreateReadUrl("acme/asset/event/image"));

        Assert.Equal(HttpStatusCode.OK, put.StatusCode);
        Assert.Equal(picture, await read.Content.ReadAsByteArrayAsync());
        Assert.Equal("image/jpeg", read.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task The_upload_url_refuses_an_upload_with_another_content_type()
    {
        using var http = new HttpClient();

        using var upload = new ByteArrayContent([1, 2, 3]);
        upload.Headers.ContentType = new("text/html");
        var put = await http.PutAsync(_storage.CreateUploadUrl("acme/asset/event/other", "image/jpeg"), upload);

        Assert.Equal(HttpStatusCode.Forbidden, put.StatusCode);
    }
}
