using System;
using Cysharp.Threading.Tasks;
using FitMe.Shared;
using NUnit.Framework;
using QuackUp.Utils;
using R3;
using UnityEngine;


namespace FitMe.Panel.Tests
{
    public sealed class NotificationManagerMissingPrefabTests
    {
        private NotificationManagerConfig _config;
        private NotificationManager _manager;
        private NotificationHub _hub;

        [SetUp]
        public void SetUp()
        {
            _config = ScriptableObject.CreateInstance<NotificationManagerConfig>();
            _hub = new NotificationHub();
            _manager = new NotificationManager(null, _config, null, _hub);
        }

        [TearDown]
        public void TearDown()
        {
            _manager?.Dispose();
            if (_config != null)
            {
                UnityEngine.Object.DestroyImmediate(_config);
            }
        }

        [Test]
        public void MissingPrefabCompletesCompletionPromise()
        {
            Assert.That(_config.NotificationPrefabDictionary.ContainsKey(NotificationType.General), Is.False);
            var completionPromise = new Promise<Unit>();
            _hub.Publish(new NotificationDisplayEvent(
                NotificationType.General,
                new GeneralNotificationData(),
                completionPromise));

            Assert.That(completionPromise.Task.Status, Is.EqualTo(UniTaskStatus.Succeeded));
        }

        [Test]
        public void Promise_CancelAfterDisposal_IsSafe()
        {
            var promise = new Promise<Unit>();
            promise.Dispose();

            Assert.That(promise.TrySetCanceled(), Is.False);
            Assert.DoesNotThrow(promise.Cancel);
            Assert.DoesNotThrow(() => promise.CancelAfter(TimeSpan.Zero));
        }

        private sealed class NotificationHub : IMessageHub
        {
            private Action<NotificationDisplayEvent> _notificationSubscriber;

            public void Publish<TMessage>(TMessage message)
            {
                if (typeof(TMessage) != typeof(NotificationDisplayEvent))
                {
                    throw new NotSupportedException($"Unexpected message type: {typeof(TMessage)}");
                }

                _notificationSubscriber?.Invoke((NotificationDisplayEvent)(object)message);
            }

            public IDisposable Subscribe<TMessage>(Action<TMessage> action)
            {
                if (typeof(TMessage) != typeof(NotificationDisplayEvent))
                {
                    throw new NotSupportedException($"Unexpected message type: {typeof(TMessage)}");
                }

                _notificationSubscriber = notificationEvent => action((TMessage)(object)notificationEvent);
                return new Subscription(() => _notificationSubscriber = null);
            }

            public Observable<TMessage> GetObservable<TMessage>()
            {
                throw new NotSupportedException("The test only exercises Publish/Subscribe.");
            }
        }

        private sealed class Subscription : IDisposable
        {
            private Action _dispose;

            public Subscription(Action dispose)
            {
                _dispose = dispose;
            }

            public void Dispose()
            {
                _dispose?.Invoke();
                _dispose = null;
            }
        }
    }
}
