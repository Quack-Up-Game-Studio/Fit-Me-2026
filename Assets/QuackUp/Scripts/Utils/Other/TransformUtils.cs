using System;
using JetBrains.Annotations;
using R3;
using UnityEngine;

namespace QuackUp.Utils
{
    public interface ITransformProvider
    {
        Transform Transform { get; }
    }
}