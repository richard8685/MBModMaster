using MBModMaster.Models;

namespace MBModMaster.Core;

public sealed class LoadOrderService
{
    public LoadOrderResult Sort(IReadOnlyList<BannerlordModule> modules)
    {
        var sortableModules = modules
            .Where(module => module.IsSinglePlayerCompatible)
            .ToList();

        var moduleById = sortableModules.ToDictionary(module => module.Id, StringComparer.OrdinalIgnoreCase);
        var originalIndex = modules
            .Select((module, index) => new { module.Id, Index = index })
            .GroupBy(item => item.Id, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First().Index, StringComparer.OrdinalIgnoreCase);

        var indegree = sortableModules.ToDictionary(module => module.Id, _ => 0, StringComparer.OrdinalIgnoreCase);
        var edges = sortableModules.ToDictionary(module => module.Id, _ => new List<string>(), StringComparer.OrdinalIgnoreCase);
        var missingDependencies = new List<MissingDependency>();

        foreach (var module in sortableModules)
        {
            foreach (var dependencyId in module.Dependencies.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (!moduleById.ContainsKey(dependencyId))
                {
                    missingDependencies.Add(new MissingDependency(module.Id, dependencyId));
                    continue;
                }

                if (string.Equals(module.Id, dependencyId, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                edges[dependencyId].Add(module.Id);
                indegree[module.Id]++;
            }

            foreach (var afterThisModuleId in module.LoadAfterThisModuleIds.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (!moduleById.ContainsKey(afterThisModuleId)
                    || string.Equals(module.Id, afterThisModuleId, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                edges[module.Id].Add(afterThisModuleId);
                indegree[afterThisModuleId]++;
            }
        }

        var ready = sortableModules
            .Where(module => indegree[module.Id] == 0)
            .OrderBy(module => GetSortPriority(module, originalIndex))
            .ToList();

        var sorted = new List<BannerlordModule>();
        while (ready.Count > 0)
        {
            var current = ready[0];
            ready.RemoveAt(0);
            sorted.Add(current);

            foreach (var dependentId in edges[current.Id]
                .OrderBy(id => originalIndex.GetValueOrDefault(id, int.MaxValue)))
            {
                indegree[dependentId]--;
                if (indegree[dependentId] == 0)
                {
                    ready.Add(moduleById[dependentId]);
                    ready = ready.OrderBy(module => GetSortPriority(module, originalIndex)).ToList();
                }
            }
        }

        var hasCycle = sorted.Count != sortableModules.Count;
        if (hasCycle)
        {
            var sortedIds = sorted.Select(module => module.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
            sorted.AddRange(sortableModules
                .Where(module => !sortedIds.Contains(module.Id))
                .OrderBy(module => GetSortPriority(module, originalIndex)));
        }

        var excludedModules = modules
            .Where(module => !module.IsSinglePlayerCompatible)
            .OrderBy(module => originalIndex.GetValueOrDefault(module.Id, int.MaxValue));

        sorted.AddRange(excludedModules);
        return new LoadOrderResult(sorted, missingDependencies, hasCycle);
    }

    private static int GetSortPriority(BannerlordModule module, IReadOnlyDictionary<string, int> originalIndex)
    {
        if (module.IsFoundationModule)
        {
            var foundationIndex = Array.FindIndex(BannerlordModule.FoundationModuleIds, id => string.Equals(id, module.Id, StringComparison.OrdinalIgnoreCase));
            return foundationIndex < 0 ? 90 : foundationIndex;
        }

        if (module.IsBaseModule)
        {
            var baseIndex = Array.FindIndex(BannerlordModule.BaseModuleIds, id => string.Equals(id, module.Id, StringComparison.OrdinalIgnoreCase));
            return baseIndex < 0 ? 200 : 100 + baseIndex;
        }

        return 1000 + originalIndex.GetValueOrDefault(module.Id, int.MaxValue);
    }
}

public sealed record LoadOrderResult(
    IReadOnlyList<BannerlordModule> Modules,
    IReadOnlyList<MissingDependency> MissingDependencies,
    bool HasCycle);

public sealed record MissingDependency(string ModuleId, string DependencyId);
