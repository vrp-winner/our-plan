using UnityEngine;
using TMPro;
using Managers;
using Systems;
using Unity.Netcode;

namespace UI
{
    /// <summary>
    /// ตัวจัดการ UI ในการแสดงสถานะ Turn/Month และเวลาของ Active Actor (Presentation Layer)
    /// อัปเดตการ Sync UI ตรงๆ จาก NetworkVariable เพื่อให้ทุก Client เห็นตรงกัน 100%
    /// </summary>
    public class TurnUIManager : MonoBehaviour
    {
        #region References (UI References)
        [Header("References")]
        [SerializeField] private TextMeshProUGUI turnText;
        [SerializeField] private TextMeshProUGUI cycleText;
        [SerializeField] private TextMeshProUGUI timeText;

        private PlayerPointSystem _activePlayerPointSystem;
        #endregion

        #region Unity Lifecycle
        /// <summary>
        /// ผูก Event การอัปเดตรอบและเทิร์นจาก TurnManager
        /// </summary>
        private void Start()
        {
            // STEP 1: ตรวจสอบ Instance ของ TurnManager
            if (TurnManager.Instance != null)
            {
                // STEP 2: ผูก Event ต่างๆ
                TurnManager.Instance.OnRoundChanged += UpdateTurnUI;
                TurnManager.Instance.OnCycleChanged += UpdateCycleUI;
                TurnManager.Instance.activeActorNetworkId.OnValueChanged += HandleActiveActorChanged;
                
                // STEP 3: [CRITICAL FIX] บังคับโหลดค่าจาก NetworkVariable ทันทีเมื่อเกิด เพื่อป้องกัน UI ว่าง
                UpdateTurnUI(TurnManager.Instance.currentRound.Value);
                UpdateCycleUI(TurnManager.Instance.currentCycle.Value);
                OnPlayerTurnChanged(TurnManager.Instance.activeActorNetworkId.Value);
            }
        }
        
        /// <summary>
        /// เคลียร์ Event เมื่อถูกทำลาย
        /// </summary>
        private void OnDestroy()
        {
            if (TurnManager.Instance != null)
            {
                TurnManager.Instance.OnRoundChanged -= UpdateTurnUI;
                TurnManager.Instance.OnCycleChanged -= UpdateCycleUI;
                TurnManager.Instance.activeActorNetworkId.OnValueChanged -= HandleActiveActorChanged;
            }
            if (_activePlayerPointSystem != null)
            {
                _activePlayerPointSystem.OnVirtualTimeChanged -= UpdateTimeUI;
            }
        }
        #endregion

        #region Update Logic (Time & Turn Logic)
        /// <summary>
        /// รับ Event เมื่อ Active Actor Network ID มีการเปลี่ยนแปลง
        /// </summary>
        /// <param name="oldId">ID เดิม</param>
        /// <param name="newId">ID ใหม่</param>
        private void HandleActiveActorChanged(ulong oldId, ulong newId)
        {
            OnPlayerTurnChanged(newId);
        }

        /// <summary>
        /// สลับการติดตามเวลาไปยังตัวละครที่กำลังอยู่ในเทิร์น (Active Actor)
        /// </summary>
        /// <param name="activeActorId">NetworkObjectId ของ Active Actor</param>
        private void OnPlayerTurnChanged(ulong activeActorId)
        {
            // STEP 1: ล้าง Event ของตัวละครก่อนหน้า 
            if (_activePlayerPointSystem != null)
            {
                _activePlayerPointSystem.OnVirtualTimeChanged -= UpdateTimeUI;
            }

            // STEP 2: ค้นหา Object ด้วย NetworkObjectId ในฝั่ง Client
            if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(activeActorId, out var netObj))
            {
                _activePlayerPointSystem = netObj.GetComponent<PlayerPointSystem>();
                
                // STEP 3: ผูก Event ตัวใหม่และสั่งโหลดข้อมูล
                if (_activePlayerPointSystem != null)
                {
                    _activePlayerPointSystem.OnVirtualTimeChanged += UpdateTimeUI;
                    _activePlayerPointSystem.RefreshTimeUI();
                }
            }
        }

        /// <summary>
        /// อัปเดตข้อความเวลาบน UI
        /// </summary>
        /// <param name="totalSeconds">เวลาทั้งหมดเป็นวินาที</param>
        private void UpdateTimeUI(int totalSeconds)
        {
            // STEP 1: แปลงเวลาเป็นรูปแบบ นาที:วินาที
            int minutes = totalSeconds / 60;
            int seconds = totalSeconds % 60;

            // STEP 2: แสดงผลและตั้งค่าสีแจ้งเตือน
            timeText.text = $"Time: {minutes:00}:{seconds:00}";
            timeText.color = totalSeconds <= 20 ? Color.red : Color.white;
        }

        /// <summary>
        /// อัปเดตรอบ (Round) บน UI
        /// </summary>
        /// <param name="turn">รอบที่</param>
        private void UpdateTurnUI(int turn) => turnText.text = turn > 0 ? $"Round: {turn}" : "";
        
        /// <summary>
        /// อัปเดตเดือน (Cycle) บน UI
        /// </summary>
        /// <param name="cycle">เดือนที่</param>
        private void UpdateCycleUI(int cycle) => cycleText.text = cycle > 0 ? $"Month: {cycle}" : "";
        #endregion
    }
}