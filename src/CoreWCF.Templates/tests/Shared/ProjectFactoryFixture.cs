// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Xunit.Abstractions;

namespace Templates.Test.Helpers;

public class ProjectFactoryFixture : IDisposable
{
    private readonly ConcurrentDictionary<string, Project> _projects = new ConcurrentDictionary<string, Project>();

    public IMessageSink DiagnosticsMessageSink { get; }

    public ProjectFactoryFixture(IMessageSink diagnosticsMessageSink)
    {
        DiagnosticsMessageSink = diagnosticsMessageSink;
    }

    public Project GetOrCreateProject(string projectKey, string targetFramework, ITestOutputHelper output)
    {
        // Different tests may have different output helpers, so need to fix up the output to write to the correct log
        if (_projects.TryGetValue(projectKey, out var project))
        {
            project.Output = output;
            return project;
        }
        return _projects.GetOrAdd(
            projectKey,
            (key, outputHelper) =>
            {
                var project = new Project
                {
                    Output = outputHelper,
                    TargetFramework = targetFramework,
                    DiagnosticsMessageSink = DiagnosticsMessageSink,
                    ProjectGuid = Path.GetRandomFileName().Replace(".", string.Empty)
                };
                project.ProjectName = $"CoreWCFService.{project.ProjectGuid}";

                var basePath = GetTemplateFolderBasePath();
                project.TemplateOutputDir = Path.Combine(basePath, project.ProjectName);
                return project;
            },
            output);
    }

    private static string GetTemplateFolderBasePath() => Path.Combine( typeof(ProjectFactoryFixture).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .Single(a => a.Key == "TestTemplatesPath")
            .Value, "BaseFolder");

    public void Dispose()
    {
        var list = new List<Exception>();
        foreach (var project in _projects)
        {
            try
            {
                project.Value.Dispose();
            }
            catch (Exception e)
            {
                list.Add(e);
            }
        }

        if (list.Count > 0)
        {
            throw new AggregateException(list);
        }
    }
}
