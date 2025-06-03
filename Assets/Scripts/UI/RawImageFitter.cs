using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    public static class RawImageFitter
    {
        public static void Fit(RawImage img)
        {
            if (img.texture == null)
                return;
            
            RectTransform parentRect = img.transform.parent as RectTransform ?? img.rectTransform;

            float texAspect = (float)img.texture.width / img.texture.height;
            float slotAspect = parentRect.rect.width / parentRect.rect.height;

            if (texAspect >= slotAspect)
            {
                CropVertical(img, texAspect, slotAspect);
            }
            else
            {
                CropHorizontal(img, texAspect, slotAspect);
            }
        }

        private static void CropVertical(RawImage img, float texAspect, float slotAspect)
        {
            float h = slotAspect / texAspect;
            float y = (1f - h) * 0.5f;
            img.uvRect = new Rect(0f, y, 1f, h);
        }

        private static void CropHorizontal(RawImage img, float texAspect, float slotAspect)
        {
            float w = texAspect / slotAspect;
            float x = (1f - w) * 0.5f;
            img.uvRect = new Rect(x, 0f, w, 1f);
        }
    }
}