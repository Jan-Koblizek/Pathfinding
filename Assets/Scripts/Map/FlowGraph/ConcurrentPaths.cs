using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Unity.VisualScripting;
using UnityEngine;

internal class ConcurrentPaths
{
    /// <summary>
    /// List of concurrent paths - all paths can be used at once; it is partially sorted by its cost 
    /// </summary>
    private readonly List<PathID> _atOnce = new List<PathID>();

    public List<PathID> GetAtOnce()
    {
        return _atOnce;
    }

    /// <summary>
    /// Merges two same paths with different flows to one.
    /// </summary>
    /// <param name="p0">Path 0</param>
    /// <param name="p1">Path 1</param>
    /// <returns>new path</returns>
    private PathID Merge(PathID p0, PathID p1)
    {
        var path0 = PlannerPath.GetPathById(p0);
        var path1 = PlannerPath.GetPathById(p1);

        PlannerPath.ClonePath(p0, path0.Flow + path1.Flow, out var joined);
        return joined.PathID;
    }

    /// <summary>
    /// Updates the input list and returns info if it was changed. The list is altered if the underlying rawIDs are same then all such paths get merged.
    /// </summary>
    /// <param name="sameCostRange">PathID that have same cost and are sorted by the underlying rawID</param>
    /// <returns>True if there was a change</returns>
    private bool MergeSortedPath(List<PathID> sameCostRange, FlowGraph flowGraph)
    {
        bool change = false;
        for (int j = 1; j < sameCostRange.Count; j++)
        {
            var p0 = sameCostRange[j - 1];
            var p1 = sameCostRange[j];
            var rawID0 = PlannerPath.RawPath.PathIDToRaw[p0];
            var rawID1 = PlannerPath.RawPath.PathIDToRaw[p1];

            List<Vector2> path0 = PlannerPath.GetPathsFlowNodeCenters(flowGraph, p0);
            List<Vector2> path1 = PlannerPath.GetPathsFlowNodeCenters(flowGraph, p1);
            bool same = true;
            if (rawID0 != rawID1) {
                for (int i = 0; i < path0.Count; i++)
                {
                    same = i < path1.Count && path1[i] == path0[i];
                    if (!same) break;
                }
            }
            if (same)
            {
                sameCostRange[j - 1] = Merge(p0, p1);
                sameCostRange.RemoveAt(j);
                j--; // j++ will increase the value to j where it is compared to the merged path
                change = true;
            }
        }

        return change;
    }

    private static int ComparePathsByCost(PathID p, PathID q)
    {
        return PlannerPath.GetPathById(p).Cost.
            CompareTo(
                PlannerPath.GetPathById(q).Cost);
    }

    private static int ComparePathsByRawPath(PathID p, PathID q)
    {
        return PlannerPath.RawPath.PathIDToRaw[p].
            CompareTo(
                PlannerPath.RawPath.PathIDToRaw[q]);
    }

    /// <summary>
    /// Sorts and merges the concurrent paths.
    /// </summary>
    internal void Merge(FlowGraph flowGraph)
    {
        //we can't just detect the twos next to each other
        //a b a b, where a, b are paths would not be found...
        if (_atOnce == null || _atOnce.Count == 0)
        {
            return;
        }

        _atOnce.Sort(ComparePathsByCost);

        var prev = 0;

        for (int i = 1; i < _atOnce.Count; i++)
        {
            PlannerPath path1 = PlannerPath.GetPathById(_atOnce[i - 1]);
            PlannerPath path2 = PlannerPath.GetPathById(_atOnce[i]);
            Single cost0 = path1.Cost;
            Single cost1 = path2.Cost;

            //cost0 != cost1 (stop of the sequence of same costs)
            if (Mathf.Abs(cost0 - cost1) > 0.05f)
            {
                int count = i - prev;
                if (count > 1)
                {
                    List<PathID> sameCostRange = _atOnce.GetRange(prev, count);
                    sameCostRange.Sort(ComparePathsByRawPath);
                    bool change = MergeSortedPath(sameCostRange, flowGraph);

                    if (change)
                    {
                        _atOnce.RemoveRange(prev, count);
                        _atOnce.InsertRange(prev, sameCostRange);
                        i = prev + sameCostRange.Count - 1;
                    }
                    /*
                    else
                    {
                        Debug.Log(count);
                    }
                    */
                }
                prev = i;
            }
        }
    }

    /// <summary>
    /// Struct which holds the state of unfinished mutation path
    /// </summary>
    private struct MutationState
    {
        public readonly List<List<NodeID>> Path;
        public readonly float Flow;

        public MutationState(List<List<NodeID>> alternatingSubPaths, float negativeFlow)
        {
            Path = alternatingSubPaths;
            Flow = negativeFlow;
        }
    }
    /// <summary>
    /// When a flow in opposite direction occurs this method mutates already existing path with flow to avoid the negative path flow
    /// </summary>
    internal void Mutate(List<List<NodeID>> alternatingSubPaths, float negativeFlow, FlowGraph flowGraph)
    {
        var stack = new Stack<MutationState>();
        stack.Push(new MutationState(alternatingSubPaths, negativeFlow));

        while (stack.Count != 0)
        {
            var state = stack.Pop();
            alternatingSubPaths = state.Path;
            negativeFlow = state.Flow;

            switch (alternatingSubPaths.Count)
            {
                case 3: break;
                case 1:
                case 0:
                    continue;
                default:
                    // 3 - ok
                    //0-1: this is ok, but the method should not have been invoked
                    //2: weird - that means that the path would return from somewhere to the goal (which does not happen in shortest path)
                    //for more than 3 (multiple negatives on the path) are ok for 3,5,7 (when joined with mutated path it will produce 3)
                    //(alternatingSubPaths.Count % 2 == 1)
                    if (alternatingSubPaths.Count % 2 == 1) //5,7,9...
                    {
                        //ok
                        break;
                    }
                    throw new Exception();
            }


            HashSet<PathID> pathsToMutate = FindPathsForMutation(alternatingSubPaths[1], negativeFlow);

            //randomly select a path that won't have full mutation but just partial
            int notFullMutationPathId = UnityEngine.Random.Range(0, pathsToMutate.Count);
            int i = 0;
            foreach (PathID pathID in pathsToMutate)
            {
                float flow = PlannerPath.GetPathById(pathID).Flow;
                
                if (i++ == notFullMutationPathId)
                {
                    flow = negativeFlow - (GetTotalFlow(pathsToMutate) - flow);
                }
                

                Mutate(alternatingSubPaths, pathID, flow, stack, flowGraph);
            }
        }
    }

    /// <summary>
    /// Mutates the path if alternating sub paths counts >3 then it uses stack to save partially finished path;
    /// </summary>
    /// <param name="alternatingSubPaths">parts to mutate with</param>
    /// <param name="pathID">path to mutate with</param>
    /// <param name="flow">size of the flow</param>
    /// <param name="stack">stack to save partially mutated path to</param>
    private void Mutate(List<List<NodeID>> alternatingSubPaths, PathID pathID, float flow, Stack<MutationState> stack, FlowGraph flowGraph)
    {
        if (alternatingSubPaths.Count < 3)
        {
            throw new ArgumentOutOfRangeException(nameof(alternatingSubPaths), "The mutation of paths need 3 sub paths");
        }

        var exists = TryGetNegativeSubpathInPath(alternatingSubPaths[1], pathID, out int j, out int i);
        if (!exists)
        {
            throw new Exception("path should exist - if it does not it was a composite path and alteringSubPath should be splitted with an empty forward path");
        }

        var path = PlannerPath.GetPathById(pathID);
        if (path.Flow < flow)
        {
            if (flow - path.Flow < 0.000001f) { flow = path.Flow; }
            else
            {
                Debug.LogError($"Error in plan with paths - part of the flow will be lost: {path.Flow - flow} - the difference between {path.Flow} < {flow}");
            }
        }

        if (path.Flow > flow)
        {
            //this should only happen maximal once per paths combination
            //make a path with smaller flow value
            PlannerPath.ClonePath(pathID, path.Flow - flow, out var newPath);

            _atOnce.Add(newPath.PathID);
        }

        //remove the mutated path
        _atOnce.Remove(pathID);

        //create a new path with given flow by mutating the paths

        var beginning = path.Path.GetRange(0, i);
        var j1 = j + 1;
        var ending = path.Path.GetRange(j1, path.Path.Count - j1);

        var alternativeCopy = alternatingSubPaths[0].GetRange(0, alternatingSubPaths[0].Count);
        //join the 4 sub paths
        alternativeCopy.AddRange(ending);
        beginning.AddRange(alternatingSubPaths[2]);

        if (!PlannerPath.CreatePath(flowGraph, alternativeCopy, flow, out var mutationA))
        {
            //here can be a not needed but possible enhancement
            //flow already existed, possible multiple usage in same atOnce (but that is still correct)
            //but for possible speedup it would be possible to merge all path that go through same nodes
            //with the sum of flows
        }
        _atOnce.Add(mutationA.PathID);

        if (alternatingSubPaths.Count <= 3)
        {
            //mutation finished, there is no more negative sub-paths, so add the newly created path
            PlannerPath.CreatePath(flowGraph, beginning, flow, out var mutationB);
            _atOnce.Add(mutationB.PathID);
        }
        else
        {
            // remaining negative flows that were not used in the mutation save into stack
            //path at index 4 - is again negative -> so after this it will be at position 2 
            var size = alternatingSubPaths.Count;
            var restOfAlternatingPath = alternatingSubPaths.GetRange(3, size - 3);

            restOfAlternatingPath.Insert(0, beginning);

            stack.Push(new MutationState(restOfAlternatingPath, flow));
        }
    }

    /// <summary>
    /// Generate all subsets of a given size from the source set.
    /// </summary>
    /// <param name="set">Source set</param>
    /// <param name="subsetSize">Size of the subsets</param>
    /// <returns>All subsets</returns>
    private IEnumerable<HashSet<PathID>> GetCombinations(IList<PathID> set, int subsetSize)
    {
        int setSize = set.Count;

        if (subsetSize > setSize)
        {
            throw new ArgumentException("Subset has to be smaller then set.");
        }

        if (setSize > 32 || setSize < 0 || subsetSize <= 0)
        {
            if (subsetSize < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(subsetSize), "Size has to be positive");
            }
            else if (setSize < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(setSize), "Size has to be positive");
            }
            else
            {
                throw new ArgumentOutOfRangeException(nameof(setSize), $"The number is too high: {setSize}");
            }
        }



        IEnumerable<HashSet<PathID>> combinations = null;
        if (subsetSize == setSize)
        {
            combinations = from a in new int[] { 1 } select FillHashSet(set.ToArray());
        }
        else if (subsetSize == 2)
        {
            combinations =
                from a in set
                from b in set
                where a.CompareTo(b) < 0
                select FillHashSet(a, b);
        }
        else if (subsetSize == 3)
        {
            combinations =
                from a in set
                from b in set
                from c in set
                where a.CompareTo(b) < 0 && b.CompareTo(c) < 0
                select FillHashSet(a, b, c);
        }
        else if (subsetSize == 4)
        {
            combinations =
                from a in set
                from b in set
                from c in set
                from d in set
                where a.CompareTo(b) < 0 && b.CompareTo(c) < 0 && c.CompareTo(d) < 0
                select FillHashSet(a, b, c, d);

        }
        else if (subsetSize == 1)
        {
            combinations = set.Select(x => new HashSet<PathID>() { x });
        }

        if (combinations != null)
        {
            foreach (var combination in combinations)
            {
                yield return combination;
            }
        }
        else
        { //slower fallback method for bigger values
            var list = RecursiveCombinations(set, subsetSize);
            foreach (var node in list)
            {
                var n = node;
                var outHashSet = new HashSet<PathID>();

                while (n.Next != null)
                {
                    outHashSet.Add(n.Value);
                    n = n.Next;
                }

                outHashSet.Add(n.Value);
                yield return outHashSet;
            }
        }

    }

    /// <summary>
    /// Creates a hashset and fills it with values from the parameters.
    /// </summary>
    /// <param name="paths">Ids to insert</param>
    /// <returns>New Hashset</returns>
    private static HashSet<PathID> FillHashSet(params PathID[] paths)
    {
        var h = new HashSet<PathID>();
        foreach (var pathID in paths)
        {
            h.Add(pathID);
        }

        return h;
    }


    /// <summary>
    /// Recursive function to compute subsets of a set for a higher number of paths.
    /// </summary>
    /// <param name="set">Set of paths to create n-tuples from</param>
    /// <param name="setSize">size of the n-tuple</param>
    /// <returns>All sub sets of a given size.</returns>
    private static IEnumerable<LinkedNode> RecursiveCombinations(ICollection<PathID> set, int setSize)
    {
        if (setSize == 1)
        {
            return set.Select(x => new LinkedNode() { Value = x, Next = null });
        }

        return from a in set
               from b in RecursiveCombinations(set, setSize - 1)
               where a.CompareTo(b.Value) < 0
               select new LinkedNode() { Value = a, Next = b };
    }

    class LinkedNode
    {
        public PathID Value { get; set; }
        public LinkedNode Next { get; set; }
    }

    /// <summary>
    /// Returns the paths that contain given edge (edge here means subpath of a same flow direction)
    /// </summary>
    /// <returns></returns>
    private IList<PathID> EdgeContainingPaths(List<NodeID> negativePath)
    {
        var pathsWithTheEdge = new List<PathID>();
        if (negativePath == null || negativePath.Count == 0)
        {
            return pathsWithTheEdge;
        }
        else if (negativePath.Count == 1)
        {
            throw new Exception("this would imply a self looping edge");
        }

        foreach (var pathID in _atOnce)
        {
            bool edgeInPath = TryGetNegativeSubpathInPath(negativePath, pathID, out _, out _);

            if (edgeInPath)
            {
                pathsWithTheEdge.Add(pathID);
            }
        }
        return pathsWithTheEdge;
    }

    /// <summary>
    /// Finds if the NegativePath is in Path and if so, then return the indexes of the subpath.
    /// </summary>
    /// <param name="negativePath">the subpath with opposite flow direction to the current flow</param>
    /// <param name="pathID">the path to search for negative subpath in </param>
    /// <param name="start">start of the subpath (-1 if false)</param>
    /// <param name="end">end of the subpath (-1 if false)</param>
    /// <returns>True if path contains negative subpath in other direction else returns false</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool TryGetNegativeSubpathInPath(List<NodeID> negativePath, PathID pathID, out int start, out int end)
    {
        var entry = negativePath[0];
        var exit = negativePath[negativePath.Count - 1];

        var path = PlannerPath.GetPathById(pathID);
        start = path.Path.IndexOf(entry);
        end = -1;

        if (start == -1)
        {
            return false;
        }

        end = path.Path.IndexOf(exit);
        if (end == -1)
        {
            return false;
        }

        //path contains both entry and exit point
        if (end < start && (start - end) == negativePath.Count - 1)
        {
            //test each point in between if the path between them is the same
            for (int i = 0; i < negativePath.Count; i++)
            {
                if (negativePath[i].nodeID != (path.Path[start - i].nodeID))
                {
                    return false;
                }
            }
        }

        return true;
    }

    /// <summary>
    /// Gets the set of paths that need to be mutated in order to satisfy the negative flow size
    /// </summary>
    /// <param name="negativePath">part of path where the negative flow occurs</param>
    /// <param name="negativeFlow">flow size </param>
    /// <returns></returns>
    private HashSet<PathID> FindPathsForMutation(List<NodeID> negativePath, float negativeFlow)
    {
        IList<PathID> pathsWithTheNegativeEdge = EdgeContainingPaths(negativePath);
        List<HashSet<PathID>> pathsCombination = GetMinPathsCombination(negativeFlow, pathsWithTheNegativeEdge);

        if (pathsCombination.Count == 0)
        {
            if (pathsWithTheNegativeEdge.Count > 0)
            {
                Debug.Assert(false, $"a combination for path mutation should have been found: {pathsWithTheNegativeEdge.Count}");
                //add all inputs as a full set
                pathsCombination.Add(new HashSet<PathID>(pathsWithTheNegativeEdge));
            }
            else
            {
                Debug.Assert(false, $"A path with negative flow was supposed to exist");
                return new HashSet<PathID>();
            }
        }

        int minIndex = 0;
        float minFlow = float.MaxValue;

        //select one of them - Heuristics - the one with smallest diff 
        for (int i = 0; i < pathsCombination.Count; i++)
        {
            var totalFlow = GetTotalFlow(pathsCombination[i]);
            if (minFlow > totalFlow)
            {
                minFlow = totalFlow;
                minIndex = i;
            }
        }

        return pathsCombination[minIndex];
    }

    /// <summary>
    /// Gets the sets of combinations of paths of a flow size that is greater than minimal flow. And that none predecessor in partial ordering does not satisfy that condition.
    /// </summary>
    /// <param name="negativeFlow">Size of the flow on negative sub path.</param>
    /// <param name="pathsWithTheEdge">List of paths that contains the negative sub path.</param>
    /// <returns>Set of sets paths that are greater or equal to the size of the flow</returns>
    private List<HashSet<PathID>> GetMinPathsCombination(float negativeFlow, IList<PathID> pathsWithTheEdge)
    {
        //test if full set is the minimal possible combination

        float maxFlow = GetTotalFlow(pathsWithTheEdge);
        float minFlow = GetMinFlow(pathsWithTheEdge);

        //Debug.Log($"Total Flow: {maxFlow}, Min Flow {minFlow}, Negative Flow {negativeFlow}");

        //has to use all of the paths
        if (negativeFlow > maxFlow - minFlow)
        {
            var set = FillHashSet(pathsWithTheEdge.ToArray());
            return new List<HashSet<PathID>>() { set };
        }

        //some paths don't need to be selected
        var foundSets = new List<HashSet<PathID>>();
        var notFoundElements = new HashSet<PathID>();
        foreach (var path in pathsWithTheEdge)
        {
            notFoundElements.Add(path);
        }

        //Hass diagram - like generator -> possible set of paths to mutate
        //start with non empty sets -- finishes with one to full set (min one path does not need to be selected)
        for (int i = 1; i <= pathsWithTheEdge.Count - 1; i++)
        {
            //the algorithm here goes from lower levels of Hass upwards,
            //meaning it is testing more and more combinations,
            //(more means more elements in the set of the paths)
            //it is not needed to reach the full set

            //here we are trying to reach minimal subset of paths that contains the edge
            //that is greater equal to the negative flow

            //it is possible to put cutoff here - there are two conditions:
            // - a strict condition - continue only if there is still unused path

            // the other is looser condition - in no way stop when the sum of unused paths flows is greater equal to the negativeFlow
            // another is: if the sum of all unused paths + *any* proper subset of the already found combinations is >= negativeFlow

            foreach (var combination in GetCombinations(pathsWithTheEdge, i))
            {
                //if subset is already in solution don't explore worst (more paths to alter) solution
                if (ContainsSubset(foundSets, combination))
                {
                    continue;
                }

                //find flow, test if it is greater
                float flowSum = GetTotalFlow(combination);
                if (flowSum >= negativeFlow)
                {
                    foundSets.Add(combination);

                    foreach (var pathID in combination)
                    {
                        notFoundElements.Remove(pathID);
                    }

                    //cutoff
                    if (notFoundElements.Count == 0)
                    {
                        return foundSets;
                    }
                }

            }
        }
        //Debug.Log(foundSets.Count);
        return foundSets;
    }

    /// <summary>
    /// Checks if set of sets contains subset of another set.
    /// </summary>
    /// <param name="set">the set</param>
    /// <param name="combination">another set</param>
    /// <returns>True if subset of combination is already present </returns>
    private static bool ContainsSubset(List<HashSet<PathID>> set, HashSet<PathID> combination)
    {
        foreach (var foundSet in set)
        {
            if (foundSet.IsSubsetOf(combination))
            {
                return true;
            }
        }

        return false;
    }

    private static float GetTotalFlow(IEnumerable<PathID> collection)
    {
        float flowSum = 0;
        foreach (var pathID in collection)
        {
            flowSum += PlannerPath.GetPathById(pathID).Flow;
        }

        return flowSum;
    }

    private float GetMinFlow(IEnumerable<PathID> paths)
    {
        float flow = float.MaxValue;
        foreach (var pathID in paths)
        {
            flow = Math.Min(PlannerPath.GetPathById(pathID).Flow, flow);
        }

        return flow;
    }


    /// <summary>
    /// Add a path into structure
    /// </summary>
    /// <param name="id">Id to add</param>
    public void Add(PathID id)
    {
        _atOnce.Add(id);
    }

    struct VirtualPath : IPath
    {
        /// <summary>
        /// Represents time step in which was the initial arrival as well as the length of the path (cost of the path)
        /// </summary>
        public float Time { get; set; }
        public float Flow { get; set; }

        /// <summary>
        /// In time how many units already finished journey
        /// </summary>
        public readonly float InitialArrivals;


        public VirtualPath(PathID pathID)
        {
            var p = PlannerPath.GetPathById(pathID);
            Time = p.Time;
            Flow = p.Flow;
            InitialArrivals = 0;
        }

        public VirtualPath(PathID pathID, VirtualPath vp)
        {
            var p = PlannerPath.GetPathById(pathID);

            var newlyFinished = vp.TransitOfPath(p.Time);
            var initFlow = vp.InitialArrivals + newlyFinished;
            var totalFlow = vp.Flow + p.Flow;

            Time = p.Time;
            Flow = totalFlow;
            InitialArrivals = initFlow;
        }

        private float TransitOfPath(float time)
        {
            var finishedTime = time - Time;
            if (finishedTime <= 0)
            {
                return 0;
            }
            var result = finishedTime * Flow;
            return result;
        }


        /// <summary>
        /// Time needed to transport <code>flowVolume</code> through the virtual path
        /// </summary>
        /// <param name="flowVolume">number of units to send</param>
        /// <returns>Total time to transport all units</returns>
        internal float TimeToTransport(float flowVolume)
        {
            if (flowVolume < InitialArrivals)
            {
                return Time;
            }

            var remainingVolume = flowVolume - InitialArrivals;
            var result = remainingVolume / Flow + Time;

            return result;
        }
    }

    internal float GetTotalTime(float numberOfUnits)
    {
        if (_atOnce == null || _atOnce.Count == 0)
        {
            return float.MaxValue;
        }

        _atOnce.Sort(ComparePathsByCost);

        var virtualPath = new VirtualPath(_atOnce[0]);
        for (int i = 1; i < _atOnce.Count; i++)
        {
            var nextPath = new VirtualPath(_atOnce[i], virtualPath);
            if (nextPath.Time > virtualPath.TimeToTransport(numberOfUnits))
            {
                //done
                break;
            }
            else
            {
                virtualPath = nextPath;
            }
        }

        var timeToTransport = virtualPath.TimeToTransport(numberOfUnits);
        return timeToTransport;
    }



    private static int TransitOfPath(PathID pathID, float time)
    {
        var path = PlannerPath.GetPathById(pathID);

        var finished = time - path.Time;
        var timeFinished = finished;
        if (timeFinished <= 0)
        {
            return 0;
        }

        var result = timeFinished * Mathf.Abs(path.Flow);
        return (int)Math.Floor(result);
    }


    /// <summary>
    /// Computes the number of unit that are able to go trough all of the paths in the given number of steps
    /// </summary>
    /// <param name="time">time from the start</param>
    /// <param name="assignment">How many units used which path</param>
    /// <returns>Total number of units in all paths</returns>
    internal int TransitOfPaths(float time, out List<int> assignment)
    {
        var sum = 0;
        assignment = new List<int>();
        foreach (var pathID in _atOnce)
        {
            var transitOfPath = TransitOfPath(pathID, time);
            sum += transitOfPath;
            assignment.Add(transitOfPath);
        }
        return sum;
    }

}