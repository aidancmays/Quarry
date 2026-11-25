using System.Collections.Generic;
using UnityEngine;

namespace Quarry
{
    /// <summary>
    /// Something that can be noticed from further away or less far than usual: a player sprinting is heard from
    /// further off, one crawling can get much closer. Put it on (or above) whatever Senses look for.
    /// </summary>
    public interface INoiseSource
    {
        /// <summary>Multiplies a sense's radius for this target: 1 = normal, 2 = from twice as far, 0 = unnoticed.</summary>
        float NoiseMultiplier { get; }
    }

    /// <summary>The sums behind Senses, kept free of components so they can be tested.</summary>
    public static class SenseMath
    {
        /// <summary>How near a target has to be: the base radius times its noise multiplier.</summary>
        public static float Radius(float baseRadius, float multiplier) => Mathf.Max(0f, baseRadius) * Mathf.Max(0f, multiplier);

        /// <summary>
        /// Within <paramref name="radius"/> across the ground, and no more than <paramref name="heightTolerance"/> above
        /// or below (0 or less = any height).
        /// </summary>
        public static bool InRange(Vector3 sensor, Vector3 target, float radius, float heightTolerance)
        {
            Vector3 offset = target - sensor;
            if (heightTolerance > 0f && Mathf.Abs(offset.y) > heightTolerance) return false;
            offset.y = 0f;
            return offset.sqrMagnitude <= radius * radius;
        }
    }

    /// <summary>
    /// Notices targets that come within a radius, scaled by each target's INoiseSource (if it has one), and
    /// optionally only when nothing is in the way. Ask it from your AI (usually on the server): <see cref="Notices"/>
    /// or <see cref="Nearest{T}"/>. The editor draws the radius, and fainter rings at the Preview Multipliers.
    /// </summary>
    public class Senses : MonoBehaviour
    {
        [Tooltip("Metres a target with no noise multiplier (or one of 1) is noticed from.")]
        [Min(0f)] [SerializeField] float radius = 6f;
        [Tooltip("Ignore targets more than this far above or below. 0 = any height.")]
        [Min(0f)] [SerializeField] float heightTolerance = 4f;
        [Tooltip("Where it senses from. Empty = this object.")]
        [SerializeField] Transform eyes;
        [Tooltip("Only notice targets nothing in Blockers stands in front of.")]
        [SerializeField] bool needsLineOfSight;
        [SerializeField] LayerMask blockers = Physics.DefaultRaycastLayers;
        [Tooltip("Editor only: fainter rings drawn at these multiples of the radius (how near sneaking or how far running gets noticed).")]
        [SerializeField] float[] previewMultipliers = { 0.35f, 0.6f, 1.6f };

        public float Radius { get => radius; set => radius = Mathf.Max(0f, value); }
        public Transform Eyes => eyes != null ? eyes : transform;

        /// <summary>A target's noise multiplier (1 if it has no INoiseSource).</summary>
        public static float MultiplierOf(Component target)
        {
            if (target == null) return 0f;
            var source = target.GetComponentInParent<INoiseSource>();
            return source != null ? source.NoiseMultiplier : 1f;
        }

        /// <summary>How near this target must come to be noticed.</summary>
        public float RadiusFor(Component target) => SenseMath.Radius(radius, MultiplierOf(target));

        /// <summary>True if the target is near enough (for its noise) and, if needed, in sight.</summary>
        public bool Notices(Component target)
        {
            if (target == null) return false;
            Vector3 from = Eyes.position;
            Vector3 to = target.transform.position;
            if (!SenseMath.InRange(from, to, RadiusFor(target), heightTolerance)) return false;
            return !needsLineOfSight || InSight(target);
        }

        /// <summary>Nothing in Blockers between the eyes and the target (colliders of either don't count).</summary>
        public bool InSight(Component target)
        {
            Vector3 from = Eyes.position;
            Vector3 to = target.transform.position + Vector3.up * 0.5f;
            int count = Physics.RaycastNonAlloc(from, (to - from).normalized, hits, Vector3.Distance(from, to), blockers, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                Transform hit = hits[i].transform;
                if (hit.IsChildOf(transform) || hit.IsChildOf(target.transform)) continue;
                return false;
            }
            return true;
        }

        /// <summary>The nearest target it notices, or null.</summary>
        public T Nearest<T>(IReadOnlyList<T> targets) where T : Component
        {
            T best = null;
            float bestDistance = float.MaxValue;
            Vector3 from = Eyes.position;
            for (int i = 0; i < targets.Count; i++)
            {
                T target = targets[i];
                if (target == null || !Notices(target)) continue;
                float d = (target.transform.position - from).sqrMagnitude;
                if (d >= bestDistance) continue;
                bestDistance = d;
                best = target;
            }
            return best;
        }

        static readonly RaycastHit[] hits = new RaycastHit[8];

        void OnDrawGizmosSelected()
        {
            Vector3 centre = Eyes.position;
            Gizmos.color = new Color(1f, 0.55f, 0.1f, 0.9f);
            DrawRing(centre, radius);
            if (previewMultipliers == null) return;
            Gizmos.color = new Color(1f, 0.55f, 0.1f, 0.35f);
            foreach (float multiplier in previewMultipliers)
                DrawRing(centre, SenseMath.Radius(radius, multiplier));
        }

        /// <summary>A flat circle of gizmo lines (for drawing radii in OnDrawGizmos).</summary>
        public static void DrawRing(Vector3 centre, float ringRadius, int segments = 48)
        {
            if (ringRadius <= 0f) return;
            Vector3 previous = centre + new Vector3(ringRadius, 0f, 0f);
            for (int i = 1; i <= segments; i++)
            {
                float a = i * Mathf.PI * 2f / segments;
                Vector3 next = centre + new Vector3(Mathf.Cos(a) * ringRadius, 0f, Mathf.Sin(a) * ringRadius);
                Gizmos.DrawLine(previous, next);
                previous = next;
            }
        }
    }
}
