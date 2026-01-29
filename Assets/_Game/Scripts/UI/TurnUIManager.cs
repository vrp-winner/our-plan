using UnityEngine;
using TMPro;
using Managers;
using Systems;

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
        
        private PlayerTimeSystem _localPlayerTime;

        private void Start()
        {
            // Subscribe Event เพื่อรอรับค่าเมื่อ Turn เปลี่ยน
            if (TurnManager.Instance != null)
            {
                TurnManager.Instance.OnTurnChanged += UpdateTurnUI;
                TurnManager.Instance.OnCycleChanged += UpdateCycleUI;
            }
            
            InvokeRepeating(nameof(FindLocalPlayer), 0.5f, 0.5f);
        }
        
        private void FindLocalPlayer()
        {
            if (_localPlayerTime != null) return;

            foreach (var player in FindObjectsByType<PlayerTimeSystem>(FindObjectsSortMode.None))
            {
                if (player.IsOwner) // ถ้าเป็นตัวของเรา
                {
                    _localPlayerTime = player;
                    _localPlayerTime.OnTimeChanged += UpdateTimeUI;
                    CancelInvoke(nameof(FindLocalPlayer));
                    break;
                }
            }
        }
        
        private void OnDestroy()
        {
            CancelInvoke(nameof(FindLocalPlayer)); // หยุดการค้นหาเมื่อ UI ถูกปิด
            
            if (TurnManager.Instance != null)
            {
                TurnManager.Instance.OnTurnChanged -= UpdateTurnUI;
                TurnManager.Instance.OnCycleChanged -= UpdateCycleUI;
            }
            if (_localPlayerTime != null)
            {
                _localPlayerTime.OnTimeChanged -= UpdateTimeUI;
            }
        }

        private void UpdateTurnUI(int turn) 
        {
            // เช็คเผื่อ null (กัน Error เวลาปิดซีน)
            // ถ้าอยากเขียนอีกแบบก็ได้นะ ใช้ ?.(null-conditional operator) => turnText?.text = $"Turn: {turn}"; ได้เหมือนกัน แค่สวยกว่า
            if (turnText != null) turnText.text = $"Turn: {turn}";
        }
        
        private void UpdateCycleUI(int cycle) 
        {
            // cycleText?.text = $"Cycle (Month): {cycle}";
            if (cycleText != null) cycleText.text = $"Cycle (Month): {cycle}";
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