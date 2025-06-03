using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace UI
{
    [RequireComponent(typeof(RawImage))]
    public class CropDragger : MonoBehaviour, IPointerDownHandler, IDragHandler
    {
        private RawImage img;
        private RectTransform slot;
        private Vector2 startPos;
        private Rect startUV;
        private bool moveH;

        void Awake()
        {
            img = GetComponent<RawImage>();
            slot = img.rectTransform;
            img.raycastTarget = true;
        }

        public void OnPointerDown(PointerEventData e)
        {
            startPos = e.position;
            startUV = img.uvRect;

            float texAspect = (float)img.texture.width / img.texture.height;
            float slotAspect = slot.rect.width / slot.rect.height;
            
            moveH = texAspect >= slotAspect;
        }

        public void OnDrag(PointerEventData e)
        {
            Vector2 delta = e.position - startPos;
            Vector2 size = slot.rect.size;
            Rect uv = startUV;

            if (moveH)
            {
                DragHorizontal(ref uv, delta.x, size.x);
            }
            else
            {
                DragVertical(ref uv, delta.y, size.y);
            }

            img.uvRect = uv;
        }

        private void DragHorizontal(ref Rect uv, float deltaX, float sizeX)
        {
            float max = 1f - uv.width;
            float dx = deltaX / sizeX;
            uv.x = Mathf.Clamp(startUV.x - dx, 0f, max);
        }

        private void DragVertical(ref Rect uv, float deltaY, float sizeY)
        {
            float max = 1f - uv.height;
            float dy = deltaY / sizeY;
            uv.y = Mathf.Clamp(startUV.y - dy, 0f, max);
        }
    }
}