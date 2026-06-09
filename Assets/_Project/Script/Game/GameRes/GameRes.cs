using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Jam.Game.GameRes
{
    public class GameRes
    {
        private const string EditorLevelPath = "Assets/_Project/LevelEditor";
        private const string NameLevel = "level_";

        public static GameObject LoadSkillPrefabs(int id)
        {
            var levelName = $"{NameLevel}{id}";
            var resourcesPath = $"{EditorLevelPath}/{levelName}";
            var prefab = Resources.Load<GameObject>(resourcesPath);

#if UNITY_EDITOR
            if (prefab == null)
            {
                prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{EditorLevelPath}/{levelName}.prefab");
            }
#endif

            if (prefab == null)
                Debug.LogError($"Khong tim thay {resourcesPath} hoac {EditorLevelPath}/{levelName}.prefab");
            return prefab;
        }
    }
}
