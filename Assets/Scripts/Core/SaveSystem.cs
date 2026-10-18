using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace ShuYu
{
    /// <summary>存档数据：一次运行的状态（职业 / 血量 / 卡组 / 路线）。</summary>
    [Serializable]
    public class SaveData
    {
        public string classId;
        public int health;
        public int maxHealth;
        public List<RelicType> relics;
        public List<CardData> deck;
        public RouteMap route;
    }

    /// <summary>存档系统：JSON 文件读写（存 Application.persistentDataPath）。</summary>
    public static class SaveSystem
    {
        static string SavePath => Path.Combine(Application.persistentDataPath, "save.json");

        public static bool HasSave => File.Exists(SavePath);

        public static void Save(SaveData data)
        {
            try { File.WriteAllText(SavePath, JsonUtility.ToJson(data)); }
            catch (System.Exception e) { Debug.LogWarning("[Save] 保存失败：" + e.Message); }
        }

        public static SaveData Load()
        {
            if (!HasSave) return null;
            try { return JsonUtility.FromJson<SaveData>(File.ReadAllText(SavePath)); }
            catch (System.Exception e) { Debug.LogWarning("[Save] 读档失败：" + e.Message); return null; }
        }

        public static void Clear()
        {
            if (HasSave) { try { File.Delete(SavePath); } catch { } }
        }
    }
}
