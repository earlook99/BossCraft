using System;
using System.Text;
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
        
        private static readonly StringBuilder _stringBuilder = new StringBuilder(256);
        
        private const string STATUS_MSG_CHECKING = "Checking server status...";
        private const string STATUS_MSG_READY = "Server is ready! Select an image to start.";
        private const string STATUS_MSG_MODELS_LOADING = "Server online, but models are still loading. Please wait...";
        private const string STATUS_MSG_NOT_ONLINE = "Server responded, but status is not 'online'.";
        private const string STATUS_MSG_PARSE_ERROR = "Failed to parse server response.";
        private const string STATUS_MSG_LOADING_IMAGE = "Loading image...";
        private const string STATUS_MSG_IMAGE_LOADED = "Image loaded! Enter boss name and click 'Generate'.";
        private const string STATUS_MSG_FAILED_LOAD = "Failed to load image.";
        private const string STATUS_MSG_NO_IMAGE = "No image to download.";
        private const string STATUS_MSG_ENCODING = "Encoding image...";
        private const string STATUS_MSG_GENERATING = "Generating boss";
        private const string STATUS_MSG_BOSS_GENERATED = "Boss generated!";
        private const string STATUS_MSG_BOSS_CREATED_PREFIX = "Boss '";
        private const string STATUS_MSG_BOSS_CREATED_SUFFIX = "' has been created! Return to main menu and start the game.";
        private const string STATUS_MSG_SAVE_ERROR = "Error: Failed to save boss data.";
        private const string STATUS_MSG_SERVER_NOT_READY = "Server is not ready. Please wait.";
        private const string STATUS_MSG_SELECT_IMAGE = "Please select an image first.";
        
        private const string ERROR_MSG_CONNECTION = "Connection failed. Server might be down or network issues.";
        private const string ERROR_MSG_TIMEOUT = "Request failed or timed out. Please try again.";
        private const string ERROR_MSG_DOWNLOAD_FORMAT = "Error: Generated image format is incorrect.";
        private const string ERROR_MSG_ENCODE_FAILED = "Error: Failed to encode image.";
        private const string ERROR_MSG_SAVE_FAILED = "Error saving image.";
        
        private const string DATA_IMAGE_JPEG = "data:image/jpeg;base64,";
        private const string FILE_PREFIX = "file://";
        private const string DEFAULT_BOSS_NAME = "GeneratedBoss";

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
            UpdateStatus(STATUS_MSG_CHECKING);

            _stringBuilder.Clear();
            _stringBuilder.Append(activeServerUrl).Append(STATUS_ENDPOINT);
            string statusUrl = _stringBuilder.ToString();

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
                _stringBuilder.Clear();
                _stringBuilder.Append("Server check failed: ").Append(e.Message);
                UpdateStatus(_stringBuilder.ToString());
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
                        UpdateStatus(STATUS_MSG_READY);
                    }
                    else
                    {
                        UpdateStatus(STATUS_MSG_MODELS_LOADING);
                    }
                }
                else
                {
                    UpdateStatus(STATUS_MSG_NOT_ONLINE);
                }
            }
            catch (Exception e)
            {
                UpdateStatus(STATUS_MSG_PARSE_ERROR);
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
            UpdateStatus(STATUS_MSG_LOADING_IMAGE);
            isProcessing = true;
            UpdateUIState();

            string url = path.StartsWith(FILE_PREFIX) ? path : FILE_PREFIX + path;
            
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
                    _stringBuilder.Clear();
                    _stringBuilder.Append("Failed to load image: ").Append(www.error);
                    UpdateStatus(_stringBuilder.ToString());
                    _isImageLoaded = false;
                }
            }
            
            isProcessing = false;
            UpdateUIState();
        }
        
        private async Task LoadImageFromBase64Async(string base64Data)
        {
            UpdateStatus(STATUS_MSG_LOADING_IMAGE);
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
                UpdateStatus(STATUS_MSG_FAILED_LOAD);
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

            UpdateStatus(STATUS_MSG_IMAGE_LOADED);
        }

        private async Task GenerateBossImageAsync()
        {
            if (uploadedTexture == null || isProcessing || !_isServerReady)
            {
                if (!_isServerReady) UpdateStatus(STATUS_MSG_SERVER_NOT_READY);
                if (uploadedTexture == null) UpdateStatus(STATUS_MSG_SELECT_IMAGE);
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

            UpdateStatus(STATUS_MSG_ENCODING);
            await AsyncUtilities.NextFrameAsync(ct);

            string requestJson = CreateGenerationRequest();

            using (var www = CreatePredictRequest(requestJson))
            {
                var progressTask = ShowProgressAsync(startTime, STATUS_MSG_GENERATING, ct);
                
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
                data = new string[] { DATA_IMAGE_JPEG + base64Image },
                parameters = parameters
            };

            return JsonUtility.ToJson(request);
        }

        private UnityWebRequest CreatePredictRequest(string json)
        {
            byte[] jsonBytes = System.Text.Encoding.UTF8.GetBytes(json);
            
            _stringBuilder.Clear();
            _stringBuilder.Append(activeServerUrl).Append(PREDICT_ENDPOINT);
            
            var www = new UnityWebRequest(_stringBuilder.ToString(), "POST");
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
                
                _stringBuilder.Clear();
                _stringBuilder.Append(prefix);
                for (int i = 0; i < dotCount; i++)
                {
                    _stringBuilder.Append('.');
                }
                _stringBuilder.Append(" (").Append(Mathf.FloorToInt(elapsed)).Append("s)");
                
                UpdateStatus(_stringBuilder.ToString());

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
                    _stringBuilder.Clear();
                    _stringBuilder.Append("Connection error: ").Append(www.error);
                    return _stringBuilder.ToString();
                case UnityWebRequest.Result.ProtocolError:
                    _stringBuilder.Clear();
                    _stringBuilder.Append("HTTP error: ").Append(www.responseCode).Append(" - ").Append(www.error);
                    return _stringBuilder.ToString();
                case UnityWebRequest.Result.DataProcessingError:
                    _stringBuilder.Clear();
                    _stringBuilder.Append("Data processing error: ").Append(www.error);
                    return _stringBuilder.ToString();
                default:
                    _stringBuilder.Clear();
                    _stringBuilder.Append("Unknown error or timeout: ").Append(www.error);
                    return _stringBuilder.ToString();
            }
        }

        private string GetUserFriendlyError(UnityWebRequest.Result result, long responseCode)
        {
            switch (result)
            {
                case UnityWebRequest.Result.ConnectionError:
                    return ERROR_MSG_CONNECTION;
                case UnityWebRequest.Result.ProtocolError:
                    _stringBuilder.Clear();
                    _stringBuilder.Append("Server error (").Append(responseCode).Append("). Please try again later.");
                    return _stringBuilder.ToString();
                case UnityWebRequest.Result.DataProcessingError:
                    return "Error processing data from server.";
                default:
                    return ERROR_MSG_TIMEOUT;
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
                _stringBuilder.Clear();
                _stringBuilder.Append("Failed to process server response: ").Append(e.Message);
                UpdateStatus(_stringBuilder.ToString());
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

            UpdateStatus(STATUS_MSG_BOSS_GENERATED);
            if (response.duration > 0) 
            {
                _stringBuilder.Clear();
                _stringBuilder.Append("Generation took ").AppendFormat("{0:F2}", response.duration).Append(" seconds");
                Debug.Log(_stringBuilder.ToString());
            }

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
                UpdateStatus(STATUS_MSG_FAILED_LOAD);
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
                
                _stringBuilder.Clear();
                _stringBuilder.Append(STATUS_MSG_BOSS_CREATED_PREFIX)
                    .Append(bossName)
                    .Append(STATUS_MSG_BOSS_CREATED_SUFFIX);
                UpdateStatus(_stringBuilder.ToString());
                
                _hasConfirmedBoss = true;
                UpdateUIState();
            }
            else
            {
                UpdateStatus(STATUS_MSG_SAVE_ERROR);
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
                UpdateStatus(STATUS_MSG_NO_IMAGE);
                return;
            }

            Texture2D textureToSave = generatedImage.texture as Texture2D;
            if (textureToSave == null)
            {
                UpdateStatus(ERROR_MSG_DOWNLOAD_FORMAT);
                return;
            }

            byte[] imageBytes = textureToSave.EncodeToPNG();
            if (imageBytes == null)
            {
                UpdateStatus(ERROR_MSG_ENCODE_FAILED);
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
                : DEFAULT_BOSS_NAME;
            
            _stringBuilder.Clear();
            _stringBuilder.Append(safeName)
                .Append('_')
                .Append(DateTime.Now.ToString("yyyyMMdd_HHmmss"))
                .Append(".png");
            
            return _stringBuilder.ToString();
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
                    
                    _stringBuilder.Clear();
                    _stringBuilder.Append("Image saved: ").Append(Path.GetFileName(path));
                    UpdateStatus(_stringBuilder.ToString());
                }
                catch (Exception e)
                {
                    UpdateStatus(ERROR_MSG_SAVE_FAILED);
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
            
            _stringBuilder.Clear();
            _stringBuilder.Append("Image downloaded: ").Append(fileName);
            UpdateStatus(_stringBuilder.ToString());
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