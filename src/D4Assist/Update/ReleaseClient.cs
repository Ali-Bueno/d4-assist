using System.Net.Http.Headers;

namespace D4Assist.Update;

/// <summary>Reads the newest release from the public GitHub repo and downloads its zip.</summary>
public sealed class ReleaseClient : IDisposable
{
    private const string LatestReleaseUrl = "https://api.github.com/repos/Ali-Bueno/d4-assist/releases/latest";
    private static readonly TimeSpan CheckTimeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan DownloadTimeout = TimeSpan.FromMinutes(5);
    private const int BufferBytes = 81920; // Stream.CopyTo's default buffer size

    private readonly HttpClient _http = new() { Timeout = Timeout.InfiniteTimeSpan };

    public ReleaseClient(Version current) => _http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("D4Assist", current.ToString(3)));

    /// <summary>The latest release; null when it has no readable tag or zip. Throws on network or HTTP errors.</summary>
    public async Task<ReleaseInfo?> GetLatestAsync()
    {
        using var timeout = new CancellationTokenSource(CheckTimeout);
        using var request = new HttpRequestMessage(HttpMethod.Get, LatestReleaseUrl);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        using var response = await _http.SendAsync(request, timeout.Token);
        response.EnsureSuccessStatusCode();
        return ReleaseInfo.Parse(await response.Content.ReadAsStringAsync(timeout.Token));
    }

    /// <summary>Saves <paramref name="url"/> to <paramref name="path"/>, reporting the percent done when the size is known.
    /// Throws on network or HTTP errors.</summary>
    public async Task DownloadAsync(string url, string path, Action<int>? percentDone = null)
    {
        using var timeout = new CancellationTokenSource(DownloadTimeout);
        using var response = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
        response.EnsureSuccessStatusCode();
        long? total = response.Content.Headers.ContentLength;
        await using var source = await response.Content.ReadAsStreamAsync(timeout.Token);
        await using var file = File.Create(path);
        var buffer = new byte[BufferBytes];
        long done = 0;
        int read;
        while ((read = await source.ReadAsync(buffer, timeout.Token)) > 0)
        {
            await file.WriteAsync(buffer.AsMemory(0, read), timeout.Token);
            done += read;
            if (total > 0) percentDone?.Invoke((int)(done * 100 / total.Value));
        }
    }

    public void Dispose() => _http.Dispose();
}
