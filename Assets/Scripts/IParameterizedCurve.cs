﻿using UnityEngine;
using System.Collections;

namespace Telescopes
{
    public interface IParameterizedCurve
    {
        Vector3 StartPosition { get; }
        Vector3 EndPosition { get; }

        Vector3 StartTangent { get; }
        Vector3 EndTangent { get; }

        void RotateAndOffset(Quaternion rotation, Vector3 center, Vector3 tangent, float radius);

    } 
}
