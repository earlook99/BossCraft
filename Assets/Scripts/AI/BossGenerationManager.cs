using System;
using System.Collections;
using System.Text;
using System.IO;
using Data;
using GameSystem;
using TMPro;
using UI;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using WebGL;
using static GameSystem.GameConstants.Network;
using static GameSystem.GameConstants.Scene;
using System.Collections.Generic;

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
        [SerializeField] private CustomBossDropdown bossHistoryDropdown;

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

        private const float SPRITE_PIXELS_PER_UNIT = 0.7f;

        private void Awake()
        {
            InitializeServerUrl();
        }

        private void InitializeServerUrl()
        {
            activeServerUrl = !string.IsNullOrEmpty(serverUrlOverride) 
                ? serverUrlOverride 
                : ServerConfig.HUGGINGFACE_URL;
        }

        private void Start()
        {
            InitializeButtons();
            InitializeUI();
            SetupBossHistoryDropdown();
            StartCoroutine(WarmupServer());
        }

        private void OnDestroy()
        {
            if (uploadedTexture != null) Destroy(uploadedTexture);
            if (generatedImage.texture != null) Destroy(generatedImage.texture);
            if (bossSprite != null && bossSprite.texture != null) Destroy(bossSprite.texture);
        }

        private void InitializeButtons()
        {
            selectImageButton.onClick.AddListener(SelectImage);
            generateButton.onClick.AddListener(() => StartCoroutine(GenerateBossImage()));
            rerollButton.onClick.AddListener(() => StartCoroutine(GenerateBossImage()));
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

        private void SetupBossHistoryDropdown()
        {
            if (bossHistoryDropdown == null) return;

            bossHistoryDropdown.SetOnBossSelectedCallback(OnBossHistorySelected);
            UpdateBossHistoryDropdown();
        }

        private void UpdateBossHistoryDropdown()
        {
            if (bossHistoryDropdown == null) return;
            bossHistoryDropdown.UpdateHistory();
        }

        private void OnBossHistorySelected(BossHistoryData bossData)
        {
            if (bossData != null)
            {
                LoadBossFromHistory(bossData);
            }
        }

        private void LoadBossFromHistory(BossHistoryData bossData)
        {
            if (bossData.imageData == null) return;

            Texture2D texture = new Texture2D(2, 2);
            if (texture.LoadImage(bossData.imageData))
            {
                // Set high quality settings for the loaded texture
                texture.filterMode = FilterMode.Trilinear;
                texture.anisoLevel = 16;
                
                UpdateGeneratedImage(texture);
                SetBossType(bossData.bossType.ToString());
                CreateBossSprite(texture);
                
                _tempImageData = bossData.imageData;
                _hasGeneratedImage = true;
                
                UpdateStatus("이전 보스를 불러왔습니다! 이름을 입력하고 확인을 누르세요.");
                UpdateUIState();
            }
            else
            {
                Destroy(texture);
                UpdateStatus("보스 이미지를 불러오는데 실패했습니다.");
            }
        }

        private void OnBossNameChanged(string inputValue)
        {
            bossName = inputValue;
            _hasName = !string.IsNullOrWhiteSpace(inputValue);
            UpdateUIState();
        }

        private IEnumerator WarmupServer()
        {
            _isServerReady = false;
            UpdateUIState();
            UpdateStatus(STATUS_MSG_CHECKING);

            string statusUrl = activeServerUrl + STATUS_ENDPOINT;

            using (UnityWebRequest www = UnityWebRequest.Get(statusUrl))
            {
                www.timeout = REQUEST_TIMEOUT;
        
                yield return www.SendWebRequest();

                if (www.result == UnityWebRequest.Result.Success)
                {
                    ProcessServerStatus(www.downloadHandler.text);
                }
                else
                {
                    HandleError(www, "Server status check failed");
                }
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

            if (bossHistoryDropdown != null)
            {
                // Disable history button during processing
                bossHistoryDropdown.SetInteractable(!_hasConfirmedBoss && !isProcessing);
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
            SelectImageInWebGL();
#else
            UpdateStatus("File selection is only supported in Editor and WebGL builds.");
#endif
        }

#if UNITY_EDITOR
        private void SelectImageInEditor()
        {
            string path = UnityEditor.EditorUtility.OpenFilePanel("Select Image", "", "png,jpg,jpeg");
            if (!string.IsNullOrEmpty(path))
            {
                StartCoroutine(LoadImage(path));
            }
        }
#endif

#if UNITY_WEBGL
        private void SelectImageInWebGL()
        {
            WebGLFileUploader.OpenFilePicker((base64Data) => 
            {
                StartCoroutine(LoadImageFromBase64(base64Data));
            });
        }
        
        private IEnumerator LoadImageFromBase64(string base64Data)
        {
            UpdateStatus(STATUS_MSG_LOADING_IMAGE);
            isProcessing = true;
            UpdateUIState();
            
            try
            {
                string base64 = base64Data;
                if (base64.Contains(","))
                {
                    base64 = base64.Split(',')[1];
                }
                
                byte[] imageBytes = Convert.FromBase64String(base64);
                Texture2D texture = new Texture2D(2, 2);
                
                if (texture.LoadImage(imageBytes))
                {
                    ProcessLoadedTexture(texture);
                }
                else
                {
                    UpdateStatus(STATUS_MSG_FAILED_LOAD);
                    _isImageLoaded = false;
                }
            }
            catch (Exception e)
            {
                UpdateStatus($"Failed to load image: {e.Message}");
                _isImageLoaded = false;
            }
            
            isProcessing = false;
            UpdateUIState();
            yield return null;
        }
#endif

        private IEnumerator LoadImage(string path)
        {
            UpdateStatus(STATUS_MSG_LOADING_IMAGE);
            isProcessing = true;
            UpdateUIState();

            string url = path.StartsWith(FILE_PREFIX) ? path : FILE_PREFIX + path;
    
            using (UnityWebRequest www = UnityWebRequestTexture.GetTexture(url))
            {
                yield return www.SendWebRequest();

                if (www.result == UnityWebRequest.Result.Success)
                {
                    ProcessLoadedTexture(DownloadHandlerTexture.GetContent(www));
                }
                else
                {
                    UpdateStatus("Failed to load image: " + www.error);
                    _isImageLoaded = false;
                }
            }
    
            isProcessing = false;
            UpdateUIState();
        }

        private void ProcessLoadedTexture(Texture2D texture)
        {
            if (uploadedTexture != null && uploadedTexture != texture) 
                Destroy(uploadedTexture);
            
            // Set high quality settings for loaded texture
            texture.filterMode = FilterMode.Trilinear;
            texture.anisoLevel = 16;
                
            uploadedTexture = texture;
            originalImage.texture = uploadedTexture;
            RawImageFitter.Fit(originalImage);
            FitAndCrop(originalImage);

            _isImageLoaded = true;
            _hasGeneratedImage = false;

            UpdateStatus(STATUS_MSG_IMAGE_LOADED);
        }

        private IEnumerator GenerateBossImage()
        {
            if (uploadedTexture == null || isProcessing || !_isServerReady)
            {
                if (!_isServerReady) UpdateStatus(STATUS_MSG_SERVER_NOT_READY);
                if (uploadedTexture == null) UpdateStatus(STATUS_MSG_SELECT_IMAGE);
                yield break;
            }
    
            yield return GenerateImage();
        }

        private IEnumerator GenerateImage()
        {
            isProcessing = true;
            UpdateUIState();

            if (loadingIndicator) loadingIndicator.SetActive(true);
            float startTime = Time.time;

            UpdateStatus(STATUS_MSG_ENCODING);
            yield return null;

            string requestJson = CreateGenerationRequest();

            using (var www = CreatePredictRequest(requestJson))
            {
                Coroutine progressCoroutine = StartCoroutine(ShowProgress(startTime, STATUS_MSG_GENERATING));
        
                yield return www.SendWebRequest();
        
                StopCoroutine(progressCoroutine);

                if (www.result == UnityWebRequest.Result.Success)
                {
                    ProcessServerResponse(www.downloadHandler.text);
                }
                else
                {
                    HandleError(www, "Image generation failed");
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
            
            string url = activeServerUrl + PREDICT_ENDPOINT;
            
            var www = new UnityWebRequest(url, "POST");
            www.uploadHandler = new UploadHandlerRaw(jsonBytes);
            www.downloadHandler = new DownloadHandlerBuffer();
            www.SetRequestHeader("Content-Type", "application/json");
            www.timeout = GENERATION_TIMEOUT;
            
            return www;
        }

        private IEnumerator ShowProgress(float startTime, string prefix)
        {
            int dotCount = 0;
            StringBuilder progressBuilder = new StringBuilder(64);
            
            while (isProcessing)
            {
                float elapsed = Time.time - startTime;
        
                progressBuilder.Clear();
                progressBuilder.Append(prefix);
                for (int i = 0; i < dotCount; i++)
                {
                    progressBuilder.Append('.');
                }
                progressBuilder.Append(" (").Append(Mathf.FloorToInt(elapsed)).Append("s)");
        
                UpdateStatus(progressBuilder.ToString());

                dotCount = (dotCount + 1) % 4;
                yield return new WaitForSeconds(PROGRESS_UPDATE_INTERVAL);
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
                    return ERROR_MSG_CONNECTION;
                case UnityWebRequest.Result.ProtocolError:
                    return $"Server error ({responseCode}). Please try again later.";
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
            
            if (!string.IsNullOrEmpty(response.pokemon_type))
            {
                SetBossType(response.pokemon_type);
            }

            UpdateStatus(STATUS_MSG_BOSS_GENERATED);
            if (response.duration > 0) 
            {
                Debug.Log($"Generation took {response.duration:F2} seconds");
            }

            CreateBossSprite(generatedTexture);
            _hasGeneratedImage = true;

            if (BossContainer.Instance != null)
            {
                BossContainer.Instance.AddToHistory(_tempImageData, BossContainer.Instance.CurrentBossType);
                UpdateBossHistoryDropdown();
            }
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

            // Set high quality settings
            texture.filterMode = FilterMode.Trilinear;
            texture.anisoLevel = 16;
            
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
        
        private void SetBossType(string typeString)
        {
            ElementType elementType = ParseElementType(typeString);
            
            if (BossContainer.Instance != null)
            {
                BossContainer.Instance.CurrentBossType = elementType;
            }
            
            Debug.Log($"Boss type set to: {elementType}");
        }
        
        private ElementType ParseElementType(string typeString)
        {
            if (string.IsNullOrEmpty(typeString))
                return ElementType.Dark;
        
            string lowercaseType = typeString.ToLower();
    
            if (lowercaseType.Contains("blaze"))
                return ElementType.Blaze;
            else if (lowercaseType.Contains("tide"))
                return ElementType.Tide;
            else if (lowercaseType.Contains("mystic"))
                return ElementType.Mystic;
            else if (lowercaseType.Contains("terra"))
                return ElementType.Terra;
            else if (lowercaseType.Contains("nature"))
                return ElementType.Nature;
            else if (lowercaseType.Contains("dark"))
                return ElementType.Dark;
            else if (lowercaseType.Contains("light"))
                return ElementType.Light;
            else if (lowercaseType.Contains("storm"))
                return ElementType.Storm;
            else
                return ElementType.Dark;
        }

        private void CreateBossSprite(Texture2D texture)
        {
            if (bossSprite != null && bossSprite.texture != null) 
                Destroy(bossSprite.texture);
            
            // Ensure texture has high quality settings
            texture.filterMode = FilterMode.Trilinear;
            texture.anisoLevel = 16;
                
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
                
                string statusMessage = STATUS_MSG_BOSS_CREATED_PREFIX + bossName + STATUS_MSG_BOSS_CREATED_SUFFIX;
                UpdateStatus(statusMessage);
                
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
#if UNITY_WEBGL && !UNITY_EDITOR
            if (!_hasGeneratedImage || _tempImageData == null)
            {
                UpdateStatus(STATUS_MSG_NO_IMAGE);
                return;
            }
            
            string fileName = GenerateFileName();
            string base64Data = DATA_IMAGE_JPEG + Convert.ToBase64String(_tempImageData);
            WebGLFileUploader.DownloadFile(fileName, base64Data);
            UpdateStatus($"Download started: {fileName}");
#else
            UpdateStatus("Download is only supported in WebGL builds.");
#endif
        }

        private string GenerateFileName()
        {
            string safeName = !string.IsNullOrEmpty(bossName) 
                ? bossName.Replace(" ", "_") 
                : DEFAULT_BOSS_NAME;
            
            return safeName + "_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".png";
        }

        private void ReturnToPreviousScene()
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene(MAIN_MENU_SCENE);
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