using UnityEngine;
using Unity.Netcode;
using Unity.Cinemachine;
using Managers;
using System.Collections;

namespace Systems
{
    /// <summary>
    /// ให้กล้องตามผู้เล่นที่อยู่ในเทิร์นปัจจุบัน
    /// </summary>
    public class TurnCameraFollower : MonoBehaviour
    {
        #region Lifecycle
        private void Start()
        {
            if (TurnManager.Instance != null)
            {
                // ฟังการเปลี่ยน active actor
                TurnManager.Instance.activeActorNetworkId.OnValueChanged += OnActiveActorChanged;
                
                // Initial Sync
                StartCoroutine(WaitAndBindCamera(TurnManager.Instance.activeActorNetworkId.Value));
            }
        }

        private void OnDestroy()
        {
            if (TurnManager.Instance != null)
            {
                TurnManager.Instance.activeActorNetworkId.OnValueChanged -= OnActiveActorChanged;
            }
        }
        #endregion

        #region Camera Logic
        private void OnActiveActorChanged(ulong oldId, ulong newId)
        {
            if (newId == 0) return;
            StartCoroutine(WaitAndBindCamera(newId));
        }

        private IEnumerator WaitAndBindCamera(ulong actorId)
        {
            if (actorId == 0) yield break;

            int attempts = 0;
            int maxAttempts = 10;
            NetworkObject netObj = null;

            // รอจนกว่า NetworkObject จะ spawn
            while (attempts < maxAttempts)
            {
                if (NetworkManager.Singleton != null && 
                    NetworkManager.Singleton.SpawnManager != null && 
                    NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(actorId, out netObj))
                {
                    break; 
                }

                attempts++;
                yield return new WaitForSeconds(0.1f);
            }

            if (netObj != null)
            {
                var vCam = FindFirstObjectByType<CinemachineCamera>();
                
                if (vCam != null)
                {
                    vCam.Follow = netObj.transform;
                    Debug.Log($"[Camera] ผูกกล้องสำเร็จ! ติดตาม Actor ID: {actorId}");
                }
            }
        }
        #endregion
    }
}