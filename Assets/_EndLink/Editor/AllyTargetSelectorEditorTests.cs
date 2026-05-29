using EndLink.Combat;
using EndLink.Party;
using EndLink.Ally;
using NUnit.Framework;
using UnityEngine;

namespace EndLink.Editor.Tests
{
    public sealed class AllyTargetSelectorEditorTests
    {
        [Test]
        public void TrySelectTarget_PrefersPrimaryTarget()
        {
            GameObject partyRoot = new("Party_Root");
            GameObject ally = new("Ally");
            GameObject nearEnemy = new("NearEnemy");
            GameObject primaryEnemy = new("PrimaryEnemy");
            GameObject player = new("Player");

            try
            {
                partyRoot.AddComponent<PartyCombatContext>();
                AllyTargetSelector selector = ally.AddComponent<AllyTargetSelector>();

                ally.transform.position = Vector3.zero;
                nearEnemy.transform.position = Vector3.right;
                primaryEnemy.transform.position = Vector3.right * 5f;

                CombatEventsBus.Raise(new CombatEvent(CombatEventType.HitLanded, player, nearEnemy));
                CombatEventsBus.Raise(new CombatEvent(CombatEventType.HitLanded, player, primaryEnemy));

                Assert.IsTrue(selector.TrySelectTarget(out Transform selectedTarget));
                Assert.AreEqual(primaryEnemy.transform, selectedTarget);
            }
            finally
            {
                Object.DestroyImmediate(partyRoot);
                Object.DestroyImmediate(ally);
                Object.DestroyImmediate(nearEnemy);
                Object.DestroyImmediate(primaryEnemy);
                Object.DestroyImmediate(player);
            }
        }

        [Test]
        public void TrySelectTarget_FallsBackToNearestKnownEnemyWhenPrimaryDies()
        {
            GameObject partyRoot = new("Party_Root");
            GameObject ally = new("Ally");
            GameObject nearEnemy = new("NearEnemy");
            GameObject farEnemy = new("FarEnemy");
            GameObject player = new("Player");

            try
            {
                partyRoot.AddComponent<PartyCombatContext>();
                AllyTargetSelector selector = ally.AddComponent<AllyTargetSelector>();

                ally.transform.position = Vector3.zero;
                nearEnemy.transform.position = Vector3.right;
                farEnemy.transform.position = Vector3.right * 6f;

                CombatEventsBus.Raise(new CombatEvent(CombatEventType.HitLanded, player, nearEnemy));
                CombatEventsBus.Raise(new CombatEvent(CombatEventType.HitLanded, player, farEnemy));
                CombatEventsBus.RaiseDead(player, farEnemy);

                Assert.IsTrue(selector.TrySelectTarget(out Transform selectedTarget));
                Assert.AreEqual(nearEnemy.transform, selectedTarget);
            }
            finally
            {
                Object.DestroyImmediate(partyRoot);
                Object.DestroyImmediate(ally);
                Object.DestroyImmediate(nearEnemy);
                Object.DestroyImmediate(farEnemy);
                Object.DestroyImmediate(player);
            }
        }
    }
}
