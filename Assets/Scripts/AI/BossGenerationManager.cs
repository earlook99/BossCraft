using System;
using System.Collections;
using System.IO;
using Data; // 사용자 정의 네임스페이스 (ElementType 등)
using GameSystem; // 사용자 정의 네임스페이스 (ServerConfig, BossContainer 등)
using TMPro;
using UI;
using Unity.Plastic.Newtonsoft.Json; // Unity 에디터용 Newtonsoft.Json
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

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
        [JsonProperty("strength")]
        public float strength = 0.85f;

        [Range(1.0f, 20.0f)]
        [JsonProperty("guidance_scale")]
        public float guidance_scale = 3.0f;

        [Range(10, 100)]
        [JsonProperty("inference_steps")]
        public int inference_steps = 40;

        [Range(0.0f, 1.0f)]
        [JsonProperty("lora_weight")]
        public float lora_weight = 0.7f;

        [TextArea(2, 4)]
        [JsonProperty("prompt")]
        public string prompt = "";

        [TextArea(2, 4)]
        [JsonProperty("negative_prompt")]
        public string negative_prompt = "realistic, photograph, human, normal animal";

        [JsonProperty("seed")]
        public int seed = -1;
    }

    // 서버의 /api/predict 경로 응답을 위한 클래스 (기존 유지, 필요시 FastAPI 응답과 정확히 일치하도록 검토)
    [Serializable]
    public class GradioResponse // 클래스 이름이 GradioResponse로 되어 있으나, 실제 FastAPI 응답 구조에 맞게 필드 조정 필요
    {
        public string[] data;
        // is_generating, average_duration, blip_description, prompt_used 등은
        // FastAPI 서버의 실제 응답에 맞춰 추가/제거/수정해야 합니다.
        // 예시: public string caption; public string pokemon_type;
        public float duration;

        // FastAPI 응답에 맞게 추가/수정 필요한 필드들 (예시)
        public string caption; // FastAPI 응답에 있는 'caption' 필드
        public string pokemon_type; // FastAPI 응답에 있는 'pokemon_type' 필드
        // public bool is_generating; // FastAPI 응답에 없다면 제거 또는 주석 처리
        // public float average_duration; // FastAPI 응답에 없다면 제거 또는 주석 처리
        // public string blip_description; // FastAPI 응답에 없다면 제거 또는 주석 처리
        // public string prompt_used; // FastAPI 응답에 없다면 제거 또는 주석 처리
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
            else if (string.IsNullOrEmpty(activeServerUrl)) // serverUrlOverride가 비어있고 activeServerUrl도 아직 설정 안됐으면
            {
#if UNITY_EDITOR
                activeServerUrl = ServerConfig.HUGGINGFACE_URL; // ServerConfig.cs에 정의된 URL 사용
#else
                activeServerUrl = "http://localhost:8000"; // 빌드 시 기본값
#endif
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
                        ServerStatusResponse serverStatus = JsonConvert.DeserializeObject<ServerStatusResponse>(www.downloadHandler.text);

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
            // if (isProcessing || !_isServerReady) return; // 서버 미준비 시 선택 불가 (UpdateUIState에서 이미 처리)
            if (isProcessing) return; // isProcessing만 체크해도 UI에서 이미 interactable 관리됨

#if UNITY_EDITOR
            string path = UnityEditor.EditorUtility.OpenFilePanel("Select Image", "", "png,jpg,jpeg");
            if (!string.IsNullOrEmpty(path))
            {
                StartCoroutine(LoadImage(path));
            }
#elif UNITY_ANDROID || UNITY_IOS
            // 모바일 플랫폼용 이미지 선택 로직 (예: NativeGallery 사용)
            // NativeGallery.GetImageFromGallery((imagePath) => {
            //     if (!string.IsNullOrEmpty(imagePath)) {
            //         StartCoroutine(LoadImage(imagePath));
            //     }
            // }, "Select Image", "image/*");
            UpdateStatus("Image selection from gallery not yet fully implemented for mobile.");
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

            JsonSerializerSettings settings = new JsonSerializerSettings
            {
                // FloatFormatHandling = FloatFormatHandling.String, // 보통 불필요
                // FloatParseHandling = FloatParseHandling.Decimal,  // 보통 불필요
                Formatting = Formatting.None // 압축된 JSON
            };

            string json = JsonConvert.SerializeObject(request, settings);
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
                // FastAPI 응답에 맞춘 GradioResponse 클래스 사용 (필드 확인 및 조정 필요)
                var response = JsonConvert.DeserializeObject<GradioResponse>(jsonResponse);

                if (response?.data != null && response.data.Length >= 1) // 최소 이미지 데이터는 있어야 함
                {
                    string imageData = response.data[0];
                    if (imageData.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase)) // MIME 타입 유연하게 체크
                    {
                        int commaIndex = imageData.IndexOf(',');
                        if (commaIndex > 0)
                        {
                            imageData = imageData.Substring(commaIndex + 1);
                        }
                    }
                    else
                    {
                        // data:image/... 형식이 아니면 순수 base64로 간주 (또는 오류 처리)
                        Debug.LogWarning("Image data does not start with 'data:image/', assuming raw base64.");
                    }


                    byte[] imageBytes = Convert.FromBase64String(imageData);
                    _tempImageData = imageBytes;
                    
                    Texture2D generatedTexture2D = new Texture2D(2, 2); // 초기 크기는 중요하지 않음, LoadImage가 실제 크기로 변경
                    if (!generatedTexture2D.LoadImage(imageBytes))
                    {
                        UpdateStatus("Failed to load generated image data.");
                        Debug.LogError("Failed to load image data from base64 string. Data might be corrupted or not an image.");
                        Destroy(generatedTexture2D); // 실패 시 텍스처 정리
                        return;
                    }
                    
                    // 이 부분이 핵심!
                    generatedTexture2D.alphaIsTransparency = true;
                    generatedTexture2D.Apply();
                    
                    // 알파 채널 체크
                    Color[] pixels = generatedTexture2D.GetPixels();
                    bool hasTransparency = false;
                    for (int i = 0; i < Mathf.Min(pixels.Length, 100); i++) // 처음 100픽셀만 체크
                    {
                        if (pixels[i].a < 1f)
                        {
                            hasTransparency = true;
                            break;
                        }
                    }
                    Debug.Log($"[DEBUG] Image has transparency: {hasTransparency}");

                    if (generatedImage.texture != null) Destroy(generatedImage.texture); // 이전 텍스처 해제
                    generatedImage.texture = generatedTexture2D;

                    // FastAPI 응답 구조에 따라 추가 정보 파싱
                    // 예: response.data[1] (보스 타입 문자열), response.data[2] (캡션)
                    // 또는 response.caption, response.pokemon_type 필드 직접 사용 (GradioResponse 클래스에 해당 필드 정의 필요)
                    string bossTypeInfo = response.data.Length > 1 ? response.data[1] : "Unknown Type";
                    string serverCaption = response.caption ?? (response.data.Length > 2 ? response.data[2] : "No caption");

                    Debug.Log($"Generated Boss Type Info: {bossTypeInfo}");
                    Debug.Log($"Generated Boss Caption from Server: {serverCaption}");
                    // 여기서 파싱한 bossTypeInfo에서 실제 ElementType을 추출하는 로직 필요

                    UpdateStatus("Boss generated!");
                    if (response.duration > 0) Debug.Log($"Generation took {response.duration:F2} seconds");


                    if (bossSprite != null) Destroy(bossSprite.texture);
                    bossSprite = Sprite.Create(
                        generatedTexture2D,
                        new Rect(0, 0, generatedTexture2D.width, generatedTexture2D.height),
                        new Vector2(0.5f, 0.5f),
                        1f
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
                Debug.LogWarning("Download attempt failed: No generated image available.");
                return;
            }

            Texture2D textureToSave = generatedImage.texture as Texture2D;
            if (textureToSave == null)
            {
                UpdateStatus("Error: Generated image format is incorrect.");
                Debug.LogError("Download attempt failed: generatedImage.texture is not a Texture2D.");
                return;
            }

            // 이미지를 PNG 바이트 배열로 인코딩
            // PNG는 품질이 좋고 투명도를 지원합니다. JPG를 사용하려면 EncodeToJPG()를 사용하세요.
            byte[] imageBytes = textureToSave.EncodeToPNG();
            if (imageBytes == null)
            {
                UpdateStatus("Error: Failed to encode image.");
                Debug.LogError("Download attempt failed: EncodeToPNG returned null.");
                return;
            }

            string fileName = (!string.IsNullOrEmpty(bossName) ? bossName.Replace(" ", "_") : "GeneratedBoss") + "_" + System.DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".png";

            #if UNITY_EDITOR
            string path = UnityEditor.EditorUtility.SaveFilePanel(
                "Save Generated Boss Image",
                "", // 기본 폴더 (비워두면 마지막 사용 폴더 또는 기본값)
                fileName,
                "png");

            if (!string.IsNullOrEmpty(path))
            {
                try
                {
                    File.WriteAllBytes(path, imageBytes);
                    UpdateStatus($"Image saved: {Path.GetFileName(path)}");
                    Debug.Log($"Image saved to: {path}");
                }
                catch (Exception e)
                {
                    UpdateStatus("Error saving image.");
                    Debug.LogError($"Failed to save image to {path}: {e.Message}");
                }
            }
            else
            {
                UpdateStatus("Download cancelled.");
            }
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