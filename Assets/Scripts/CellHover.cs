using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace ShuYu
{
    /// <summary>格子悬停检测：进入/离开时回调（用于出牌目标高亮）。</summary>
    public class CellHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public Action<bool> onHover;
        public void OnPointerEnter(PointerEventData e) { onHover?.Invoke(true); }
        public void OnPointerExit(PointerEventData e) { onHover?.Invoke(false); }
    }
}
