using UnityEngine;
using UnityEngine.UI;

namespace ShuYu
{
    /// <summary>颜色脉冲组件：在 from/to 两色间正弦往返 lerp，用于核心金色脉冲发光（自包含，随 Build 重建无副作用）。</summary>
    public class Pulse : MonoBehaviour
    {
        Graphic _target;
        Color _from, _to;
        float _speed = 2f;

        public void Setup(Graphic target, Color from, Color to, float speed = 2f)
        {
            _target = target;
            _from = from;
            _to = to;
            _speed = speed;
        }

        void Update()
        {
            if (_target == null) return;
            float t = (Mathf.Sin(Time.time * _speed) + 1f) * 0.5f;
            _target.color = Color.Lerp(_from, _to, t);
        }
    }
}
