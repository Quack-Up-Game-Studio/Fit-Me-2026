using System;
using System.Collections.Generic;
using MessagePipe;
using R3;

namespace QuackUp.Utils
{
    public interface IMessageHub
    {
        void Publish<TMessage>(TMessage message);
        IDisposable Subscribe<TMessage>(Action<TMessage> action);
        Observable<TMessage> GetObservable<TMessage>();
    }
    
    public abstract class MessageWrapper
    {
        public abstract Type MessageType { get; }
    }

    public class MessageWrapper<T> : MessageWrapper
    {
        private readonly IPublisher<T> _publisher;
        private readonly ISubscriber<T> _subscriber;
    
        public override Type MessageType => typeof(T);
    
        public MessageWrapper(IPublisher<T> publisher, ISubscriber<T> subscriber)
        {
            _publisher = publisher;
            _subscriber = subscriber;
        }
    
        public void Publish(T message)
        {
            if (_publisher == null)
                throw new InvalidOperationException($"No publisher of {typeof(T)} available in this message hub.");
            _publisher.Publish(message);
        }

        public IDisposable Subscribe(Action<T> handler)
        {
            return _subscriber == null
                ? throw new InvalidOperationException($"No subscriber of {typeof(T)} available in this message hub.")
                : _subscriber.Subscribe(handler);
        }

        public Observable<T> GetObservable()
        {
            return _subscriber == null
                ? throw new InvalidOperationException($"No subscriber of {typeof(T)} available in this message hub.")
                : _subscriber.AsObservable().ToObservable();
        }
    }
    
    public class MessageHub : IMessageHub
    {
        protected readonly Dictionary<Type, MessageWrapper> MessageWrappers = new();
        
        public virtual void Publish<TMessage>(TMessage message)
        {
            var type = typeof(TMessage);
            if (MessageWrappers.TryGetValue(type, out var wrapper))
            {
                var typedWrapper = (MessageWrapper<TMessage>)wrapper;
                typedWrapper.Publish(message);
            }
            else
            {
                throw new InvalidOperationException($"No subscribers for message type {type}");
            }
        }
        
        public virtual IDisposable Subscribe<TMessage>(Action<TMessage> action)
        {
            var type = typeof(TMessage);
            if (!MessageWrappers.TryGetValue(type, out var wrapper))
                throw new InvalidOperationException($"No publishers for message type {type}");
            var typedWrapper = (MessageWrapper<TMessage>)wrapper;
            return typedWrapper.Subscribe(action);
        }
        
        public virtual Observable<TMessage> GetObservable<TMessage>()
        {
            var type = typeof(TMessage);
            if (!MessageWrappers.TryGetValue(type, out var wrapper))
                throw new InvalidOperationException($"No subscribers for message type {type}");
            var typedWrapper = (MessageWrapper<TMessage>)wrapper;
            return typedWrapper.GetObservable();
        }
    }
}   