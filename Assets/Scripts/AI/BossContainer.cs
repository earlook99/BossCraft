using GameSystem;
using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace AI
{
    [Serializable]
    public class BossHistoryData
    {
        public byte[] imageData;
        public byte[] thumbnailData;
        public ElementType bossType;
        public long timestamp;

        public BossHistoryData(byte[] imageData, ElementType bossType)
        {
            this.imageData = imageData;
            this.bossType = bossType;
            this.timestamp = DateTime.Now.ToBinary();
            this.thumbnailData = CreateThumbnail(imageData);
        }

        private byte[] CreateThumbnail(byte[] originalImageData)
        {
            try
            {
                Texture2D originalTexture = new Texture2D(2, 2);
                if (!originalTexture.LoadImage(originalImageData))
                {
                    Object.DestroyImmediate(originalTexture);
                    return null;
                }

                // Increase thumbnail size for better quality
                const int thumbnailSize = 512;
                // Use high quality settings for RenderTexture
                RenderTexture renderTexture = RenderTexture.GetTemporary(thumbnailSize, thumbnailSize, 0, RenderTextureFormat.ARGB32);
                renderTexture.filterMode = FilterMode.Trilinear;
                renderTexture.antiAliasing = 4;
                
                // Set high quality texture settings
                originalTexture.filterMode = FilterMode.Trilinear;
                originalTexture.anisoLevel = 16;
                
                Graphics.Blit(originalTexture, renderTexture);

                RenderTexture.active = renderTexture;
                Texture2D thumbnailTexture = new Texture2D(thumbnailSize, thumbnailSize, TextureFormat.ARGB32, false);
                thumbnailTexture.ReadPixels(new Rect(0, 0, thumbnailSize, thumbnailSize), 0, 0);
                thumbnailTexture.Apply();
                RenderTexture.active = null;

                // Use PNG for better quality (lossless)
                byte[] thumbnailBytes = thumbnailTexture.EncodeToPNG();

                UnityEngine.Object.DestroyImmediate(originalTexture);
                UnityEngine.Object.DestroyImmediate(thumbnailTexture);
                RenderTexture.ReleaseTemporary(renderTexture);

                return thumbnailBytes;
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to create thumbnail: {e.Message}");
                return null;
            }
        }

        public Texture2D GetThumbnailTexture()
        {
            if (thumbnailData == null || thumbnailData.Length == 0)
            {
                Debug.LogError("[BossHistoryData] thumbnailData is null or empty!");
                return null;
            }

            try
            {
                Debug.Log($"[BossHistoryData] Loading thumbnail, data size: {thumbnailData.Length} bytes");
                
                Texture2D thumbnail = new Texture2D(2, 2);
                if (thumbnail.LoadImage(thumbnailData))
                {
                    Debug.Log($"[BossHistoryData] Thumbnail loaded successfully: {thumbnail.width}x{thumbnail.height}");
                    return thumbnail;
                }
                
                Debug.LogError("[BossHistoryData] Failed to LoadImage from thumbnailData!");
                UnityEngine.Object.DestroyImmediate(thumbnail);
                return null;
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to load thumbnail: {e.Message}");
                return null;
            }
        }
    }

    [Serializable]
    public class BossHistoryList
    {
        public List<BossHistoryData> bosses = new List<BossHistoryData>();
    }

    public class BossContainer : MonoBehaviour
    {
        private static BossContainer _instance;
        public static BossContainer Instance => _instance;

        public byte[] CurrentBossImageData;
        public string CurrentBossName;
        public ElementType CurrentBossType;

        private const int MAX_HISTORY_COUNT = 6;
        private const string HISTORY_PREF_KEY = "BossHistory";
        
        private BossHistoryList bossHistory = new BossHistoryList();

        private void Awake()
        {
            if (_instance == null)
            {
                _instance = this;
                DontDestroyOnLoad(this.gameObject);
                LoadBossHistory();
            }
            else
            {
                Destroy(gameObject);
            }
        }

        public void AddToHistory(byte[] imageData, ElementType bossType)
        {
            var newBoss = new BossHistoryData(imageData, bossType);
            bossHistory.bosses.Insert(0, newBoss);

            if (bossHistory.bosses.Count > MAX_HISTORY_COUNT)
            {
                bossHistory.bosses.RemoveAt(bossHistory.bosses.Count - 1);
            }

            SaveBossHistory();
        }

        public List<BossHistoryData> GetBossHistory()
        {
            return bossHistory.bosses;
        }

        public bool HasHistory()
        {
            return bossHistory.bosses.Count > 0;
        }

        private void SaveBossHistory()
        {
            try
            {
                var historyToSave = new BossHistoryList();
                
                foreach (var boss in bossHistory.bosses)
                {
                    if (boss.imageData != null && boss.imageData.Length > 0)
                    {
                        historyToSave.bosses.Add(new BossHistoryData(boss.imageData, boss.bossType));
                    }
                }

                string json = JsonUtility.ToJson(historyToSave);
                string base64Json = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(json));
                PlayerPrefs.SetString(HISTORY_PREF_KEY, base64Json);
                PlayerPrefs.Save();
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to save boss history: {e.Message}");
            }
        }

        private void LoadBossHistory()
        {
            try
            {
                if (PlayerPrefs.HasKey(HISTORY_PREF_KEY))
                {
                    string base64Json = PlayerPrefs.GetString(HISTORY_PREF_KEY);
                    string json = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(base64Json));
                    bossHistory = JsonUtility.FromJson<BossHistoryList>(json);
                    
                    if (bossHistory == null)
                        bossHistory = new BossHistoryList();
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to load boss history: {e.Message}");
                bossHistory = new BossHistoryList();
            }
        }
    }
}