namespace Frongle.Api.Assets;

/// <summary>Gives browsers short-lived links to put image files in object storage and to read them back.</summary>
public interface IImageStorage
{
    /// <summary>Makes a link that accepts one upload of the given type.</summary>
    /// <param name="key">Where the file goes in the bucket.</param>
    /// <param name="contentType">The media type that the upload must declare. A different type is refused.</param>
    /// <returns>A URL for an HTTP PUT with the file as the body and <paramref name="contentType"/> as the Content-Type.</returns>
    string CreateUploadUrl(string key, string contentType);

    /// <summary>Makes a link that shows a stored file.</summary>
    /// <param name="key">Where the file is in the bucket.</param>
    /// <returns>A URL for an HTTP GET.</returns>
    string CreateReadUrl(string key);
}
