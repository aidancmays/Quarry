using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Quarry.Tests
{
    public class SpawnActivationTests
    {
        static List<SpawnActivation.Candidate> Row(int count, float apart, string group = null)
        {
            var list = new List<SpawnActivation.Candidate>();
            for (int i = 0; i < count; i++)
                list.Add(new SpawnActivation.Candidate { Position = new Vector3(i * apart, 0f, 50f), Weight = 1f, Group = group });
            return list;
        }

        static SpawnActivation.Settings Loose => new SpawnActivation.Settings
        {
            fraction = new Vector2(0.2f, 0.8f),
            groupTendency = new Vector2(0f, 1f),
            max = 0,
            clearOfStart = 0f,
            spacing = 0f,
        };

        [Test]
        public void HarderLevelsUseMoreSpawnPoints()
        {
            var points = Row(20, 10f);
            int easy = SpawnActivation.Choose(points, null, 0f, Loose, new System.Random(1)).Count;
            int hard = SpawnActivation.Choose(points, null, 1f, Loose, new System.Random(1)).Count;
            Assert.AreEqual(4, easy);
            Assert.AreEqual(16, hard);
        }

        [Test]
        public void SameSeedSamePoints()
        {
            var points = Row(20, 10f);
            Assert.AreEqual(SpawnActivation.Choose(points, null, 0.5f, Loose, new System.Random(9)),
                SpawnActivation.Choose(points, null, 0.5f, Loose, new System.Random(9)));
        }

        [Test]
        public void HardLevelsWakeWholeGroupsAndEasyOnesDont()
        {
            var points = new List<SpawnActivation.Candidate>();
            points.AddRange(Row(5, 2f, "crypt"));
            var settings = Loose;
            settings.fraction = new Vector2(0.2f, 0.2f); // one draw either way

            Assert.AreEqual(5, SpawnActivation.Choose(points, null, 1f, settings, new System.Random(3)).Count, "tendency 1: the whole group");
            Assert.AreEqual(1, SpawnActivation.Choose(points, null, 0f, settings, new System.Random(3)).Count, "tendency 0: just the one");
        }

        [Test]
        public void StartSpacingDifficultyAndAlwaysAreRespected()
        {
            var points = Row(10, 1f);
            points[0] = new SpawnActivation.Candidate { Position = Vector3.zero, Weight = 1f }; // at the start
            points[5] = new SpawnActivation.Candidate { Position = new Vector3(5f, 0f, 50f), Weight = 0f, Always = true };
            points[9] = new SpawnActivation.Candidate { Position = new Vector3(9f, 0f, 50f), Weight = 1f, MinDifficulty = 0.9f };

            var settings = Loose;
            settings.clearOfStart = 5f;
            settings.spacing = 3f;
            settings.fraction = new Vector2(1f, 1f);
            var used = SpawnActivation.Choose(points, new[] { Vector3.zero }, 0.5f, settings, new System.Random(4));

            Assert.IsFalse(used.Contains(0), "too close to the start");
            Assert.IsTrue(used.Contains(5), "always");
            Assert.IsFalse(used.Contains(9), "harder levels only");
            for (int a = 0; a < used.Count; a++)
                for (int b = a + 1; b < used.Count; b++)
                    Assert.GreaterOrEqual(Mathf.Abs(points[used[a]].Position.x - points[used[b]].Position.x), 3f, "spaced apart");
        }

        [Test]
        public void MaxCapsTheCount()
        {
            var settings = Loose;
            settings.max = 3;
            Assert.AreEqual(3, SpawnActivation.Choose(Row(20, 10f), null, 1f, settings, new System.Random(2)).Count);
        }

        [Test]
        public void WavesGrowWithDifficulty()
        {
            var plan = WavePlan.Default;
            Assert.AreEqual(2, plan.WaveCount(0f));
            Assert.AreEqual(4, plan.WaveCount(1f));
            Assert.AreEqual(2, plan.CountFor(0, 0f));
            Assert.AreEqual(4 + 3, plan.CountFor(3, 1f), "first wave of 4, one more each wave");
            Assert.Greater(plan.DelayFor(0f), plan.DelayFor(1f), "less breathing room when hard");
            Assert.AreEqual(2 + 3, plan.Total(0f));
        }
    }
}
