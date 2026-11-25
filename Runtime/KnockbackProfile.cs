using System;
using System.Collections.Generic;
using GameplayTags;
using UnityEngine;

namespace Quarry
{
    /// <summary>
    /// How a kind of creature takes hits: how far damage pushes it, how long it staggers, and how much more or less
    /// of both each damage type does (a skeleton shrugs off a stab and flies apart from a hammer). A type counts as
    /// its parents, so an entry for Damage covers every damage type without one of its own; untyped hits and types
    /// with no entry get 1.
    /// </summary>
    [CreateAssetMenu(menuName = "Quarry/Knockback Profile", fileName = "Knockback Profile")]
    public class KnockbackProfile : ScriptableObject
    {
        [Serializable]
        public struct TypeScale
        {
            public GameplayTag type;
            [Tooltip("Multiplies the push.")]
            [Min(0f)] public float knockback;
            [Tooltip("Multiplies the stagger.")]
            [Min(0f)] public float stagger;

            public TypeScale(GameplayTag type, float knockback, float stagger)
            {
                this.type = type;
                this.knockback = knockback;
                this.stagger = stagger;
            }
        }

        public const float DefaultDamageToKnockback = 0.15f;
        public const float DefaultBaseStagger = 0.12f;
        public const float DefaultStaggerPerDamage = 0.015f;
        public const float DefaultMaxStagger = 1.5f;

        [Tooltip("Extra impulse (N s) per point of damage, on top of the hit's own knockback.")]
        [Min(0f)] [SerializeField] float damageToKnockback = DefaultDamageToKnockback;
        [Tooltip("Seconds every hit staggers for, before damage and type.")]
        [Min(0f)] [SerializeField] float baseStagger = DefaultBaseStagger;
        [Tooltip("More seconds of stagger per point of damage.")]
        [Min(0f)] [SerializeField] float staggerPerDamage = DefaultStaggerPerDamage;
        [Tooltip("Longest a single hit staggers for.")]
        [Min(0f)] [SerializeField] float maxStagger = DefaultMaxStagger;
        [SerializeField] List<TypeScale> types = new List<TypeScale>();

        public float DamageToKnockback => damageToKnockback;
        public float BaseStagger => baseStagger;
        public float StaggerPerDamage => staggerPerDamage;
        public float MaxStagger => maxStagger;
        public IReadOnlyList<TypeScale> Types => types;

        /// <summary>The knockback and stagger multipliers for a damage type (1 and 1 for none).</summary>
        public void ScalesFor(GameplayTag type, out float knockback, out float stagger)
        {
            if (KnockbackMath.TryMatch(type, types, t => t.type, out TypeScale match))
            {
                knockback = match.knockback;
                stagger = match.stagger;
                return;
            }
            knockback = 1f;
            stagger = 1f;
        }

        /// <summary>What a hit does to something with this profile and <paramref name="poise"/>.</summary>
        public KnockbackMath.Result Evaluate(float damage, float knockback, GameplayTag type, float poise)
        {
            ScalesFor(type, out float typeKnockback, out float typeStagger);
            return KnockbackMath.Evaluate(damage, knockback, typeKnockback, typeStagger, poise,
                damageToKnockback, baseStagger, staggerPerDamage, maxStagger);
        }

        /// <summary>The defaults, for a Knockback without a profile.</summary>
        public static KnockbackMath.Result EvaluateDefault(float damage, float knockback, float poise) =>
            KnockbackMath.Evaluate(damage, knockback, 1f, 1f, poise,
                DefaultDamageToKnockback, DefaultBaseStagger, DefaultStaggerPerDamage, DefaultMaxStagger);

        /// <summary>For setting profiles up from code (editor builders, tests).</summary>
        public void Setup(float damageToKnockback, float baseStagger, float staggerPerDamage, float maxStagger, IEnumerable<TypeScale> scales)
        {
            this.damageToKnockback = damageToKnockback;
            this.baseStagger = baseStagger;
            this.staggerPerDamage = staggerPerDamage;
            this.maxStagger = maxStagger;
            types = new List<TypeScale>(scales ?? Array.Empty<TypeScale>());
        }
    }
}
