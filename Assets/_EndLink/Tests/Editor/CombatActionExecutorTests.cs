using System.Reflection;
using EndLink.Ally;
using EndLink.Combat;
using EndLink.Enemies;
using NUnit.Framework;
using UnityEngine;

namespace EndLink.Tests.Editor
{
    public sealed class CombatActionExecutorTests
    {
        private GameObject _actor;
        private GameObject _hitboxPrefab;
        private GameObject _spawnedHitbox;
        private CombatActionDefinition _actionA;
        private CombatActionDefinition _actionB;

        [TearDown]
        public void TearDown()
        {
            if (_spawnedHitbox != null)
            {
                Object.DestroyImmediate(_spawnedHitbox);
            }

            if (_hitboxPrefab != null)
            {
                Object.DestroyImmediate(_hitboxPrefab);
            }

            if (_actor != null)
            {
                Object.DestroyImmediate(_actor);
            }

            if (_actionA != null)
            {
                Object.DestroyImmediate(_actionA);
            }

            if (_actionB != null)
            {
                Object.DestroyImmediate(_actionB);
            }
        }

        [Test]
        public void AllCombatDrivers_ImplementUnifiedExecutorInterface()
        {
            Assert.That(typeof(ICombatActionExecutor).IsAssignableFrom(typeof(PlayerCombatDriver)), Is.True);
            Assert.That(typeof(ICombatActionExecutor).IsAssignableFrom(typeof(AllyCombatDriver)), Is.True);
            Assert.That(typeof(ICombatActionExecutor).IsAssignableFrom(typeof(EnemyCombatDriver)), Is.True);
        }

        [Test]
        public void PlayerDriver_TracksCooldownPerAction()
        {
            _actor = new GameObject("Player");
            PlayerCombatDriver driver = _actor.AddComponent<PlayerCombatDriver>();
            _hitboxPrefab = CreateHitboxPrefab();
            _actionA = CreateAction("action_a", 5f);
            _actionB = CreateAction("action_b", 5f);

            ICombatActionExecutor executor = driver;

            Assert.That(executor.TryExecute(_actionA), Is.True);
            _spawnedHitbox = FindSpawnedHitbox();
            Assert.That(executor.CanExecute(_actionA), Is.False);
            Assert.That(executor.GetCooldownRemaining(_actionA), Is.GreaterThan(0f));
            Assert.That(executor.CanExecute(_actionB), Is.True);
            Assert.That(executor.GetCooldownRemaining(_actionB), Is.EqualTo(0f));
        }

        private GameObject FindSpawnedHitbox()
        {
            HitboxBase[] hitboxes = Object.FindObjectsByType<HitboxBase>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            for (int i = 0; i < hitboxes.Length; i++)
            {
                if (hitboxes[i] != null && hitboxes[i].gameObject != _hitboxPrefab)
                {
                    return hitboxes[i].gameObject;
                }
            }

            return null;
        }

        private GameObject CreateHitboxPrefab()
        {
            GameObject hitbox = new("TestHitbox");
            hitbox.AddComponent<BoxCollider>().isTrigger = true;
            hitbox.AddComponent<HitboxBase>();
            return hitbox;
        }

        private CombatActionDefinition CreateAction(string actionId, float cooldown)
        {
            CombatActionDefinition action = ScriptableObject.CreateInstance<CombatActionDefinition>();
            SetPrivateField(action, "actionId", actionId);
            SetPrivateField(action, "cooldown", cooldown);
            SetPrivateField(action, "hitboxPrefab", _hitboxPrefab);
            return action;
        }

        private static void SetPrivateField<TValue>(
            CombatActionDefinition action,
            string fieldName,
            TValue value)
        {
            FieldInfo field = typeof(CombatActionDefinition).GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.That(field, Is.Not.Null, $"Missing field: {fieldName}");
            field.SetValue(action, value);
        }
    }
}
