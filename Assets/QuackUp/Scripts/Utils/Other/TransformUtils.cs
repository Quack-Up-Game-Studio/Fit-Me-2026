using System;
using JetBrains.Annotations;
using R3;
using UnityEngine;

namespace QuackUp.Utils
{
    public class TransformData : IDisposable
    {
        public ReactiveProperty<Vector3> Position { get; private set; } = new(Vector3.zero);
        public ReactiveProperty<Vector3> LocalPosition { get; private set;} = new(Vector3.zero);
        public ReactiveProperty<Quaternion> Rotation { get; private set; } = new(Quaternion.identity);
        public ReactiveProperty<Quaternion> LocalRotation { get; private set; } = new(Quaternion.identity);
        public ReactiveProperty<Vector3> LocalScale { get; private set; } = new(Vector3.one);

        public Observable<TransformData> OnChanged { get; private set; }

        public TransformData()
        {
            OnChanged = Observable.Merge(
                Position.Select(_ => this),
                LocalPosition.Select(_ => this),
                Rotation.Select(_ => this),
                LocalRotation.Select(_ => this),
                LocalScale.Select(_ => this)
            );
        }
        
        public TransformData(Transform transform) : this()
        {
            Position.Value = transform.position;
            LocalPosition.Value  = transform.localPosition;
            Rotation.Value  = transform.rotation;
            LocalRotation.Value  = transform.localRotation;
            LocalScale.Value  = transform.localScale;
        }

        public void Dispose()
        {
            Position?.Dispose();
            LocalPosition?.Dispose();
            Rotation?.Dispose();
            LocalRotation?.Dispose();
            LocalScale?.Dispose();
        }
        
        public void SetParent([CanBeNull] Transform parent, bool worldPositionStays = true)
        {
           
        }
    }
}