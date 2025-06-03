using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace WebGL
{
    public class WebGLFileUploader : MonoBehaviour
    {
        private static WebGLFileUploader _instance;
        private Action<string> _onFileSelected;
        
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern void OpenImagePicker(string objectName, string methodName);
        
        [DllImport("__Internal")]
        private static extern void DownloadImage(string filename, string base64Data);
#endif
        
        private void Awake()
        {
            if (_instance == null)
            {
                _instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }
        
        public static void OpenFilePicker(Action<string> callback)
        {
            EnsureInstance();
            
            _instance._onFileSelected = callback;
            
#if UNITY_WEBGL && !UNITY_EDITOR
            OpenImagePicker(_instance.gameObject.name, "OnFileSelected");
#else
            Debug.LogError("File picker is only supported in WebGL builds!");
#endif
        }
        
        public static void DownloadFile(string filename, string base64Data)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            DownloadImage(filename, base64Data);
#else
            Debug.LogError("Download is only supported in WebGL builds!");
#endif
        }
        
        private static void EnsureInstance()
        {
            if (_instance == null)
            {
                GameObject go = new GameObject("WebGLFileUploader");
                _instance = go.AddComponent<WebGLFileUploader>();
            }
        }
        
        public void OnFileSelected(string base64Data)
        {
            _onFileSelected?.Invoke(base64Data);
        }
    }
}