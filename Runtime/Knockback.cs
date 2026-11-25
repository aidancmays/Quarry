using System;
using GameplayTags;
using UnityEngine;

namespace Quarry
{
    /// <summary>
    /// Server: hits push this and stagger it, by how much damage they did and of what type (its Knockback Profile),
    /// over its Poise. A non-kinematic Rigidbody here gets the push as a change of velocity. Anything else (a
    /// CharacterController, a NavMeshAgent, a kinematic mover) reads <see cref="Velocity"/>, which dies away by
    /// Damping, adds it to its own movement, and pauses its AI while <see cref="IsStaggered"/>.
    ///
    /// Takes every hit from a Health next to it, or call <see cref="Apply(in DamageInfo)"/> yourself.
    /// </summary>
    [DisallowMultipleComponent]
    public class Knockback : MonoBehaviour
    {
        [SerializeField] KnockbackProfile profile;
        [Tooltip("How hard it is to move and stagger: push speed is impulse over this, and so is stagger time. Roughly its mass in tens of kilograms.")]
        [Min(0.01f)] [SerializeField] float poise = 1f;
        [Tooltip("How fast a push dies away (per second), for movers that read Velocity.")]
        [Min(0f)] [SerializeField] float damping = 7f;
        [Tooltip("Fastest it can be sent off (m/s).")]
        [Min(0f)] [SerializeField] float maxSpeed = 14f;
        [Tooltip("Push along the ground only (before Lift).")]
        [SerializeField] bool horizontal = true;
        [Tooltip("Upward m/s added per m/s of push, so big hits lift it a little.")]
        [Min(0f)] [SerializeField] float lift = 0.1f;
        [Tooltip("Take every hit from the Health next to it.")]
        [SerializeField] bool fromHealth = true;

        Rigidbody body;
        Health health;

        /// <summary>The push still on it (m/s), for movers that move themselves. Stays zero for Rigidbodies.</summary>
        public Vector3 Velocity { get; private set; }
        /// <summary>Seconds of stagger left.</summary>
        public float StaggerLeft { get; private set; }
        public bool IsStaggered => StaggerLeft > 0f;
        public KnockbackProfile Profile { get => profile; set => profile = value; }
        public float Poise { get => poise; set => poise = Mathf.Max(0.01f, value); }

        /// <summary>Server: a hit pushed it (the push, in m/s, and the stagger it caused, in seconds).</summary>
        public event Action<Vector3, float> Knocked;

        void Awake()
        {
            body = GetComponent<Rigidbody>();
            health = GetComponent<Health>();
        }

        void OnEnable()
        {
            if (fromHealth && health != null) health.Damaged += OnDamaged;
        }

        void OnDisable()
        {
            if (health != null) health.Damaged -= OnDamaged;
        }

        void OnDamaged(DamageInfo damage) => Apply(damage);

        public KnockbackMath.Result Apply(in DamageInfo damage) =>
            Apply(damage.Amount, damage.Knockback, damage.Type, damage.Direction, damage.Point);

        /// <summary>Pushes and staggers it as a hit would.</summary>
        public KnockbackMath.Result Apply(float damage, float knockback, GameplayTag type, Vector3 direction, Vector3 point)
        {
            var result = profile != null
                ? profile.Evaluate(damage, knockback, type, poise)
                : KnockbackProfile.EvaluateDefault(damage, knockback, poise);
            result.Speed = Mathf.Min(result.Speed, maxSpeed);

            Vector3 push = KnockbackMath.PushDirection(direction, point, transform.position, -transform.forward, horizontal) * result.Speed;
            if (lift > 0f) push.y += result.Speed * lift;

            if (body != null && !body.isKinematic)
            {
                body.AddForce(push, ForceMode.VelocityChange);
            }
            else
            {
                Velocity += push;
                if (Velocity.magnitude > maxSpeed) Velocity = Velocity.normalized * maxSpeed;
            }
            StaggerLeft = Mathf.Max(StaggerLeft, result.Stagger);
            Knocked?.Invoke(push, result.Stagger);
            return result;
        }

        /// <summary>Drops any push and stagger still on it.</summary>
        public void Clear()
        {
            Velocity = Vector3.zero;
            StaggerLeft = 0f;
        }

        /// <summary>For movers: it landed or hit a wall, so take away the part of the push going into it.</summary>
        public void Block(Vector3 normal)
        {
            float into = Vector3.Dot(Velocity, normal);
            if (into < 0f) Velocity -= normal * into;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (StaggerLeft > 0f) StaggerLeft = Mathf.Max(0f, StaggerLeft - dt);
            if (Velocity != Vector3.zero)
            {
                Velocity = KnockbackMath.Decay(Velocity, damping, dt);
                if (Velocity.sqrMagnitude < 1e-4f) Velocity = Vector3.zero;
            }
        }
    }
}
