using UnityEngine;

namespace Jam
{
    public interface IClickableObject
    {
        public void OnObjectClicked();
        public bool CanBeClicked();
        public void OnClickBlocked();
    }
}