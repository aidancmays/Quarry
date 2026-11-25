using System;
using GameplayTags;
using Unity.Netcode;
using UnityEngine;

namespace Quarry
{
    /// <summary>
    /// How much more punishment something can take. The server spends it (<see cref="ServerApply"/>); every machine
    /// sees the amount and hears when it changes and when it dies. While dead it holds the Dead Tag (a TagHolder is
    /// added if there is none) and takes no more damage. A Knockback next to it is pushed by each hit.
    ///
    /// Works unspawned too (offline, tests): changes then happen locally.
    /// </summary>
    [DisallowMultipleComponent]
    public class Health : NetworkBehaviour
    {
        [Min(1f)] [SerializeField] float maxHealth = 50f;
        [Tooltip("Held while dead, on every machine. Empty = none.")]
        [SerializeField] GameplayTag deadTag;

        readonly NetworkVariable<float> current = new(-1f);
        readonly NetworkVariable<float> max = new(-1f);

        float localCurrent = -1f;
        bool deadTagAdded;
        TagHolder tags;

        /// <summary>Server: damage it took (raised after the change, and after Died if it killed).</summary>
        public event Action<DamageInfo> Damaged;
        /// <summary>Every machine: the amount changed (previous, current).</summary>
        public event Action<float, float> Changed;
        /// <summary>Every machine: it reached zero.</summary>
        public event Action Died;
        /// <summary>Every machine: brought back from zero.</summary>
        public event Action Revived;

        /// <summary>Server: any Health took damage (for debug panels, hit sounds and the like).</summary>
        public static event Action<Health, DamageInfo> AnyDamaged;

        public float Max => IsSpawned && max.Value > 0f ? max.Value : maxHealth;

        public float Current
        {
            get
            {
                if (IsSpawned && current.Value >= 0f) return current.Value;
                return localCurrent >= 0f ? localCurrent : maxHealth;
            }
        }

        /// <summary>0..1 of Max left.</summary>
        public float Fraction => Max > 0f ? Mathf.Clamp01(Current / Max) : 0f;
        public bool IsDead => Current <= 0f;
        /// <summary>Time.time it last went down, on this machine. Negative = never.</summary>
        public float LastDamagedTime { get; private set; } = -1f;

        bool CanWrite => !IsSpawned || IsServer;

        protected virtual void Awake() => tags = GetComponent<TagHolder>();

        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                max.Value = maxHealth;
                current.Value = localCurrent >= 0f ? Mathf.Min(localCurrent, maxHealth) : maxHealth;
            }
            current.OnValueChanged += OnSynced;
            UpdateDeadTag();
        }

        public override void OnNetworkDespawn() => current.OnValueChanged -= OnSynced;

        /// <summary>
        /// Server: takes the damage (none while dead), then raises Damaged, which pushes a Knockback next to it.
        /// Returns the health actually lost.
        /// </summary>
        public float ServerApply(in DamageInfo damage)
        {
            if (!CanWrite || IsDead) return 0f;
            float before = Current;
            float after = Mathf.Max(0f, before - Mathf.Max(0f, damage.Amount));
            Write(after);
            Damaged?.Invoke(damage);
            AnyDamaged?.Invoke(this, damage);
            return before - after;
        }

        /// <summary>Server: gives back health (not to the dead: use <see cref="ServerRevive"/>).</summary>
        public void ServerHeal(float amount)
        {
            if (!CanWrite || IsDead || amount <= 0f) return;
            Write(Mathf.Min(Max, Current + amount));
        }

        /// <summary>Server: back from the dead (or just topped up) to this fraction of Max.</summary>
        public void ServerRevive(float fraction = 1f)
        {
            if (!CanWrite) return;
            Write(Mathf.Max(1e-3f, Max * Mathf.Clamp01(fraction)));
        }

        /// <summary>Server: a new Max (from a stat, a difficulty setting). Refill tops it up to the new Max.</summary>
        public void ServerSetMax(float value, bool refill)
        {
            if (!CanWrite) return;
            maxHealth = Mathf.Max(1f, value);
            if (IsSpawned) max.Value = maxHealth;
            Write(refill ? maxHealth : Mathf.Min(Current, maxHealth));
        }

        /// <summary>Server: straight to zero.</summary>
        public void ServerKill()
        {
            if (!CanWrite || IsDead) return;
            Write(0f);
        }

        void Write(float value)
        {
            if (IsSpawned)
            {
                current.Value = value; // OnSynced raises the events on the server too
                return;
            }
            float before = Current;
            localCurrent = value;
            Raise(before, value);
        }

        void OnSynced(float previous, float now) => Raise(previous < 0f ? Max : previous, now);

        void Raise(float previous, float now)
        {
            if (Mathf.Approximately(previous, now)) return;
            if (now < previous) LastDamagedTime = Time.time;
            Changed?.Invoke(previous, now);
            UpdateDeadTag();
            if (previous > 0f && now <= 0f) Died?.Invoke();
            else if (previous <= 0f && now > 0f) Revived?.Invoke();
        }

        void UpdateDeadTag()
        {
            if (deadTag == null) return;
            bool dead = IsDead;
            if (dead == deadTagAdded) return;
            if (tags == null) tags = TagHolder.On(this);
            deadTagAdded = dead;
            if (dead) tags.Add(deadTag);
            else tags.Remove(deadTag);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => AnyDamaged = null;
    }
}
