using System.Reflection;
using EndLink.Enemies;
using NUnit.Framework;
using UnityEngine;

namespace EndLink.Tests
{
    public sealed class EnemyHealthTests
    {
        [Test]
        public void ResetHealth_UsesSkinnedRendererForHitFeedback()
        {
            GameObject root = new GameObject("Enemy");
            GameObject body = new GameObject("Body");
            body.transform.SetParent(root.transform);
            SkinnedMeshRenderer skinnedRenderer = body.AddComponent<SkinnedMeshRenderer>();
            EnemyHealth health = root.AddComponent<EnemyHealth>();

            try
            {
                health.ResetHealth();

                FieldInfo feedbackRendererField = typeof(EnemyHealth).GetField(
                    "feedbackRenderer",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                Assert.IsNotNull(feedbackRendererField);
                Assert.AreSame(skinnedRenderer, feedbackRendererField.GetValue(health));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void EnemyBalance_DepletionOpensAndRecoveryClosesExecutionWindow()
        {
            GameObject root = new GameObject("Enemy");
            EnemyHealth health = root.AddComponent<EnemyHealth>();
            EnemyBalance balance = root.AddComponent<EnemyBalance>();

            try
            {
                health.ResetHealth();
                balance.ResetBalance();

                Assert.IsTrue(balance.ApplyBalanceDamage(40f, null));
                Assert.AreEqual(60f, balance.CurrentBalance, 0.001f);
                Assert.IsFalse(balance.IsStaggered);
                Assert.IsFalse(balance.CanBeExecuted);

                Assert.IsTrue(balance.ApplyBalanceDamage(60f, null));
                Assert.AreEqual(0f, balance.CurrentBalance, 0.001f);
                Assert.IsTrue(balance.IsStaggered);
                Assert.IsTrue(balance.CanBeExecuted);

                balance.RecoverFromStagger();

                Assert.AreEqual(balance.MaxBalance, balance.CurrentBalance, 0.001f);
                Assert.IsFalse(balance.IsStaggered);
                Assert.IsFalse(balance.CanBeExecuted);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void HitReaction_UsesActionStrengthInsteadOfDamageValue()
        {
            Assert.IsFalse(EnemyStateMachine.ShouldTriggerHitReaction(0f, 1f, 1f, 0f));
            Assert.IsFalse(EnemyStateMachine.ShouldTriggerHitReaction(0.9f, 1f, 1f, 0f));
            Assert.IsTrue(EnemyStateMachine.ShouldTriggerHitReaction(1f, 1f, 1f, 0f));
            Assert.IsFalse(EnemyStateMachine.ShouldTriggerHitReaction(2f, 1f, 0.5f, 1f));
        }
    }
}
