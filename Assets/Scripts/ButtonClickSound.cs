using UnityEngine;
using UnityEngine.UI;

namespace ShuYu
{
    /// <summary>按钮点击音：挂在 Button 上，运行时自动接点击音（组件可序列化，场景里的按钮也能生效）。</summary>
    public class ButtonClickSound : MonoBehaviour
    {
        void Awake()
        {
            var btn = GetComponent<Button>();
            if (btn != null)
                btn.onClick.AddListener(() => { if (SfxPlayer.Instance != null) SfxPlayer.Instance.Play(SfxType.Click); });
        }
    }
}
