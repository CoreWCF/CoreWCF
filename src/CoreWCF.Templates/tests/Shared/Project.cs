// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Internal;
using Microsoft.Extensions.CommandLineUtils;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;
using Xunit.Sdk;
using static Templates.Test.Helpers.ProcessLock;

namespace Templates.Test.Helpers;

[DebuggerDisplay("{ToString(),nq}")]
public class Project : IDisposable
{
    public string ProjectName { get; set; }
    public string ProjectArguments { get; set; }
    public string ProjectGuid { get; set; }
    public string TemplateOutputDir { get; set; }
    public string TargetFramework { get; set; } = "net8.0";
    public string RuntimeIdentifier { get; set; } = string.Empty;
    public static DevelopmentCertificate DevCert { get; } = DevelopmentCertificate.Create(AppContext.BaseDirectory);

    public string TemplateBuildDir => Path.Combine(TemplateOutputDir, "bin", "Debug", TargetFramework, RuntimeIdentifier);
    public string TemplatePublishDir => Path.Combine(TemplateOutputDir, "bin", "Release", TargetFramework, RuntimeIdentifier, "publish");

    public ITestOutputHelper Output { get; set; }
    public IMessageSink DiagnosticsMessageSink { get; set; }

    internal async Task<ProcessResult> RunDotNetNewAsync(
        string templateName,
        string auth = null,
        string language = null,
        bool useLocalDB = false,
        bool noHttps = false,
        bool errorOnRestoreError = true,
        string[] args = null,
        // Used to set special options in MSBuild
        IDictionary<string, string> environmentVariables = null)
    {
        var argString = $"new {templateName}";
        environmentVariables ??= new Dictionary<string, string>() { { "DOTNET_CLI_UI_LANGUAGE", "en" } };
        if (!string.IsNullOrEmpty(auth))
        {
            argString += $" --auth {auth}";
        }

        if (!string.IsNullOrEmpty(language))
        {
            argString += $" -lang {language}";
        }

        if (useLocalDB)
        {
            argString += $" --use-local-db";
        }

        if (noHttps)
        {
            argString += $" --no-https";
        }

        argString += " --no-restore";

        if (args != null)
        {
            foreach (var arg in args)
            {
                argString += " " + arg;
            }
        }

        // Save a copy of the arguments used for better diagnostic error messages later.
        // We omit the hive argument and the template output dir as they are not relevant and add noise.
        ProjectArguments = argString;
        argString += $" -o {TemplateOutputDir}";

        // Only run one instance of 'dotnet new' at once, as a workaround for
        // https://github.com/aspnet/templating/issues/63

        await DotNetNewLock.WaitAsync();
        try
        {
            Output.WriteLine("Acquired DotNetNewLock");

            if (Directory.Exists(TemplateOutputDir))
            {
                Output.WriteLine($"Template directory already exists, deleting contents of {TemplateOutputDir}");
                Directory.Delete(TemplateOutputDir, recursive: true);
            }

            using var execution = ProcessEx.Run(Output, AppContext.BaseDirectory, DotNetMuxer.MuxerPathOrDefault(), argString, environmentVariables);
            await execution.Exited;

            var result = new ProcessResult(execution);

            // Because dotnet new automatically restores but silently ignores restore errors, need to handle restore errors explicitly
            if (errorOnRestoreError && (execution.Output.Contains("Restore failed.") || execution.Error.Contains("Restore failed.")))
            {
                result.ExitCode = -1;
            }

            return result;
        }
        finally
        {
            DotNetNewLock.Release();
            Output.WriteLine("Released DotNetNewLock");
        }
    }

    internal async Task<ProcessResult> RunDotNetRestoreAsync()
    {
        Output.WriteLine("Restoring packages...");

        using var result = ProcessEx.Run(Output, TemplateOutputDir, DotNetMuxer.MuxerPathOrDefault(), $@"restore -v detailed --force --force-evaluate -s https://pkgs.dev.azure.com/dotnet/CoreWCF/_packaging/CoreWCF/nuget/v3/index.json -s https://api.nuget.org/v3/index.json");
        await result.Exited;
        return new ProcessResult(result);
    }

    internal async Task<ProcessResult> RunDotNetPublishAsync(IDictionary<string, string> packageOptions = null, string additionalArgs = null, bool noRestore = true)
    {
        Output.WriteLine("Publishing ASP.NET Core application...");

        // Avoid restoring as part of build or publish. These projects should have already restored as part of running dotnet new. Explicitly disabling restore
        // should avoid any global contention and we can execute a build or publish in a lock-free way

        var restoreArgs = noRestore ? "--no-restore" : null;

        using var result = ProcessEx.Run(Output, TemplateOutputDir, DotNetMuxer.MuxerPathOrDefault(), $"publish {restoreArgs} -c Release /bl {additionalArgs}", packageOptions);
        await result.Exited;
        return new ProcessResult(result);
    }

    internal async Task<ProcessResult> RunDotNetBuildAsync(IDictionary<string, string> packageOptions = null, string additionalArgs = null)
    {
        Output.WriteLine("Building ASP.NET Core application...");

        // Avoid restoring as part of build or publish. These projects should have already restored as part of running dotnet new. Explicitly disabling restore
        // should avoid any global contention and we can execute a build or publish in a lock-free way

        using var result = ProcessEx.Run(Output, TemplateOutputDir, DotNetMuxer.MuxerPathOrDefault(), $"build --no-restore -c Debug /bl {additionalArgs}", packageOptions);
        await result.Exited;
        return new ProcessResult(result);
    }

    internal AspNetProcess StartBuiltProjectAsync(bool hasListeningUri = true, ILogger logger = null, bool useHttps = true)
    {
        var environment = new Dictionary<string, string>
        {
            ["ASPNETCORE_URLS"] = useHttps ? "https://127.0.0.1:0" : "http://127.0.0.1:0",
            ["ASPNETCORE_ENVIRONMENT"] = "Development",
            ["ASPNETCORE_Logging__Console__LogLevel__Default"] = "Debug",
            ["ASPNETCORE_Logging__Console__LogLevel__System"] = "Debug",
            ["ASPNETCORE_Logging__Console__LogLevel__Microsoft"] = "Debug",
            ["ASPNETCORE_Logging__Console__FormatterOptions__IncludeScopes"] = "true",
        };

        var projectDll = Path.Combine(TemplateBuildDir, $"{ProjectName}.dll");
        return new AspNetProcess(DevCert, Output, TemplateOutputDir, projectDll, environment, published: false, hasListeningUri: hasListeningUri, logger: logger);
    }

    internal AspNetProcess StartPublishedProjectAsync(bool hasListeningUri = true, bool usePublishedAppHost = false, bool useHttps = true)
    {
        var environment = new Dictionary<string, string>
        {
            ["ASPNETCORE_URLS"] = useHttps ? "https://127.0.0.1:0" : "http://127.0.0.1:0",
            ["ASPNETCORE_Logging__Console__LogLevel__Default"] = "Debug",
            ["ASPNETCORE_Logging__Console__LogLevel__System"] = "Debug",
            ["ASPNETCORE_Logging__Console__LogLevel__Microsoft"] = "Debug",
            ["ASPNETCORE_Logging__Console__FormatterOptions__IncludeScopes"] = "true",
        };

        var projectDll = Path.Combine(TemplatePublishDir, $"{ProjectName}.dll");
        return new AspNetProcess(DevCert, Output, TemplatePublishDir, projectDll, environment, published: true, hasListeningUri: hasListeningUri, usePublishedAppHost: usePublishedAppHost);
    }

    public void AssertFileExists(string path, bool shouldExist)
    {
        var fullPath = Path.Combine(TemplateOutputDir, path);
        var doesExist = File.Exists(fullPath);

        if (shouldExist)
        {
            Assert.True(doesExist, "Expected file to exist, but it doesn't: " + path);
        }
        else
        {
            Assert.False(doesExist, "Expected file not to exist, but it does: " + path);
        }
    }

    public string ReadFile(string path)
    {
        AssertFileExists(path, shouldExist: true);
        return File.ReadAllText(Path.Combine(TemplateOutputDir, path));
    }

    public void Dispose()
    {
        DeleteOutputDirectory();
    }

    public void DeleteOutputDirectory()
    {
        const int NumAttempts = 10;

        for (var numAttemptsRemaining = NumAttempts; numAttemptsRemaining > 0; numAttemptsRemaining--)
        {
            try
            {
                Directory.Delete(TemplateOutputDir, true);
                return;
            }
            catch (Exception ex)
            {
                if (numAttemptsRemaining > 1)
                {
                    DiagnosticsMessageSink.OnMessage(new DiagnosticMessage($"Failed to delete directory {TemplateOutputDir} because of error {ex.Message}. Will try again {numAttemptsRemaining - 1} more time(s)."));
                    Thread.Sleep(3000);
                }
                else
                {
                    DiagnosticsMessageSink.OnMessage(new DiagnosticMessage($"Giving up trying to delete directory {TemplateOutputDir} after {NumAttempts} attempts. Most recent error was: {ex.StackTrace}"));
                }
            }
        }
    }

    public override string ToString() => $"{ProjectName}: {TemplateOutputDir}";
}
