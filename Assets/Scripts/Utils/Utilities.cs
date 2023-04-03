using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class Utilities
{
    public static void AddToHashsetDictionary<TKey, TValue>(IDictionary<TKey, HashSet<TValue>> dictionary, TKey key, TValue value)
    {
        if (dictionary.TryGetValue(key, out var hashSet))
        {
            hashSet.Add(value);
        }
        else
        {
            dictionary[key] = new HashSet<TValue>() { value };
        }
    }
}
