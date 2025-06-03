using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class SpriteOutlineToggle : MonoBehaviour
{
    [Header("Outline Parameters")]
    public Color outlineColor = Color.yellow;
    
    [Range(0f, 10f)]
    public float outlineWidthPX = 1f;

    private SpriteRenderer sr;
    private MaterialPropertyBlock mpb;

    private static readonly int ID_OutlineColor = Shader.PropertyToID("_OutlineColor");
    private static readonly int ID_OutlineWidthPX = Shader.PropertyToID("_OutlineWidthPX");

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        mpb = new MaterialPropertyBlock();
    }

    public void SetOutline(bool enable)
    {
        sr.GetPropertyBlock(mpb);

        if (enable && outlineWidthPX > 0f)
        {
            mpb.SetColor(ID_OutlineColor, outlineColor);
            mpb.SetFloat(ID_OutlineWidthPX, outlineWidthPX);
        }
        else
        {
            mpb.SetFloat(ID_OutlineWidthPX, 0f);
        }

        sr.SetPropertyBlock(mpb);
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.F))
        {
            ToggleOutline();
        }
    }

    private void ToggleOutline()
    {
        sr.GetPropertyBlock(mpb);
        bool isOutlineActive = mpb.GetFloat(ID_OutlineWidthPX) > 0f;
        SetOutline(!isOutlineActive);
    }
}