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

    public static float Modulo2Pi(float angle)
    {
        //optimized to answer quickly for values that won't change
        var pi = Mathf.PI * 2f;
        var a = angle;
        return 0 <= a && a < pi ?
            a :
            ((a %= pi) < 0) ?
                a + pi :
                a;
    }


}
