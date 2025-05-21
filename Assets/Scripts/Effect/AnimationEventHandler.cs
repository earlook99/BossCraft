using UnityEngine;

namespace Effect
{
    public class AnimationEventHandler : MonoBehaviour
    {
        public void OnAnimationEnd()
        {
            Destroy(gameObject);
        }
    }
}
