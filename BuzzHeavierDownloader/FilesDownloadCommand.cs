using System.ComponentModel;
using System.Diagnostics;
using Spectre.Console;
using Spectre.Console.Cli;

namespace BuzzHeavierDownloader;

internal sealed class FilesDownloadCommand : AsyncCommand<FilesDownloadCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [Description(
            "Source of files. It can be a link to Google Drive file, file id or a path to new line separeted file that contains links or ids")]
        [CommandArgument(0, "<source>")]
        public string Source { get; init; } = null!;

        [Description(
            "Destination folder where files will be saved. If not specified, program will write to the current directory")]
        [CommandArgument(1, "[dest]")]
        public string? Destination { get; init; }

        [Description(
            "Sets the degree of parallelism when downloading files.")]
        [CommandOption("-p|--parallelLevel")]
        [DefaultValue(4)]
        public int ParallelismLevel { get; init; }

        [Description(
            "Sets buffer size when downloading files.")]
        [CommandOption("-b|--bufferSize")]
        [DefaultValue(1024 * 512)]
        public int BufferSize { get; init; }
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        if (!File.Exists(settings.Source))
        {
            throw new FileNotFoundException("Source file not found", settings.Source);
        }

        if (settings.Destination is not null && !Directory.Exists(settings.Destination))
        {
            throw new DirectoryNotFoundException("Destination directory not found");
        }

        var ids = await File.ReadAllLinesAsync(settings.Source);

        var dest = settings.Destination ?? Environment.CurrentDirectory;
        using var downloader = new Downloader(dest, settings.BufferSize);

        var taskDescriptionColumn = new TaskDescriptionColumn
        {
            Alignment = Justify.Left
        };
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
                taskDescriptionColumn // Task description
            ]);

        await progress.StartAsync(async ctx =>
        {
            var stopwatch = Stopwatch.StartNew();
            await Parallel.ForEachAsync(ids, new ParallelOptions { MaxDegreeOfParallelism = settings.ParallelismLevel },
                async (s, token) =>
                {
                    try
                    {
                        if (!string.IsNullOrWhiteSpace(s))
                        {
                            await downloader.DownloadAsync(s, ctx);
                        }
                    }
                    catch (Exception ex)
                    {
                        AnsiConsole.Markup("[red]Error occured while downloading {0}[/]", s);
                        AnsiConsole.WriteException(ex);
                    }
                });
            stopwatch.Stop();

            AnsiConsole.Markup(
                "Downloaded [green]{0}[/] files in [yellow]{1}[/] hours [yellow]{2}[/] minutes [yellow]{3}[/] seconds",
                ids.Length,
                stopwatch.Elapsed.Hours,
                stopwatch.Elapsed.Minutes,
                stopwatch.Elapsed.Seconds
            );
        });

        return 0;
    }
}