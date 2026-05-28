using System.Reflection;
using EndLink.Combat;
using NUnit.Framework;
using UnityEngine;

namespace EndLink.Editor.Tests
{
    public sealed class PlayerCombatDriverEditorTests
    {
        [Test]
        public void ExecuteAction_FacesLockedTargetBeforeSpawningHitbox()
        {
            GameObject actor = new("Player");
            GameObject target = new("Target");
            GameObject hitboxPrefab = new("HitboxPrefab");
            CombatActionDefinition action = ScriptableObject.CreateInstance<CombatActionDefinition>();

            try
            {
                actor.transform.position = Vector3.zero;
                actor.transform.rotation = Quaternion.LookRotation(Vector3.forward, Vector3.up);
                target.transform.position = Vector3.right * 5f;

                PlayerTargeting targeting = actor.AddComponent<PlayerTargeting>();
                PlayerCombatDriver combatDriver = actor.AddComponent<PlayerCombatDriver>();
                targeting.SetCurrentTarget(target.transform);

                hitboxPrefab.AddComponent<BoxCollider>();
                hitboxPrefab.AddComponent<HitboxBase>();
                SetPrivateField(action, "hitboxPrefab", hitboxPrefab);
                SetPrivateField(action, "hitboxSpawnDistance", 1f);
                SetPrivateField(action, "hitboxSpawnHeight", 0f);
                SetPrivateField(action, "cooldown", 0f);

                Assert.IsTrue(combatDriver.ExecuteAction(action));

                Vector3 expectedForward = Vector3.right;
                Assert.That(Vector3.Dot(actor.transform.forward, expectedForward), Is.GreaterThan(0.999f));
            }
            finally
            {
                Object.DestroyImmediate(action);
                Object.DestroyImmediate(actor);
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(hitboxPrefab);

                foreach (HitboxBase hitbox in Object.FindObjectsByType<HitboxBase>(FindObjectsSortMode.None))
                {
                    Object.DestroyImmediate(hitbox.gameObject);
                }
            }
        }

        private static void SetPrivateField<TValue>(object instance, string fieldName, TValue value)
        {
            FieldInfo field = instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, $"Missing field {fieldName}");
            field.SetValue(instance, value);
        }
    }
}
