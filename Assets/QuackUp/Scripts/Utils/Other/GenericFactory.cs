using System;
using UnityEngine;

namespace QuackUp.Utils
{
    public interface IFactory<out T>
    {
        public T Current { get; }
        public T Create();
    }
    
    public interface IGameObjectFactory<out T> : IFactory<T> where T : class
    {
        public GameObject CurrentGameObject { get; }
        public T Create(out GameObject gameObject);
    }
}