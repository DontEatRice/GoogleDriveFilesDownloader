using System.Buffers;
using System.Web;
using Spectre.Console;

namespace BuzzHeavierDownloader;

public class Downloader : IDisposable
{
    private readonly HttpClient _client;
    private readonly string _destination;
    private readonly int _bufferSize;

    public Downloader(string destination, int bufferSize)
    {
        _destination = destination;
        _bufferSize = bufferSize;
        _client = new HttpClient();
        _client.BaseAddress = new Uri("https://buzzheavier.com/");
        _client.Timeout = TimeSpan.FromMinutes(45);
    }

    public async Task DownloadAsync(string id, ProgressContext context) //
    {
        var downloadLink = await GetDownloadLinkAsync(id);
        var request = new HttpRequestMessage(HttpMethod.Get, new Uri(downloadLink));
        using var response = await _client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();
        var contentLength = response.Content.Headers.ContentLength ?? 0;
        var fileName = $"{id}.mkv";
        if (response.Content.Headers.TryGetValues("Content-Disposition", out var headerValues))
        {
            var value = headerValues.FirstOrDefault()?.Replace("attachment; filename*=UTF-8''", string.Empty);
            if (!string.IsNullOrWhiteSpace(value))
            {
                fileName = HttpUtility.UrlDecode(value);
            }
        }
        var task = context.AddTask(Markup.Escape(fileName), false, contentLength);
        task.IsIndeterminate();
        await using var destFileStream = new FileStream(Path.Join(_destination, fileName), FileMode.Create);
        await using var stream = await response.Content.ReadAsStreamAsync();
        var buffer = ArrayPool<byte>.Shared.Rent(_bufferSize);
        try
        {
            task.StartTask();
            task.IsIndeterminate(false);
            long contentProgress = 0;
            while (true)
            {
                int readLength;
                if ((readLength = await stream.ReadAsync(new Memory<byte>(buffer)).ConfigureAwait(false)) != 0)
                {
                    await destFileStream.WriteAsync(new ReadOnlyMemory<byte>(buffer, 0, readLength)).ConfigureAwait(false);
                    contentProgress += readLength;
                    task.Value(contentProgress);
                }
                else
                {
                    break;
                }
            }
            task.Value(task.MaxValue);
            task.StopTask();
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
            if (!task.IsFinished)
            {
                task.StopTask();
            }
        }
    }

    private async Task<string> GetDownloadLinkAsync(string id)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get,  id + "/download");
        request.Headers.Add("Hx-Current-Url", "https://buzzheavier.com/" + id);
        request.Headers.Add("Hx-Request", "true");
        request.Headers.Add("Dnt", "1");
        request.Headers.Add("Referer", "https://buzzheavier.com/" + id);
        using var response = await _client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        if (response.Headers.TryGetValues("Hx-Redirect", out var values))
        {
            return values.FirstOrDefault() ?? throw new Exception("No header found!");
        }
        throw new Exception("No header found!");
    }
    
    public void Dispose()
    {
        _client.Dispose();
        GC.SuppressFinalize(this);
    }
}