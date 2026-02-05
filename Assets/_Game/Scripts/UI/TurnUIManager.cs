using UnityEngine;
using TMPro;
using Managers;
using Systems;
using Unity.Netcode;

namespace UI
{
    /// <summary>
    /// Class นี้ทำหน้าที่จัดการ UI ที่เกี่ยวกับเทิร์นโดยเฉพาะ (View Layer)
    /// </summary>
    public class TurnUIManager : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private TextMeshProUGUI turnText;
        [SerializeField] private TextMeshProUGUI cycleText;
        [SerializeField] private TextMeshProUGUI timeText;
        
        // เก็บ Reference ของคนที่ "กำลังเล่นอยู่" (ไม่ใช่แค่ตัวเรา)
        private PlayerTimeSystem _activePlayerTimeSystem;

        private void Start()
        {
            // Subscribe Event เพื่อรอรับค่าเมื่อ Turn เปลี่ยน
            if (TurnManager.Instance != null)
            {
                TurnManager.Instance.OnRoundChanged += UpdateTurnUI;
                TurnManager.Instance.OnCycleChanged += UpdateCycleUI;
                TurnManager.Instance.OnPlayerTurnChanged += OnPlayerTurnChanged;
                TurnManager.Instance.OnGameStateChanged += OnGameStateChanged;
                
                // เช็คสถานะเริ่มต้น
                if (TurnManager.Instance.IsGameStarted)
                {
                    OnGameStateChanged(true);
                }
                else
                {
                    ClearUI();
                }
            }
        }
        
        private void OnDestroy()
        {
            if (TurnManager.Instance != null)
            {
                TurnManager.Instance.OnRoundChanged -= UpdateTurnUI;
                TurnManager.Instance.OnCycleChanged -= UpdateCycleUI;
                TurnManager.Instance.OnPlayerTurnChanged -= OnPlayerTurnChanged;
                TurnManager.Instance.OnGameStateChanged -= OnGameStateChanged;
            }
            
            // ล้าง Event ของคนเก่า (ถ้ามี)
            if (_activePlayerTimeSystem != null)
            {
                _activePlayerTimeSystem.OnTimeChanged -= UpdateTimeUI;
            }
        }
        
        private void ClearUI()
        {
            if (turnText != null) turnText.text = "";
            if (cycleText != null) cycleText.text = "";
            if (timeText != null) timeText.text = "";
        }
        
        private void OnGameStateChanged(bool isStarted)
        {
            if (isStarted)
            {
                UpdateTurnUI(TurnManager.Instance.CurrentRound);
                UpdateCycleUI(TurnManager.Instance.CurrentCycle);
                
                // เริ่มหาเวลาของคนแรก
                OnPlayerTurnChanged(TurnManager.Instance.CurrentActivePlayerId);
            }
            else
            {
                ClearUI();
            }
        }
        
        // เมื่อมีการเปลี่ยนตาเล่น (ได้รับ ID ของคนเล่นคนใหม่)
        private void OnPlayerTurnChanged(ulong activePlayerId)
        {
            // ยกเลิกการฟังเวลาของคนเก่า
            if (_activePlayerTimeSystem != null)
            {
                _activePlayerTimeSystem.OnTimeChanged -= UpdateTimeUI;
                _activePlayerTimeSystem = null;
            }

            // ค้นหา Object ของคนเล่นคนใหม่ (FIXED LOGIC)
            // เราจะไม่ใช้ TryGetValue(activePlayerId) เพราะ Key มันไม่ใช่ ClientID
            // เราต้องวนลูปหาว่า "NetworkObject ตัวไหนที่มี OwnerClientId ตรงกับ activePlayerId"
            
            PlayerTimeSystem foundPlayer = null;

            foreach (var netObj in NetworkManager.Singleton.SpawnManager.SpawnedObjects.Values)
            {
                if (netObj.OwnerClientId == activePlayerId)
                {
                    foundPlayer = netObj.GetComponent<PlayerTimeSystem>();
                    break; // เจอแล้วหยุดหาทันที
                }
            }

            if (foundPlayer != null)
            {
                _activePlayerTimeSystem = foundPlayer;
                _activePlayerTimeSystem.OnTimeChanged += UpdateTimeUI;
                
                Debug.Log($"[UI] จับคู่เวลาสำเร็จ! ของผู้เล่น ID: {activePlayerId}");
            }
            else
            {
                // กรณีนี้อาจเกิดขึ้นถ้าระบบ Network ยัง Spawn ตัวไม่ทัน (Rare case)
                // เราอาจจะใช้ Coroutine รอค้นหาอีกรอบได้ในอนาคต แต่เบื้องต้นวิธีนี้ควรจะเจอถ้าตัวละครเกิดแล้ว
                Debug.LogWarning($"[UI] ยังหา Player Object ของ ClientID {activePlayerId} ไม่เจอในรายการ SpawnedObjects");
            }
        }

        private void UpdateTurnUI(int turn) 
        {
            // เช็คเผื่อ null (กัน Error เวลาปิดซีน)
            // ถ้าอยากเขียนอีกแบบก็ได้นะ ใช้ ?.(null-conditional operator) => turnText?.text = (turn > 0) ? $"Round: {turn}" : ""; ได้เหมือนกัน แค่สวยกว่า
            if (turnText != null) turnText.text = (turn > 0) ? $"Round: {turn}" : "";
        }
        
        private void UpdateCycleUI(int cycle) 
        {
            // cycleText?.text = (cycle > 0) ? $"Cycle (Month): {cycle}" : "";
            if (cycleText != null) cycleText.text = (cycle > 0) ? $"Cycle (Month): {cycle}" : "";
        }
        
        private void UpdateTimeUI(float time)
        {
            // แปลงวินาที เป็นรูปแบบ นาที:วินาที (เช่น 01:59)
            int minutes = Mathf.FloorToInt(time / 60F);
            int seconds = Mathf.FloorToInt(time % 60F);
            
            // String Interpolation ($"...")
            timeText.text = $"Time: {minutes:00}:{seconds:00}"; 

            // เปลี่ยนสีถ้าเวลาน้อย
            timeText.color = time <= 10 ? Color.red : Color.white;
        }
    }   
}