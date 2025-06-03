using System;
using System.Threading;
using System.Threading.Tasks;
using System.IO;
using Data;
using GameSystem;
using GameSystem.Utils;
using TMPro;
using UI;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using WebGL;

namespace AI
{
    [Serializable]
    public class ServerStatusResponse
    {
        public string status;
        public bool models_loaded;
    }

    [Serializable]
    public class BossGenerationRequest
    {
        public string[] data;
        public GenerationParameters parameters;
    }

    [Serializable]
    public class GenerationParameters
    {
        [Range(0.1f, 1.0f)]
        public float strength = 0.85f;

        [Range(1.0f, 20.0f)]
        public float guidance_scale = 3.0f;

        [Range(10, 100)]
        public int inference_steps = 40;

        [Range(0.0f, 1.0f)]
        public float lora_weight = 0.7f;

        [TextArea(2, 4)]
        public string prompt = "";

        [TextArea(2, 4)]
        public string negative_prompt = "realistic, photograph, human, normal animal";

        public int seed = -1;
    }
    
    [Serializable]
    public class ParametersUsed
    {
        public float strength;
        public float guidance_scale;
        public int inference_steps;
        public float lora_weight;
        public int seed;
        public string intended_type_pass1;
        public string observed_type_pass2;
        public string final_type_decision;
    }

    [Serializable]
    public class ServerPredictResponse
    {
        public string[] data;
        public string caption;
        public string pokemon_type;
        public float duration;
        public ParametersUsed parameters_used;
    }
    
    public class BossGenerationManager : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private Button selectImageButton;
        [SerializeField] private Button generateButton;
        [SerializeField] private Button returnButton;
        [SerializeField] private Button rerollButton;
        [SerializeField] private Button confirmBossButton;
        [SerializeField] private Button downloadButton;
        [SerializeField] private RawImage originalImage;
        [SerializeField] private RawImage generatedImage;
        [SerializeField] private TextMeshProUGUI statusText;
        [SerializeField] private GameObject loadingIndicator;
        [SerializeField] private TMP_InputField bossNameField;

        [Header("Generation Parameters")]
        [SerializeField] private GenerationParameters parameters = new GenerationParameters();

        [Header("Server Settings")]
        [SerializeField] private string serverUrlOverride = "";

        private string activeServerUrl = "";
        private Texture2D uploadedTexture;
        private bool isProcessing = false;
        private bool _hasName = false;
        private bool _isImageLoaded = false;
        private bool _hasGeneratedImage = false;
        private bool _isServerReady = false;
        private bool _hasConfirmedBoss = false;
        private Sprite bossSprite;
        private string bossName = "";
        private byte[] _tempImageData;
        private CancellationTokenSource _cts;

        private const int REQUEST_TIMEOUT = 60;
        private const int GENERATION_TIMEOUT = 180;
        private const int JPEG_QUALITY = 85;
        private const float SPRITE_PIXELS_PER_UNIT = 0.7f;
        private const float PROGRESS_UPDATE_INTERVAL = 0.5f;
        private const string STATUS_ENDPOINT = "/status";
        private const string PREDICT_ENDPOINT = "/api/predict";
        private const string DATA_IMAGE_PREFIX = "data:image/";
        private const char DATA_SEPARATOR = ',';

        private void Awake()
        {
            InitializeServerUrl();
            _cts = new CancellationTokenSource();
        }

        private void InitializeServerUrl()
        {
            activeServerUrl = !string.IsNullOrEmpty(serverUrlOverride) 
                ? serverUrlOverride 
                : ServerConfig.HUGGINGFACE_URL;
        }

        private async void Start()
        {
            InitializeButtons();
            InitializeUI();
            await WarmupServerAsync(_cts.Token);
        }

        private void OnDestroy()
        {
            _cts?.Cancel();
            _cts?.Dispose();
            
            if (uploadedTexture != null) Destroy(uploadedTexture);
            if (generatedImage.texture != null) Destroy(generatedImage.texture);
            if (bossSprite != null && bossSprite.texture != null) Destroy(bossSprite.texture);
        }

        private void InitializeButtons()
        {
            selectImageButton.onClick.AddListener(SelectImage);
            generateButton.onClick.AddListener(() => _ = GenerateBossImageAsync());
            rerollButton.onClick.AddListener(() => _ = GenerateBossImageAsync());
            confirmBossButton.onClick.AddListener(ConfirmBoss);
            returnButton.onClick.AddListener(ReturnToPreviousScene);
            downloadButton.onClick.AddListener(DownloadGeneratedImage);
            bossNameField.onValueChanged.AddListener(OnBossNameChanged);
        }

        private void InitializeUI()
        {
            if (loadingIndicator) loadingIndicator.SetActive(false);
            _isImageLoaded = false;
            _hasGeneratedImage = false;
            _isServerReady = false;
            UpdateUIState();
        }

        private void OnBossNameChanged(string inputValue)
        {
            bossName = inputValue;
            _hasName = !string.IsNullOrWhiteSpace(inputValue);
            UpdateUIState();
        }

        private async Task WarmupServerAsync(CancellationToken ct)
        {
            _isServerReady = false;
            UpdateUIState();
            UpdateStatus("Checking server status...");

            string statusUrl = $"{activeServerUrl}{STATUS_ENDPOINT}";

            try
            {
                using (UnityWebRequest www = UnityWebRequest.Get(statusUrl))
                {
                    www.timeout = REQUEST_TIMEOUT;
                    
                    var operation = www.SendWebRequest();
                    while (!operation.isDone && !ct.IsCancellationRequested)
                    {
                        await AsyncUtilities.NextFrameAsync(ct);
                    }

                    if (ct.IsCancellationRequested) return;

                    if (www.result == UnityWebRequest.Result.Success)
                    {
                        ProcessServerStatus(www.downloadHandler.text);
                    }
                    else
                    {
                        HandleError(www, "Server status check failed");
                    }
                }
            }
            catch (Exception e)
            {
                UpdateStatus($"Server check failed: {e.Message}");
                Debug.LogError($"Warmup error: {e}");
            }
            
            UpdateUIState();
        }

        private void ProcessServerStatus(string response)
        {
            try
            {
                ServerStatusResponse serverStatus = JsonUtility.FromJson<ServerStatusResponse>(response);

                if (serverStatus != null && serverStatus.status == "online")
                {
                    if (serverStatus.models_loaded)
                    {
                        _isServerReady = true;
                        UpdateStatus("Server is ready! Select an image to start.");
                    }
                    else
                    {
                        UpdateStatus("Server online, but models are still loading. Please wait...");
                    }
                }
                else
                {
                    UpdateStatus("Server responded, but status is not 'online'.");
                }
            }
            catch (Exception e)
            {
                UpdateStatus("Failed to parse server response.");
                Debug.LogError($"Error parsing server status: {e.Message}");
            }
        }

        private void UpdateUIState()
        {
            bool canInteractWithServer = _isServerReady && !isProcessing;

            selectImageButton.interactable = canInteractWithServer && !_hasConfirmedBoss;
            returnButton.interactable = !isProcessing;
            confirmBossButton.interactable = canInteractWithServer && _hasName && _hasGeneratedImage && !_hasConfirmedBoss;

            UpdateGenerateButtons(canInteractWithServer);

            if (bossNameField != null)
            {
                bossNameField.interactable = !_hasConfirmedBoss;
            }
        }

        private void UpdateGenerateButtons(bool canInteractWithServer)
        {
            if (_hasGeneratedImage)
            {
                generateButton.gameObject.SetActive(false);
                rerollButton.gameObject.SetActive(true);
                rerollButton.interactable = canInteractWithServer && !_hasConfirmedBoss;
                downloadButton.gameObject.SetActive(true);
                downloadButton.interactable = true;
            }
            else
            {
                generateButton.gameObject.SetActive(true);
                rerollButton.gameObject.SetActive(false);
                generateButton.interactable = canInteractWithServer && _isImageLoaded && !_hasConfirmedBoss;
                downloadButton.gameObject.SetActive(false);
            }
        }

        private void SelectImage()
        {
            if (isProcessing) return;

#if UNITY_EDITOR
            SelectImageInEditor();
#elif UNITY_WEBGL
            WebGLFileUploader.OpenFilePicker(async (base64Data) => {
                await LoadImageFromBase64Async(base64Data);
            });
#else
            UpdateStatus("File selection not implemented for this platform.");
#endif
        }

#if UNITY_EDITOR
        private async void SelectImageInEditor()
        {
            string path = UnityEditor.EditorUtility.OpenFilePanel("Select Image", "", "png,jpg,jpeg");
            if (!string.IsNullOrEmpty(path))
            {
                await LoadImageAsync(path);
            }
        }
#endif

        private async Task LoadImageAsync(string path)
        {
            UpdateStatus("Loading image...");
            isProcessing = true;
            UpdateUIState();

            string url = path.StartsWith("file://") ? path : "file://" + path;
            
            using (UnityWebRequest www = UnityWebRequestTexture.GetTexture(url))
            {
                var operation = www.SendWebRequest();
                while (!operation.isDone && !_cts.Token.IsCancellationRequested)
                {
                    await AsyncUtilities.NextFrameAsync(_cts.Token);
                }

                if (www.result == UnityWebRequest.Result.Success)
                {
                    ProcessLoadedTexture(DownloadHandlerTexture.GetContent(www));
                }
                else
                {
                    UpdateStatus($"Failed to load image: {www.error}");
                    _isImageLoaded = false;
                }
            }
            
            isProcessing = false;
            UpdateUIState();
        }
        
        private async Task LoadImageFromBase64Async(string base64Data)
        {
            UpdateStatus("Loading image...");
            isProcessing = true;
            UpdateUIState();

            string base64 = ExtractBase64Data(base64Data);
            byte[] imageBytes = Convert.FromBase64String(base64);
    
            if (uploadedTexture != null) Destroy(uploadedTexture);
    
            uploadedTexture = new Texture2D(2, 2);
            if (uploadedTexture.LoadImage(imageBytes))
            {
                ProcessLoadedTexture(uploadedTexture);
            }
            else
            {
                UpdateStatus("Failed to load image.");
                _isImageLoaded = false;
            }

            isProcessing = false;
            UpdateUIState();
            
            await Task.Yield();
        }

        private string ExtractBase64Data(string base64Data)
        {
            return base64Data.Contains(DATA_SEPARATOR) 
                ? base64Data.Split(DATA_SEPARATOR)[1] 
                : base64Data;
        }

        private void ProcessLoadedTexture(Texture2D texture)
        {
            if (uploadedTexture != null && uploadedTexture != texture) 
                Destroy(uploadedTexture);
                
            uploadedTexture = texture;
            originalImage.texture = uploadedTexture;
            RawImageFitter.Fit(originalImage);
            FitAndCrop(originalImage);

            _isImageLoaded = true;
            _hasGeneratedImage = false;

            UpdateStatus("Image loaded! Enter boss name and click 'Generate'.");
        }

        private async Task GenerateBossImageAsync()
        {
            if (uploadedTexture == null || isProcessing || !_isServerReady)
            {
                if (!_isServerReady) UpdateStatus("Server is not ready. Please wait.");
                if (uploadedTexture == null) UpdateStatus("Please select an image first.");
                return;
            }
            
            await GenerateImageAsync(_cts.Token);
        }

        private async Task GenerateImageAsync(CancellationToken ct)
        {
            isProcessing = true;
            UpdateUIState();

            if (loadingIndicator) loadingIndicator.SetActive(true);
            float startTime = Time.time;

            UpdateStatus("Encoding image...");
            await AsyncUtilities.NextFrameAsync(ct);

            string requestJson = CreateGenerationRequest();

            using (var www = CreatePredictRequest(requestJson))
            {
                var progressTask = ShowProgressAsync(startTime, "Generating boss", ct);
                
                var operation = www.SendWebRequest();
                while (!operation.isDone && !ct.IsCancellationRequested)
                {
                    await AsyncUtilities.NextFrameAsync(ct);
                }

                if (!ct.IsCancellationRequested)
                {
                    if (www.result == UnityWebRequest.Result.Success)
                    {
                        ProcessServerResponse(www.downloadHandler.text);
                    }
                    else
                    {
                        HandleError(www, "Image generation failed");
                    }
                }
            }

            if (loadingIndicator) loadingIndicator.SetActive(false);
            isProcessing = false;
            UpdateUIState();
        }

        private string CreateGenerationRequest()
        {
            byte[] imageBytes = uploadedTexture.EncodeToJPG(JPEG_QUALITY);
            string base64Image = Convert.ToBase64String(imageBytes);

            var request = new BossGenerationRequest
            {
                data = new string[] { $"data:image/jpeg;base64,{base64Image}" },
                parameters = parameters
            };

            return JsonUtility.ToJson(request);
        }

        private UnityWebRequest CreatePredictRequest(string json)
        {
            byte[] jsonBytes = System.Text.Encoding.UTF8.GetBytes(json);
            
            var www = new UnityWebRequest($"{activeServerUrl}{PREDICT_ENDPOINT}", "POST");
            www.uploadHandler = new UploadHandlerRaw(jsonBytes);
            www.downloadHandler = new DownloadHandlerBuffer();
            www.SetRequestHeader("Content-Type", "application/json");
            www.timeout = GENERATION_TIMEOUT;
            
            return www;
        }

        private async Task ShowProgressAsync(float startTime, string prefix, CancellationToken ct)
        {
            int dotCount = 0;
            while (isProcessing && !ct.IsCancellationRequested)
            {
                float elapsed = Time.time - startTime;
                string dots = new string('.', dotCount);
                UpdateStatus($"{prefix}{dots} ({Mathf.FloorToInt(elapsed)}s)");

                dotCount = (dotCount + 1) % 4;
                await AsyncUtilities.WaitForSecondsAsync(PROGRESS_UPDATE_INTERVAL, ct);
            }
        }

        private void HandleError(UnityWebRequest www, string contextMessage = "Server request failed")
        {
            string detailedError = DetermineErrorType(www);
            UpdateStatus(GetUserFriendlyError(www.result, www.responseCode));
            Debug.LogError($"{contextMessage}: {detailedError} (URL: {www.url})");

            if (!string.IsNullOrEmpty(www.downloadHandler?.text))
            {
                Debug.LogError($"Error Response Body: {www.downloadHandler.text}");
            }
        }

        private string DetermineErrorType(UnityWebRequest www)
        {
            if (www.responseCode == 0 && string.IsNullOrEmpty(www.error))
                return "Cannot connect to server. Check URL and network.";
            
            switch (www.result)
            {
                case UnityWebRequest.Result.ConnectionError:
                    return $"Connection error: {www.error}";
                case UnityWebRequest.Result.ProtocolError:
                    return $"HTTP error: {www.responseCode} - {www.error}";
                case UnityWebRequest.Result.DataProcessingError:
                    return $"Data processing error: {www.error}";
                default:
                    return $"Unknown error or timeout: {www.error}";
            }
        }

        private string GetUserFriendlyError(UnityWebRequest.Result result, long responseCode)
        {
            switch (result)
            {
                case UnityWebRequest.Result.ConnectionError:
                    return "Connection failed. Server might be down or network issues.";
                case UnityWebRequest.Result.ProtocolError:
                    return $"Server error ({responseCode}). Please try again later.";
                case UnityWebRequest.Result.DataProcessingError:
                    return "Error processing data from server.";
                default:
                    return "Request failed or timed out. Please try again.";
            }
        }
        
        private void ProcessServerResponse(string jsonResponse)
        {
            try
            {
                var response = JsonUtility.FromJson<ServerPredictResponse>(jsonResponse);
                
                if (response?.data != null && response.data.Length >= 1)
                {
                    ProcessGeneratedImage(response);
                }
                else
                {
                    UpdateStatus("Invalid response format from server.");
                }
            }
            catch (Exception e)
            {
                UpdateStatus($"Failed to process server response: {e.Message}");
                Debug.LogError($"Error parsing response: {e}");
            }
        }

        private void ProcessGeneratedImage(ServerPredictResponse response)
        {
            string imageData = ExtractImageData(response.data[0]);
            byte[] imageBytes = Convert.FromBase64String(imageData);
            _tempImageData = imageBytes;
            
            Texture2D generatedTexture = CreateTextureFromBytes(imageBytes);
            if (generatedTexture == null) return;

            UpdateGeneratedImage(generatedTexture);
            LogTypeInference(response.parameters_used);

            UpdateStatus("Boss generated!");
            if (response.duration > 0) 
                Debug.Log($"Generation took {response.duration:F2} seconds");

            CreateBossSprite(generatedTexture);
            _hasGeneratedImage = true;
        }

        private string ExtractImageData(string data)
        {
            if (data.StartsWith(DATA_IMAGE_PREFIX, StringComparison.OrdinalIgnoreCase))
            {
                int commaIndex = data.IndexOf(DATA_SEPARATOR);
                if (commaIndex > 0) 
                    return data.Substring(commaIndex + 1);
            }
            return data;
        }

        private Texture2D CreateTextureFromBytes(byte[] imageBytes)
        {
            Texture2D texture = new Texture2D(2, 2);
            if (!texture.LoadImage(imageBytes))
            {
                UpdateStatus("Failed to load generated image data.");
                Destroy(texture);
                return null;
            }

#if UNITY_EDITOR
            texture.alphaIsTransparency = true;
            texture.Apply();
#endif
            
            return texture;
        }

        private void UpdateGeneratedImage(Texture2D texture)
        {
            if (generatedImage.texture != null) 
                Destroy(generatedImage.texture);
                
            generatedImage.texture = texture;
            FitAndCrop(generatedImage);
        }

        private void LogTypeInference(ParametersUsed parameters)
        {
            if (parameters != null)
            {
                Debug.Log($"[Type Inference] Intended Type (Pass 1): {parameters.intended_type_pass1}");
                Debug.Log($"[Type Inference] Observed Type (Pass 2): {parameters.observed_type_pass2}");
                Debug.Log($"[Type Inference] Final Type Decision: {parameters.final_type_decision}");
            }
        }

        private void CreateBossSprite(Texture2D texture)
        {
            if (bossSprite != null && bossSprite.texture != null) 
                Destroy(bossSprite.texture);
                
            bossSprite = Sprite.Create(
                texture,
                new Rect(0, 0, texture.width, texture.height),
                new Vector2(0.5f, 0.5f),
                SPRITE_PIXELS_PER_UNIT
            );
        }

        private void ConfirmBoss()
        {
            if (!CanConfirmBoss()) return;

            if (BossContainer.Instance != null)
            {
                SaveBossData();
                UpdateStatus($"Boss '{bossName}' has been created! Return to main menu and start the game.");
                _hasConfirmedBoss = true;
                UpdateUIState();
            }
            else
            {
                UpdateStatus("Error: Failed to save boss data.");
            }
        }

        private bool CanConfirmBoss()
        {
            return _isServerReady && !isProcessing && _hasGeneratedImage && _hasName;
        }

        private void SaveBossData()
        {
            BossContainer.Instance.CurrentBossImageData = _tempImageData;
            BossContainer.Instance.CurrentBossName = bossName;
        }

        private void DownloadGeneratedImage()
        {
            if (!_hasGeneratedImage || generatedImage.texture == null)
            {
                UpdateStatus("No image to download.");
                return;
            }

            Texture2D textureToSave = generatedImage.texture as Texture2D;
            if (textureToSave == null)
            {
                UpdateStatus("Error: Generated image format is incorrect.");
                return;
            }

            byte[] imageBytes = textureToSave.EncodeToPNG();
            if (imageBytes == null)
            {
                UpdateStatus("Error: Failed to encode image.");
                return;
            }

            string fileName = GenerateFileName();

#if UNITY_EDITOR
            DownloadImageInEditor(imageBytes, fileName);
#elif UNITY_WEBGL
            DownloadImageInWebGL(imageBytes, fileName);
#else
            UpdateStatus("Download not supported on this platform.");
#endif
        }

        private string GenerateFileName()
        {
            string safeName = !string.IsNullOrEmpty(bossName) 
                ? bossName.Replace(" ", "_") 
                : "GeneratedBoss";
            return $"{safeName}_{DateTime.Now:yyyyMMdd_HHmmss}.png";
        }

#if UNITY_EDITOR
        private void DownloadImageInEditor(byte[] imageBytes, string fileName)
        {
            string path = UnityEditor.EditorUtility.SaveFilePanel(
                "Save Generated Boss Image",
                "",
                fileName,
                "png");

            if (!string.IsNullOrEmpty(path))
            {
                try
                {
                    File.WriteAllBytes(path, imageBytes);
                    UpdateStatus($"Image saved: {Path.GetFileName(path)}");
                }
                catch (Exception e)
                {
                    UpdateStatus("Error saving image.");
                    Debug.LogError($"Failed to save image: {e.Message}");
                }
            }
        }
#endif

#if UNITY_WEBGL
        private void DownloadImageInWebGL(byte[] imageBytes, string fileName)
        {
            string base64Data = "data:image/png;base64," + Convert.ToBase64String(imageBytes);
            WebGLFileUploader.DownloadFile(fileName, base64Data);
            UpdateStatus($"Image downloaded: {fileName}");
        }
#endif

        private void ReturnToPreviousScene()
        {
            Debug.Log("Return button clicked. Implement scene transition.");
        }

        private void UpdateStatus(string message)
        {
            if (statusText) statusText.text = message;
        }

        public static void FitAndCrop(RawImage img)
        {
            if (img.texture == null) return;

            float texAspect = (float)img.texture.width / img.texture.height;
            float slotAspect = img.rectTransform.rect.width / img.rectTransform.rect.height;

            if (texAspect > slotAspect)
            {
                float wantedWidth = slotAspect / texAspect;
                float offsetX = (1f - wantedWidth) * 0.5f;
                img.uvRect = new Rect(offsetX, 0f, wantedWidth, 1f);
            }
            else
            {
                float wantedHeight = texAspect / slotAspect;
                float offsetY = (1f - wantedHeight) * 0.5f;
                img.uvRect = new Rect(0f, offsetY, 1f, wantedHeight);
            }
        }
    }
}