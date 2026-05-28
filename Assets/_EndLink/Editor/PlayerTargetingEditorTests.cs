using System.Reflection;
using EndLink.Combat;
using NUnit.Framework;
using UnityEngine;

namespace EndLink.Editor.Tests
{
    public sealed class PlayerTargetingEditorTests
    {
        [Test]
        public void TargetIndicator_FacesViewReference()
        {
            GameObject player = new("Player");
            GameObject target = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            GameObject markerPrefab = new("TargetMarker");
            GameObject view = new("ViewReference");

            try
            {
                player.transform.position = Vector3.zero;
                target.transform.position = new Vector3(0f, 1f, 5f);
                view.transform.position = new Vector3(3f, 3f, -4f);
                view.transform.LookAt(target.transform.position);

                PlayerTargeting targeting = player.AddComponent<PlayerTargeting>();
                SetPrivateField(targeting, "targetIndicatorPrefab", markerPrefab);
                SetPrivateField(targeting, "viewReference", view.transform);

                targeting.SetCurrentTarget(target.transform);

                GameObject markerInstance = GetPrivateField<GameObject>(targeting, "_targetIndicatorInstance");
                Assert.IsNotNull(markerInstance);

                Vector3 toView = (view.transform.position - markerInstance.transform.position).normalized;
                Assert.That(Vector3.Dot(markerInstance.transform.forward, toView), Is.GreaterThan(0.999f));
            }
            finally
            {
                Object.DestroyImmediate(player);
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(markerPrefab);
                Object.DestroyImmediate(view);
            }
        }

        private static void SetPrivateField<TValue>(object instance, string fieldName, TValue value)
        {
            FieldInfo field = instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, $"Missing field {fieldName}");
            field.SetValue(instance, value);
        }

        private static TValue GetPrivateField<TValue>(object instance, string fieldName)
        {
            FieldInfo field = instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, $"Missing field {fieldName}");
            return (TValue)field.GetValue(instance);
        }
    }
}
