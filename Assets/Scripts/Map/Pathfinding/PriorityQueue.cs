using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ItemWithPriority<P, T> where P : IComparable<P>
{
    public P priority;
    public T item;

    public ItemWithPriority(P priority, T item)
    {
        this.item = item;
        this.priority = priority;
    }
}


public class PriorityQueue<P, T> where P : IComparable<P>
{
    private List<ItemWithPriority<P, T>> data;

    public PriorityQueue()
    {
        this.data = new List<ItemWithPriority<P, T>>();
    }

    public void Enqueue(P priority, T item)
    {
        data.Add(new ItemWithPriority<P, T>(priority, item));
        int childIndex = data.Count - 1; // child index; start at end
        while (childIndex > 0)
        {
            int parentIndex = (childIndex - 1) / 2; // parent index
            if (data[childIndex].priority.CompareTo(data[parentIndex].priority) >= 0)
            {
                break;
            }
            ItemWithPriority<P, T> tmp = data[childIndex];
            data[childIndex] = data[parentIndex];
            data[parentIndex] = tmp;
            childIndex = parentIndex;
        }
    }

    public void Enqueue(ItemWithPriority<P, T> priorityItem)
    {
        data.Add(priorityItem);
        int childIndex = data.Count - 1; // child index; start at end
        while (childIndex > 0)
        {
            int parentIndex = (childIndex - 1) / 2; // parent index
            if (data[childIndex].priority.CompareTo(data[parentIndex].priority) >= 0)
            {
                break;
            }
            ItemWithPriority<P, T> tmp = data[childIndex];
            data[childIndex] = data[parentIndex];
            data[parentIndex] = tmp;
            childIndex = parentIndex;
        }
    }

    public ItemWithPriority<P, T> Dequeue()
    {
        // assumes pq is not empty; up to calling code
        int lastIndex = data.Count - 1; // last index (before removal)
        ItemWithPriority<P, T> frontItem = data[0];   // fetch the front
        data[0] = data[lastIndex];
        data.RemoveAt(lastIndex);

        --lastIndex; // last index (after removal)
        int parentIndex = 0; // parent index. start at front of pq
        while (true)
        {
            int childIndex = parentIndex * 2 + 1; // left child index of parent
            if (childIndex > lastIndex) break;  // no children so done
            int rightChild = childIndex + 1;     // right child
            if (rightChild <= lastIndex && data[rightChild].priority.CompareTo(data[childIndex].priority) < 0)
            {
                childIndex = rightChild;
            }
            if (data[parentIndex].priority.CompareTo(data[childIndex].priority) <= 0)
            {
                break;
            }
            ItemWithPriority<P, T> tmp = data[parentIndex];
            data[parentIndex] = data[childIndex];
            data[childIndex] = tmp; // swap parent and child
            parentIndex = childIndex;
        }
        return frontItem;
    }

    public ItemWithPriority<P, T> Peek()
    {
        return data[0];
    }

    public int Count()
    {
        return data.Count;
    }

    public void Clear()
    {
        data.Clear();
    }

    public override string ToString()
    {
        string s = "";
        for (int i = 0; i < data.Count; ++i)
            s += data[i].ToString() + " ";
        s += "count = " + data.Count;
        return s;
    }

    public bool IsConsistent()
    {
        // is the heap property true for all data?
        if (data.Count == 0) return true;
        int lastIndex = data.Count - 1; // last index
        for (int parentIndex = 0; parentIndex < data.Count; ++parentIndex) // each parent index
        {
            int leftChildIndex = 2 * parentIndex + 1; // left child index
            int rightChildIndex = 2 * parentIndex + 2; // right child index

            if (leftChildIndex <= lastIndex && data[parentIndex].priority.CompareTo(data[leftChildIndex].priority) > 0) return false; // if lc exists and it's greater than parent then bad.
            if (rightChildIndex <= lastIndex && data[parentIndex].priority.CompareTo(data[rightChildIndex].priority) > 0) return false; // check the right child too.
        }
        return true; // passed all checks
    }
}