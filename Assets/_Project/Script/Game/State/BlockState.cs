using System;
using UnityEngine;

namespace Jam.Game.State
{
    public class BlockState : MonoBehaviour
    {
        private void Start()
        {
            GameRes.GameRes.LoadSkillPrefabs(1);
        }
    }
}