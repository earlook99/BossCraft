using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class SpriteOutlineToggle : MonoBehaviour
{
    [Header("Outline Parameters (defaults)")]
    public Color outlineColor = Color.yellow;
    [Range(0f, 10f)]
    public float outlineWidthPX = 1f;   // “몇 픽셀” 두께

    SpriteRenderer          sr;
    MaterialPropertyBlock   mpb;

    // Shader property IDs  ⬅ ✨ 변경
    static readonly int ID_OutlineColor   = Shader.PropertyToID("_OutlineColor");
    static readonly int ID_OutlineWidthPX = Shader.PropertyToID("_OutlineWidthPX");

    void Awake()
    {
        sr  = GetComponent<SpriteRenderer>();
        mpb = new MaterialPropertyBlock();
    }

    /// <summary>외곽선을 켜거나 끈다.</summary>
    public void SetOutline(bool enable)
    {
        sr.GetPropertyBlock(mpb);

        if (enable && outlineWidthPX > 0f)
        {
            mpb.SetColor(ID_OutlineColor,   outlineColor);
            mpb.SetFloat(ID_OutlineWidthPX, outlineWidthPX);
        }
        else
        {
            // 0 픽셀 = 완전 OFF
            mpb.SetFloat(ID_OutlineWidthPX, 0f);
        }

        sr.SetPropertyBlock(mpb);
    }

    // 데모: F 키로 토글
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.F))
        {
            sr.GetPropertyBlock(mpb);
            bool onNow = mpb.GetFloat(ID_OutlineWidthPX) > 0f;
            SetOutline(!onNow);
        }
    }
}