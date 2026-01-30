using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Sirenix.OdinInspector;
using Spine;
using Spine.Unity;
using UnityEngine;
using Event = Spine.Event;

namespace QuackUp.Utils
{

    [Serializable]
    public struct DeconstructedAnimationWrapper<TKey>
    {
        public DeconstructedAnimationWrapper(SkeletonDataAsset refAsset, TKey key, string animation)
        {
            this.refAsset = refAsset;
            this.key = key;
            this.animation = animation;
        }
        [HideInInspector, SerializeField] private SkeletonDataAsset refAsset;
        [ReadOnly, SerializeField] public TKey key;
        [SpineAnimation(dataField: nameof(refAsset)), 
         SerializeField] public string animation;
    }
    
    public static class SpineUtils
    {
        public static async UniTask WaitUntilComplete(this TrackEntry trackEntry, bool interruptAsComplete = false, 
            CancellationToken cancellationToken = default)
        {
            if (trackEntry is null) return;
            if (trackEntry.IsComplete) return;
            if (trackEntry.Loop)
            {
                DebugUtils.LogWarning("Track entry is looped, cannot wait until complete");
                return;
            }
            var tcs = new UniTaskCompletionSource();
            cancellationToken.Register(() => tcs.TrySetCanceled());
            trackEntry.Complete += TrackEntryOnComplete;
            trackEntry.Interrupt += TrackEntryOnInterrupt;
            trackEntry.Dispose += TrackEntryOnDispose;
            await tcs.Task;
            return;
            
            void TrackEntryOnComplete(TrackEntry trackEntry1)
            {
                trackEntry1.Complete -= TrackEntryOnComplete;
                tcs.TrySetResult();
            }
            
            void TrackEntryOnInterrupt(TrackEntry trackEntry1)
            {
                trackEntry1.Interrupt -= TrackEntryOnInterrupt;
                if (interruptAsComplete)
                {
                    TrackEntryOnComplete(trackEntry1);
                }
                else
                {
                    if (tcs.GetStatus(0) != UniTaskStatus.Pending) return;
                    DebugUtils.LogWarning("Track entry interrupted before complete");
                    tcs.TrySetCanceled();
                }
            }
            
            void TrackEntryOnDispose(TrackEntry trackEntry1)
            {
                trackEntry1.Dispose -= TrackEntryOnDispose;
                if (tcs.GetStatus(0) != UniTaskStatus.Pending) return;
                DebugUtils.LogWarning("Track entry disposed before complete");
                tcs.TrySetCanceled();
            }
        }

        public static async UniTask WaitUntilEvent(this TrackEntry trackEntry,
            string eventName, CancellationToken cancellationToken = default)
        {
            if (trackEntry is null) return;
            var tcs = new UniTaskCompletionSource();
            cancellationToken.Register(() => tcs.TrySetCanceled());
            trackEntry.Event += TrackEntryOnEvent;
            trackEntry.Complete += TrackEntryOnComplete;
            trackEntry.Interrupt += TrackEntryOnInterrupt;
            trackEntry.Dispose += TrackEntryOnDispose;
            await tcs.Task;
            return;
            
            void TrackEntryOnEvent(TrackEntry trackEntry1, Event e)
            {
                trackEntry1.Event -= TrackEntryOnEvent;
                if (e.Data.Name == eventName)
                {
                    tcs.TrySetResult();
                }
            }
            
            void TrackEntryOnComplete(TrackEntry trackEntry1)
            {
                trackEntry1.Complete -= TrackEntryOnComplete;
                if (tcs.GetStatus(0) != UniTaskStatus.Pending) return;
                DebugUtils.LogWarning($"Track entry completed before event {eventName}, make sure that the event exists");
                tcs.TrySetCanceled();
            }
            
            void TrackEntryOnInterrupt(TrackEntry trackEntry1)
            {
                trackEntry1.Interrupt -= TrackEntryOnInterrupt;
                if (tcs.GetStatus(0) != UniTaskStatus.Pending) return;
                DebugUtils.LogWarning($"Track entry interrupted before event {eventName}");
                tcs.TrySetCanceled();
            }
            
            void TrackEntryOnDispose(TrackEntry trackEntry1)
            {
                trackEntry1.Dispose -= TrackEntryOnDispose;
                if (tcs.GetStatus(0) != UniTaskStatus.Pending) return;
                DebugUtils.LogWarning($"Track entry disposed before event {eventName}, make sure that the event exists");
                tcs.TrySetCanceled();
            }
        }
    }
}