// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Threading.Tasks;
using Docker.DotNet;

namespace CoreWCF.Kafka.Tests.Helpers;

public static class DockerEx
{
    private static readonly Lazy<DockerClient> s_client = new(() => new DockerClientConfiguration().CreateClient());

    public static Task PauseAsync(string containerName) => s_client.Value.Containers.PauseContainerAsync(containerName);

    public static Task UnpauseAsync(string containerName) => s_client.Value.Containers.UnpauseContainerAsync(containerName);
}
