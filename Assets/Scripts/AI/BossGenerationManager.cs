using System;
using System.Collections;
using System.IO;
using Data; // 사용자 정의 네임스페이스 (ElementType 등)
using GameSystem; // 사용자 정의 네임스페이스 (ServerConfig, BossContainer 등)
using TMPro;
using UI;
// using Unity.Plastic.Newtonsoft.Json; // Unity 에디터용 Newtonsoft.Json
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using WebGL;

namespace AI
{
    // 서버의 /status 경로 응답을 위한 클래스
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
        // [JsonProperty("strength")]
        public float strength = 0.85f;

        [Range(1.0f, 20.0f)]
        // [JsonProperty("guidance_scale")]
        public float guidance_scale = 3.0f;

        [Range(10, 100)]
        // [JsonProperty("inference_steps")]
        public int inference_steps = 40;

        [Range(0.0f, 1.0f)]
       //  [JsonProperty("lora_weight")]
        public float lora_weight = 0.7f;

        [TextArea(2, 4)]
        // [JsonProperty("prompt")]
        public string prompt = "";

        [TextArea(2, 4)]
        // [JsonProperty("negative_prompt")]
        public string negative_prompt = "realistic, photograph, human, normal animal";

        // [JsonProperty("seed")]
        public int seed = -1;
    }
    
    [Serializable]
    public class ParametersUsed // 서버가 반환하는 parameters_used 필드를 위한 클래스
    {
        public float strength;
        public float guidance_scale;
        public int inference_steps;
        public float lora_weight;
        public int seed;
        public string intended_type_pass1; // 👈 1단계 의도된 타입
        public string observed_type_pass2; // 👈 2단계 관찰된 타입
        public string final_type_decision; // 👈 최종 결정된 타입
    }

    // 서버의 /api/predict 경로 응답을 위한 클래스 (기존 유지, 필요시 FastAPI 응답과 정확히 일치하도록 검토)
    [Serializable]
    public class ServerPredictResponse // GradioResponse 대신 좀 더 명확한 이름으로 변경 (선택 사항)
    {
        public string[] data; // [0]: base64 image, [1]: type string for display, [2]: original caption from GPT pass 1
        public string caption; // The keyword caption from GPT Pass 1
        public string pokemon_type; // The final decided type
        public float duration;
        public ParametersUsed parameters_used; // 👈 추가된 필드
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
        [Tooltip("If empty, uses ServerConfig.HUGGINGFACE_URL in Editor, or http://localhost:8000 in build.")]
        [SerializeField] private string serverUrlOverride = ""; // 인스펙터에서 URL을 직접 설정할 수 있도록 변경 (선택 사항)

        private string activeServerUrl = ""; // 실제 사용될 서버 URL

        private Texture2D uploadedTexture;

        // 상태 플래그
        private bool isProcessing = false;
        private bool _hasName = false;
        private bool _isImageLoaded = false;
        private bool _hasGeneratedImage = false;
        private bool _isServerReady = false; // 서버 준비 완료 상태 플래그 추가
        private bool _hasConfirmedBoss = false;

        private Sprite bossSprite;
        private string bossName = "";
        
        private byte[] _tempImageData;

        private void Awake()
        {
            if (!string.IsNullOrEmpty(serverUrlOverride))
            {
                activeServerUrl = serverUrlOverride;
            }
            else
            {
                // WebGL 빌드에서도 ServerConfig 사용
                activeServerUrl = ServerConfig.HUGGINGFACE_URL;
            }
    
            Debug.Log($"Active Server URL set to: {activeServerUrl}");
        }

        private void Start()
        {
            selectImageButton.onClick.AddListener(SelectImage);
            generateButton.onClick.AddListener(GenerateBossImage);
            rerollButton.onClick.AddListener(GenerateBossImage);
            confirmBossButton.onClick.AddListener(ConfirmBoss); // ConfirmBoss 리스너 연결
            returnButton.onClick.AddListener(ReturnToPreviousScene); // Return 버튼 리스너 (함수 구현 필요)
            downloadButton.onClick.AddListener(DownloadGeneratedImage);


            bossNameField.onValueChanged.AddListener(OnBossNameChanged);

            if (loadingIndicator) loadingIndicator.SetActive(false);

            _isImageLoaded = false;
            _hasGeneratedImage = false;
            _isServerReady = false; // 서버 준비 안된 상태로 시작
            UpdateUIState();

            //UpdateStatus("Click 'Select Image' to start"); // 초기 메시지는 WarmupServer에서 관리
            Debug.Log($"Server URL for requests: {activeServerUrl}");

            StartCoroutine(WarmupServer());
        }

        private void OnBossNameChanged(string inputValue)
        {
            bossName = inputValue;
            _hasName = !string.IsNullOrWhiteSpace(inputValue);
            UpdateUIState();
        }

        private IEnumerator WarmupServer()
        {
            _isServerReady = false; // 워밍업 시작 시 서버 미준비 상태로 설정
            UpdateUIState(); // AI 관련 버튼 비활성화
            UpdateStatus("Checking server status...");

            // 요청 URL을 /status 로 변경
            string statusUrl = $"{activeServerUrl}/status";
            Debug.Log($"Attempting to connect to server status at: {statusUrl}");

            using (UnityWebRequest www = UnityWebRequest.Get(statusUrl))
            {
                www.timeout = 60; // 서버가 깨어나는 시간 등을 고려
                yield return www.SendWebRequest();

                if (www.result == UnityWebRequest.Result.Success)
                {
                    try
                    {
                        Debug.Log($"Server response from {statusUrl}: {www.downloadHandler.text}");
                        ServerStatusResponse serverStatus = JsonUtility.FromJson<ServerStatusResponse>(www.downloadHandler.text);

                        if (serverStatus != null && serverStatus.status == "online")
                        {
                            if (serverStatus.models_loaded)
                            {
                                _isServerReady = true;
                                UpdateStatus("Server is ready! Select an image to start.");
                                Debug.Log("Server is online and models are loaded.");
                            }
                            else
                            {
                                UpdateStatus("Server online, but models are still loading. Please wait...");
                                Debug.LogWarning("Server is online, but models are not loaded yet. You might need to retry or wait longer.");
                                // 필요시 여기서 재시도 로직 호출 (예: StartCoroutine(RetryWarmupAfterDelay(10f));)
                            }
                        }
                        else
                        {
                            UpdateStatus("Server responded, but status is not 'online'.");
                            Debug.LogWarning($"Server status unknown or not 'online'. Response: {www.downloadHandler.text}");
                        }
                    }
                    catch (Exception e)
                    {
                        UpdateStatus("Failed to parse server response.");
                        Debug.LogError($"Error parsing server status from {statusUrl}: {e.Message} - Response: {www.downloadHandler.text}");
                    }
                }
                else
                {
                    HandleError(www, "Server status check failed"); // 오류 처리 함수에 컨텍스트 전달
                }
            }
            UpdateUIState(); // 워밍업 시도 후 UI 최종 업데이트
        }

        private void UpdateUIState()
        {
            bool canInteractWithServer = _isServerReady && !isProcessing;

            selectImageButton.interactable = canInteractWithServer && !_hasConfirmedBoss;
            returnButton.interactable = !isProcessing;
    
            // 보스 확정 후에는 confirm 버튼 비활성화
            confirmBossButton.interactable = (canInteractWithServer && _hasName && _hasGeneratedImage && !_hasConfirmedBoss);

            if (_hasGeneratedImage)
            {
                generateButton.gameObject.SetActive(false);
                rerollButton.gameObject.SetActive(true);
                rerollButton.interactable = canInteractWithServer && !_hasConfirmedBoss;
                downloadButton.gameObject.SetActive(true);
                downloadButton.interactable = true;  // 항상 활성화
            }
            else
            {
                generateButton.gameObject.SetActive(true);
                rerollButton.gameObject.SetActive(false);
                generateButton.interactable = (canInteractWithServer && _isImageLoaded && !_hasConfirmedBoss);
                downloadButton.gameObject.SetActive(false);
            }
    
            // 보스 확정 후 이름 필드도 비활성화
            if (bossNameField != null)
            {
                bossNameField.interactable = !_hasConfirmedBoss;
            }
        }
        
        public static void FitAndCrop(RawImage img)
        {
            if (img.texture == null) return;

            // 1) 각 종횡비 계산
            float texAspect  = (float)img.texture.width  / img.texture.height;
            float slotAspect =  img.rectTransform.rect.width / img.rectTransform.rect.height;

            // 2) 잘라낼 쪽 결정 & UVRect 수정
            if (texAspect > slotAspect)          // 텍스처가 ‘더 가로로 긴’ 경우 → 좌우 자르기
            {
                float wantedWidth = slotAspect / texAspect;     // 0~1 사이 폭
                float offsetX     = (1f - wantedWidth) * 0.5f;  // 가운데 정렬
                img.uvRect = new Rect(offsetX, 0f, wantedWidth, 1f);
            }
            else                                  // 텍스처가 ‘더 세로로 긴’ 경우 → 위아래 자르기
            {
                float wantedHeight = texAspect / slotAspect;
                float offsetY      = (1f - wantedHeight) * 0.5f;
                img.uvRect = new Rect(0f, offsetY, 1f, wantedHeight);
            }
        }

        private void SelectImage()
        {
            if (isProcessing) return;

#if UNITY_EDITOR
            string path = UnityEditor.EditorUtility.OpenFilePanel("Select Image", "", "png,jpg,jpeg");
            if (!string.IsNullOrEmpty(path))
            {
                StartCoroutine(LoadImage(path));
            }
#elif UNITY_WEBGL
            WebGLFileUploader.OpenFilePicker((base64Data) => {
                StartCoroutine(LoadImageFromBase64(base64Data));
            });
#else
            UpdateStatus("File selection not implemented for this platform.");
#endif
        }

        private IEnumerator LoadImage(string path)
        {
            UpdateStatus("Loading image...");
            isProcessing = true; // 이미지 로딩도 처리 중으로 간주 (선택 사항)
            UpdateUIState();

            // 로컬 파일 경로는 file:// 접두사 필요
            using (UnityWebRequest www = UnityWebRequestTexture.GetTexture(path.StartsWith("file://") ? path : "file://" + path))
            {
                yield return www.SendWebRequest();

                if (www.result == UnityWebRequest.Result.Success)
                {
                    if (uploadedTexture != null) Destroy(uploadedTexture); // 이전 텍스처 메모리 해제
                    uploadedTexture = DownloadHandlerTexture.GetContent(www);
                    originalImage.texture = uploadedTexture;
                    RawImageFitter.Fit(originalImage);
                    
                    FitAndCrop(originalImage);

                    _isImageLoaded = true;
                    _hasGeneratedImage = false; // 새 이미지 로드 시 이전 생성 결과는 무효화

                    UpdateStatus("Image loaded! Enter boss name and click 'Generate'.");
                }
                else
                {
                    UpdateStatus($"Failed to load image: {www.error}");
                    Debug.LogError($"Error loading image from path: {path} - {www.error}");
                    _isImageLoaded = false;
                }
            }
            isProcessing = false;
            UpdateUIState();
        }
        
        private IEnumerator LoadImageFromBase64(string base64Data)
        {
            UpdateStatus("Loading image...");
            isProcessing = true;
            UpdateUIState();

            string base64 = base64Data;
            if (base64.Contains(","))
            {
                base64 = base64.Split(',')[1];
            }

            byte[] imageBytes = Convert.FromBase64String(base64);
    
            if (uploadedTexture != null) Destroy(uploadedTexture);
    
            uploadedTexture = new Texture2D(2, 2);
            if (uploadedTexture.LoadImage(imageBytes))
            {
                originalImage.texture = uploadedTexture;
                RawImageFitter.Fit(originalImage);
                FitAndCrop(originalImage);

                _isImageLoaded = true;
                _hasGeneratedImage = false;
        
                UpdateStatus("Image loaded! Enter boss name and click 'Generate'.");
            }
            else
            {
                UpdateStatus("Failed to load image.");
                _isImageLoaded = false;
            }

            isProcessing = false;
            UpdateUIState();
            yield return null;
        }

        private void GenerateBossImage()
        {
            if (uploadedTexture == null || isProcessing || !_isServerReady) // 서버 미준비 시 생성 불가
            {
                if (!_isServerReady) UpdateStatus("Server is not ready. Please wait.");
                if (uploadedTexture == null) UpdateStatus("Please select an image first.");
                return;
            }
            StartCoroutine(GenerateImageCoroutine());
        }

        private IEnumerator GenerateImageCoroutine()
        {
            isProcessing = true;
            UpdateUIState();

            if (loadingIndicator) loadingIndicator.SetActive(true);
            float startTime = Time.time;

            UpdateStatus("Encoding image...");
            yield return null; // 한 프레임 지연으로 UI 업데이트 반영

            byte[] imageBytes = uploadedTexture.EncodeToJPG(85); // JPG로 인코딩, 퀄리티 85
            string base64Image = Convert.ToBase64String(imageBytes);

            var request = new BossGenerationRequest
            {
                data = new string[] { $"data:image/jpeg;base64,{base64Image}" }, // MIME 타입 jpeg로 변경
                parameters = parameters
            };

            // JsonSerializerSettings settings = new JsonSerializerSettings
            // {
            //     // FloatFormatHandling = FloatFormatHandling.String, // 보통 불필요
            //     // FloatParseHandling = FloatParseHandling.Decimal,  // 보통 불필요
            //     Formatting = Formatting.None // 압축된 JSON
            // };

            string json = JsonUtility.ToJson(request);
            Debug.Log($"Request JSON (first 1000 chars): {json.Substring(0, Mathf.Min(json.Length, 1000))}"); // 너무 길면 자르기

            byte[] jsonBytes = System.Text.Encoding.UTF8.GetBytes(json);

            using (UnityWebRequest www = new UnityWebRequest($"{activeServerUrl}/api/predict", "POST"))
            {
                www.uploadHandler = new UploadHandlerRaw(jsonBytes);
                www.downloadHandler = new DownloadHandlerBuffer();
                www.SetRequestHeader("Content-Type", "application/json");
                www.timeout = 180; // 이미지 생성 시간을 고려하여 타임아웃 늘림 (120 -> 180)

                UpdateStatus("Generating boss (0s)..."); // 초기 진행 상태
                Coroutine progressCoroutine = StartCoroutine(ShowProgress(startTime, "Generating boss"));

                yield return www.SendWebRequest();

                if (progressCoroutine != null) StopCoroutine(progressCoroutine);

                if (www.result == UnityWebRequest.Result.Success)
                {
                    Debug.Log("Success! Processing response...");
                    ProcessServerResponse(www.downloadHandler.text); // 메소드 이름 변경 (GradioResponse -> ServerResponse)
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

        private IEnumerator ShowProgress(float startTime, string prefix = "Processing")
        {
            int dotCount = 0;
            while (isProcessing) // isProcessing 플래그를 사용하여 코루틴 제어
            {
                float elapsed = Time.time - startTime;
                string dots = new string('.', dotCount);
                UpdateStatus($"{prefix}{dots} ({Mathf.FloorToInt(elapsed)}s)");

                dotCount = (dotCount + 1) % 4;
                yield return new WaitForSeconds(0.5f);
            }
        }

        private void HandleError(UnityWebRequest www, string contextMessage = "Server request failed")
        {
            string detailedError;
            if (www.responseCode == 0 && string.IsNullOrEmpty(www.error)) // 연결 시도조차 못한 경우 (예: DNS 오류, 서버 주소 오타)
            {
                detailedError = "Cannot connect to server. Check URL and network.";
                UpdateStatus(detailedError);
                Debug.LogError($"{contextMessage}: {detailedError} (URL: {www.url})");
            }
            else if (www.result == UnityWebRequest.Result.ConnectionError) // 연결 관련 오류
            {
                detailedError = $"Connection error: {www.error}";
                UpdateStatus("Connection failed. Server might be down or network issues.");
                Debug.LogError($"{contextMessage}: {detailedError} (URL: {www.url})");
            }
            else if (www.result == UnityWebRequest.Result.ProtocolError) // HTTP 오류 (4xx, 5xx)
            {
                detailedError = $"HTTP error: {www.responseCode} - {www.error}";
                UpdateStatus($"Server error ({www.responseCode}). Please try again later.");
                Debug.LogError($"{contextMessage}: {detailedError} (URL: {www.url})");
            }
            else if (www.result == UnityWebRequest.Result.DataProcessingError) // 데이터 처리 오류 (거의 발생 안함)
            {
                detailedError = $"Data processing error: {www.error}";
                UpdateStatus("Error processing data from server.");
                Debug.LogError($"{contextMessage}: {detailedError} (URL: {www.url})");
            }
            else // 기타 타임아웃 등
            {
                detailedError = $"Unknown error or timeout: {www.error}";
                UpdateStatus("Request failed or timed out. Please try again.");
                Debug.LogError($"{contextMessage}: {detailedError} (URL: {www.url})");
            }

            if (!string.IsNullOrEmpty(www.downloadHandler?.text))
            {
                Debug.LogError($"Error Response Body from {www.url}: {www.downloadHandler.text}");
            }
        }
        
        private void ProcessServerResponse(string jsonResponse)
        {
            try
            {
                Debug.Log($"[RAW SERVER RESPONSE] /api/predict: {jsonResponse}"); // 전체 JSON 로깅 (디버깅용)
                var response = JsonUtility.FromJson<ServerPredictResponse>(jsonResponse);
                
                if (response?.data != null && response.data.Length >= 1)
                {
                    // ... (이미지 처리 로직은 기존과 동일) ...
                    string imageData = response.data[0];
                    if (imageData.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase)) 
                    {
                        int commaIndex = imageData.IndexOf(',');
                        if (commaIndex > 0) imageData = imageData.Substring(commaIndex + 1);
                    }
                    else { Debug.LogWarning("Image data does not start with 'data:image/', assuming raw base64."); }
                    
                    byte[] imageBytes = Convert.FromBase64String(imageData);
                    _tempImageData = imageBytes;
                    
                    Texture2D generatedTexture2D = new Texture2D(2, 2);
                    if (!generatedTexture2D.LoadImage(imageBytes))
                    {
                        UpdateStatus("Failed to load generated image data.");
                        Debug.LogError("Failed to load image data from base64. Data might be corrupted.");
                        Destroy(generatedTexture2D); return;
                    }
#if UNITY_EDITOR
                    generatedTexture2D.alphaIsTransparency = true; generatedTexture2D.Apply();
#endif
                    
                    // 알파 채널 존재 여부 간단히 확인 (디버깅용)
                    // Color[] pixels = generatedTexture2D.GetPixels(0, 0, Mathf.Min(generatedTexture2D.width, 10), Mathf.Min(generatedTexture2D.height, 10)); // 샘플 영역
                    // bool hasTransparency = false;
                    // foreach (Color pixel in pixels) { if (pixel.a < 1.0f) { hasTransparency = true; break; } }
                    // Debug.Log($"[DEBUG] Generated image has transparency: {hasTransparency}");


                    if (generatedImage.texture != null) Destroy(generatedImage.texture);
                    generatedImage.texture = generatedTexture2D;
                    FitAndCrop(generatedImage); // FitAndCrop 추가 (만약 필요하다면)


                    // --- 타입 추론 과정 로깅 ---
                    if (response.parameters_used != null)
                    {
                        Debug.Log($"[Type Inference] Intended Type (Pass 1): {response.parameters_used.intended_type_pass1}");
                        Debug.Log($"[Type Inference] Observed Type (Pass 2): {response.parameters_used.observed_type_pass2}");
                        Debug.Log($"[Type Inference] Final Type Decision: {response.parameters_used.final_type_decision}");
                    }
                    else
                    {
                        Debug.LogWarning("[Type Inference] 'parameters_used' field not found in server response.");
                    }
                    // --- 타입 추론 로깅 끝 ---

                    // 서버에서 최종 결정된 타입을 사용하여 BossTypeInfo 구성
                    string bossTypeInfo = response.data.Length > 1 ? response.data[1] : $"{response.pokemon_type} Type Boss"; // data[1] 또는 pokemon_type 사용
                                                                                                                             // 서버 응답의 data[1]도 최종 타입을 반영하도록 수정 필요
                    string serverCaption = response.caption; // GPT Pass 1의 키워드 캡션

                    Debug.Log($"Generated Boss Display Info: {bossTypeInfo}"); // 최종 타입이 반영된 이름
                    Debug.Log($"Generated Keyword Caption from Server: {serverCaption}");
                    
                    UpdateStatus("Boss generated!");
                    if (response.duration > 0) Debug.Log($"Generation took {response.duration:F2} seconds");

                    if (bossSprite != null && bossSprite.texture != null) Destroy(bossSprite.texture); // 이전 스프라이트 텍스처 해제
                    bossSprite = Sprite.Create(
                        generatedTexture2D,
                        new Rect(0, 0, generatedTexture2D.width, generatedTexture2D.height),
                        new Vector2(0.5f, 0.5f)
                        // pixelsPerUnit은 필요에 따라 설정 (기본값 100)
                    );
                    _hasGeneratedImage = true;
                }
                else
                {
                    UpdateStatus("Invalid response format from server.");
                    Debug.LogError($"Response data array is null, empty, or invalid. Response: {jsonResponse}");
                }
            }
            catch (Exception e)
            {
                UpdateStatus($"Failed to process server response: {e.Message}");
                Debug.LogError($"Error parsing generated image response: {e}. Response JSON: {jsonResponse}");
            }
        }

        private void ConfirmBoss()
        {
            if (!_isServerReady || isProcessing || !_hasGeneratedImage || !_hasName)
            {
                Debug.LogWarning("Cannot confirm boss. Conditions not met.");
                return;
            }

            if (BossContainer.Instance != null)
            {
                BossContainer.Instance.CurrentBossImageData = _tempImageData;
                BossContainer.Instance.CurrentBossName = bossName;
        
                Debug.Log($"Boss {bossName} confirmed with sprite.");
        
                UpdateStatus($"Boss '{bossName}' has been created! Return to main menu and start the game.");
        
                _hasConfirmedBoss = true;
                UpdateUIState();
            }
            else
            {
                Debug.LogError("BossContainer.Instance is null. Cannot save boss data.");
                UpdateStatus("Error: Failed to save boss data.");
            }
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

            string fileName = (!string.IsNullOrEmpty(bossName) ? bossName.Replace(" ", "_") : "GeneratedBoss") 
                              + "_" + System.DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".png";

#if UNITY_EDITOR
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
#elif UNITY_WEBGL
            string base64Data = "data:image/png;base64," + Convert.ToBase64String(imageBytes);
            WebGLFileUploader.DownloadFile(fileName, base64Data);
            UpdateStatus($"Image downloaded: {fileName}");
#else
            UpdateStatus("Download not supported on this platform.");
#endif
        }

        private void ReturnToPreviousScene() // 예시 함수
        {
            // 이전 씬으로 돌아가는 로직 (예: 메인 메뉴)
            // UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenuScene");
            Debug.Log("Return button clicked. Implement scene transition.");
        }


        private void UpdateStatus(string message)
        {
            if (statusText) statusText.text = message;
        }

        // 앱 종료 또는 오브젝트 파괴 시 메모리 정리 (선택 사항)
        private void OnDestroy()
        {
            if (uploadedTexture != null) Destroy(uploadedTexture);
            if (generatedImage.texture != null) Destroy(generatedImage.texture); // RawImage의 텍스처도 해제
            if (bossSprite != null && bossSprite.texture != null) Destroy(bossSprite.texture); // 스프라이트의 텍스처도 해제
        }
    }
}