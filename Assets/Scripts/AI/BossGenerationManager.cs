using System;
using System.Collections;
using Data;
using GameSystem;
using TMPro;
using Unity.Plastic.Newtonsoft.Json;
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
        [JsonProperty("strength")]
        public float strength = 0.85f;

        [Range(1.0f, 20.0f)]
        [JsonProperty("guidance_scale")]
        public float guidance_scale = 3.0f;  // JSON 키 이름과 일치하도록

        [Range(10, 100)]
        [JsonProperty("inference_steps")]
        public int inference_steps = 40;     // JSON 키 이름과 일치하도록

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

    public class BossGenerationManager : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private Button selectImageButton;
        [SerializeField] private Button generateButton;
        [SerializeField] private Button returnButton;
        [SerializeField] private Button rerollButton;
        [SerializeField] private Button confirmBossButton;
        [SerializeField] private RawImage originalImage;
        [SerializeField] private RawImage generatedImage;
        [SerializeField] private TextMeshProUGUI statusText;
        [SerializeField] private GameObject loadingIndicator;
        [SerializeField] private TMP_InputField bossNameField;

        [Header("Generation Parameters")]
        [SerializeField] private GenerationParameters parameters = new GenerationParameters();

        [Header("Server Settings")]
        private string serverUrl = "";

        private Texture2D uploadedTexture;

        // 상태 플래그
        private bool isProcessing = false;       // 서버 요청 중 여부
        private bool _hasName = false;           // 보스 이름을 입력했는지 여부
        private bool _isImageLoaded = false;     // 이미지 로드 여부
        private bool _hasGeneratedImage = false; // 보스 이미지를 생성했는지 여부

        private Sprite bossSprite;
        private string bossName = "";

        private void Awake()
        {
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
            rerollButton.onClick.AddListener(GenerateBossImage);

            bossNameField.onValueChanged.AddListener(OnBossNameChanged);

            if (loadingIndicator) loadingIndicator.SetActive(false);

            // 초기 UI 상태 설정 (처음엔 이미지X -> generate만 보이되 disable, reroll은 숨김)
            _isImageLoaded = false;
            _hasGeneratedImage = false;
            UpdateUIState();

            UpdateStatus("Click 'Select Image' to start");
            Debug.Log($"Server URL: {serverUrl}");

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
            UpdateStatus("Waking up server...");

            using (UnityWebRequest www = UnityWebRequest.Get($"{serverUrl}/warmup"))
            {
                www.timeout = 60;
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
        /// UI 상태를 갱신:
        ///  1) generateButton과 rerollButton은 SetActive로 “하나만” 보이도록  
        ///  2) 나머지 버튼은 항상 보이되, 서버 요청 중이면 interactable만 꺼서 클릭 불가
        ///  3) generateButton은 이미지가 로드되지 않았으면 비활성화, 로드되면 활성화
        /// </summary>
        private void UpdateUIState()
        {
            // 1) selectImageButton / confirmBossButton / returnButton 은 항상 보임
            selectImageButton.interactable = !isProcessing; 
            returnButton.interactable       = !isProcessing; 
            confirmBossButton.interactable  = (!isProcessing && _hasName && _hasGeneratedImage);

            // 2) 보스 이미지 생성 전/후에 따라 generate <-> reroll 전환
            if (_hasGeneratedImage)
            {
                // 보스가 이미 생성됨 → reroll 모드
                generateButton.gameObject.SetActive(false);
                rerollButton.gameObject.SetActive(true);
                // 요청 중이면 클릭 막기
                rerollButton.interactable = !isProcessing;
            }
            else
            {
                // 보스가 아직 미생성 → generate 모드
                generateButton.gameObject.SetActive(true);
                rerollButton.gameObject.SetActive(false);

                // 이미지가 아직 없으면 generateButton은 비활성화
                // 이미 있으면(업로드 후) 비활성화 해제
                generateButton.interactable = (!isProcessing && _isImageLoaded);
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
#else
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

                    // 새 이미지를 불러왔으므로, 보스 이미지는 아직 없는 상태로 리셋
                    _isImageLoaded = true;
                    _hasGeneratedImage = false;

                    UpdateStatus("Image loaded! Click 'Generate' to create boss");
                    UpdateUIState();
                }
                else
                {
                    UpdateStatus($"Failed to load image: {www.error}");
                    Debug.LogError($"Error loading from path: {path}");
                }
            }
        }

        private void GenerateBossImage()
        {
            if (uploadedTexture == null || isProcessing) return;
            StartCoroutine(GenerateImageCoroutine());
        }

        private IEnumerator GenerateImageCoroutine()
        {
            isProcessing = true;
            UpdateUIState();

            if (loadingIndicator) loadingIndicator.SetActive(true);

            float startTime = Time.time;
            UpdateStatus("Connecting to server...");

            byte[] imageBytes = uploadedTexture.EncodeToJPG(85);
            string base64Image = Convert.ToBase64String(imageBytes);

            var request = new BossGenerationRequest
            {
                data = new string[] { $"data:image/jpeg;base64,{base64Image}" },
                parameters = parameters
            };

            var settings = new JsonSerializerSettings
            {
                FloatFormatHandling = FloatFormatHandling.String,
                FloatParseHandling = FloatParseHandling.Decimal,
                Formatting = Formatting.None
            };

            string json = JsonConvert.SerializeObject(request, settings);
            Debug.Log($"Request JSON: {json}");

            byte[] jsonBytes = System.Text.Encoding.UTF8.GetBytes(json);

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

            isProcessing = false;
            UpdateUIState();
        }

        private IEnumerator ShowProgress(float startTime)
        {
            int dotCount = 0;

            while (true)
            {
                float elapsed = Time.time - startTime;
                string dots = new string('.', dotCount);
                UpdateStatus($"Processing{dots}");

                dotCount = (dotCount + 1) % 4;

                yield return new WaitForSeconds(0.5f);
            }
        }

        private void HandleError(UnityWebRequest www)
        {
            if (www.responseCode == 0)
            {
                UpdateStatus("Connection failed. Server might be starting up. Please try again in 30 seconds.");
                Debug.LogError("No response from server");
            }
            else if (www.error != null && www.error.Contains("timeout"))
            {
                UpdateStatus("Request timed out. The server might be busy. Please try again.");
                Debug.LogError("Request timeout");
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

        private void ProcessGradioResponse(string jsonResponse)
        {
            try
            {
                var response = JsonConvert.DeserializeObject<GradioResponse>(jsonResponse);

                if (response.data != null && response.data.Length >= 2)
                {
                    string imageData = response.data[0];
                    if (imageData.StartsWith("data:"))
                    {
                        int commaIndex = imageData.IndexOf(',');
                        if (commaIndex > 0)
                        {
                            imageData = imageData.Substring(commaIndex + 1);
                        }
                    }

                    byte[] imageBytes = Convert.FromBase64String(imageData);
                    Texture2D generatedTexture2D = new Texture2D(2, 2);
                    generatedTexture2D.LoadImage(imageBytes);

                    generatedImage.texture = generatedTexture2D;

                    string elementType = response.data[1];

                    // 디버깅용 로그들
                    if (!string.IsNullOrEmpty(response.blip_description))
                    {
                        Debug.Log($"<color=cyan>[BLIP Description]</color> {response.blip_description}");
                    }
                    if (!string.IsNullOrEmpty(response.prompt_used))
                    {
                        Debug.Log($"<color=yellow>[Generated Prompt]</color> {response.prompt_used}");
                    }
                    if (response.data.Length >= 3)
                    {
                        string blipDesc = response.data[2];
                        Debug.Log($"<color=green>[BLIP from data array]</color> {blipDesc}");
                    }

                    UpdateStatus("Boss generated!");
                    Debug.Log($"Generation took {response.duration:F2} seconds");

                    bossSprite = Sprite.Create(
                        generatedTexture2D,
                        new Rect(0, 0, generatedTexture2D.width, generatedTexture2D.height),
                        new Vector2(0.5f, 0.5f)
                    );

                    _hasGeneratedImage = true;
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
                Debug.LogError($"Parse error: {e}");
                Debug.LogError($"Response was: {jsonResponse}");
            }
        }

        private void ConfirmBoss()
        {
            confirmBossButton.interactable = false;

            // 임시로 Blaze 설정
            BossContainer.Instance.CurrentBossSprite = bossSprite;
            BossContainer.Instance.CurrentBossType   = ElementType.Blaze;
            BossContainer.Instance.CurrentBossName   = bossName;
        }

        private void UpdateStatus(string message)
        {
            if (statusText) statusText.text = message;
        }
    }

    [Serializable]
    public class GradioResponse
    {
        public string[] data;
        public bool is_generating;
        public float duration;
        public float average_duration;
        public string blip_description;
        public string prompt_used;
    }
}
