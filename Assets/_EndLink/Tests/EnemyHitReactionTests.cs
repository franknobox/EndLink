using EndLink.Combat;
using EndLink.Enemies;
using NUnit.Framework;
using UnityEngine;

namespace EndLink.Tests
{
    public sealed class EnemyHitReactionTests
    {
        [Test]
        public void EnemyHealthStoresLastDamageSourceFromHitboxOwner()
        {
            GameObject attacker = new GameObject("Attacker");
            GameObject enemy = new GameObject("Enemy");

            try
            {
                EnemyHealth health = enemy.AddComponent<EnemyHealth>();
                BoxCollider hitCollider = enemy.AddComponent<BoxCollider>();
                health.ResetHealth();

                HitboxHitInfo hitInfo = new HitboxHitInfo(
                    null,
                    attacker,
                    hitCollider,
                    null,
                    10f,
                    CombatDamageType.StructuralDamage,
                    0f,
                    null,
                    0f,
                    1,
                    Vector3.zero,
                    Vector3.forward);

                health.ReceiveHit(hitInfo);

                Assert.AreSame(attacker, health.LastDamageSource);
            }
            finally
            {
                Object.DestroyImmediate(enemy);
                Object.DestroyImmediate(attacker);
            }
        }

        [Test]
        public void HitReactionRequiresDamageThresholdAndCooldownWindow()
        {
            Assert.IsFalse(EnemyStateMachine.ShouldTriggerHitReaction(
                damage: 4,
                heavyHitDamageThreshold: 5f,
                currentTime: 1f,
                nextAllowedTime: 0f));

            Assert.IsFalse(EnemyStateMachine.ShouldTriggerHitReaction(
                damage: 10,
                heavyHitDamageThreshold: 5f,
                currentTime: 0.5f,
                nextAllowedTime: 1f));

            Assert.IsTrue(EnemyStateMachine.ShouldTriggerHitReaction(
                damage: 5,
                heavyHitDamageThreshold: 5f,
                currentTime: 1f,
                nextAllowedTime: 1f));

            Assert.IsTrue(EnemyStateMachine.ShouldTriggerHitReaction(
                damage: 1,
                heavyHitDamageThreshold: 0f,
                currentTime: 1f,
                nextAllowedTime: 0f));
        }

        [Test]
        public void DamageSourceTargetResolveRequiresCombatTarget()
        {
            GameObject attacker = new GameObject("Attacker");
            GameObject objectWithoutTarget = new GameObject("NoTarget");

            try
            {
                attacker.AddComponent<CombatTarget>();
                Transform resolvedTarget;

                Assert.IsTrue(EnemyStateMachine.TryResolveDamageSourceTarget(
                    attacker,
                    out resolvedTarget));
                Assert.AreSame(attacker.transform, resolvedTarget);

                Assert.IsFalse(EnemyStateMachine.TryResolveDamageSourceTarget(
                    objectWithoutTarget,
                    out resolvedTarget));
                Assert.IsNull(resolvedTarget);
            }
            finally
            {
                Object.DestroyImmediate(objectWithoutTarget);
                Object.DestroyImmediate(attacker);
            }
        }
    }
}
