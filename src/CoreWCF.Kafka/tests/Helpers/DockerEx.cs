// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

namespace CoreWCF.Kafka.Tests.Helpers;

public static class DockerEx
{
    public static async Task<IReadOnlyList<string>> RunAsync(string containerName, string command)
    {
        // Execute a docker command and return the standard output
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "docker",
                Arguments = $"exec -i {containerName} {command}",
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
