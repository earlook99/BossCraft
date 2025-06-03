using UnityEngine;

namespace UI
{
    public class LoadingCircle : MonoBehaviour
    {
        private RectTransform rectComponent;
        [SerializeField] private float rotateSpeed = 200f;
        
        private void Awake()
        {
            rectComponent = GetComponent<RectTransform>();
        }
        
        private void OnEnable()
        {
            if (rectComponent == null)
                rectComponent = GetComponent<RectTransform>();
        }

        private void Update()
        {
            if (rectComponent != null)
                rectComponent.Rotate(0f, 0f, rotateSpeed * Time.deltaTime);
        }
    }
}