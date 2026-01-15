#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;
using UnityEngine;

namespace QuackUp.Utils
{
    public interface IViewLocator
    {
        void RegisterView(Guid id, MonoBehaviour view);
        void UnregisterView(Guid id);
        bool TryGetView<TView>(Guid id, [NotNullWhen(true)] out TView? view) where TView : MonoBehaviour;
    }
    
    public class ViewLocator : IViewLocator
    {
        private readonly Dictionary<Guid, MonoBehaviour> _registeredViews = new();

        public void RegisterView(Guid id, MonoBehaviour view)
        {
            _registeredViews.TryAdd(id, view);
        }

        public void UnregisterView(Guid id)
        {
            _registeredViews.Remove(id);
        }

        public bool TryGetView<TView>(Guid id, [NotNullWhen(true)] out TView? view) where TView : MonoBehaviour
        {
            if (_registeredViews.TryGetValue(id, out var registeredView) && registeredView is TView typedView)
            {
                view = typedView;
                return true;
            }

            view = null;
            return false;
        }
    }
}