using UnityEngine;

namespace Quarry
{
    /// <summary>The look of a Health Bar without a Bar Prefab: a fill that shrinks from the right.</summary>
    public class PlainHealthBarView : MonoBehaviour, IHealthBarView
    {
        public RectTransform Fill;

        public void SetFraction(float fraction, bool instant)
        {
            if (Fill != null) Fill.anchorMax = new Vector2(Mathf.Clamp01(fraction), 1f);
        }
    }
}
