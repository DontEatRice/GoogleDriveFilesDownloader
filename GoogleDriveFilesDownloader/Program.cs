// https://developers.google.com/drive/api/guides/ref-export-formats
// https://developers.google.com/drive/api/guides/mime-types
using GoogleDriveFilesDownloader;
using Spectre.Console.Cli;

var app = new CommandApp<FilesDownloadCommand>();
return app.Run(args);