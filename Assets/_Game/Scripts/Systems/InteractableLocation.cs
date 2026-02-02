using UnityEngine;
using Configs;
using UI;

namespace Systems
{
    /// <summary>
    /// สคริปต์สำหรับแปะที่ตึก เพื่อตรวจจับการชนของผู้เล่น
    /// </summary>
    [RequireComponent(typeof(BoxCollider))] // บังคับว่าต้องมี Collider
    public class InteractableLocation : MonoBehaviour
    {
        [Header("Config")]
        [SerializeField] private LocationConfig _locationConfig;

        private void OnTriggerEnter(Collider other)
        {
            // เช็คว่าสิ่งที่ชนมี PlayerTimeSystem หรือไม่ (เช็คว่าเป็นตัวผู้เล่นไหม)
            if (other.TryGetComponent(out PlayerTimeSystem player))
            {
                // UI จะขึ้นเฉพาะเครื่องที่เป็น "เจ้าของ" ตัวละครนั้น
                if (player.IsOwner)
                {
                    Debug.Log($"[Interaction] ผู้เล่นเดินเข้าสู่ {_locationConfig.LocationName}");
                    InteractionUIManager.Instance.ShowLocation(_locationConfig, player);
                }
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (other.TryGetComponent(out PlayerTimeSystem player))
            {
                if (player.IsOwner)
                {
                    InteractionUIManager.Instance.HideUI();
                }
            }
        }
    }
}