using Cleave;
using UnityEngine;

namespace Quarry.Cleave
{
    /// <summary>
    /// Cleave swings that land on this Hittable become damage: its Health takes them (and so does a Knockback next
    /// to it), or with no Health the Knockback alone is pushed. The weapon's damage type comes along. Swings pass
    /// through it once its Health is dead.
    /// </summary>
    [RequireComponent(typeof(Hittable))]
    [DisallowMultipleComponent]
    public class CleaveDamage : MonoBehaviour
    {
        [Tooltip("Multiplies the damage swings do to it.")]
        [Min(0f)] [SerializeField] float damageScale = 1f;
        [Tooltip("Multiplies the swings' own knockback (damage still adds its share through the Knockback Profile).")]
        [Min(0f)] [SerializeField] float knockbackScale = 1f;
        [Tooltip("Stop swings landing on it once it is dead (and again when revived).")]
        [SerializeField] bool unhittableWhenDead = true;

        Hittable hittable;
        Health health;
        Knockback knockback;

        void Awake()
        {
            hittable = GetComponent<Hittable>();
            health = GetComponent<Health>();
            knockback = GetComponent<Knockback>();
            hittable.Struck += OnStruck;
            if (health != null)
            {
                health.Died += OnDied;
                health.Revived += OnRevived;
            }
        }

        void OnDestroy()
        {
            if (hittable != null) hittable.Struck -= OnStruck;
            if (health != null)
            {
                health.Died -= OnDied;
                health.Revived -= OnRevived;
            }
        }

        void OnStruck(MeleeHit hit)
        {
            DamageInfo damage = ToDamage(hit);
            damage.Amount *= damageScale;
            damage.Knockback *= knockbackScale;
            if (health != null) health.ServerApply(damage);
            else if (knockback != null) knockback.Apply(damage);
        }

        void OnDied()
        {
            if (unhittableWhenDead) hittable.CanBeHit = false;
        }

        void OnRevived()
        {
            if (unhittableWhenDead) hittable.CanBeHit = true;
        }

        /// <summary>A Cleave hit as Quarry damage.</summary>
        public static DamageInfo ToDamage(MeleeHit hit) => new DamageInfo
        {
            Amount = hit.Damage,
            Knockback = hit.Knockback,
            Type = hit.DamageType,
            Point = hit.Point,
            Direction = hit.Direction,
            Source = hit.Attacker,
        };
    }
}
