using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Volo.Abp.DependencyInjection;

namespace Dixels.Portal.Data;

/* Starts the ABP CLI through the OS shell: bash on macOS/Linux, cmd.exe on Windows. */
public class ProcessAbpCliRunner : IAbpCliRunner, ITransientDependency
{
    public void CreateMigrationAndRunMigrator(string entityFrameworkCoreProjectFolder)
    {
        string argumentPrefix;
        string fileName;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX) || RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            argumentPrefix = "-c";
            fileName = "/bin/bash";
        }
        else
        {
            argumentPrefix = "/C";
            fileName = "cmd.exe";
        }

        var procStartInfo = new ProcessStartInfo(fileName,
            $"{argumentPrefix} \"abp create-migration-and-run-migrator \"{entityFrameworkCoreProjectFolder}\"\""
        );

        try
        {
            Process.Start(procStartInfo);
        }
        catch (Exception)
        {
            throw new Exception("Couldn't run ABP CLI...");
        }
    }
}
