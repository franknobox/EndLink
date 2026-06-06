using EndLink.Combat;
using NUnit.Framework;
using UnityEngine;

namespace EndLink.Tests.Editor
{
    public sealed class CombatTargetTests
    {
        private GameObject _root;

        [TearDown]
        public void TearDown()
        {
            if (_root != null)
            {
                Object.DestroyImmediate(_root);
            }
        }

        [Test]
        public void RootAndLockPoint_FallBackToComponentTransform()
        {
            _root = new GameObject("TargetRoot");
            CombatTarget target = _root.AddComponent<CombatTarget>();

            Assert.That(target.RootTransform, Is.EqualTo(_root.transform));
            Assert.That(target.LockPoint, Is.EqualTo(_root.transform));
        }

        [Test]
        public void ChildCollider_ResolvesToSingleRootTarget()
        {
            _root = new GameObject("TargetRoot");
            CombatTarget expectedTarget = _root.AddComponent<CombatTarget>();

            GameObject child = new GameObject("Body");
            child.transform.SetParent(_root.transform);
            BoxCollider childCollider = child.AddComponent<BoxCollider>();

            bool resolved = CombatTargetUtility.TryResolve(childCollider, out ICombatTarget actualTarget);

            Assert.That(resolved, Is.True);
            Assert.That(actualTarget, Is.SameAs(expectedTarget));
            Assert.That(actualTarget.RootTransform, Is.EqualTo(_root.transform));
        }

        [Test]
        public void DeadLifeSource_MakesTargetInvalid()
        {
            _root = new GameObject("TargetRoot");
            TestCombatLifeState lifeState = _root.AddComponent<TestCombatLifeState>();
            CombatTarget target = _root.AddComponent<CombatTarget>();

            Assert.That(target.IsAlive, Is.True);
            Assert.That(target.IsTargetable, Is.True);

            lifeState.IsAliveValue = false;

            Assert.That(target.IsAlive, Is.False);
            Assert.That(target.IsTargetable, Is.False);
        }

        [Test]
        public void ClosestPointAndPlanarDistance_UseBodyColliderSurface()
        {
            _root = new GameObject("TargetRoot");
            CombatTarget target = _root.AddComponent<CombatTarget>();

            GameObject body = new GameObject("Body");
            body.transform.SetParent(_root.transform);
            BoxCollider bodyCollider = body.AddComponent<BoxCollider>();
            bodyCollider.size = new Vector3(2f, 2f, 2f);

            Vector3 from = new Vector3(5f, 0f, 0f);
            Physics.SyncTransforms();

            Vector3 closestPoint = target.GetClosestPoint(from);
            float surfaceDistance = target.GetSurfaceDistance(from);

            Assert.That(closestPoint.x, Is.EqualTo(1f).Within(0.001f));
            Assert.That(closestPoint.y, Is.EqualTo(0f).Within(0.001f));
            Assert.That(closestPoint.z, Is.EqualTo(0f).Within(0.001f));
            Assert.That(surfaceDistance, Is.EqualTo(4f).Within(0.001f));
        }

        private sealed class TestCombatLifeState : MonoBehaviour, ICombatTargetLifeState
        {
            public bool IsAliveValue { get; set; } = true;

            public bool IsAlive => IsAliveValue;
        }
    }
}
