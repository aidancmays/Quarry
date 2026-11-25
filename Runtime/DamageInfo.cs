using GameplayTags;
using UnityEngine;

namespace Quarry
{
    /// <summary>One lot of damage arriving at something, as the server applies it.</summary>
    public struct DamageInfo
    {
        /// <summary>Health it takes away.</summary>
        public float Amount;
        /// <summary>The hit's own push, in N s, before Knockback adds to it for the damage and its type.</summary>
        public float Knockback;
        /// <summary>What kind of damage it is (Damage.Slash, Damage.Blunt...). Null = untyped.</summary>
        public GameplayTag Type;
        /// <summary>Where it landed, in world space.</summary>
        public Vector3 Point;
        /// <summary>Which way it pushes (world space). Zero = away from Point.</summary>
        public Vector3 Direction;
        /// <summary>Who or what dealt it (an attacker, a trap). May be null.</summary>
        public Object Source;

        public DamageInfo(float amount, float knockback, GameplayTag type, Vector3 point, Vector3 direction, Object source = null)
        {
            Amount = amount;
            Knockback = knockback;
            Type = type;
            Point = point;
            Direction = direction;
            Source = source;
        }
    }
}
