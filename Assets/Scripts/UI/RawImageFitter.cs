using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    /// <summary>
    /// 무조건 Crop(커버) 방식으로 이미지를 표시합니다.
    /// - 가로가 넓으면 상하를 잘라서 슬롯에 꽉 채우고
    /// - 세로가 길면 좌우를 잘라서 슬롯에 꽉 채우는 방식.
    /// uvRect로 잘린 영역을 제어하므로, CropDragger를 통해 드래그도 가능해집니다.
    /// </summary>
    public static class RawImageFitter
    {
        /// <summary>
        /// Crop 모드로만 이미지 영역을 조정한다.
        /// </summary>
        public static void Fit(RawImage img)
        {
            if (img.texture == null)
            {
                return;
            }

            Debug.Log("Fit() called!");
            
            // 부모 RectTransform이 있으면 그 크기를 기준으로 함
            RectTransform parentRect = img.transform.parent as RectTransform;
            if (parentRect == null)
            {
                parentRect = img.rectTransform;
            }

            float texAspect  = (float)img.texture.width / img.texture.height;
            float slotAspect = parentRect.rect.width / parentRect.rect.height;

            // Cover 방식: 넓은 이미지는 상하를, 세로로 긴 이미지는 좌우를 잘라 슬롯을 꽉 채움
            if (texAspect >= slotAspect)
            {
                // 넓은 이미지: 상하 잘림
                float h = slotAspect / texAspect;
                float y = (1f - h) * 0.5f;
                img.uvRect = new Rect(0f, y, 1f, h);

            }
            else
            {
                // 세로로 긴 이미지: 좌우 잘림
                float w = texAspect / slotAspect;
                float x = (1f - w) * 0.5f;
                img.uvRect = new Rect(x, 0f, w, 1f);

            }
        }
    }
}