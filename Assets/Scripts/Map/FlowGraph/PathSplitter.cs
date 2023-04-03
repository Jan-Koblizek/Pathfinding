using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking.Types;

/// <summary>
/// Class to build alternating flow.
/// Alternation means one sub flow goes with the flow the other goes opposite.
/// </summary>
public class PathSplitter
{
    private bool _isCounterFlow;
    private List<List<NodeID>> _alternatingSubFlows;
    private List<NodeID> _currentPath;

    public PathSplitter()
    {
        _alternatingSubFlows = new List<List<NodeID>>();
        _currentPath = new List<NodeID>();
    }

    /// <summary>
    /// Processes the edge to join right sub-flow
    /// </summary>
    /// <param name="start">edge start</param>
    /// <param name="end">edge end</param>
    /// <param name="flowDirection"><code>true</code>if the direction is new or in direction of previous or <code>false</code> if flow is in opposite direction</param>
    public void AddEdge(int start, int end, bool flowDirection)
    {
        if (flowDirection == _isCounterFlow)
        {
            if (_currentPath.Count == 0)
            {
                _currentPath.Add(new NodeID(start));
            }

            _currentPath.Add(new NodeID(end));
        }
        else
        {
            _isCounterFlow = flowDirection;
            _alternatingSubFlows.Add(_currentPath);
            _currentPath = new List<NodeID>() { new NodeID(start), new NodeID(end) };
        }
    }

    /// <summary>
    /// Gets the resulting alternating sub flow list. 
    /// </summary>
    /// <returns>Alternating sequence of paths in one direction (consisting of nodes to pass)</returns>
    public List<List<NodeID>> Finish()
    {
        _alternatingSubFlows.Add(_currentPath);
        var ret = _alternatingSubFlows;
        _currentPath = null;
        _alternatingSubFlows = null;
        return ret;
    }
}
