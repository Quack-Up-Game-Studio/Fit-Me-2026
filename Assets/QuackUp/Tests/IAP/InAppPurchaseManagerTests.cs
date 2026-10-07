using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using Cysharp.Threading.Tasks;
using FitMe.GameData;
using MessagePack;
using NUnit.Framework;
using QuackUp.IAP;
using R3;
using UnityEngine.Purchasing;
using UnityEngine;
using UnityEngine.TestTools;
using System.Threading.Tasks;

namespace QuackUp.IAP.Tests
{
    public sealed class InAppPurchaseManagerTests
    {
        [Test]
        public void PurchaseEnergyGrantIsDurableAndIdempotentAcrossSaveReload()
        {
            var applyGrant = typeof(EnergyManagerSaveData).GetMethod("TryApplyPurchaseGrant");
            Assert.That(applyGrant, Is.Not.Null, "Energy save data must expose an idempotent transaction grant.");

            var saveData = new EnergyManagerSaveData { CurrentEnergy = 10 };
            Assert.That(applyGrant.Invoke(saveData, new object[] { "tx-durable", 5 }), Is.EqualTo(true));

            var restored = MessagePackSerializer.Deserialize<EnergyManagerSaveData>(
                MessagePackSerializer.Serialize(saveData));

            Assert.That(applyGrant.Invoke(restored, new object[] { "tx-durable", 5 }), Is.EqualTo(false));
            Assert.That(restored.CurrentEnergy, Is.EqualTo(15));
        }

        private static readonly MethodInfo OnPurchaseConfirmedMethod =
            typeof(InAppPurchaseManager).GetMethod("OnPurchaseConfirmed", BindingFlags.Instance | BindingFlags.NonPublic);

        private SpyPurchaseEffects _effects;
        private SpyPurchasePersistence _persistence;
        private InAppPurchaseManager _manager;
        private IDisposable _successSubscription;
        private int _successCount;
        private int _coordinatorAttemptCount;
        private int _coordinatorActiveAttempts;
        private int _coordinatorMaximumConcurrentAttempts;

        [SetUp]
        public void SetUp()
        {
            _effects = new SpyPurchaseEffects();
            _persistence = new SpyPurchasePersistence();
            _manager = new InAppPurchaseManager(_effects, _persistence);
            _successCount = 0;
            _coordinatorAttemptCount = 0;
            _coordinatorActiveAttempts = 0;
            _coordinatorMaximumConcurrentAttempts = 0;
            _successSubscription = _manager.OnPurchaseSuccess.Subscribe(_ => _successCount++);
        }

        [TearDown]
        public void TearDown()
        {
            _successSubscription?.Dispose();
            _manager?.Dispose();
        }

        [Test]
        public void FailedConfirmationDoesNotGrantOrRecordPurchase()
        {
            var failedOrder = new FailedOrder(
                new EmptyCart(),
                PurchaseFailureReason.PurchasingUnavailable,
                "store unavailable");

            InvokeConfirmationCallback(failedOrder);

            Assert.That(_effects.ProductEffectCount, Is.Zero);
            Assert.That(_effects.SubscriptionStateChangeCount, Is.Zero);
            Assert.That(_successCount, Is.Zero);
            Assert.That(_persistence.RecordedPurchaseCount, Is.Zero);
            Assert.That(_persistence.PlayerTierUpdateCount, Is.Zero);
        }

        [Test]
        public void DuplicateConfirmedTransactionIsProcessedOnlyOnce()
        {
            var transactionId = "tx-42";
            var confirmedOrder = new ConfirmedOrder(new EmptyCart(), new TestOrderInfo(transactionId));
            var duplicateOrder = new ConfirmedOrder(new EmptyCart(), new TestOrderInfo(transactionId));

            InvokeConfirmationCallback(confirmedOrder);
            InvokeConfirmationCallback(duplicateOrder);

            Assert.That(_effects.ProductEffectCount, Is.EqualTo(1));
            Assert.That(_successCount, Is.EqualTo(1));
            Assert.That(_persistence.RecordedPurchaseCount, Is.EqualTo(1));
        }

        [Test]
        public void FailedPurchasePersistenceCanRetryWithoutReapplyingProductEffects()
        {
            _persistence.FailNextRecordPurchase = true;
            var order = new ConfirmedOrder(new EmptyCart(), new TestOrderInfo("tx-persistence-retry"));
            LogAssert.Expect(LogType.Error,
                new Regex("IAP: Failed to process confirmed transaction tx-persistence-retry:"));

            InvokeConfirmationCallback(order);
            Assert.That(_persistence.RecordPurchaseAttemptCount, Is.EqualTo(1));
            Assert.That(_effects.ProductEffectCount, Is.EqualTo(1));

            InvokeConfirmationCallback(order);

            Assert.That(_persistence.RecordPurchaseAttemptCount, Is.EqualTo(2));
            Assert.That(_persistence.RecordedPurchaseCount, Is.EqualTo(1));
            Assert.That(_effects.ProductEffectCount, Is.EqualTo(1));
            Assert.That(_successCount, Is.EqualTo(1));
        }

        [Test]
        public void RetryAfterPersistenceSideEffectDoesNotDuplicatePurchaseRecord()
        {
            _persistence.ThrowAfterRecordingNextPurchase = true;
            var order = new ConfirmedOrder(new EmptyCart(), new TestOrderInfo("tx-record-partial"));
            LogAssert.Expect(LogType.Error,
                new Regex("IAP: Failed to process confirmed transaction tx-record-partial:"));

            InvokeConfirmationCallback(order);
            InvokeConfirmationCallback(order);

            Assert.That(_persistence.RecordedPurchaseCount, Is.EqualTo(1));
            Assert.That(_effects.ProductEffectCount, Is.EqualTo(1));
            Assert.That(_successCount, Is.EqualTo(1));
        }

        [Test]
        public void PartialProductEffectCanRetrySafelyAndCompleteThePurchase()
        {
            _effects.ThrowAfterApplyingNextProductEffect = true;
            var order = new ConfirmedOrder(new EmptyCart(), new TestOrderInfo("tx-effect-indeterminate"));
            LogAssert.Expect(LogType.Error,
                new Regex("IAP: Failed to process confirmed transaction tx-effect-indeterminate:"));
            InvokeConfirmationCallback(order);
            InvokeConfirmationCallback(order);

            Assert.That(_effects.ProductEffectCount, Is.EqualTo(1));
            Assert.That(_persistence.RecordedPurchaseCount, Is.EqualTo(1));
            Assert.That(_successCount, Is.EqualTo(1));
        }

        [Test]
        public void SubscriptionPurchaseUpdatesTierAsActive()
        {
            var definition = new ProductDefinition(ProductIds.MonthlyPass, ProductType.Subscription);
            var constructor = typeof(Product).GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic,
                null, new[] { typeof(ProductDefinition), typeof(ProductMetadata) }, null);
            Assert.That(constructor, Is.Not.Null);
            var product = (Product)constructor.Invoke(new object[] { definition, new ProductMetadata() });
            var order = new ConfirmedOrder(
                new Cart(new CartItem(product)), new TestOrderInfo("tx-subscription-tier"));

            InvokeConfirmationCallback(order);

            Assert.That(_persistence.PlayerTierValues.Count, Is.GreaterThan(0));
            Assert.That(_persistence.PlayerTierValues.Contains(false), Is.False);
            Assert.That(_persistence.PlayerTierValues[^1], Is.True);
            Assert.That(_persistence.PlayerTierUpdateCount, Is.EqualTo(1));
        }

        [Test]
        public void SubscriptionEffectRetryDoesNotDuplicateConfirmedSubscriptionInfo()
        {
            _effects.ThrowAfterApplyingNextSubscriptionState = true;
            var definition = new ProductDefinition(ProductIds.MonthlyPass, ProductType.Subscription);
            var constructor = typeof(Product).GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic,
                null, new[] { typeof(ProductDefinition), typeof(ProductMetadata) }, null);
            Assert.That(constructor, Is.Not.Null);
            var product = (Product)constructor.Invoke(new object[] { definition, new ProductMetadata() });
            var order = new ConfirmedOrder(new Cart(new CartItem(product)), new TestOrderInfo("tx-subscription-retry"));
            LogAssert.Expect(LogType.Error,
                new Regex("IAP: Failed to process confirmed transaction tx-subscription-retry:"));

            InvokeConfirmationCallback(order);
            InvokeConfirmationCallback(order);

            var subscriptionsField = typeof(InAppPurchaseManager).GetField("_confirmedSubscriptions",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(subscriptionsField, Is.Not.Null);
            var subscriptions = (List<SubscriptionInfo>)subscriptionsField.GetValue(_manager);
            Assert.That(subscriptions, Has.Count.EqualTo(1));
            Assert.That(_effects.SubscriptionStateChangeCount, Is.EqualTo(1));
            Assert.That(_persistence.PlayerTierUpdateCount, Is.EqualTo(1));
            Assert.That(_successCount, Is.EqualTo(1));
        }

        [Test]
        public void SubscriptionInfoIsDeduplicatedAcrossFetchAndConfirmationAndRejectsMissingIds()
        {
            var addSubscription = typeof(InAppPurchaseManager).GetMethod("AddConfirmedSubscription",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(addSubscription, Is.Not.Null);

            addSubscription.Invoke(_manager, new object[] { "tx-fetch", null });
            addSubscription.Invoke(_manager, new object[] { "tx-fetch", null });
            addSubscription.Invoke(_manager, new object[] { string.Empty, null });

            var subscriptionsField = typeof(InAppPurchaseManager).GetField("_confirmedSubscriptions",
                BindingFlags.Instance | BindingFlags.NonPublic);
            var subscriptions = (List<SubscriptionInfo>)subscriptionsField.GetValue(_manager);
            Assert.That(subscriptions, Has.Count.EqualTo(1));
        }

        [Test]
        public void FailedSubscriptionRefreshDoesNotRevokeExistingState()
        {
            var applyRefreshResult = typeof(InAppPurchaseManager).GetMethod("ApplySubscriptionRefreshResult",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(applyRefreshResult, Is.Not.Null,
                "Subscription state changes must be conditional on a successful purchase refresh.");

            applyRefreshResult.Invoke(_manager, new object[] { false });

            Assert.That(_effects.SubscriptionStateChangeCount, Is.Zero);
            Assert.That(_persistence.PlayerTierUpdateCount, Is.Zero);
        }

        [Test]
        public async Task ConfirmedPurchaseWaitsForSaveDataReadinessBeforeApplyingEffects()
        {
            _persistence.DelaySaveDataReadiness();
            var confirmedOrder = new ConfirmedOrder(new EmptyCart(), new TestOrderInfo("tx-before-ready"));

            InvokeConfirmationCallback(confirmedOrder);

            Assert.That(_effects.ProductEffectCount, Is.Zero);
            Assert.That(_successCount, Is.Zero);

            _persistence.CompleteSaveDataReadiness();
            await UniTask.WaitUntil(() => _successCount == 1);

            Assert.That(_effects.ProductEffectCount, Is.EqualTo(1));
            Assert.That(_persistence.RecordedPurchaseCount, Is.EqualTo(1));
        }

        [Test]
        public void NonRetryableStoreDisconnectionInvalidatesAllReadinessFlags()
        {
            SetReadiness(nameof(InAppPurchaseManager.IsConnected), true);
            SetReadiness(nameof(InAppPurchaseManager.IsProductReady), true);
            SetReadiness(nameof(InAppPurchaseManager.IsPurchaseReady), true);
            LogAssert.Expect(LogType.Error, "IAP: Store connection failed: disconnected");

            InvokeStoreDisconnected(new StoreConnectionFailureDescription("disconnected"),
                new UniTaskCompletionSource<bool>());

            Assert.That(_manager.IsConnected, Is.False);
            Assert.That(_manager.IsProductReady, Is.False);
            Assert.That(_manager.IsPurchaseReady, Is.False);
            Assert.That(_manager.IsIAPReady, Is.False);
        }

        [Test]
        public async Task DuplicateConfirmationWhileSaveIsUnreadyIsAppliedOnce()
        {
            _persistence.DelaySaveDataReadiness();
            const string transactionId = "tx-delayed-duplicate";
            var firstOrder = new ConfirmedOrder(new EmptyCart(), new TestOrderInfo(transactionId));
            var duplicateOrder = new ConfirmedOrder(new EmptyCart(), new TestOrderInfo(transactionId));

            InvokeConfirmationCallback(firstOrder);
            InvokeConfirmationCallback(duplicateOrder);

            Assert.That(_effects.ProductEffectCount, Is.Zero);
            _persistence.CompleteSaveDataReadiness();
            await UniTask.WaitUntil(() => _successCount == 1);

            Assert.That(_effects.ProductEffectCount, Is.EqualTo(1));
            Assert.That(_persistence.RecordedPurchaseCount, Is.EqualTo(1));
        }

        [Test]
        public void ConfirmedOrderWithoutTransactionIdDoesNotApplyEffects()
        {
            LogAssert.Expect(LogType.Error,
                "IAP: Confirmed order has no transaction ID; purchase effects were not applied.");
            var order = new ConfirmedOrder(new EmptyCart(), new TestOrderInfo(null));

            InvokeConfirmationCallback(order);

            Assert.That(_effects.ProductEffectCount, Is.Zero);
            Assert.That(_successCount, Is.Zero);
            Assert.That(_persistence.RecordedPurchaseCount, Is.Zero);
        }

        [Test]
        public async Task RetryDuringDelayedInitializationRunsSequentially()
        {
            var coordinator = new StoreInitializationCoordinator(RunCoordinatorAttempt);
            try
            {
                var worker = coordinator.Run();
                await UniTask.WaitUntil(() => _coordinatorActiveAttempts == 1);
                coordinator.Run();
                await worker;

                Assert.That(_coordinatorAttemptCount, Is.EqualTo(2));
                Assert.That(_coordinatorMaximumConcurrentAttempts, Is.EqualTo(1));
            }
            finally
            {
                coordinator.Dispose();
            }
        }

        private async UniTask<bool> RunCoordinatorAttempt(int generation, CancellationToken cancellationToken)
        {
            _coordinatorAttemptCount++;
            _coordinatorActiveAttempts++;
            _coordinatorMaximumConcurrentAttempts = Math.Max(
                _coordinatorMaximumConcurrentAttempts, _coordinatorActiveAttempts);
            try
            {
                if (_coordinatorAttemptCount == 1)
                {
                    var cancellationSignal = new UniTaskCompletionSource();
                    using (cancellationToken.Register(() => cancellationSignal.TrySetResult()))
                        await cancellationSignal.Task;
                    cancellationToken.ThrowIfCancellationRequested();
                }

                return true;
            }
            finally
            {
                _coordinatorActiveAttempts--;
            }
        }

        private void InvokeConfirmationCallback(Order order)
        {
            Assert.That(OnPurchaseConfirmedMethod, Is.Not.Null);
            OnPurchaseConfirmedMethod.Invoke(_manager, new object[] { order });
        }

        private void InvokeStoreDisconnected(StoreConnectionFailureDescription description,
            UniTaskCompletionSource<bool> connectionCompletion)
        {
            var method = typeof(InAppPurchaseManager).GetMethod("OnStoreDisconnected",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            method.Invoke(_manager, new object[] { description, connectionCompletion });
        }

        private void SetReadiness(string propertyName, bool value)
        {
            var property = typeof(InAppPurchaseManager).GetProperty(propertyName,
                BindingFlags.Instance | BindingFlags.Public);
            Assert.That(property, Is.Not.Null);
            property.SetValue(_manager, value);
        }

        private sealed class EmptyCart : ICart
        {
            public IReadOnlyList<CartItem> Items() => Array.Empty<CartItem>();
        }

        private sealed class TestOrderInfo : IOrderInfo
        {
            public TestOrderInfo(string transactionId)
            {
                TransactionID = transactionId;
            }

            public IAppleOrderInfo Apple => null;
            public IGoogleOrderInfo Google => null;
            public List<IPurchasedProductInfo> PurchasedProductInfo { get; set; } = new();
            public string Receipt => "test-receipt";
            public string TransactionID { get; }
        }

        private sealed class SpyPurchaseEffects : IStorePurchaseEffects
        {
            private readonly HashSet<string> _appliedProductEffectTransactions = new(StringComparer.Ordinal);
            private bool _subscriptionActive;

            public int ProductEffectCount { get; private set; }
            public int SubscriptionStateChangeCount { get; private set; }
            public bool ThrowAfterApplyingNextProductEffect { get; set; }
            public bool ThrowAfterApplyingNextSubscriptionState { get; set; }

            public void ApplyProductEffects(Order order, string transactionId)
            {
                if (!_appliedProductEffectTransactions.Add(transactionId)) return;

                ProductEffectCount++;
                if (ThrowAfterApplyingNextProductEffect)
                {
                    ThrowAfterApplyingNextProductEffect = false;
                    throw new InvalidOperationException("simulated effect failure after mutation");
                }
            }
            public void ApplySubscriptionState(bool active)
            {
                if (_subscriptionActive == active) return;
                _subscriptionActive = active;
                SubscriptionStateChangeCount++;
                if (ThrowAfterApplyingNextSubscriptionState)
                {
                    ThrowAfterApplyingNextSubscriptionState = false;
                    throw new InvalidOperationException("simulated subscription effect failure after mutation");
                }
            }
        }

        private sealed class SpyPurchasePersistence : IPurchasePersistenceAndAnalytics
        {
            private UniTaskCompletionSource _saveDataReadiness;
            private readonly HashSet<string> _recordedTransactions = new(StringComparer.Ordinal);
            public int RecordedPurchaseCount { get; private set; }
            public int RecordPurchaseAttemptCount { get; private set; }
            public int PlayerTierUpdateCount { get; private set; }
            public List<bool> PlayerTierValues { get; } = new();
            public bool FailNextRecordPurchase { get; set; }
            public bool ThrowAfterRecordingNextPurchase { get; set; }
            public UniTask WaitForSaveDataReady => _saveDataReadiness?.Task ?? UniTask.CompletedTask;

            public void DelaySaveDataReadiness() => _saveDataReadiness = new UniTaskCompletionSource();
            public void CompleteSaveDataReadiness() => _saveDataReadiness.TrySetResult();

            public UniTask RecordPurchase(Order order, string transactionId)
            {
                RecordPurchaseAttemptCount++;
                if (FailNextRecordPurchase)
                {
                    FailNextRecordPurchase = false;
                    throw new InvalidOperationException("simulated persistence failure");
                }

                if (_recordedTransactions.Add(transactionId)) RecordedPurchaseCount++;
                if (ThrowAfterRecordingNextPurchase)
                {
                    ThrowAfterRecordingNextPurchase = false;
                    throw new InvalidOperationException("simulated failure after recording");
                }

                return UniTask.CompletedTask;
            }
            public void UpdatePlayerTier(bool activeSubscription)
            {
                PlayerTierUpdateCount++;
                PlayerTierValues.Add(activeSubscription);
            }
        }
    }
}