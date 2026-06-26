using EndLink.World;
using NUnit.Framework;
using UnityEngine;

namespace EndLink.Tests
{
    public class WorldInteractionTests
    {
        private GameObject _interactorObject;
        private GameObject _nearObject;
        private GameObject _farObject;

        [TearDown]
        public void TearDown()
        {
            DestroyIfExists(_interactorObject);
            DestroyIfExists(_nearObject);
            DestroyIfExists(_farObject);
        }

        [Test]
        public void RefreshCurrentInteractable_SelectsNearestEnabledInteractable()
        {
            WorldInteractor interactor = CreateInteractor(Vector3.zero, 3f);
            TestInteractable near = CreateInteractable("Near", new Vector3(1f, 0f, 0f));
            CreateInteractable("Far", new Vector3(2f, 0f, 0f));

            Physics.SyncTransforms();

            WorldInteractable current = interactor.RefreshCurrentInteractable();

            Assert.AreSame(near, current);
        }

        [Test]
        public void TryInteractCurrent_InvokesSelectedInteractable()
        {
            WorldInteractor interactor = CreateInteractor(Vector3.zero, 3f);
            TestInteractable near = CreateInteractable("Near", new Vector3(1f, 0f, 0f));

            Physics.SyncTransforms();

            bool interacted = interactor.TryInteractCurrent(_interactorObject);

            Assert.IsTrue(interacted);
            Assert.AreEqual(1, near.InteractionCount);
        }

        private WorldInteractor CreateInteractor(Vector3 position, float radius)
        {
            _interactorObject = new GameObject("WorldInteractor_Test");
            _interactorObject.transform.position = position;
            WorldInteractor interactor = _interactorObject.AddComponent<WorldInteractor>();
            interactor.InteractionRadius = radius;
            interactor.RefreshInterval = 0f;
            return interactor;
        }

        private TestInteractable CreateInteractable(string name, Vector3 position)
        {
            GameObject interactableObject = new GameObject(name);
            interactableObject.transform.position = position;
            interactableObject.AddComponent<BoxCollider>();
            TestInteractable interactable = interactableObject.AddComponent<TestInteractable>();

            if (_nearObject == null)
            {
                _nearObject = interactableObject;
            }
            else
            {
                _farObject = interactableObject;
            }

            return interactable;
        }

        private static void DestroyIfExists(Object target)
        {
            if (target != null)
            {
                Object.DestroyImmediate(target);
            }
        }

        private sealed class TestInteractable : WorldInteractable
        {
            public int InteractionCount { get; private set; }

            protected override bool OnInteract(GameObject interactor)
            {
                InteractionCount++;
                return true;
            }
        }
    }
}
