using System.Collections;
using System.Collections.Generic;
using UnityEngine;

internal interface IPath
{
    float Cost { get; }
    float Flow { get; }
}
