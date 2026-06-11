using UnityEngine;

namespace Jam.Game.State
{
    public class BlockState : MonoBehaviour
    {
        [SerializeField] private Transform _posSpawnLevel;

        private void Start()
        {
            var prefab = GameRes.GameRes.LoadSkillPrefabs(5);
            if (prefab == null) return;
            Instantiate(prefab, _posSpawnLevel.position, Quaternion.identity);
        }
    }
}
