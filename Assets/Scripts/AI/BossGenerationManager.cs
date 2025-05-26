using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

namespace AI
{
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
        public float guidance_scale = 3.0f;  // JSON 키 이름과 일치하도록
    
        [Range(10, 100)]
        public int inference_steps = 40;     // JSON 키 이름과 일치하도록
    
        [Range(0.0f, 1.0f)]
        public float lora_weight = 0.7f;
    
        [TextArea(2, 4)]
        public string negative_prompt = "realistic, photograph, human, normal animal";
    
        public int seed = -1;
    }
    
    /// <summary>
    /// 이미지 업로드 및 AI 보스 생성 테스트
    /// </summary>
    public class BossGenerationManager : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private Button selectImageButton;
        [SerializeField] private Button generateButton;
        [SerializeField] private Button returnButton;
        [SerializeField] private RawImage originalImage;
        [SerializeField] private RawImage generatedImage;
        [SerializeField] private TextMeshProUGUI statusText;
        [SerializeField] private GameObject loadingIndicator;
        
        [Header("Generation Parameters")]
        [SerializeField] private GenerationParameters parameters = new GenerationParameters();
        
        [Header("Server Settings")]
        private string serverUrl = "";
        
        private Texture2D uploadedTexture;
        private bool isProcessing = false;
        
        private void Awake()
        {
            // Inspector에 값이 없으면 Config에서 가져오기
            if (string.IsNullOrEmpty(serverUrl))
            {
                #if UNITY_EDITOR
                    serverUrl = ServerConfig.HUGGINGFACE_URL;
                #else
                    serverUrl = "http://localhost:8000"; // 빌드 시 기본값
                #endif
            }
        }
        
        private void Start()
        {
            selectImageButton.onClick.AddListener(SelectImage);
            generateButton.onClick.AddListener(GenerateBossImage);
            
            generateButton.interactable = false;
            if (loadingIndicator) loadingIndicator.SetActive(false);
            
            UpdateStatus("Click 'Select Image' to start");
            Debug.Log($"Server URL: {serverUrl}");
            
            // 서버 웜업
            StartCoroutine(WarmupServer());
        }
        
        /// <summary>
        /// 서버 웜업 - Cold Start 문제 해결
        /// </summary>
        private IEnumerator WarmupServer()
        {
            UpdateStatus("Waking up server...");
            
            using (UnityWebRequest www = UnityWebRequest.Get($"{serverUrl}/warmup"))
            {
                www.timeout = 60; // Cold start는 시간이 더 걸림
                yield return www.SendWebRequest();
                
                if (www.result == UnityWebRequest.Result.Success)
                {
                    UpdateStatus("Server is ready! Select an image to start.");
                    Debug.Log("Server warmup successful");
                }
                else
                {
                    UpdateStatus("Server warmup failed. It might take a moment to start.");
                    Debug.LogWarning($"Server warmup failed: {www.error}");
                }
            }
        }
        
        /// <summary>
        /// 이미지 선택 (간단한 테스트용)
        /// </summary>
        private void SelectImage()
        {
            if (isProcessing) return;
            
            // Unity 에디터에서 테스트용
            #if UNITY_EDITOR
            string path = UnityEditor.EditorUtility.OpenFilePanel("Select Image", "", "png,jpg,jpeg");
            if (!string.IsNullOrEmpty(path))
            {
                StartCoroutine(LoadImage(path));
            }
            #else
            // 빌드에서는 다른 방법 필요 (플러그인 사용 등)
            UpdateStatus("File selection not implemented for this platform");
            #endif
        }
        
        private IEnumerator LoadImage(string path)
        {
            using (UnityWebRequest www = UnityWebRequestTexture.GetTexture("file://" + path))
            {
                yield return www.SendWebRequest();
                
                if (www.result == UnityWebRequest.Result.Success)
                {
                    uploadedTexture = DownloadHandlerTexture.GetContent(www);
                    originalImage.texture = uploadedTexture;
                    generateButton.interactable = true;
                    UpdateStatus("Image loaded! Click 'Generate' to create boss");
                }
                else
                {
                    UpdateStatus($"Failed to load image: {www.error}");
                    Debug.LogError($"Error loading from path: {path}");
                }
            }
        }
        
        /// <summary>
        /// AI 서버에 이미지 생성 요청
        /// </summary>
        private void GenerateBossImage()
        {
            if (uploadedTexture == null || isProcessing) return;
            
            StartCoroutine(GenerateImageCoroutine());
        }
        
        private IEnumerator GenerateImageCoroutine()
        {
            isProcessing = true;
            if (loadingIndicator) loadingIndicator.SetActive(true);
            generateButton.interactable = false;
            selectImageButton.interactable = false;
            returnButton.interactable = false;
    
            float startTime = Time.time;
            UpdateStatus("Connecting to server...");

            // 이미지를 Base64로 인코딩
            byte[] imageBytes = uploadedTexture.EncodeToJPG(85);
            string base64Image = Convert.ToBase64String(imageBytes);

            // 요청 데이터 생성
            var request = new BossGenerationRequest
            {
                data = new string[] { $"data:image/jpeg;base64,{base64Image}" },
                parameters = parameters  // Inspector에서 설정한 parameters 객체 그대로 사용
            };
    
            string json = JsonUtility.ToJson(request);
            Debug.Log($"Request JSON: {json}"); // 디버깅용
    
            byte[] jsonBytes = System.Text.Encoding.UTF8.GetBytes(json);

            // API 호출
            using (UnityWebRequest www = new UnityWebRequest($"{serverUrl}/api/predict", "POST"))
            {
                www.uploadHandler = new UploadHandlerRaw(jsonBytes);
                www.downloadHandler = new DownloadHandlerBuffer();
                www.SetRequestHeader("Content-Type", "application/json");
                www.timeout = 120;

                Coroutine progressCoroutine = StartCoroutine(ShowProgress(startTime));
        
                yield return www.SendWebRequest();
        
                if (progressCoroutine != null)
                    StopCoroutine(progressCoroutine);

                if (www.result == UnityWebRequest.Result.Success)
                {
                    Debug.Log("Success! Processing response...");
                    ProcessGradioResponse(www.downloadHandler.text);
                }
                else
                {
                    HandleError(www);
                }
            }

            if (loadingIndicator) loadingIndicator.SetActive(false);
            generateButton.interactable = true;
            selectImageButton.interactable = true;
            returnButton.interactable = true;
            isProcessing = false;
        }
        
        /// <summary>
        /// 진행 시간 표시
        /// </summary>
        private IEnumerator ShowProgress(float startTime)
        {
            int dotCount = 0;
    
            while (true)
            {
                float elapsed = Time.time - startTime;
        
                // 점 생성 (0~3개 반복)
                string dots = new string('.', dotCount);
                UpdateStatus($"Processing{dots}");
        
                // 0, 1, 2, 3, 0, 1, 2, 3... 반복
                dotCount = (dotCount + 1) % 4;
        
                yield return new WaitForSeconds(0.5f);
            }
        }
        
        /// <summary>
        /// 에러 처리 개선
        /// </summary>
        private void HandleError(UnityWebRequest www)
        {
            if (www.responseCode == 0)
            {
                UpdateStatus("Connection failed. Server might be starting up. Please try again in 30 seconds.");
                Debug.LogError("No response from server - it might be sleeping or starting up");
            }
            else if (www.error.Contains("timeout"))
            {
                UpdateStatus("Request timed out. The server might be busy. Please try again.");
                Debug.LogError("Request timeout - server is taking too long");
            }
            else if (www.responseCode >= 500)
            {
                UpdateStatus($"Server error ({www.responseCode}). Please try again later.");
                Debug.LogError($"Server error: {www.responseCode}");
            }
            else
            {
                UpdateStatus($"Error: {www.error} (Code: {www.responseCode})");
                Debug.LogError($"HTTP Status: {www.responseCode}");
            }
            
            if (!string.IsNullOrEmpty(www.downloadHandler?.text))
            {
                Debug.LogError("Response: " + www.downloadHandler.text);
            }
        }
        
        /// <summary>
        /// Gradio 응답 처리
        /// </summary>
        private void ProcessGradioResponse(string jsonResponse)
        {
            try
            {
                // Gradio 응답 파싱
                var response = JsonUtility.FromJson<GradioResponse>(jsonResponse);
                
                if (response.data != null && response.data.Length >= 2)
                {
                    // 첫 번째 데이터: 이미지 (base64)
                    string imageData = response.data[0];
                    
                    // "data:image/png;base64," 프리픽스 제거
                    if (imageData.StartsWith("data:"))
                    {
                        int commaIndex = imageData.IndexOf(',');
                        if (commaIndex > 0)
                        {
                            imageData = imageData.Substring(commaIndex + 1);
                        }
                    }
                    
                    // Base64 디코딩
                    byte[] imageBytes = Convert.FromBase64String(imageData);
                    Texture2D generatedTexture = new Texture2D(2, 2);
                    generatedTexture.LoadImage(imageBytes);
                    
                    // 생성된 이미지 표시
                    generatedImage.texture = generatedTexture;
                    
                    // 두 번째 데이터: 타입
                    string elementType = response.data[1];
                    
                    // BLIP 설명 로그 출력
                    if (!string.IsNullOrEmpty(response.blip_description))
                    {
                        Debug.Log($"<color=cyan>[BLIP Description]</color> {response.blip_description}");
                    }
            
                    // 사용된 프롬프트도 출력
                    if (!string.IsNullOrEmpty(response.prompt_used))
                    {
                        Debug.Log($"<color=yellow>[Generated Prompt]</color> {response.prompt_used}");
                    }
            
                    // 세 번째 데이터가 있으면 BLIP 설명 (하위 호환성)
                    if (response.data.Length >= 3)
                    {
                        string blipDesc = response.data[2];
                        Debug.Log($"<color=green>[BLIP from data array]</color> {blipDesc}");
                    }
                    
                    UpdateStatus($"Boss generated! Type: {elementType}");
                    Debug.Log($"Generation took {response.duration:F2} seconds");
                }
                else
                {
                    UpdateStatus("Invalid response format");
                    Debug.LogError("Response data array is null or too short");
                }
            }
            catch (Exception e)
            {
                UpdateStatus($"Failed to process response: {e.Message}");
                Debug.LogError("Parse error: " + e);
                Debug.LogError("Response was: " + jsonResponse);
            }
        }
        
        private void UpdateStatus(string message)
        {
            if (statusText) statusText.text = message;
            // Debug.Log($"[BossImageGenerator] {message}");
        }
    }
    
    /// <summary>
    /// Gradio API 응답 구조
    /// </summary>
    [Serializable]
    public class GradioResponse
    {
        public string[] data;
        public bool is_generating;
        public float duration;
        public float average_duration;
        public string blip_description;  // 추가
        public string prompt_used;       // 추가
    }
}