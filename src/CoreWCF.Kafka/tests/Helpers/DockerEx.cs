// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Xunit.Abstractions;

namespace CoreWCF.Kafka.Tests.Helpers;

public static class DockerEx
{
    public static async Task PauseAsync(ITestOutputHelper output, string containerName)
    {
        // build a command to pause the container
        string command = $"pause {containerName}";
        output.WriteLine($"Pausing container {containerName}");
        var lines = await ExecAsync($"compose pause {containerName}");
        foreach (string line in lines)
        {
            output.WriteLine(line);
        }
        output.WriteLine($"Container {containerName} paused");
    }

    public static async Task UnpauseAsync(ITestOutputHelper output, string containerName)
    {
        // build a command to unpause the container
        string command = $"unpause {containerName}";
        output.WriteLine($"Unpausing container {containerName}");
        var lines = await ExecAsync($"compose unpause {containerName}");
        foreach (string line in lines)
        {
            output.WriteLine(line);
        }
        output.WriteLine($"Container {containerName} unpaused");
    }

    private static async Task<IReadOnlyList<string>> ExecAsync(string command)
    {
        // Execute a docker command and return the standard output
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "docker",
                Arguments = command,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            }
        };
        process.Start();
        Console.WriteLine($@"{process.StartInfo.FileName} {process.StartInfo.Arguments}");
        List<string> output = new();
        while (!process.StandardOutput.EndOfStream)
        {
            string line = await process.StandardOutput.ReadLineAsync();
            Console.WriteLine(line);
            output.Add(line);
        }

        await process.WaitForExitAsync();

        return output.AsReadOnly();
    }
}
