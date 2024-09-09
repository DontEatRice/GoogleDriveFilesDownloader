using Google.Apis.Download;
using Google.Apis.Drive.v3;
using Google.Apis.Services;

namespace GoogleDriveFilesDownloader;

public sealed class Downloader : IDisposable
{
    private static readonly Dictionary<string, GoogleMimeTypeInfo> GoogleWorkspaceMimeTypes = new Dictionary<string, GoogleMimeTypeInfo>
    {
        {"application/vnd.google-apps.audio", new GoogleMimeTypeInfo(false)},
        {"application/vnd.google-apps.document", new GoogleMimeTypeInfo(false, "Google Docs")},
        {"application/vnd.google-apps.drawing", new GoogleMimeTypeInfo(false, "Google Drawings")},
        {"application/vnd.google-apps.file", new GoogleMimeTypeInfo(false, "Google Drive file")}
    };
    
    private readonly DriveService _driveService;

    public Downloader(string apiKey)
    {
        _driveService = new DriveService(new BaseClientService.Initializer
        {
            ApplicationName = "GoogleDriveFilesDownloader",
            ApiKey = apiKey
        });
    }

    public async Task<Result<Google.Apis.Drive.v3.Data.File>> GetFileInfoAsync(string fileId)
    {
        var request = _driveService.Files.Get(fileId);

        request.Fields = "fileExtension,size,mimeType,name,capabilities,id";
        try
        {
            var fileInfo = await request.ExecuteAsync();

            if (fileInfo.Capabilities.CanDownload is false)
            {
                return $"File {fileInfo.Name} is not downloadable";
            }

            if (GoogleWorkspaceMimeTypes.TryGetValue(fileInfo.MimeType, out var mimeTypeInfo))
            {
                if (!mimeTypeInfo.CanDownload)
                {
                    return $"Downloading files of type {mimeTypeInfo.FriendlyName ?? fileInfo.MimeType} is not supported! File name: {fileInfo.Name}";
                }
            }

            return fileInfo;
        }
        catch (Exception ex)
        {
            return  $"Error while getting information about file {fileId}: " + ex.Message;
        }
    }

    public Task DownloadAsync(Google.Apis.Drive.v3.Data.File fileInfo, string destination, Action<IDownloadProgress> progressCallback)
    {
        var request = _driveService.Files.Get(fileInfo.Id);

        var fileName = fileInfo.Name.Trim();
        if (!string.IsNullOrEmpty(fileInfo.FileExtension) && !fileName.EndsWith("." + fileInfo.FileExtension))
        {
            fileName += "." + fileInfo.FileExtension;
        }

        request.AcknowledgeAbuse = true;
        request.Fields = string.Empty;

        var fileStream = File.OpenWrite(Path.Join(destination, fileName));

        request.MediaDownloader.ProgressChanged += progressCallback;
        request.MediaDownloader.ChunkSize = 0x100000 * 4;
        return request.DownloadAsync(fileStream);
    }

    public void Dispose()
    {
        _driveService.Dispose();
    }

    private sealed record GoogleMimeTypeInfo(bool CanDownload, string? FriendlyName = null, string? DefaultExportMimeType = null);
}