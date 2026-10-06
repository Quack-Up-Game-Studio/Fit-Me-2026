using System;
using Debug = QuackUp.Utils.DebugUtils;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using PrimeTween;
using QuackUp.Utils;
using Sirenix.OdinInspector;
using Sirenix.Serialization;
using UnityEngine;

namespace FitMe.Panel
{
    public interface ITransitionObjectProvider
    {
        bool TryGetTransitionObject<T>(string key, out T component) where T : Component;
    }
    
    public interface IUITransition
    {
        void Initialize(ITransitionObjectProvider provider);
        Sequence? Transition(CancellationToken cancellationToken = default, CancelBehavior cancelBehavior = CancelBehavior.Stop);
    }
    
    [Serializable]
    public struct CrossfadeSettings
    {
        public CrossfadeType crossFadeType;
        [ShowIf(nameof(crossFadeType), CrossfadeType.InOnly)] public bool hidePreviousPanel;
        public float customOffset;
    }
    
     public enum TransitionGroupType
    {
        Group,
        Chain
    }

    public enum CancelBehavior
    {
        Stop,
        Complete
    }
    
    public interface ITransitionGroup
    {
        UniTask Transition(CancellationToken cancellationToken = default);
    }
    
    public record CustomTransitionGroup : ITransitionGroup
    {
        private readonly Sequence _transitionSequence;
        private readonly CancelBehavior _cancelBehavior;

        public CustomTransitionGroup(Sequence transitionSequence, CancelBehavior cancelBehavior = CancelBehavior.Stop)
        {
            _transitionSequence = transitionSequence;
            _cancelBehavior = cancelBehavior;
        }

        public async UniTask Transition(CancellationToken cancellationToken = default)
        {
            using var registration = cancellationToken.Register(() =>
            {
                switch (_cancelBehavior)
                {
                    case CancelBehavior.Stop:
                        _transitionSequence.Stop();
                        break;
                    case CancelBehavior.Complete:
                        _transitionSequence.Complete();
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            });
            await _transitionSequence.ToUniTask();
        }
    }
    
    [ShowOdinSerializedPropertiesInInspector]
    [Serializable]
    public record BasicTransitionGroup : ITransitionGroup
    {
        [SerializeField] private bool overrideDefaultCycles;
        [SerializeField, ShowIf(nameof(overrideDefaultCycles))] private int cycles = 1;
        [SerializeField, ShowIf("@this.cycles != 1 && this.cycles != 0")] private Sequence.SequenceCycleMode cycleMode;
        [SerializeField] private CancelBehavior cancelBehavior = CancelBehavior.Stop;
        [NonSerialized, OdinSerialize, HideReferenceObjectPicker]
        [TableList(DrawScrollView = false)]
        public List<TransitionData> transition = new();
        
        private bool _isInitialized;
        
        public void Initialize(ITransitionObjectProvider provider)
        {
            _isInitialized = true;
            if (transition == null || transition.Count == 0) return;

            foreach (var data in transition)
            {
                data.transition?.Initialize(provider);
            }
        }

        public async UniTask Transition(CancellationToken cancellationToken = default)
        {
            if (!_isInitialized)
            {
                Debug.LogWarning("TransitionGroup: Transition called before Initialize. Make sure to call Initialize first.");
                return;
            }
            if (transition == null || transition.Count == 0)
            {
                return;
            }
            var sequence = !overrideDefaultCycles ? Sequence.Create() : Sequence.Create(cycles, cycleMode);
            foreach (var data in transition)
            {
                var childSequence = data.transition?.Transition();
                if (childSequence == null) continue;
                switch (data.transitionGroupType)
                {
                    case TransitionGroupType.Group:
                        _ = sequence.Group(childSequence.Value);
                        break;
                    case TransitionGroupType.Chain:
                        _ = sequence.Chain(childSequence.Value);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
                if (data.delay > 0) _ = sequence.ChainDelay(data.delay);
            }
            using var registration = cancellationToken.Register(() => CancelTransition(sequence));
            await sequence.ToUniTask();
        }

        private void CancelTransition(Sequence transitionSequence)
        {
            switch (cancelBehavior)
            {
                case CancelBehavior.Stop:
                    transitionSequence.Stop();
                    break;
                case CancelBehavior.Complete:
                    transitionSequence.Complete();
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }
    
    [Serializable]
    [ShowOdinSerializedPropertiesInInspector]
    public record TransitionData
    {
        public IUITransition transition;
        public TransitionGroupType transitionGroupType = TransitionGroupType.Group;
        public float delay = 0f;
    }
}