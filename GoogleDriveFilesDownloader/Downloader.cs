using Google.Apis.Download;
using Google.Apis.Drive.v3;
using Google.Apis.Services;

namespace GoogleDriveFilesDownloader;

public sealed class Downloader : IDisposable
{
    private readonly string _destination;
    private readonly DriveService _driveService;

    public Downloader(string apiKey, string destination)
    {
        _destination = destination;
        _driveService = new DriveService(new BaseClientService.Initializer
        {
            ApplicationName = "GoogleDriveFilesDownloader",
            ApiKey = apiKey
        });
    }

    public Google.Apis.Drive.v3.Data.File GetFileInfo(string fileId)
    {
        var request = _driveService.Files.Get(fileId);

        // request.AcknowledgeAbuse = true;
        request.Fields = "fileExtension,size,mimeType,name,capabilities,id";
        return request.Execute();
    }

    public void Download(Google.Apis.Drive.v3.Data.File fileInfo, Action<IDownloadProgress> progressCallback)
    {
        var request = _driveService.Files.Get(fileInfo.Id);

        var fileName = fileInfo.Name;
        if (!string.IsNullOrEmpty(fileInfo.FileExtension) && !fileName.EndsWith("." + fileInfo.FileExtension))
        {
            fileName += "." + fileInfo.FileExtension;
        }

        request.AcknowledgeAbuse = true;
        request.Fields = string.Empty;

        var fileStream = File.OpenWrite(Path.Join(_destination, fileName));

        request.MediaDownloader.ProgressChanged += progressCallback;
        // request.MediaDownloader.
        request.Download(fileStream);
    }

    public void Dispose()
    {
        _driveService.Dispose();
    }
}