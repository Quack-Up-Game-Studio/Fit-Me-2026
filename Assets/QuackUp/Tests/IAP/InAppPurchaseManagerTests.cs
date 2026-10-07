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
using System.IO;
using QuackUp.Analytics;
using QuackUp.Save;
using QuackUp.SocialService;
using QuackUp.GoogleAdMob;
using FitMe.Panel;

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
        public void CompletedPurchaseLedgerSurvivesPlayerRecordSaveReload()
        {
            var saveType = typeof(PlayerRecordSaveData);
            var markRecorded = saveType.GetMethod("TryMarkPurchaseRecorded");
            var markCompleted = saveType.GetMethod("TryMarkPurchaseCompleted");
            var isCompleted = saveType.GetMethod("IsPurchaseCompleted");
            Assert.That(markRecorded, Is.Not.Null);
            Assert.That(markCompleted, Is.Not.Null);
            Assert.That(isCompleted, Is.Not.Null);

            var saveData = new PlayerRecordSaveData();
            Assert.That(markRecorded.Invoke(saveData, new object[] { "tx-ledger" }), Is.EqualTo(true));
            Assert.That(markCompleted.Invoke(saveData, new object[] { "tx-ledger" }), Is.EqualTo(true));

            var restored = MessagePackSerializer.Deserialize<PlayerRecordSaveData>(
                MessagePackSerializer.Serialize(saveData));

            Assert.That(isCompleted.Invoke(restored, new object[] { "tx-ledger" }), Is.EqualTo(true));
            Assert.That(markRecorded.Invoke(restored, new object[] { "tx-ledger" }), Is.EqualTo(false));
        }

        [Test]
        public void PersistentlyCompletedConfirmationDoesNotReapplyPurchaseWork()
        {
            _persistence.MarkPurchaseCompleted("tx-already-complete");
            InvokeConfirmationCallback(new ConfirmedOrder(new EmptyCart(),
                new TestOrderInfo("tx-already-complete")));

            Assert.That(_effects.ProductEffectCount, Is.Zero);
            Assert.That(_persistence.RecordedPurchaseCount, Is.Zero);
            Assert.That(_successCount, Is.Zero);
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
        public void FailedEnergyWriteIsRetriedBeforePurchaseCompletionSurvivesReload()
        {
            using var fixture = new PurchaseSaveFixture();
            using var manager = new InAppPurchaseManager(
                new StorePurchaseEffects(fixture.Energy, null), fixture.Persistence);
            var order = CreateEnergyOrder("tx-energy-write");
            var successes = 0;
            using var subscription = manager.OnPurchaseSuccess.Subscribe(_ => successes++);

            fixture.BlockWrite();
            LogAssert.Expect(LogType.Error, new Regex("Error creating ZIP file:"));
            LogAssert.Expect(LogType.Error, new Regex("IAP: Failed to process confirmed transaction tx-energy-write:"));
            OnPurchaseConfirmedMethod.Invoke(manager, new object[] { order });
            Assert.That(successes, Is.Zero);
            Assert.That(fixture.Persistence.IsPurchaseCompleted("tx-energy-write"), Is.False);

            fixture.UnblockWrite();
            OnPurchaseConfirmedMethod.Invoke(manager, new object[] { order });
            Assert.That(successes, Is.EqualTo(1));

            fixture.Reload();
            Assert.That(fixture.EnergyData.CurrentEnergy, Is.EqualTo(13),
                "A completed purchase must have its energy grant on disk, exactly once.");
            Assert.That(fixture.EnergyData.AppliedPurchaseTransactionIds, Is.EquivalentTo(new[] { "tx-energy-write" }));
            Assert.That(fixture.Persistence.IsPurchaseCompleted("tx-energy-write"), Is.True);
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

        [TestCase("sameOwner")]
        [TestCase("energyBar")]
        [TestCase("outOfEnergy")]
        [TestCase("gameOver")]
        public void RejectedSecondEnergyAdRequestDoesNotCancelFirstOwnersReward(string competitor)
        {
            using var fixture = new PurchaseSaveFixture();
            using var ads = new AdsService(null, null, null);
            var rewarded = new ControlledRewardedAd();
            var instances = (Dictionary<Type, AdsInstance>)typeof(AdsService)
                .GetField("_adsInstances", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(ads);
            instances.Add(typeof(RewardedAdInstance), rewarded);
            using var firstOwner = new EnergyBarViewModel(fixture.Energy, ads);
            Action competingRequest;
            IDisposable rejectedOwner = null;
            switch (competitor)
            {
                case "sameOwner":
                    competingRequest = () => firstOwner.WatchAdCommand.Execute(Unit.Default);
                    break;
                case "outOfEnergy":
                    var outOfEnergy = new OutOfEnergyManager(null, ads, fixture.Energy, null, TimeSpan.Zero, 1);
                    ((ReactiveProperty<int>)typeof(OutOfEnergyManager)
                        .GetField("_remainingAdCount", BindingFlags.Instance | BindingFlags.NonPublic)
                        .GetValue(outOfEnergy)).Value = 1;
                    rejectedOwner = outOfEnergy;
                    competingRequest = outOfEnergy.WatchAds;
                    break;
                case "gameOver":
                    var gameOver = new GameOverPanelViewModel(null, ads, _manager, 1, 10f, null, null);
                    rejectedOwner = gameOver;
                    competingRequest = () => gameOver.AdsContinueCommand.Execute(Unit.Default);
                    break;
                default:
                    var energyBar = new EnergyBarViewModel(fixture.Energy, ads);
                    rejectedOwner = energyBar;
                    competingRequest = () => energyBar.WatchAdCommand.Execute(Unit.Default);
                    break;
            }

            try
            {
                firstOwner.WatchAdCommand.Execute(Unit.Default);
                competingRequest();
                rewarded.EarnReward();

                Assert.That(fixture.Energy.CurrentEnergy.CurrentValue, Is.EqualTo(11),
                    "Only the first admitted owner must receive its reward after a competing request.");
                Assert.That(rewarded.ShowCalls, Is.EqualTo(1), "Reject a competitor before it touches the SDK show/load path.");
                Assert.That(rewarded.LoadCalls, Is.Zero, "An active ad must not be replaced by a rejected request.");
            }
            finally
            {
                rejectedOwner?.Dispose();
            }
        }

        [Test]
        public void DisposedRewardOwnerKeepsShowExclusiveUntilClose()
        {
            using var fixture = new PurchaseSaveFixture();
            using var ads = new AdsService(null, null, null);
            var rewarded = new ControlledRewardedAd();
            var instances = (Dictionary<Type, AdsInstance>)typeof(AdsService)
                .GetField("_adsInstances", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(ads);
            instances.Add(typeof(RewardedAdInstance), rewarded);
            using var firstOwner = new EnergyBarViewModel(fixture.Energy, ads);
            using var nextOwner = new EnergyBarViewModel(fixture.Energy, ads);
            firstOwner.WatchAdCommand.Execute(Unit.Default);
            firstOwner.Dispose();
            nextOwner.WatchAdCommand.Execute(Unit.Default);
            rewarded.EarnReward();
            Assert.That(fixture.Energy.CurrentEnergy.CurrentValue, Is.EqualTo(10));
            Assert.That(rewarded.ShowCalls, Is.EqualTo(1));

            rewarded.Close();
            nextOwner.WatchAdCommand.Execute(Unit.Default);
            rewarded.EarnReward();
            rewarded.EarnReward();
            Assert.That(fixture.Energy.CurrentEnergy.CurrentValue, Is.EqualTo(11));
            Assert.That(rewarded.ShowCalls, Is.EqualTo(2));
        }


        [Test]
        public void OutstandingLoadCompletionCannotReplaceShowingRewardedAd()
        {
            var rewarded = new LoadRaceRewardedAd();
            var loadGeneration = rewarded.BeginTestLoad();
            Assert.That(rewarded.CanAcceptTestLoad(loadGeneration), Is.True);
            rewarded.BeginTestShow();
            Assert.That(rewarded.CanAcceptTestLoad(loadGeneration), Is.False,
                "A load that finishes after show begins cannot replace the ad currently owned by the show.");
        }

        [Test]
        public void RejectedEnergySerializationCannotCompletePurchase()
        {
            using var fixture = new PurchaseSaveFixture();
            using var manager = new InAppPurchaseManager(
                new StorePurchaseEffects(fixture.Energy, null), fixture.Persistence);
            var successes = 0;
            using var subscription = manager.OnPurchaseSuccess.Subscribe(_ => successes++);
            fixture.RejectEnergyWrites();
            LogAssert.Expect(LogType.Error, new Regex("IAP: Failed to process confirmed transaction tx-rejected-write:"));
            OnPurchaseConfirmedMethod.Invoke(manager, new object[] { CreateEnergyOrder("tx-rejected-write") });
            Assert.That(successes, Is.Zero, "Serialization rejection is not a durable energy grant.");
            Assert.That(fixture.Persistence.IsPurchaseCompleted("tx-rejected-write"), Is.False);
        }

        [Test]
        public void FailedCompletionWriteDoesNotLeaveAnInMemoryCompletedPurchase()
        {
            using var fixture = new PurchaseSaveFixture();
            fixture.BlockWrite();
            LogAssert.Expect(LogType.Error, new Regex("Error creating ZIP file:"));
            Assert.Catch(() => fixture.Persistence.MarkPurchaseCompleted("tx-completion-write"));
            Assert.That(fixture.Persistence.IsPurchaseCompleted("tx-completion-write"), Is.False,
                "An unsaved completion marker must not bypass a later confirmation retry.");

            fixture.UnblockWrite();
            fixture.Persistence.MarkPurchaseCompleted("tx-completion-write");
            fixture.Reload();
            Assert.That(fixture.Persistence.IsPurchaseCompleted("tx-completion-write"), Is.True);
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
                coordinator.RequestRetry();
                await worker;

                Assert.That(_coordinatorAttemptCount, Is.EqualTo(2));
                Assert.That(_coordinatorMaximumConcurrentAttempts, Is.EqualTo(1));
            }
            finally
            {
                coordinator.Dispose();
            }
        }

        [Test]
        public async Task ConcurrentRunJoinsActiveInitializationWithoutSchedulingRetry()
        {
            var attemptRelease = new UniTaskCompletionSource();
            var attempts = 0;
            var activeAttempts = 0;
            var maximumConcurrentAttempts = 0;
            var coordinator = new StoreInitializationCoordinator(async (_, _) =>
            {
                attempts++;
                activeAttempts++;
                maximumConcurrentAttempts = Math.Max(maximumConcurrentAttempts, activeAttempts);
                try
                {
                    await attemptRelease.Task;
                    return true;
                }
                finally
                {
                    activeAttempts--;
                }
            });

            try
            {
                var firstRun = coordinator.Run();
                await UniTask.WaitUntil(() => activeAttempts == 1);
                var joinedRun = coordinator.Run();
                attemptRelease.TrySetResult();
                var firstSucceeded = await firstRun;
                var joinedSucceeded = await joinedRun;

                Assert.That(firstSucceeded, Is.True);
                Assert.That(joinedSucceeded, Is.True);
                Assert.That(attempts, Is.EqualTo(1));
                Assert.That(maximumConcurrentAttempts, Is.EqualTo(1));
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
            method.Invoke(_manager, new object[]
            {
                description, connectionCompletion, 0, CancellationToken.None
            });
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

        private sealed class PurchaseSaveFixture : IDisposable
        {
            private readonly string _directory = Path.Combine(Environment.GetEnvironmentVariable("TMPDIR"),
                "purchase-save-tests-" + Guid.NewGuid().ToString("N"));
            private readonly MessagePackSaveConfig _config;
            private readonly EnergyManagerConfig _energyConfig;
            private readonly EnergyManagerSaveObject _energySave;
            private readonly PlayerRecordSaveObject _playerSave;
            private readonly MessagePackSaveManager _saveManager;
            public EnergyManager Energy { get; }
            public PurchasePersistenceAndAnalytics Persistence { get; }
            public EnergyManagerSaveData EnergyData => _energySave.GetSaveData<EnergyManagerSaveData>();
            private string TemporaryArchive => Path.Combine(_directory, "purchase.sav.copy.tmp");

            public PurchaseSaveFixture()
            {
                _config = MessagePackSaveConfig.CreateForTests(new SaveSettings
                {
                    saveLocation = SaveLocation.Custom, saveDirectory = _directory, saveFileName = "purchase"
                });
                _energySave = ScriptableObject.CreateInstance<RejectableEnergySaveObject>();
                _playerSave = ScriptableObject.CreateInstance<PlayerRecordSaveObject>();
                _energySave.Reset();
                _playerSave.Reset();
                EnergyData.CurrentEnergy = 10;
                _playerSave.GetSaveData<PlayerRecordSaveData>().IsFirstTimePlayer = false;
                _saveManager = new MessagePackSaveManager(_config);
                _saveManager.RegisterSaveObject("energy", _energySave);
                _saveManager.RegisterSaveObject("player", _playerSave);
                _saveManager.SaveAll();
                _saveManager.MarkSaveDataReady();
                var analytics = new NoOpAnalytics();
                var cloud = new MockCloudSaveService();
                _energyConfig = ScriptableObject.CreateInstance<EnergyManagerConfig>();
                typeof(EnergyManagerConfig).GetProperty(nameof(EnergyManagerConfig.MaxEnergy)).SetValue(_energyConfig, 50);
                Energy = new EnergyManager(_energyConfig, _saveManager, cloud, null, analytics);
                typeof(EnergyManager).GetField("_saveObject", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(Energy, _energySave);
                ((ReactiveProperty<int>)typeof(EnergyManager)
                    .GetField("_currentEnergy", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(Energy)).Value = 10;
                Persistence = new PurchasePersistenceAndAnalytics(_saveManager, cloud, analytics);
            }

            public void BlockWrite() => Directory.CreateDirectory(TemporaryArchive);
            public void UnblockWrite() => Directory.Delete(TemporaryArchive);
            public void Reload() => _saveManager.LoadAll();
            public void RejectEnergyWrites() => ((RejectableEnergySaveObject)_energySave).RejectWrites = true;

            public void Dispose()
            {
                Energy.Dispose();
                _saveManager.Dispose();
                UnityEngine.Object.DestroyImmediate(_energySave);
                UnityEngine.Object.DestroyImmediate(_playerSave);
                UnityEngine.Object.DestroyImmediate(_config);
                UnityEngine.Object.DestroyImmediate(_energyConfig);
                if (Directory.Exists(_directory)) Directory.Delete(_directory, true);
            }
        }

        private sealed class NoOpAnalytics : IAnalyticsService
        {
            public void TrackProgression(ProgressionStatus status, string progression01) { }
            public void TrackProgression(ProgressionStatus status, string progression01, string progression02) { }
            public void TrackProgression(ProgressionStatus status, string progression01, string progression02,
                int score, Dictionary<string, object> customFields) { }
            public void TrackResourceFlow(ResourceFlowType flowType, string currency, float amount,
                string itemType, string itemId) { }
            public void TrackBusinessEvent(string currency, int amount, string itemType, string itemId, string cartType) { }
            public void SetPlayerTier(string tier) { }
            public void TrackAdEvent(AdAction action, AdType type, string unitId, string placement) { }
        }

        private sealed class RejectableEnergySaveObject : EnergyManagerSaveObject
        {
            public bool RejectWrites { get; set; }
            public override bool TrySerializeSaveData(out byte[] bytes, MessagePackSerializerOptions options = null)
            {
                if (!RejectWrites) return base.TrySerializeSaveData(out bytes, options);
                bytes = null;
                return false;
            }
        }

        private sealed class ControlledRewardedAd : RewardedAdInstance
        {
            private bool _showing;
            public int ShowCalls { get; private set; }
            public int LoadCalls { get; private set; }
            public ControlledRewardedAd() : base(null, null, null) { }
            public override bool CanShowAd() => Enabled && !_showing;
            public override void Load() => LoadCalls++;
            public override bool TryShow()
            {
                ShowCalls++;
                if (!_showing)
                {
                    _showing = true;
                    return true;
                }
                Load();
                _onAdFailed.OnNext(Unit.Default);
                return false;
            }
            public void EarnReward() => ((Subject<Unit>)typeof(RewardedAdInstance)
                .GetField("_onUserEarnedReward", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(this)).OnNext(Unit.Default);
            public void Close()
            {
                _showing = false;
                _onAdClosed.OnNext(Unit.Default);
            }
        }

        private sealed class LoadRaceRewardedAd : RewardedAdInstance
        {
            public LoadRaceRewardedAd() : base(null, null, null) { }
            public override bool CanShowAd() => true;
            public override void Load() { }
            public override bool TryShow() => true;
            public int BeginTestLoad() => BeginLoadGeneration();
            public bool CanAcceptTestLoad(int generation) => CanAcceptLoadedAd(generation);
            public void BeginTestShow() => MarkShowStarted();
        }

        private static ConfirmedOrder CreateEnergyOrder(string transactionId)
        {
            var constructor = typeof(Product).GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic,
                null, new[] { typeof(ProductDefinition), typeof(ProductMetadata) }, null);
            var product = (Product)constructor.Invoke(new object[]
                { new ProductDefinition(ProductIds.Energy2, ProductType.Consumable), new ProductMetadata() });
            return new ConfirmedOrder(new Cart(new CartItem(product)), new TestOrderInfo(transactionId));
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
            private readonly HashSet<string> _completedTransactions = new(StringComparer.Ordinal);
            public int RecordedPurchaseCount { get; private set; }
            public int RecordPurchaseAttemptCount { get; private set; }
            public int PlayerTierUpdateCount { get; private set; }
            public List<bool> PlayerTierValues { get; } = new();
            public bool FailNextRecordPurchase { get; set; }
            public bool ThrowAfterRecordingNextPurchase { get; set; }
            public UniTask WaitForSaveDataReady => _saveDataReadiness?.Task ?? UniTask.CompletedTask;

            public bool IsPurchaseCompleted(string transactionId) => _completedTransactions.Contains(transactionId);
            public void MarkPurchaseCompleted(string transactionId) => _completedTransactions.Add(transactionId);

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