using UnityEngine;
using UnityEngine.UI;

namespace ShuYu
{
    public enum MonsterAnimState { Idle, Attack, Hit }

    /// <summary>逐帧精灵动画播放器：idle 循环；attack/hit 播完自动回 idle。挂在立绘 Image 上。</summary>
    public class SpriteAnimator : MonoBehaviour
    {
        Image _img;
        Sprite[] _idle, _attack, _hit;
        float _idleInterval, _attackInterval, _hitInterval;

        MonsterAnimState _state = MonsterAnimState.Idle;
        bool _playingOnce;   // 正在播一次性动画（attack/hit），播完回 idle
        int _frame;
        float _timer;

        public void Setup(Image img, Sprite[] idle, Sprite[] attack, Sprite[] hit,
            float idleInterval = 0.1f, float attackInterval = 0.09f, float hitInterval = 0.08f)
        {
            _img = img;
            _idle = idle;
            _attack = attack;
            _hit = hit;
            _idleInterval = idleInterval;
            _attackInterval = attackInterval;
            _hitInterval = hitInterval;
            _state = MonsterAnimState.Idle;
            _frame = 0;
            _timer = 0f;
            ApplyFrame();
        }

        /// <summary>切到指定动画。attack/hit 是一次性（播完回 idle），idle 立即切并循环。</summary>
        public void Play(MonsterAnimState state)
        {
            _state = state;
            _playingOnce = state != MonsterAnimState.Idle;
            _frame = 0;
            _timer = 0f;
            ApplyFrame();
        }

        void Update()
        {
            if (_img == null) return;
            var frames = CurrentFrames();
            if (frames == null || frames.Length <= 1) return;   // 单帧/空无需推进

            _timer += Time.deltaTime;
            if (_timer < CurrentInterval()) return;
            _timer = 0f;
            _frame++;

            if (_frame >= frames.Length)
            {
                _frame = 0;
                if (_playingOnce)
                {
                    // 一次性动画播完，回 idle
                    _playingOnce = false;
                    _state = MonsterAnimState.Idle;
                    frames = CurrentFrames();
                    if (frames == null || frames.Length == 0) return;
                }
            }
            ApplyFrame();
        }

        Sprite[] CurrentFrames()
        {
            switch (_state)
            {
                case MonsterAnimState.Attack: return _attack != null && _attack.Length > 0 ? _attack : _idle;
                case MonsterAnimState.Hit: return _hit != null && _hit.Length > 0 ? _hit : _idle;
                default: return _idle;
            }
        }

        float CurrentInterval()
        {
            switch (_state)
            {
                case MonsterAnimState.Attack: return _attackInterval;
                case MonsterAnimState.Hit: return _hitInterval;
                default: return _idleInterval;
            }
        }

        void ApplyFrame()
        {
            var frames = CurrentFrames();
            if (frames == null || frames.Length == 0) return;
            _frame = Mathf.Clamp(_frame, 0, frames.Length - 1);
            _img.sprite = frames[_frame];
        }
    }
}
