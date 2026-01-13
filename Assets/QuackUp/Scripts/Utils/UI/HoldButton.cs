using System;
using R3;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

namespace QuackUp.Utils
{
    public class HoldButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerClickHandler
    {
        [field: SerializeField] public UnityEvent OnFirstHold { get; private set; } = new();
        [field: SerializeField] public UnityEvent OnHold { get; private set; } = new();
        [field: SerializeField] public UnityEvent OnClick { get; private set; } = new();
        [field: SerializeField] public UnityEvent OnRelease { get; private set; } = new();
        [ReadOnly]
        [ShowInInspector] private SerializableReactiveProperty<bool> _isHolding = new(false);
        [ReadOnly]
        [ShowInInspector] public float HoldDuration { get; private set; }
        
        private IDisposable _bindings;

        private void OnEnable()
        {
            Bind();
        }

        private void OnDisable()
        {
            _bindings?.Dispose();
            _isHolding.Value = false;
            HoldDuration = 0f;
        }
        
        private void Bind()
        {
            var disposableBuilder = Disposable.CreateBuilder();
            _isHolding
                .IgnoreFirstValueWhenSubscribe()
                .DistinctUntilChanged()
                .Where(x => x)
                .Subscribe(_ => OnFirstHold.Invoke())
                .AddTo(ref disposableBuilder);
            
            _isHolding
                .IgnoreFirstValueWhenSubscribe()
                .DistinctUntilChanged()
                .EveryUpdateWhen(x => x)
                .Subscribe(_ =>
                {
                    HoldDuration += Time.deltaTime;
                    OnHold.Invoke();
                })
                .AddTo(ref disposableBuilder);

            _isHolding
                .IgnoreFirstValueWhenSubscribe()
                .DistinctUntilChanged()
                .Where(x => !x)
                .Subscribe(_ =>
                {
                    OnRelease.Invoke();
                    HoldDuration = 0f;
                })
                .AddTo(ref disposableBuilder);
            _bindings = disposableBuilder.Build();
        }
        
        public void OnPointerDown(PointerEventData eventData)
        {
            _isHolding.Value = true;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            _isHolding.Value = false;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            OnClick.Invoke();
        }
    }
}