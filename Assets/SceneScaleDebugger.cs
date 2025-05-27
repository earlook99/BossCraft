using UnityEngine;
using Unity.Cinemachine;
using UnityEditor;
using UnityEngine.UI;

public class SceneScaleDebugger : MonoBehaviour
{
    [ContextMenu("Check Scene Scale Issues")]
    void CheckScaleIssues()
    {
        Debug.Log("=== SCENE SCALE CHECK ===");
        
        // 카메라 체크
        var cameras = FindObjectsOfType<Camera>();
        foreach (var cam in cameras)
        {
            Debug.Log($"Camera '{cam.name}': Ortho Size = {cam.orthographicSize}, Position Z = {cam.transform.position.z}");
        }
        
        // Virtual Camera 체크
        var vcams = FindObjectsOfType<CinemachineCamera>();
        foreach (var vcam in vcams)
        {
            Debug.Log($"VCam '{vcam.name}': Ortho Size = {vcam.Lens.OrthographicSize}, Position Z = {vcam.transform.position.z}");
        }
        
        // 스프라이트 체크 - Z값 포함!
        var sprites = FindObjectsOfType<SpriteRenderer>();
        foreach (var sprite in sprites)
        {
            var pos = sprite.transform.position;
            var scale = sprite.transform.localScale;
            
            // Z값이 0이 아니면 표시
            if (pos.z != 0)
            {
                Debug.Log($"Sprite '{sprite.name}': Position Z = {pos.z} (연출용?)");
            }
            
            // 스케일이 1,1,1이 아니면 경고
            if (scale != Vector3.one)
            {
                Debug.LogWarning($"Sprite '{sprite.name}': Scale = {scale}, Position = ({pos.x:F2}, {pos.y:F2}, {pos.z:F2})");
            }
            
            // Sorting Layer와 Order도 체크
            Debug.Log($"  └─ Sorting Layer: {sprite.sortingLayerName}, Order: {sprite.sortingOrder}");
        }
        
        // Canvas 체크
        var canvases = FindObjectsOfType<Canvas>();
        foreach (var canvas in canvases)
        {
            Debug.Log($"Canvas '{canvas.name}': Render Mode = {canvas.renderMode}, Plane Distance = {canvas.planeDistance}");
            var scaler = canvas.GetComponent<CanvasScaler>();
            if (scaler == null)
            {
                Debug.LogError($"Canvas '{canvas.name}'에 Canvas Scaler가 없습니다!");
            }
        }
    }
}