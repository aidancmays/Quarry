using UnityEngine;
using UnityEngine.UI;

namespace Quarry
{
    /// <summary>
    /// Draws a bar's fill. Put it on the root of a Health Bar's Bar Prefab to give the bar your own look.
    /// </summary>
    public interface IHealthBarView
    {
        /// <summary>0..1 health left. <paramref name="instant"/>: jump there (first shown) rather than animate.</summary>
        void SetFraction(float fraction, bool instant);
    }

    /// <summary>
    /// A health bar that floats over a Health's head. It is drawn on a shared screen overlay (so the frame's post
    /// effects leave it readable and it always faces the camera), at the point above the Anchor, smaller the further
    /// away it is. Hidden until the Health is first hurt, it shows for Show Time after each hit and then fades;
    /// hidden on death, beyond Max Distance, and while something stands between it and the camera.
    ///
    /// The bar is a copy of Bar Prefab (a RectTransform with an IHealthBarView on it); without one it is a plain bar.
    /// Every machine shows its own; nothing is networked.
    /// </summary>
    [DisallowMultipleComponent]
    public class HealthBar : MonoBehaviour
    {
        [Tooltip("Empty = the Health on this object or above it.")]
        [SerializeField] Health health;
        [Tooltip("What it floats above (the head). Empty = this object.")]
        [SerializeField] Transform anchor;
        [Tooltip("From the anchor, in world space.")]
        [SerializeField] Vector3 offset = new Vector3(0f, 0.45f, 0f);
        [Tooltip("The bar's look: a prefab with a RectTransform and an IHealthBarView. Empty = a plain bar.")]
        [SerializeField] RectTransform barPrefab;
        [Tooltip("Size in pixels (at 1080p) at Reference Distance.")]
        [SerializeField] Vector2 size = new Vector2(110f, 11f);
        [Tooltip("Metres away it is drawn at Size; nearer it grows (up to Max Scale), further it shrinks (down to Min Scale).")]
        [Min(0.1f)] [SerializeField] float referenceDistance = 5f;
        [Min(0f)] [SerializeField] float minScale = 0.45f;
        [Min(0f)] [SerializeField] float maxScale = 1.25f;
        [Tooltip("Not shown further away than this.")]
        [Min(0f)] [SerializeField] float maxDistance = 30f;
        [Tooltip("Hidden while at full health.")]
        [SerializeField] bool hideWhenFull = true;
        [Tooltip("Seconds it stays after a hit before fading. 0 = always shown once hurt.")]
        [Min(0f)] [SerializeField] float showTime = 4f;
        [Min(0.01f)] [SerializeField] float fadeTime = 1f;
        [Tooltip("Hidden while something in Occluders is between it and the camera.")]
        [SerializeField] bool needsLineOfSight = true;
        [SerializeField] LayerMask occluders = Physics.DefaultRaycastLayers;

        const int OverlayOrder = -10; // under the HUD and menus

        static Canvas overlay;
        RectTransform bar;
        CanvasGroup group;
        IHealthBarView view;
        bool shown;
        float seen = -1f; // Time.time it was last hurt (or first shown)

        public Health Health => health;
        public float Alpha => group != null ? group.alpha : 0f;

        void Awake()
        {
            if (health == null) health = GetComponentInParent<Health>();
            if (anchor == null) anchor = transform;
        }

        void OnEnable()
        {
            if (health != null) health.Changed += OnChanged;
        }

        void OnDisable()
        {
            if (health != null) health.Changed -= OnChanged;
            Hide();
        }

        void OnDestroy()
        {
            if (bar != null) Destroy(bar.gameObject);
        }

        void OnChanged(float previous, float current)
        {
            if (current < previous) seen = Time.time;
        }

        void LateUpdate()
        {
            float alpha = TargetAlpha();
            Camera cam = alpha > 0f ? Camera.main : null;
            if (cam == null)
            {
                Hide();
                return;
            }

            Vector3 world = anchor.position + offset;
            Vector3 screen = cam.WorldToScreenPoint(world);
            float distance = Vector3.Distance(cam.transform.position, world);
            if (screen.z <= 0f || (maxDistance > 0f && distance > maxDistance) || (needsLineOfSight && Blocked(cam.transform.position, world)))
            {
                Hide();
                return;
            }

            if (bar == null) Build();
            if (!shown)
            {
                bar.gameObject.SetActive(true);
                view?.SetFraction(health.Fraction, true);
                shown = true;
            }
            else view?.SetFraction(health.Fraction, false);

            float scale = Mathf.Clamp(referenceDistance / Mathf.Max(0.01f, distance), minScale, maxScale) * Screen.height / 1080f;
            bar.position = new Vector3(screen.x, screen.y, 0f);
            bar.sizeDelta = size * scale;
            group.alpha = alpha;
        }

        float TargetAlpha()
        {
            if (health == null || health.IsDead) return 0f;
            if (hideWhenFull && health.Fraction >= 0.9999f) return 0f;
            if (seen < 0f)
            {
                if (hideWhenFull) return 0f; // not hurt yet
                return 1f;
            }
            if (showTime <= 0f) return 1f;
            float since = Time.time - seen - showTime;
            return since <= 0f ? 1f : Mathf.Clamp01(1f - since / fadeTime);
        }

        bool Blocked(Vector3 from, Vector3 to)
        {
            Vector3 d = to - from;
            float length = d.magnitude;
            if (length < 0.01f) return false;
            int count = Physics.RaycastNonAlloc(from, d / length, hits, length, occluders, QueryTriggerInteraction.Ignore);
            Transform own = health.transform;
            for (int i = 0; i < count; i++)
            {
                Transform hit = hits[i].transform;
                if (hit.IsChildOf(own)) continue;
                // The camera's own body (a first person player) doesn't count.
                if (Camera.main != null && Camera.main.transform.IsChildOf(hit.root)) continue;
                return true;
            }
            return false;
        }

        static readonly RaycastHit[] hits = new RaycastHit[8];

        void Hide()
        {
            if (bar != null && shown) bar.gameObject.SetActive(false);
            shown = false;
        }

        void Build()
        {
            Canvas canvas = Overlay();
            if (barPrefab != null)
            {
                bar = Instantiate(barPrefab, canvas.transform, false);
                view = bar.GetComponent<IHealthBarView>();
            }
            else
            {
                bar = PlainBar(canvas.transform, out var plain);
                view = plain;
            }
            bar.name = $"{name} Health";
            bar.anchorMin = bar.anchorMax = Vector2.zero;
            bar.pivot = new Vector2(0.5f, 0f);
            group = bar.GetComponent<CanvasGroup>();
            if (group == null) group = bar.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;
            bar.gameObject.SetActive(false);
        }

        static Canvas Overlay()
        {
            if (overlay != null) return overlay;
            var go = new GameObject("Quarry Health Bars", typeof(RectTransform));
            DontDestroyOnLoad(go);
            overlay = go.AddComponent<Canvas>();
            overlay.renderMode = RenderMode.ScreenSpaceOverlay;
            overlay.sortingOrder = OverlayOrder;
            return overlay;
        }

        static RectTransform PlainBar(Transform parent, out PlainHealthBarView plain)
        {
            var root = new GameObject("Health Bar", typeof(RectTransform), typeof(Image));
            var rt = (RectTransform)root.transform;
            rt.SetParent(parent, false);
            var back = root.GetComponent<Image>();
            back.color = new Color(0f, 0f, 0f, 0.6f);
            back.raycastTarget = false;

            var fill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            var frt = (RectTransform)fill.transform;
            frt.SetParent(rt, false);
            frt.anchorMin = Vector2.zero;
            frt.anchorMax = Vector2.one;
            frt.offsetMin = frt.offsetMax = Vector2.zero;
            var image = fill.GetComponent<Image>();
            image.color = new Color(0.8f, 0.15f, 0.1f, 1f);
            image.raycastTarget = false;

            plain = root.AddComponent<PlainHealthBarView>();
            plain.Fill = frt;
            return rt;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => overlay = null;
    }
}
