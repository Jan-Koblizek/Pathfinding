using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking.Types;

class AlternativeFlowToPathsDistribution
{
    private readonly List<ConcurrentPaths> _alternatives = new List<ConcurrentPaths>();

    internal void MutatePath(AlternativeFlowToPathsDistribution completed, List<List<NodeID>> alternatingSubPaths, float negativeFlow, FlowGraph flowGraph)
    {
        //save all paths
        List<ConcurrentPaths> copy = new List<ConcurrentPaths>(_alternatives);
        completed._alternatives.AddRange(copy);

        foreach (var concurrentPaths in _alternatives)
        {
            concurrentPaths.Mutate(alternatingSubPaths, negativeFlow, flowGraph);
        }
    }

    internal void AddPath(PlannerPath orderedPartialFlow)
    {
        if (_alternatives.Count == 0)
        {
            _alternatives.Add(new ConcurrentPaths());
        }

        foreach (var alternative in _alternatives)
        {
            alternative.Add(orderedPartialFlow.PathID);
        }
    }

    internal List<ConcurrentPaths> Finish(AlternativeFlowToPathsDistribution completed)
    {
        foreach (var concurrentPath in _alternatives)
        {
            concurrentPath.Merge();
        }

        completed._alternatives.AddRange(_alternatives);
        return completed._alternatives;
    }
}