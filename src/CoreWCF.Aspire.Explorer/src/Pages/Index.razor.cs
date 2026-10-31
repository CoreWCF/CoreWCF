// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CoreWCF.Aspire.Explorer.Model;
using CoreWCF.Aspire.Explorer.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace CoreWCF.Aspire.Explorer.Pages;

/// <summary>
/// The explorer page. It owns every piece of mutable state - which services are loaded, the filter,
/// the selected operation and the in-flight invocation - and hands read-only views of it to the tree
/// and detail components.
/// </summary>
public sealed partial class Index : IDisposable
{
    private readonly List<ServiceNode> _nodes = new();

    private List<FilteredService> _view = new();
    private string _filter = string.Empty;

    private OperationSelection? _selection;
    private string _envelope = string.Empty;
    private string _requestTab = "req-formatted";
    private string _responseTab = "resp-formatted";

    private bool _invoking;
    private CancellationTokenSource? _invokeCts;
    private SoapInvocationResult? _result;
    private ParsedSoapResponse? _parsed;
    private string? _error;

    [Inject]
    private ServiceCatalog Catalog { get; set; } = default!;

    [Inject]
    private WsdlExplorerService Explorer { get; set; } = default!;

    [Inject]
    private SoapInvoker Invoker { get; set; } = default!;

    private bool HasFilter => _filter.Trim().Length > 0;

    private string CountLabel
    {
        get
        {
            var operations = TreeFilter.CountOperations(_view);
            var services = _view.Count;
            return $"{operations} {(operations == 1 ? "operation" : "operations")} in " +
                   $"{services} {(services == 1 ? "service" : "services")}";
        }
    }

    protected override async Task OnInitializedAsync()
    {
        foreach (var descriptor in Catalog.Services)
        {
            _nodes.Add(new ServiceNode(descriptor) { IsExpanded = true });
        }

        Refresh();

        // Load every service up front rather than on first expand. A node whose WSDL has not been
        // read has no child items, so fluent-tree-item draws no expand chevron - there would be
        // nothing to click to trigger the lazy load. Eager loading also makes the toolbar filter
        // meaningful immediately. There are a handful of services at most, and each is cached after.
        foreach (var node in _nodes)
        {
            await LoadAsync(node);
        }
    }

    private void Refresh() => _view = TreeFilter.Apply(_nodes, _filter);

    private void OnFilterChanged(string? value)
    {
        _filter = value ?? string.Empty;

        // A match deep in a collapsed service would otherwise stay hidden behind a closed node.
        if (HasFilter)
        {
            foreach (var node in _nodes)
            {
                node.IsExpanded = true;
            }
        }

        Refresh();
    }

    private Task EnsureLoadedAsync(ServiceNode node)
        => node.IsLoaded || node.IsLoading ? Task.CompletedTask : LoadAsync(node);

    private Task ReloadAsync(ServiceNode node)
    {
        node.Model = null;
        node.Error = null;
        return LoadAsync(node);
    }

    private async Task ReloadAllAsync()
    {
        foreach (var node in _nodes)
        {
            if (node.IsLoaded && !node.IsLoading)
            {
                await ReloadAsync(node);
            }
        }
    }

    private async Task LoadAsync(ServiceNode node)
    {
        node.IsLoading = true;
        node.Error = null;
        Refresh();
        StateHasChanged();

        try
        {
            node.Model = await Explorer.LoadAsync(node.Descriptor);
        }
        catch (Exception ex)
        {
            node.Error = $"Failed to load WSDL: {ex.Message}";
        }
        finally
        {
            node.IsLoading = false;
            DropSelectionIfStale(node);
            Refresh();
        }
    }

    /// <summary>
    /// A reload replaces the operation instances, so a selection pointing into the previous model
    /// would keep editing objects the tree no longer shows.
    /// </summary>
    private void DropSelectionIfStale(ServiceNode node)
    {
        if (_selection is null || !ReferenceEquals(_selection.Service, node))
        {
            return;
        }

        foreach (var contract in node.Model?.Contracts ?? new List<WsdlContract>())
        {
            foreach (var operation in contract.Operations)
            {
                if (operation.Name == _selection.Operation.Name && contract.Name == _selection.Contract.Name)
                {
                    OnSelectionChanged(new OperationSelection(node, contract, operation));
                    return;
                }
            }
        }

        ClearSelection();
    }

    private void OnSelectionChanged(OperationSelection selection)
    {
        _selection = selection;
        _envelope = selection.Operation.SampleRequestEnvelope;
        _requestTab = selection.Operation.CanUseFormattedRequest ? "req-formatted" : "req-xml";
        _responseTab = "resp-formatted";
        ClearResult();
    }

    private void ClearSelection()
    {
        _selection = null;
        _envelope = string.Empty;
        ClearResult();
    }

    private void ClearResult()
    {
        _result = null;
        _parsed = null;
        _error = null;
    }

    private Task OnKeyDownAsync(KeyboardEventArgs args)
        => args.CtrlKey && args.Key == "Enter" ? InvokeOperationAsync() : Task.CompletedTask;

    private async Task InvokeOperationAsync()
    {
        if (_selection is null || _invoking)
        {
            return;
        }

        var operation = _selection.Operation;
        var envelope = _requestTab == "req-formatted" && operation.CanUseFormattedRequest
            ? SoapRequestBuilder.BuildEnvelope(operation)
            : _envelope;

        _invoking = true;
        ClearResult();

        _invokeCts?.Dispose();
        _invokeCts = new CancellationTokenSource();

        try
        {
            _result = await Invoker.InvokeAsync(
                _selection.EndpointAddress, operation, envelope, _invokeCts.Token);
            _parsed = SoapResponseParser.Parse(_result.Body);
            _responseTab = _parsed is { Rows.Count: > 0 } ? "resp-formatted" : "resp-xml";
        }
        catch (OperationCanceledException)
        {
            _error = "Invocation cancelled.";
        }
        catch (Exception ex)
        {
            _error = $"{ex.GetType().Name}: {ex.Message}";
        }
        finally
        {
            _invoking = false;
        }
    }

    private void CancelInvoke() => _invokeCts?.Cancel();

    public void Dispose()
    {
        _invokeCts?.Cancel();
        _invokeCts?.Dispose();
    }
}
