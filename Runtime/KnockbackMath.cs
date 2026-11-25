using System;
using System.Collections.Generic;
using GameplayTags;
using UnityEngine;

namespace Quarry
{
    /// <summary>The numbers behind Knockback, kept free of components so they can be tested.</summary>
    public static class KnockbackMath
    {
        /// <summary>How a hit moves something.</summary>
        public struct Result
        {
            /// <summary>N s, after damage and type.</summary>
            public float Impulse;
            /// <summary>m/s it is sent off at (Impulse over Poise).</summary>
            public float Speed;
            /// <summary>Seconds it can't act.</summary>
            public float Stagger;
        }

        /// <summary>
        /// Impulse = (the hit's knockback + damage x Damage To Knockback) x the type's knockback multiplier, and speed
        /// is that over Poise. Stagger = (Base Stagger + damage x Stagger Per Damage) x the type's stagger multiplier,
        /// over Poise, no more than Max Stagger, and none for a hit with neither damage nor push.
        /// </summary>
        public static Result Evaluate(float damage, float knockback, float typeKnockback, float typeStagger, float poise,
            float damageToKnockback, float baseStagger, float staggerPerDamage, float maxStagger)
        {
            damage = Mathf.Max(0f, damage);
            knockback = Mathf.Max(0f, knockback);
            poise = Mathf.Max(0.01f, poise);
            float impulse = (knockback + damage * Mathf.Max(0f, damageToKnockback)) * Mathf.Max(0f, typeKnockback);
            float stagger = 0f;
            if (damage > 0f || knockback > 0f)
            {
                stagger = (Mathf.Max(0f, baseStagger) + damage * Mathf.Max(0f, staggerPerDamage)) * Mathf.Max(0f, typeStagger) / poise;
                stagger = Mathf.Min(stagger, Mathf.Max(0f, maxStagger));
            }
            return new Result { Impulse = impulse, Speed = impulse / poise, Stagger = stagger };
        }

        /// <summary>A push dying away: exponentially, by <paramref name="damping"/> per second.</summary>
        public static Vector3 Decay(Vector3 velocity, float damping, float deltaTime) =>
            velocity * Mathf.Exp(-Mathf.Max(0f, damping) * Mathf.Max(0f, deltaTime));

        /// <summary>
        /// Which way a hit pushes: its direction, else away from where it landed, else <paramref name="fallback"/>.
        /// Flattened onto the ground when <paramref name="flat"/>. Unit length, or zero if there is no way at all.
        /// </summary>
        public static Vector3 PushDirection(Vector3 direction, Vector3 point, Vector3 position, Vector3 fallback, bool flat)
        {
            Vector3 d = direction;
            if (flat) d.y = 0f;
            if (d.sqrMagnitude < 1e-8f)
            {
                d = position - point;
                if (flat) d.y = 0f;
            }
            if (d.sqrMagnitude < 1e-8f)
            {
                d = fallback;
                if (flat) d.y = 0f;
            }
            return d.sqrMagnitude < 1e-8f ? Vector3.zero : d.normalized;
        }

        /// <summary>
        /// Steps from <paramref name="type"/> up its parents to <paramref name="ancestor"/>: 0 = the same tag,
        /// 1 = its parent, and so on. -1 = not one of them.
        /// </summary>
        public static int Distance(GameplayTag type, GameplayTag ancestor)
        {
            if (type == null || ancestor == null) return -1;
            GameplayTag tag = type;
            for (int steps = 0; tag != null && steps < 32; steps++) // 32: a parent loop made by mistake
            {
                if (tag == ancestor) return steps;
                tag = tag.Parent;
            }
            return -1;
        }

        /// <summary>
        /// The entry whose tag is nearest <paramref name="type"/> up its tree (the tag itself, else its parent, and
        /// so on). False = none applies.
        /// </summary>
        public static bool TryMatch<T>(GameplayTag type, IReadOnlyList<T> entries, Func<T, GameplayTag> tagOf, out T match)
        {
            match = default;
            if (type == null || entries == null) return false;
            int best = int.MaxValue;
            for (int i = 0; i < entries.Count; i++)
            {
                int d = Distance(type, tagOf(entries[i]));
                if (d < 0 || d >= best) continue;
                best = d;
                match = entries[i];
            }
            return best != int.MaxValue;
        }
    }
}
