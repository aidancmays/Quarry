using System;
using System.Collections.Generic;
using UnityEngine;

namespace Quarry
{
    /// <summary>
    /// Which of a level's hand-placed spawn points to use, from its seed and how hard it is meant to be (0 easy, 1
    /// hard). Not every spawn point is used: harder levels use more of them, and are more likely to wake a spawn
    /// point's whole group when one of the group is picked. Kept free of components so it can be tested.
    /// </summary>
    public static class SpawnActivation
    {
        /// <summary>One spawn point to choose from.</summary>
        public struct Candidate
        {
            public Vector3 Position;
            /// <summary>Points sharing a group tend to be used together. Empty = no group.</summary>
            public string Group;
            /// <summary>How likely it is to be picked, against the others (0 = only through its group).</summary>
            public float Weight;
            /// <summary>Not used below this difficulty.</summary>
            public float MinDifficulty;
            /// <summary>Always used (at or above its Min Difficulty).</summary>
            public bool Always;
        }

        /// <summary>How difficulty turns into spawn points used.</summary>
        [Serializable]
        public struct Settings
        {
            [Tooltip("Share of the usable spawn points used, on the easiest level (x) and the hardest (y).")]
            public Vector2 fraction;
            [Tooltip("Chance each other point in a picked point's group is used too, easiest (x) to hardest (y).")]
            public Vector2 groupTendency;
            [Tooltip("Most points used on one level. 0 = no limit.")]
            [Min(0)] public int max;
            [Tooltip("Points closer than this to a start position are never used.")]
            [Min(0f)] public float clearOfStart;
            [Tooltip("A point picked on its own is never this close to another one in use (group mates may be).")]
            [Min(0f)] public float spacing;

            public static Settings Default => new Settings
            {
                fraction = new Vector2(0.25f, 0.8f),
                groupTendency = new Vector2(0.15f, 0.85f),
                max = 12,
                clearOfStart = 16f,
                spacing = 5f,
            };
        }

        /// <summary>How many of <paramref name="usable"/> points a level of this difficulty uses.</summary>
        public static int TargetCount(int usable, float difficulty, in Settings settings)
        {
            if (usable <= 0) return 0;
            float d = Mathf.Clamp01(difficulty);
            int count = Mathf.RoundToInt(usable * Mathf.Clamp01(Mathf.Lerp(settings.fraction.x, settings.fraction.y, d)));
            if (settings.max > 0) count = Mathf.Min(count, settings.max);
            return Mathf.Clamp(count, 0, usable);
        }

        /// <summary>Chance a picked point's group mates are used too.</summary>
        public static float GroupTendency(float difficulty, in Settings settings) =>
            Mathf.Clamp01(Mathf.Lerp(settings.groupTendency.x, settings.groupTendency.y, Mathf.Clamp01(difficulty)));

        /// <summary>
        /// The indices of the candidates to use. Always points come first; then points are drawn by weight until the
        /// target count is reached, and each draw may pull in the rest of its group (by the group tendency). The
        /// same candidates, difficulty and random sequence always give the same answer.
        /// </summary>
        public static List<int> Choose(IReadOnlyList<Candidate> candidates, IReadOnlyList<Vector3> starts, float difficulty,
            in Settings settings, System.Random rng)
        {
            var used = new List<int>();
            if (candidates == null || candidates.Count == 0) return used;
            difficulty = Mathf.Clamp01(difficulty);

            var usable = new List<int>();
            for (int i = 0; i < candidates.Count; i++)
                if (difficulty >= candidates[i].MinDifficulty && !Near(candidates[i].Position, starts, settings.clearOfStart))
                    usable.Add(i);

            int target = TargetCount(usable.Count, difficulty, settings);
            float tendency = GroupTendency(difficulty, settings);
            var taken = new HashSet<int>();

            foreach (int i in usable)
                if (candidates[i].Always) Take(i, used, taken);

            var pool = new List<int>();
            while (used.Count < target)
            {
                pool.Clear();
                float total = 0f;
                foreach (int i in usable)
                {
                    if (taken.Contains(i) || candidates[i].Weight <= 0f) continue;
                    if (Crowded(candidates, i, used, settings.spacing)) continue;
                    pool.Add(i);
                    total += candidates[i].Weight;
                }
                if (pool.Count == 0) break;

                int pick = pool[pool.Count - 1];
                double roll = rng.NextDouble() * total;
                foreach (int i in pool)
                {
                    roll -= candidates[i].Weight;
                    if (roll <= 0) { pick = i; break; }
                }
                Take(pick, used, taken);

                string group = candidates[pick].Group;
                if (string.IsNullOrEmpty(group)) continue;
                foreach (int i in usable)
                {
                    if (taken.Contains(i) || candidates[i].Group != group) continue;
                    if (settings.max > 0 && used.Count >= settings.max) break;
                    if (rng.NextDouble() < tendency) Take(i, used, taken);
                }
            }
            return used;
        }

        static void Take(int i, List<int> used, HashSet<int> taken)
        {
            if (taken.Add(i)) used.Add(i);
        }

        static bool Crowded(IReadOnlyList<Candidate> candidates, int i, List<int> used, float spacing)
        {
            if (spacing <= 0f) return false;
            foreach (int u in used)
            {
                Vector3 d = candidates[u].Position - candidates[i].Position;
                d.y = 0f;
                if (d.sqrMagnitude < spacing * spacing) return true;
            }
            return false;
        }

        static bool Near(Vector3 point, IReadOnlyList<Vector3> others, float distance)
        {
            if (others == null || distance <= 0f) return false;
            foreach (Vector3 o in others)
            {
                Vector3 d = o - point;
                d.y = 0f;
                if (d.sqrMagnitude < distance * distance) return true;
            }
            return false;
        }
    }
}
