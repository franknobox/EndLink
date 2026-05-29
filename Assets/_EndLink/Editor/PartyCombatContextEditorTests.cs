using EndLink.Combat;
using EndLink.Party;
using NUnit.Framework;
using UnityEngine;

namespace EndLink.Editor.Tests
{
    public sealed class PartyCombatContextEditorTests
    {
        [Test]
        public void HitLanded_EntersCombatAndTracksTarget()
        {
            GameObject partyRoot = new("Party_Root");
            GameObject player = new("Player");
            GameObject enemy = new("Enemy");

            try
            {
                PartyCombatContext context = partyRoot.AddComponent<PartyCombatContext>();

                CombatEventsBus.Raise(new CombatEvent(CombatEventType.HitLanded, player, enemy));

                Assert.IsTrue(context.IsInCombat);
                Assert.AreEqual(enemy.transform, context.CurrentPrimaryTarget);
                Assert.AreEqual(1, context.KnownEnemyCount);
                Assert.IsTrue(context.ContainsKnownEnemy(enemy.transform));
            }
            finally
            {
                Object.DestroyImmediate(partyRoot);
                Object.DestroyImmediate(player);
                Object.DestroyImmediate(enemy);
            }
        }

        [Test]
        public void Dead_RemovesKnownEnemyAndClearsPrimaryTarget()
        {
            GameObject partyRoot = new("Party_Root");
            GameObject player = new("Player");
            GameObject enemy = new("Enemy");

            try
            {
                PartyCombatContext context = partyRoot.AddComponent<PartyCombatContext>();

                CombatEventsBus.Raise(new CombatEvent(CombatEventType.HitLanded, player, enemy));
                CombatEventsBus.RaiseDead(player, enemy);

                Assert.IsFalse(context.IsInCombat);
                Assert.IsNull(context.CurrentPrimaryTarget);
                Assert.AreEqual(0, context.KnownEnemyCount);
            }
            finally
            {
                Object.DestroyImmediate(partyRoot);
                Object.DestroyImmediate(player);
                Object.DestroyImmediate(enemy);
            }
        }
    }
}
