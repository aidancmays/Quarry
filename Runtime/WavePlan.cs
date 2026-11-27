using System;
using UnityEngine;

namespace Quarry
{
    /// <summary>
    /// How an encounter's waves go at a difficulty (0 easy, 1 hard): how many waves, how many in each (growing wave
    /// by wave), and the pause between them. Kept free of components so it can be tested.
    /// </summary>
    [Serializable]
    public struct WavePlan
    {
        [Tooltip("Waves on the easiest level (x) and the hardest (y).")]
        public Vector2Int waves;
        [Tooltip("In the first wave, easiest (x) to hardest (y).")]
        public Vector2Int firstWave;
        [Tooltip("More in each later wave than the one before.")]
        [Min(0)] public int growth;
        [Tooltip("Seconds between a wave being cleared and the next coming, easiest (x) to hardest (y).")]
        public Vector2 delay;

        public static WavePlan Default => new WavePlan
        {
            waves = new Vector2Int(2, 4),
            firstWave = new Vector2Int(2, 4),
            growth = 1,
            delay = new Vector2(6f, 3f),
        };

        public int WaveCount(float difficulty) =>
            Mathf.Max(1, Mathf.RoundToInt(Mathf.Lerp(waves.x, waves.y, Mathf.Clamp01(difficulty))));

        /// <summary>How many come in wave <paramref name="wave"/> (0 = the first).</summary>
        public int CountFor(int wave, float difficulty) =>
            Mathf.Max(1, Mathf.RoundToInt(Mathf.Lerp(firstWave.x, firstWave.y, Mathf.Clamp01(difficulty))) + Mathf.Max(0, wave) * growth);

        public float DelayFor(float difficulty) => Mathf.Max(0f, Mathf.Lerp(delay.x, delay.y, Mathf.Clamp01(difficulty)));

        /// <summary>Everything that comes over all the waves.</summary>
        public int Total(float difficulty)
        {
            int total = 0;
            for (int w = 0, n = WaveCount(difficulty); w < n; w++) total += CountFor(w, difficulty);
            return total;
        }
    }
}
