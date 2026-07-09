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
    }
}
