using UnityEngine;
using TMPro;
using Managers;
using Systems;
using Unity.Netcode;

namespace UI
{
    public class TurnUIManager : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private TextMeshProUGUI turnText;
        [SerializeField] private TextMeshProUGUI cycleText;
        [SerializeField] private TextMeshProUGUI timeText;

        // เปลี่ยน Reference เป็น PlayerPointSystem
        private PlayerPointSystem _activePlayerPointSystem;

        private void Start()
        {
            if (TurnManager.Instance != null)
            {
                TurnManager.Instance.OnRoundChanged += UpdateTurnUI;
                TurnManager.Instance.OnCycleChanged += UpdateCycleUI;
                TurnManager.Instance.OnPlayerTurnChanged += OnPlayerTurnChanged;
            }
        }

        private void OnPlayerTurnChanged(ulong activePlayerId)
        {
            // ล้างการฟัง Event ของคนเก่า
            if (_activePlayerPointSystem != null)
            {
                _activePlayerPointSystem.OnVirtualTimeChanged -= UpdateTimeUI;
            }

            foreach (var netObj in NetworkManager.Singleton.SpawnManager.SpawnedObjects.Values)
            {
                if (netObj.OwnerClientId == activePlayerId)
                {
                    _activePlayerPointSystem = netObj.GetComponent<PlayerPointSystem>();
                    if (_activePlayerPointSystem != null)
                    {
                        // ฟัง Event "เวลาจำลอง" ที่ถูกคำนวณมาแล้วจาก PointSystem
                        _activePlayerPointSystem.OnVirtualTimeChanged += UpdateTimeUI;
                    }
                    break;
                }
            }
        }

        private void UpdateTimeUI(int totalSeconds)
        {
            int minutes = totalSeconds / 60;
            int seconds = totalSeconds % 60;

            // แสดงผลเป็นเวลา (02:00, 01:54...) ตามแต้มที่เหลือ
            timeText.text = $"Time: {minutes:00}:{seconds:00}";
            timeText.color = totalSeconds <= 20 ? Color.red : Color.white;
        }

        // ฟังก์ชัน UpdateTurnUI และ UpdateCycleUI ใช้ของเดิมได้เลย
        private void UpdateTurnUI(int turn) => turnText.text = turn > 0 ? $"Round: {turn}" : "";
        private void UpdateCycleUI(int cycle) => cycleText.text = cycle > 0 ? $"Month: {cycle}" : "";
    }
}