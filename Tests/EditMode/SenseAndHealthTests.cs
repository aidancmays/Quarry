using NUnit.Framework;
using UnityEngine;

namespace Quarry.Tests
{
    public class SenseAndHealthTests
    {
        sealed class Noisy : MonoBehaviour, INoiseSource
        {
            public float multiplier = 1f;
            public float NoiseMultiplier => multiplier;
        }

        [Test]
        public void RadiusScalesWithNoise()
        {
            Assert.AreEqual(9.6f, SenseMath.Radius(6f, 1.6f), 1e-5f);
            Assert.AreEqual(2.1f, SenseMath.Radius(6f, 0.35f), 1e-5f);
            Assert.AreEqual(0f, SenseMath.Radius(6f, -1f));
        }

        [Test]
        public void InRangeIsAcrossTheGroundWithinAHeight()
        {
            Assert.IsTrue(SenseMath.InRange(Vector3.zero, new Vector3(3f, 2f, 4f), 5f, 3f));
            Assert.IsFalse(SenseMath.InRange(Vector3.zero, new Vector3(3f, 0f, 4.1f), 5f, 3f));
            Assert.IsFalse(SenseMath.InRange(Vector3.zero, new Vector3(1f, 5f, 0f), 5f, 3f), "a floor above");
            Assert.IsTrue(SenseMath.InRange(Vector3.zero, new Vector3(1f, 50f, 0f), 5f, 0f), "any height");
        }

        [Test]
        public void SensesNoticeQuietTargetsOnlyCloser()
        {
            var sensor = new GameObject("Sensor").AddComponent<Senses>();
            var target = new GameObject("Target").AddComponent<Noisy>();
            try
            {
                sensor.Radius = 6f;
                target.transform.position = new Vector3(4f, 0f, 0f);
                Assert.IsTrue(sensor.Notices(target), "walking at 4 m of 6");
                target.multiplier = 0.35f;
                Assert.IsFalse(sensor.Notices(target), "crawling at 4 m of 2.1");
                target.transform.position = new Vector3(2f, 0f, 0f);
                Assert.IsTrue(sensor.Notices(target), "crawling at 2 m");
                target.multiplier = 1.6f;
                target.transform.position = new Vector3(9f, 0f, 0f);
                Assert.IsTrue(sensor.Notices(target), "sprinting at 9 m of 9.6");
                Assert.AreSame(target, sensor.Nearest(new[] { target }));
            }
            finally
            {
                Object.DestroyImmediate(sensor.gameObject);
                Object.DestroyImmediate(target.gameObject);
            }
        }

        [Test]
        public void HealthOfflineTakesDamageDiesAndRevives()
        {
            var go = new GameObject("Health");
            var health = go.AddComponent<Health>();
            try
            {
                int died = 0, revived = 0, damaged = 0;
                health.Died += () => died++;
                health.Revived += () => revived++;
                health.Damaged += _ => damaged++;
                Assert.AreEqual(health.Max, health.Current);

                float lost = health.ServerApply(new DamageInfo { Amount = 20f });
                Assert.AreEqual(20f, lost, 1e-5f);
                Assert.AreEqual(health.Max - 20f, health.Current, 1e-5f);

                health.ServerApply(new DamageInfo { Amount = 1000f });
                Assert.IsTrue(health.IsDead);
                Assert.AreEqual(1, died);
                Assert.AreEqual(0f, health.ServerApply(new DamageInfo { Amount = 5f }), "no more damage once dead");
                Assert.AreEqual(2, damaged);

                health.ServerRevive(0.5f);
                Assert.IsFalse(health.IsDead);
                Assert.AreEqual(1, revived);
                Assert.AreEqual(0.5f, health.Fraction, 1e-4f);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void KnockbackIsVelocityForMovers()
        {
            var go = new GameObject("Mover");
            var knockback = go.AddComponent<Knockback>();
            try
            {
                knockback.Apply(new DamageInfo { Amount = 10f, Knockback = 2f, Direction = Vector3.right });
                Assert.Greater(knockback.Velocity.x, 0f);
                Assert.AreEqual(0f, knockback.Velocity.z, 1e-5f);
                Assert.IsTrue(knockback.IsStaggered);
                knockback.Block(Vector3.left);
                Assert.AreEqual(0f, knockback.Velocity.x, 1e-5f, "a wall to the right stops the push into it");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }
    }
}
