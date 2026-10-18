using UnityEngine;

namespace ShuYu
{
    /// <summary>高危数字颤抖：值越高抖得越厉害（SPEC 13.4：8 明显 / 9 剧烈）。挂数字文本上，随 Build 重建无副作用。</summary>
    public class NumberShake : MonoBehaviour
    {
        float _amp;
        Vector3 _base;

        public void Setup(float amp)
        {
            _amp = amp;
            _base = transform.localPosition;
        }

        void Update()
        {
            if (_amp <= 0f) return;
            transform.localPosition = _base + new Vector3(
                Mathf.Sin(Time.time * 40f) * _amp,
                Mathf.Sin(Time.time * 47f + 1.7f) * _amp, 0f);
        }
    }
}
