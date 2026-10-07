using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using QuackUp.Utils;

namespace FitMe.Tutorial.Tests
{
    public sealed class ExtendedStateMachineNavigationTests
    {
        [Test]
        public void NextToKey_ReportsEachIntermediateStateKeyAndIndex()
        {
            var stateMachine = new RecordingStateMachine();
            stateMachine.AddState("A", new TestState());
            stateMachine.AddState("B", new TestState());
            stateMachine.AddState("C", new TestState());
            stateMachine.AddState("D", new TestState());

            stateMachine.JumpTo("A").GetAwaiter().GetResult();
            stateMachine.NextTo("D").GetAwaiter().GetResult();

            Assert.That(stateMachine.Transitions, Is.EqualTo(new[]
            {
                ("A", 0),
                ("B", 1),
                ("C", 2),
                ("D", 3)
            }));
            Assert.That(stateMachine.CurrentStateKey, Is.EqualTo("D"));
            Assert.That(stateMachine.CurrentStateIndex, Is.EqualTo(3));
        }

        private sealed class RecordingStateMachine : ExtendedStateMachine<TestState>
        {
            public List<(string Key, int Index)> Transitions { get; } = new();

            protected override async UniTask ChangeStateInternal(TestState targetState, int? index = null, string key = null)
            {
                await base.ChangeStateInternal(targetState, index, key);
                Transitions.Add((CurrentStateKey, CurrentStateIndex));
            }
        }

        private sealed class TestState : State
        {
        }
    }
}
