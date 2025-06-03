using System;
using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;

namespace CameraSystem
{
    public enum CineCamType
    {
        Player1,
        Player2,
        Player3,
        Player4,
        ZoomOut,
        Boss
    }
    
    [Serializable]
    public struct CamMapping
    {
        public CineCamType CamType;
        public CinemachineCamera VirtualCamera;
    }
    
    public class CameraManager : MonoBehaviour
    {
        [SerializeField] private UnityEngine.Camera mainCamera;
        [SerializeField] private List<CamMapping> camMappings;
        
        private Dictionary<CineCamType, CinemachineCamera> camDict;

        private const int DEFAULT_PRIORITY = 0;
        private const int ACTIVE_PRIORITY = 10;

        public UnityEngine.Camera MainCamera => mainCamera;

        private void Awake()
        {
            InitializeCameraDictionary();
        }

        private void InitializeCameraDictionary()
        {
            camDict = new Dictionary<CineCamType, CinemachineCamera>();
            
            foreach (var mapping in camMappings)
            {
                if (!camDict.ContainsKey(mapping.CamType))
                {
                    camDict.Add(mapping.CamType, mapping.VirtualCamera);
                }
                else
                {
                    Debug.LogWarning($"Duplicate CamType: {mapping.CamType}");
                }
            }
        }

        public void SwitchCameraTo(CineCamType camType)
        {
            ResetAllCameraPriorities();

            if (camDict.TryGetValue(camType, out var virtualCam))
            {
                virtualCam.Priority = ACTIVE_PRIORITY;
            }
            else
            {
                Debug.LogError($"[CameraManager] Camera type {camType} not found!");
            }
        }

        private void ResetAllCameraPriorities()
        {
            foreach (var kvp in camDict)
            {
                kvp.Value.Priority = DEFAULT_PRIORITY;
            }
        }
    }
}