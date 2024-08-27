using System.Text.Json;
using Google.Apis.Download;
using Spectre.Console;
using Spectre.Console.Cli;

namespace GoogleDriveFilesDownloader;

internal sealed class FilesDownloadCommand : Command<FilesDownloadCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<source>")] 
        public string Source { get; init; } = null!;
        [CommandArgument(1, "[dest]")]
        public string? Destination { get; set; }

        [CommandOption("-a|--apiKey")] 
        public string ApiKey { get; init; } = null!;
    }

    public override int Execute(CommandContext context, Settings settings)
    {
        var fileIds = SourceParser.GetIdsFromSource(settings.Source);
        
        var progress = AnsiConsole.Progress()
            .HideCompleted(false)
            .AutoRefresh(true)
            .AutoClear(false)
            .Columns([
                new TaskDescriptionColumn(), // Task description
                new ProgressBarColumn(), // Progress bar
                new PercentageColumn(), // Percentage
                new TransferSpeedColumn(),
                new RemainingTimeColumn(),
                new SpinnerColumn(), // Spinner,
                new DownloadedColumn()
            ]);

        var downloader = new Downloader(settings.ApiKey, Environment.CurrentDirectory);
        
        foreach (var fileId in fileIds)
        {
            var fileInfo = downloader.GetFileInfo(fileId);
            AnsiConsole.Markup("Downloading {0}", Markup.Escape(fileInfo.Name));
            progress.Start(ctx =>
            {
                var task = ctx.AddTask("[green]Download[/]", maxValue: fileInfo.Size ?? 0);
                downloader.Download(fileInfo, downloadProgress =>
                {
                    if (!task.IsStarted)
                    {
                        task.StartTask();
                    }
                    switch (downloadProgress.Status)
                    {
                        case DownloadStatus.Downloading:
                            task.Value(downloadProgress.BytesDownloaded);
                            break;
                        case DownloadStatus.Failed:
                            task.StopTask();
                            AnsiConsole.Markup(
                                "[red]Error during download occured![/] Error message: {0}", 
                                Markup.Escape(downloadProgress.Exception.Message)
                            );
                            break;
                        case DownloadStatus.Completed:
                            task.Value(task.MaxValue);
                            task.StopTask();
                            break;
                    }
                });
            });
        }
        
        return 0;
    }
}