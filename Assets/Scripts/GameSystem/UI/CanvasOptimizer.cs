using UnityEngine;
using UnityEngine.UI;

namespace GameSystem.UI
{
    public class CanvasOptimizer : MonoBehaviour
    {
        private static CanvasOptimizer _instance;
        public static CanvasOptimizer Instance => _instance;
        
        [Header("Canvas References")]
        [SerializeField] private Canvas _staticCanvas;
        [SerializeField] private Canvas _dynamicCanvas;
        [SerializeField] private Canvas _overlayCanvas;
        
        [Header("Canvas Settings")]
        [SerializeField] private bool _pixelPerfect = false;
        [SerializeField] private int _staticCanvasSortOrder = 0;
        [SerializeField] private int _dynamicCanvasSortOrder = 10;
        [SerializeField] private int _overlayCanvasSortOrder = 20;
        
        private GraphicRaycaster _staticRaycaster;
        private GraphicRaycaster _dynamicRaycaster;
        private GraphicRaycaster _overlayRaycaster;
        
        public Canvas StaticCanvas => _staticCanvas;
        public Canvas DynamicCanvas => _dynamicCanvas;
        public Canvas OverlayCanvas => _overlayCanvas;
        
        private void Awake()
        {
            if (_instance == null)
            {
                _instance = this;
                DontDestroyOnLoad(gameObject);
                InitializeCanvases();
            }
            else
            {
                Destroy(gameObject);
            }
        }
        
        private void InitializeCanvases()
        {
            if (_staticCanvas == null || _dynamicCanvas == null || _overlayCanvas == null)
            {
                CreateCanvases();
            }
            
            ConfigureCanvas(_staticCanvas, "StaticCanvas", _staticCanvasSortOrder, ref _staticRaycaster);
            ConfigureCanvas(_dynamicCanvas, "DynamicCanvas", _dynamicCanvasSortOrder, ref _dynamicRaycaster);
            ConfigureCanvas(_overlayCanvas, "OverlayCanvas", _overlayCanvasSortOrder, ref _overlayRaycaster);
            
            _staticRaycaster.enabled = false;
            _dynamicRaycaster.enabled = true;
            _overlayRaycaster.enabled = true;
        }
        
        private void CreateCanvases()
        {
            GameObject rootUI = new GameObject("UI_Root");
            rootUI.transform.SetParent(transform);
            
            if (_staticCanvas == null)
                _staticCanvas = CreateCanvas("StaticCanvas", rootUI.transform);
            if (_dynamicCanvas == null)
                _dynamicCanvas = CreateCanvas("DynamicCanvas", rootUI.transform);
            if (_overlayCanvas == null)
                _overlayCanvas = CreateCanvas("OverlayCanvas", rootUI.transform);
        }
        
        private Canvas CreateCanvas(string name, Transform parent)
        {
            GameObject canvasGO = new GameObject(name);
            canvasGO.transform.SetParent(parent);
            
            Canvas canvas = canvasGO.AddComponent<Canvas>();
            if (canvas.name == "StaticCanvas")
            {
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
            }
            else
            {
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            }
            
            CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            
            canvasGO.AddComponent<GraphicRaycaster>();
            
            return canvas;
        }
        
        private void ConfigureCanvas(Canvas canvas, string name, int sortOrder, ref GraphicRaycaster raycaster)
        {
            canvas.name = name;
            canvas.sortingOrder = sortOrder;
            canvas.pixelPerfect = _pixelPerfect;
            
            raycaster = canvas.GetComponent<GraphicRaycaster>();
            if (raycaster == null)
                raycaster = canvas.gameObject.AddComponent<GraphicRaycaster>();
        }
        
        public void MoveToCanvas(GameObject uiElement, CanvasType canvasType)
        {
            Canvas targetCanvas = GetCanvas(canvasType);
            if (targetCanvas != null && uiElement != null)
            {
                uiElement.transform.SetParent(targetCanvas.transform, false);
            }
        }
        
        public Canvas GetCanvas(CanvasType canvasType)
        {
            switch (canvasType)
            {
                case CanvasType.Static:
                    return _staticCanvas;
                case CanvasType.Dynamic:
                    return _dynamicCanvas;
                case CanvasType.Overlay:
                    return _overlayCanvas;
                default:
                    return _dynamicCanvas;
            }
        }
        
        public void SetCanvasInteractable(CanvasType canvasType, bool interactable)
        {
            GraphicRaycaster raycaster = GetRaycaster(canvasType);
            if (raycaster != null)
                raycaster.enabled = interactable;
        }
        
        private GraphicRaycaster GetRaycaster(CanvasType canvasType)
        {
            switch (canvasType)
            {
                case CanvasType.Static:
                    return _staticRaycaster;
                case CanvasType.Dynamic:
                    return _dynamicRaycaster;
                case CanvasType.Overlay:
                    return _overlayRaycaster;
                default:
                    return null;
            }
        }
        
        public void OptimizeUIElement(GameObject uiElement)
        {
            Graphic[] graphics = uiElement.GetComponentsInChildren<Graphic>(true);
            
            foreach (var graphic in graphics)
            {
                if (graphic is Image image && image.sprite == null)
                {
                    image.raycastTarget = false;
                }
                else if (graphic is Text || graphic is TMPro.TextMeshProUGUI)
                {
                    graphic.raycastTarget = false;
                }
                
                if (graphic.GetComponent<Button>() == null && 
                    graphic.GetComponent<UnityEngine.UI.Toggle>() == null &&
                    graphic.GetComponent<Slider>() == null &&
                    graphic.GetComponent<Scrollbar>() == null &&
                    graphic.GetComponent<UnityEngine.UI.ScrollRect>() == null)
                {
                    graphic.raycastTarget = false;
                }
            }
        }
    }
    
    public enum CanvasType
    {
        Static,
        Dynamic,
        Overlay
    }
}