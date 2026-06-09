using UnityEngine;

namespace Jam.Game.GameRes
{
    public class GameRes
    {
        private const string LevelPatch = "Assets/_Project/LevelEditor";
        private const string NameLevel = "level_";

        public static GameObject LoadSkillPrefabs(int id)
        {
            var prefab = Resources.Load<GameObject>(LevelPatch + NameLevel + id);
            if (prefab == null)
                Debug.Log("Khong tim thay " + LevelPatch + NameLevel + id);
            return prefab;
        }
    }
}