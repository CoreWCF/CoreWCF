// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;

namespace CoreWCF.Aspire.Explorer.Model;

/// <summary>A contract and the subset of its operations that survived the toolbar filter.</summary>
public sealed record FilteredContract(WsdlContract Contract, IReadOnlyList<WsdlOperation> Operations);

/// <summary>A service and the contracts that survived the toolbar filter.</summary>
public sealed record FilteredService(ServiceNode Node, IReadOnlyList<FilteredContract> Contracts);

/// <summary>
/// Applies the toolbar filter to the loaded WSDL models. Lives here rather than in the tree component
/// so the toolbar's match count and the tree render from exactly the same computation.
/// </summary>
public static class TreeFilter
{
    /// <summary>
    /// Filters the whole catalogue. Services whose WSDL has not been loaded yet are always kept: the
    /// operations are unknown, so hiding them would silently claim there is no match to be found.
    /// </summary>
    public static List<FilteredService> Apply(IEnumerable<ServiceNode> nodes, string? filter)
    {
        var results = new List<FilteredService>();
        var trimmed = filter?.Trim();
        var hasFilter = !string.IsNullOrEmpty(trimmed);

        foreach (var node in nodes)
        {
            var serviceMatches = !hasFilter || Matches(node.Name, trimmed);
            var contracts = new List<FilteredContract>();

            if (node.Model is not null)
            {
                foreach (var contract in node.Model.Contracts)
                {
                    // A matching service or contract name keeps all of its operations, so a name
                    // search does not also silently filter the operations underneath it.
                    var keepAll = serviceMatches || Matches(contract.Name, trimmed);
                    var operations = keepAll
                        ? contract.Operations
                        : contract.Operations.FindAll(o => Matches(o.Name, trimmed));

                    if (operations.Count > 0)
                    {
                        contracts.Add(new FilteredContract(contract, operations));
                    }
                }
            }

            if (!hasFilter || serviceMatches || contracts.Count > 0 || !node.IsLoaded)
            {
                results.Add(new FilteredService(node, contracts));
            }
        }

        return results;
    }

    /// <summary>Total number of operations currently visible, for the toolbar count.</summary>
    public static int CountOperations(IEnumerable<FilteredService> services)
    {
        var total = 0;
        foreach (var service in services)
        {
            foreach (var contract in service.Contracts)
            {
                total += contract.Operations.Count;
            }
        }

        return total;
    }

    private static bool Matches(string value, string? filter)
        => string.IsNullOrEmpty(filter) || value.Contains(filter, StringComparison.OrdinalIgnoreCase);
}
