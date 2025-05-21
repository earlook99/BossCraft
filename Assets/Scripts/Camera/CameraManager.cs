using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.Cinemachine;

namespace Camera
{
    public enum CineCamType
    {
        BattleNormal,
        BattleOutZoom
    }
    
    [Serializable]
    public struct CamMapping
    {
        public CineCamType CamType;
        public CinemachineCamera VirtualCamera;
    }
    
    public class CameraManager : MonoBehaviour
    {
        [SerializeField] private List<CamMapping> camMappings;
        
        private Dictionary<CineCamType, CinemachineCamera> camDict;

        private void Awake()
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
                    Debug.LogWarning($"중복된 CamType: {mapping.CamType}");
                }
            }
        }

        public void SwitchCameraTo(CineCamType camType)
        {
            foreach (var kvp in camDict)
            {
                kvp.Value.Priority = 0;
            }

            if (camDict.TryGetValue(camType, out var virtualCam))
            {
                virtualCam.Priority = 10;
            }
            else
            {
                Debug.LogError($"[CameraManager] {camType} 타입 카메라가 camDict에 없습니다!");
            }
        }
    }
}
