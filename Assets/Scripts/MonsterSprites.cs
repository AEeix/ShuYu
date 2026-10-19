using System.Collections.Generic;
using UnityEngine;

namespace ShuYu
{
    /// <summary>一只怪物的三态逐帧动画（idle/attack/hit 各一组 Sprite + 帧间隔）。</summary>
    public struct MonsterAnimation
    {
        public Sprite[] idle, attack, hit;
        public float idleInterval, attackInterval, hitInterval;
        public bool Valid => idle != null && idle.Length > 0;
    }

    /// <summary>怪物立绘逐帧动画目录：从 Resources/Sprites 加载 frame_XXX.png，按怪物 key 缓存。</summary>
    public static class MonsterSprites
    {
        static readonly Dictionary<string, MonsterAnimation> _cache = new Dictionary<string, MonsterAnimation>();

        /// <summary>取某怪物的三态动画（key：flower / crab）。没有素材返回 Valid=false。</summary>
        public static MonsterAnimation Get(string key)
        {
            if (_cache.TryGetValue(key, out var a)) return a;

            a = new MonsterAnimation
            {
                idle = LoadFrames("Sprites/" + key + "_idle"),
                attack = LoadFrames("Sprites/" + key + "_attack"),
                hit = LoadFrames("Sprites/" + key + "_hit"),
            };

            // 帧间隔按 GIF 原始时长近似：flower ~100/80/90ms，crab ~140/120/90ms
            if (key == "crab")
            {
                a.idleInterval = 0.14f; a.attackInterval = 0.12f; a.hitInterval = 0.09f;
            }
            else
            {
                a.idleInterval = 0.10f; a.attackInterval = 0.08f; a.hitInterval = 0.09f;
            }

            _cache[key] = a;
            return a;
        }

        /// <summary>加载某文件夹下全部帧纹理，按文件名排序后转成 Sprite（frame_000 → frame_NNN）。</summary>
        static Sprite[] LoadFrames(string folder)
        {
            var texs = Resources.LoadAll<Texture2D>(folder);
            if (texs == null || texs.Length == 0) return null;

            System.Array.Sort(texs, (x, y) => string.CompareOrdinal(x.name, y.name));

            var sprites = new Sprite[texs.Length];
            for (int i = 0; i < texs.Length; i++)
            {
                var t = texs[i];
                sprites[i] = Sprite.Create(t, new Rect(0, 0, t.width, t.height), new Vector2(0.5f, 0.5f), 100f);
                sprites[i].name = t.name;
            }
            return sprites;
        }
    }
}
