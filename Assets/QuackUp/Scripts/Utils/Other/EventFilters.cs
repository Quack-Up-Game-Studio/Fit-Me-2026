using System;
using MessagePipe;

namespace QuackUp.Utils
{
    #region Interfaces
    /// <summary>
    /// Publisher and Subscriber has to have the same identifier object.
    /// </summary>
    public interface IObjectIdentifier<out T>
    {
        public T IdentifierObject { get; }
    }

    /// <summary>
    /// Publisher and Subscriber has to have the same Id identifier.
    /// </summary>
    public interface IIdIdentifier
    {
        public int Id { get; }
    }
    
    /// <summary>
    /// Publisher and Subscriber has to have the same Guid identifier.
    /// </summary>
    public interface IGuidIdentifier
    {
        public Guid Id { get; }
    }
    #endregion

    #region Filters

    /// <summary>
    /// Filter that checks if the message has the same object identifier.
    /// </summary>
    /// <typeparam name="TMessage">Message type</typeparam>
    /// <typeparam name="TIdentifier">Identifier type</typeparam>
    public class ObjectIdentifierFilter<TMessage, TIdentifier> : MessageHandlerFilter<TMessage>, IObjectIdentifier<TIdentifier>
        where TMessage : IObjectIdentifier<TIdentifier>
    {
        public TIdentifier IdentifierObject { get; }
    
        public ObjectIdentifierFilter(TIdentifier obj)
        {
            IdentifierObject = obj;
        }

        public override void Handle(TMessage message, Action<TMessage> next)
        {
            if (!message.IdentifierObject.Equals(IdentifierObject)) 
                return;

            // // No need for type checks since TIdentifier is already CharacterHub for ICharacterHubIdentifier
            // if (message is ICharacterHubIdentifier && message.IdentifierObject is CharacterHub hub && !hub)
            //     return;

            next(message);
        }
    }
    
    /// <summary>
    /// Filter that checks if the message has the same Id identifier.
    /// </summary>
    /// <typeparam name="T">Message type</typeparam>
    public class IdIdentifierFilter<T> : MessageHandlerFilter<T>, IIdIdentifier where T : IIdIdentifier
    {
        public int Id { get; }
        
        public IdIdentifierFilter(int id)
        {
            Id = id;
        }
        
        public override void Handle(T message, Action<T> next)
        {
            if (!message.Id.Equals(Id)) return;
            next(message);
        }
    }
    
    /// <summary>
    /// Filter that checks if the message has the same Guid identifier.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class GuidIdentifierFilter<T> : MessageHandlerFilter<T>, IGuidIdentifier where T : IGuidIdentifier
    {
        public Guid Id { get; }
        
        public GuidIdentifierFilter(Guid id)
        {
            Id = id;
        }
        
        public override void Handle(T message, Action<T> next)
        {
            if (!message.Id.Equals(Id)) return;
            next(message);
        }
    }

    /// <summary>
    /// Filter that checks if the message is a duplicate.
    /// </summary>
    /// <typeparam name="T">Message type</typeparam>
    public class DuplicateFilter<T> : MessageHandlerFilter<T>
    {
        private T _lastMessage;
        public override void Handle(T message, Action<T> next)
        {
            if (message.Equals(_lastMessage)) return;
            _lastMessage = message;
            next(message);
        }
    }
    #endregion
}