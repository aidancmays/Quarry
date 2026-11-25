using System.Collections.Generic;
using GameplayTags;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Quarry.Tests
{
    public class KnockbackMathTests
    {
        readonly List<Object> made = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (Object o in made) Object.DestroyImmediate(o);
            made.Clear();
        }

        GameplayTag Tag(string name, GameplayTag parent = null)
        {
            var tag = ScriptableObject.CreateInstance<GameplayTag>();
            tag.name = name;
            if (parent != null)
            {
                var so = new SerializedObject(tag);
                so.FindProperty("parent").objectReferenceValue = parent;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            made.Add(tag);
            return tag;
        }

        [Test]
        public void ImpulseIsKnockbackPlusDamageShareTimesTypeOverPoise()
        {
            var r = KnockbackMath.Evaluate(damage: 20f, knockback: 2f, typeKnockback: 1.5f, typeStagger: 1f, poise: 2f,
                damageToKnockback: 0.1f, baseStagger: 0f, staggerPerDamage: 0f, maxStagger: 10f);
            Assert.AreEqual((2f + 20f * 0.1f) * 1.5f, r.Impulse, 1e-5f);
            Assert.AreEqual(r.Impulse / 2f, r.Speed, 1e-5f);
        }

        [Test]
        public void HarderHitsPushFurtherAndStaggerLonger()
        {
            var light = KnockbackMath.Evaluate(5f, 1f, 1f, 1f, 1f, 0.15f, 0.1f, 0.02f, 5f);
            var heavy = KnockbackMath.Evaluate(30f, 6f, 1f, 1f, 1f, 0.15f, 0.1f, 0.02f, 5f);
            Assert.Greater(heavy.Speed, light.Speed);
            Assert.Greater(heavy.Stagger, light.Stagger);
            Assert.AreEqual(0.1f + 5f * 0.02f, light.Stagger, 1e-5f);
        }

        [Test]
        public void PoiseResistsPushAndStagger()
        {
            var nimble = KnockbackMath.Evaluate(10f, 2f, 1f, 1f, 0.5f, 0.15f, 0.1f, 0.02f, 5f);
            var heavy = KnockbackMath.Evaluate(10f, 2f, 1f, 1f, 4f, 0.15f, 0.1f, 0.02f, 5f);
            Assert.AreEqual(nimble.Impulse, heavy.Impulse, 1e-5f, "same impulse");
            Assert.Greater(nimble.Speed, heavy.Speed);
            Assert.Greater(nimble.Stagger, heavy.Stagger);
        }

        [Test]
        public void StaggerIsCappedAndNoneForAnEmptyHit()
        {
            var huge = KnockbackMath.Evaluate(1000f, 0f, 1f, 3f, 1f, 0.15f, 0.1f, 0.02f, 1.5f);
            Assert.AreEqual(1.5f, huge.Stagger, 1e-5f);
            var nothing = KnockbackMath.Evaluate(0f, 0f, 1f, 1f, 1f, 0.15f, 0.1f, 0.02f, 1.5f);
            Assert.AreEqual(0f, nothing.Stagger);
            Assert.AreEqual(0f, nothing.Speed);
        }

        [Test]
        public void DecayDiesAwayExponentially()
        {
            Vector3 v = new Vector3(4f, 0f, 0f);
            Assert.AreEqual(4f * Mathf.Exp(-2f), KnockbackMath.Decay(v, 4f, 0.5f).x, 1e-5f);
            Assert.AreEqual(v, KnockbackMath.Decay(v, 0f, 1f));
        }

        [Test]
        public void PushDirectionFallsBackAndFlattens()
        {
            Vector3 d = KnockbackMath.PushDirection(new Vector3(0f, -1f, 1f), Vector3.zero, Vector3.zero, Vector3.back, true);
            Assert.AreEqual(Vector3.forward, d);
            d = KnockbackMath.PushDirection(Vector3.zero, Vector3.zero, Vector3.right * 2f, Vector3.back, true);
            Assert.AreEqual(Vector3.right, d, "away from where it landed");
            d = KnockbackMath.PushDirection(Vector3.down, Vector3.zero, Vector3.zero, Vector3.back, true);
            Assert.AreEqual(Vector3.back, d, "straight down flattens to nothing, so the fallback");
        }

        [Test]
        public void TypesMatchTheNearestAncestor()
        {
            var damage = Tag("Damage");
            var blunt = Tag("Blunt", damage);
            var hammer = Tag("Hammer", blunt);
            var slash = Tag("Slash", damage);
            var other = Tag("Other");

            Assert.AreEqual(0, KnockbackMath.Distance(blunt, blunt));
            Assert.AreEqual(2, KnockbackMath.Distance(hammer, damage));
            Assert.AreEqual(-1, KnockbackMath.Distance(damage, blunt), "a parent isn't its child");

            var profile = ScriptableObject.CreateInstance<KnockbackProfile>();
            made.Add(profile);
            profile.Setup(0.1f, 0.1f, 0.01f, 2f, new[]
            {
                new KnockbackProfile.TypeScale(damage, 0.5f, 0.5f),
                new KnockbackProfile.TypeScale(blunt, 2f, 3f),
            });

            profile.ScalesFor(hammer, out float k, out float s);
            Assert.AreEqual(2f, k, "Hammer is Blunt, the nearer entry");
            Assert.AreEqual(3f, s);
            profile.ScalesFor(slash, out k, out _);
            Assert.AreEqual(0.5f, k, "Slash falls back to Damage");
            profile.ScalesFor(other, out k, out s);
            Assert.AreEqual(1f, k, "no entry: 1");
            Assert.AreEqual(1f, s);
            profile.ScalesFor(null, out k, out _);
            Assert.AreEqual(1f, k, "untyped: 1");
        }

        [Test]
        public void BluntOutpushesSlashThroughAProfile()
        {
            var damage = Tag("Damage");
            var blunt = Tag("Blunt", damage);
            var slash = Tag("Slash", damage);
            var profile = ScriptableObject.CreateInstance<KnockbackProfile>();
            made.Add(profile);
            profile.Setup(0.15f, 0.1f, 0.02f, 2f, new[]
            {
                new KnockbackProfile.TypeScale(blunt, 1.6f, 1.5f),
                new KnockbackProfile.TypeScale(slash, 0.8f, 0.9f),
            });
            var b = profile.Evaluate(15f, 3f, blunt, 1f);
            var sl = profile.Evaluate(15f, 3f, slash, 1f);
            Assert.Greater(b.Speed, sl.Speed);
            Assert.Greater(b.Stagger, sl.Stagger);
        }
    }
}
