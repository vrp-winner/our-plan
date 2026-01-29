using UnityEngine;
using TMPro;
using Unity.Netcode;

/// <summary>
/// Class นี้ทำหน้าที่จัดการ UI ที่เกี่ยวกับเทิร์นโดยเฉพาะ (View Layer)
/// </summary>
public class TurnUIManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private TextMeshProUGUI turnText;
    [SerializeField] private TextMeshProUGUI cycleText;
    [SerializeField] private TextMeshProUGUI timeText;
    
    private PlayerTimeSystem localPlayerTime;

    private void Start()
    {
        // Subscribe Event เพื่อรอรับค่าเมื่อ Turn เปลี่ยน
        // อาจจะต้องรอให้ TurnManager พร้อมก่อนในเคส Network จริง แต่เบื้องต้นใช้แบบนี้ไปก่อนละกันนะ
        if (TurnManager.Instance != null)
        {
            TurnManager.Instance.OnTurnChanged += UpdateTurnUI;
            TurnManager.Instance.OnCycleChanged += UpdateCycleUI;
        }
        
        InvokeRepeating(nameof(FindLocalPlayer), 0.5f, 0.5f);
    }
    
    private void FindLocalPlayer()
    {
        if (localPlayerTime != null) return;

        foreach (var player in FindObjectsByType<PlayerTimeSystem>(FindObjectsSortMode.None))
        {
            if (player.IsOwner) // ถ้าเป็นตัวของเรา
            {
                localPlayerTime = player;
                localPlayerTime.OnTimeChanged += UpdateTimeUI;
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
        if (localPlayerTime != null)
        {
            localPlayerTime.OnTimeChanged -= UpdateTimeUI;
        }
    }

    private void UpdateTurnUI(int turn) => turnText.text = $"Turn: {turn}";
    private void UpdateCycleUI(int cycle) => cycleText.text = $"Cycle (Month): {cycle}";
    
    private void UpdateTimeUI(float time)
    {
        // แปลงวินาที เป็นรูปแบบ นาที:วินาที (เช่น 01:59)
        int minutes = Mathf.FloorToInt(time / 60F);
        int seconds = Mathf.FloorToInt(time % 60F);
        timeText.text = string.Format("Time: {0:00}:{1:00}", minutes, seconds);

        // เปลี่ยนสีถ้าเวลาน้อย
        timeText.color = time <= 10 ? Color.red : Color.white;
    }
}