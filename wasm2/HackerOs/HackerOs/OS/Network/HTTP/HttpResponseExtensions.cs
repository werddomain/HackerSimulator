using System.Threading.Tasks;

namespace HackerOs.OS.Network.HTTP;

/// <summary>
/// Extension helpers for <see cref="HttpResponse"/>.
/// </summary>
public static class HttpResponseExtensions
{
    /// <summary>
    /// Writes all bytes to the response body stream.
    /// </summary>
    /// <param name="response">The HTTP response.</param>
    /// <param name="buffer">Byte array to write.</param>
    public static async Task WriteBytesAsync(this HttpResponse response, byte[] buffer)
    {
        if (buffer == null || buffer.Length == 0)
            return;
        await response.WriteAsync(buffer, 0, buffer.Length);
    }
}
