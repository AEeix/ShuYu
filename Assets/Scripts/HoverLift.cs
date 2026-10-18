using UnityEngine;
using UnityEngine.EventSystems;

namespace ShuYu
{
    /// <summary>悬停抬起：鼠标进入时平滑上移，离开时还原（用于选职卡等）。</summary>
    public class HoverLift : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        RectTransform _rt;
        Vector2 _basePos;
        bool _hovered;
        float _lift;

        void Awake()
        {
            _rt = (RectTransform)transform;
            _basePos = _rt.anchoredPosition;
        }

        public void OnPointerEnter(PointerEventData e) { _hovered = true; SfxPlayer.Instance?.Play(SfxType.Hover); }
        public void OnPointerExit(PointerEventData e) { _hovered = false; }

        void Update()
        {
            float target = _hovered ? 16f : 0f;
            _lift = Mathf.Lerp(_lift, target, Time.deltaTime * 12f);
            _rt.anchoredPosition = _basePos + new Vector2(0f, _lift);
        }
    }
}
