using System.Collections.Generic;
using UnityEngine;

namespace Jam
{
    [System.Serializable]
    public class FuncBlockData
    {
        [SerializeField] private FuncBlockType type;
        public FuncBlockType Type
        {
            get => type;
            set => type = value;
        }

        [SerializeField] private int value;
        public int Value
        {
            get => value;
            set => this.value = value;
        }

        public FuncBlockData(FuncBlockType newType, int newValue = 0)
        {
            type = newType;
            value = newValue;
        }
    }

    public abstract class FuncBlock : MonoBehaviour
    {
        public abstract IReadOnlyList<FuncBlockData> FuncBlocks { get; }
        
        public virtual void CustomFunc(FuncBlockType type, int value = 0)
        {
            switch (type)
            {
                case FuncBlockType.Boom:
                    break;
                case FuncBlockType.Freeze:
                    break;
                case FuncBlockType.Turn:
                    break;
                case FuncBlockType.DragHor:
                    break;
                case FuncBlockType.DragVer:
                    break;
                case FuncBlockType.None:
                    break;
            }
        }
    }
}
