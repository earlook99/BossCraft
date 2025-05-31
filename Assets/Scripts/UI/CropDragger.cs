using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace UI
{
    /// <summary>
    /// RawImage의 uvRect를 드래그로 이동시켜
    /// 잘려 있는 부분을 스크롤하듯 보이게 합니다.
    /// </summary>
    [RequireComponent(typeof(RawImage))]
    public class CropDragger : MonoBehaviour, IPointerDownHandler, IDragHandler
    {
        RawImage img;
        RectTransform slot;
        Vector2 startPos;
        Rect startUV;
        bool moveH;  // true=좌우 이동, false=상하 이동

        void Awake()
        {
            img = GetComponent<RawImage>();
            slot = img.rectTransform;

            // 드래그 이벤트를 받기 위해 RaycastTarget On
            img.raycastTarget = true;
        }

        public void OnPointerDown(PointerEventData e)
        {
            startPos = e.position;
            startUV  = img.uvRect;

            float texAspect  = (float)img.texture.width / img.texture.height;
            float slotAspect = slot.rect.width / slot.rect.height;
            
            moveH = (texAspect >= slotAspect);

            Debug.Log("[CropDragger] OnPointerDown fired!");
        }

        public void OnDrag(PointerEventData e)
        {
            Vector2 delta = e.position - startPos;
            Vector2 size = slot.rect.size;
            Rect uv = startUV;

            if (moveH)
            {
                // 좌우 드래그
                float max = 1f - uv.width;
                float dx = delta.x / size.x;
                uv.x = Mathf.Clamp(startUV.x - dx, 0f, max);
                
                Debug.Log($"delta.x={delta.x}, new uvRect={uv}");
            }
            else
            {
                // 상하 드래그
                float max = 1f - uv.height;
                float dy = delta.y / size.y;
                uv.y = Mathf.Clamp(startUV.y - dy, 0f, max);
                
                Debug.Log($"delta.y={delta.y}, new uvRect={uv}");
            }

            img.uvRect = uv;
        }
    }
}