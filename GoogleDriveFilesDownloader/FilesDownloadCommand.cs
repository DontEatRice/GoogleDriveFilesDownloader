﻿using System.ComponentModel;
using System.Diagnostics;
using Google.Apis.Download;
using Spectre.Console;
using Spectre.Console.Cli;
using File = Google.Apis.Drive.v3.Data.File;

namespace GoogleDriveFilesDownloader;

internal sealed class FilesDownloadCommand : Command<FilesDownloadCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [Description("Source of files. It can be a link to Google Drive file, file id or a path to new line separeted file that contains links or ids")]
        [CommandArgument(0, "<source>")] 
        public string Source { get; init; } = null!;
        [Description("Destination folder where files will be saved. If not specified, program will write to the current directory")]
        [CommandArgument(1, "[dest]")]
        public string? Destination { get; init; }
        [Description("Sets the degree of parallelism when downloading files. Setting it over 2 * processor cores will have no affect")]
        [CommandOption("-p|--parallelLevel")]
        [DefaultValue(2)]
        public int ParallelismLevel { get; init; }

        [Description("Google Cloud Api Key with Google Drive API enabled")]
        [CommandOption("-a|--apiKey")] 
        public string ApiKey { get; init; } = null!;
    }

    public override int Execute(CommandContext context, Settings settings)
    {
        var fileIds = SourceParser.GetIdsFromSource(settings.Source);

        string destinationDir;
        if (!string.IsNullOrEmpty(settings.Destination))
        {
            if (Directory.Exists(settings.Destination))
            {
                destinationDir = settings.Destination;
            }
            else
            {
                throw new ArgumentException($"{settings.Destination} - this folder does not exists",
                    nameof(settings.Destination));
            }
        }
        else
        {
            destinationDir = Environment.CurrentDirectory;
        }
        
        var progress = AnsiConsole.Progress()
            .HideCompleted(false)
            .AutoRefresh(true)
            .AutoClear(false)
            .Columns([
                new ProgressBarColumn(), // Progress bar
                new PercentageColumn(), // Percentage
                new TransferSpeedColumn(),
                new RemainingTimeColumn(),
                new SpinnerColumn(), // Spinner,
                new DownloadedColumn(),
                new TaskDescriptionColumn() // Task description
            ]);

        var stopWatch = Stopwatch.StartNew();

        var downloadedCount = 0;
        var downloader = new Downloader(settings.ApiKey, destinationDir);
        foreach (var chunk in fileIds.Chunk(settings.ParallelismLevel))
        {
            var fileInfosResults = chunk.Select(fileId => downloader.GetFileInfo(fileId)).AsParallel().ToArray();
            var fileInfos = new List<File>();
            foreach (var fileInfoResult in fileInfosResults)
            {
                if (!fileInfoResult.IsOk)
                {
                    AnsiConsole.MarkupInterpolated($"[red]{fileInfoResult.Error}[/]");
                    continue;
                }
                fileInfos.Add(fileInfoResult.Value);
            }

            progress.Start(ctx =>
            {
                Parallel.ForEach(fileInfos, fileInfo =>
                {
                    var descFileName = fileInfo.Name.Trim();
                    if (descFileName.Length > 80)
                    {
                        descFileName = descFileName.PadLeft(80) + "...";
                    }
                    var task = ctx.AddTask(Markup.Escape(descFileName), autoStart: false, maxValue: fileInfo.Size ?? 0);
                    downloader.Download(fileInfo, downloadProgress =>
                    {
                        if (!task.IsStarted && downloadProgress.Status == DownloadStatus.Downloading)
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
                                Interlocked.Add(ref downloadedCount, 1);
                                break;
                            case DownloadStatus.NotStarted:
                                break;
                        }
                    });
                });
            });
        }

        stopWatch.Stop();
        AnsiConsole.Markup(
            "Downloaded [green]{0}[/] files in [yellow]{1}[/] hours [yellow]{2}[/] minutes [yellow]{3}[/] seconds",
            downloadedCount, 
            stopWatch.Elapsed.Hours,
            stopWatch.Elapsed.Minutes,
            stopWatch.Elapsed.Seconds
        ); 
        
        return 0;
    }
}