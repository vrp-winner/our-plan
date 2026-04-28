using System.Collections;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

namespace Systems.UI
{
    public class PlayerProfileSlot : MonoBehaviour
    {
        [Header("Slot Setup")]
        [Tooltip("กำหนดว่ากรอบนี้เป็นของใคร (0 = P1, 1 = P2, 2 = P3, 3 = P4)")]
        public int playerIndex = 0;

        [Header("UI References")]
        public GameObject rootPanel; 
        public Image avatarImage;
        public Image playerNumImage;
        
        [Header("Status Text")]
        public TextMeshProUGUI stressText;
        public TextMeshProUGUI moneyText;
        public TextMeshProUGUI relationshipText;

        [Header("Art Database (ใส่เรียงลำดับ: ญ.แดง, ช.ส้ม, ญ.เขียว, ช.เหลือง)")]
        [Tooltip("ใส่รูปกรอบ (เลือกแบบ _NewR2 หรือ _NewL2 ให้เหมาะกับฝั่งของจอ)")]
        public Sprite[] frameSprites;
        
        [Tooltip("ใส่รูปคำว่า Player (ใส่ให้ตรงกับ Index ของกรอบนี้ให้ครบทั้ง 4 สี)")]
        public Sprite[] playerNumSprites;

        private PlayerStatus _boundPlayer;

        private void Start()
        {
            rootPanel.SetActive(false);
            StartCoroutine(WaitAndBindPlayerCoroutine());
        }
        
        // คอยตรวจสอบว่าผู้เล่นหลุดหรือกดออกไปหรือยัง
        private void Update()
        {
            // ถ้าระบบเคยเจอผู้เล่นแล้ว แต่จู่ๆ GameObject โดนทำลายเพราะผู้เล่นกดออกห้อง
            // ให้สั่งปิดกรอบ UI ตัวเองทิ้งไปเลย
            if (rootPanel.activeSelf && _boundPlayer == null)
            {
                rootPanel.SetActive(false);
                Debug.Log($"[ProfileUI] ผู้เล่นมุมที่ {playerIndex} ออกจากเกม ปิดกรอบทิ้ง");
            }
        }

        private void OnDestroy()
        {
            if (_boundPlayer != null)
            {
                _boundPlayer.Relationship.OnValueChanged -= OnStatsChanged;
                _boundPlayer.Stress.OnValueChanged -= OnStatsChanged;
                _boundPlayer.PersonalMoney.OnValueChanged -= OnMoneyChanged;
            }
        }

        private IEnumerator WaitAndBindPlayerCoroutine()
        {
            yield return new WaitUntil(() => NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening);
            yield return new WaitForSeconds(0.5f);

            PlayerStatus[] allPlayers = FindObjectsByType<PlayerStatus>(FindObjectsSortMode.None);
            
            foreach (var player in allPlayers)
            {
                if (player.teamId.Value == playerIndex)
                {
                    BindPlayer(player);
                    yield break; 
                }
            }

            Debug.Log($"[ProfileUI] ไม่พบผู้เล่น Index {playerIndex} ปิดการแสดงผลกรอบนี้");
        }

        private void BindPlayer(PlayerStatus player)
        {
            _boundPlayer = player;
            int avatarIdx = player.avatarIndex.Value;

            // ระบบสลับรูปภาพ (Sprite Swapping)
            // 1. เปลี่ยนรูปกรอบและหน้าตัวละคร
            if (avatarIdx >= 0 && avatarIdx < frameSprites.Length && frameSprites[avatarIdx] != null)
            {
                avatarImage.sprite = frameSprites[avatarIdx];
                
                // คืนค่า Scale ให้เป็นปกติ เผื่อก่อนหน้านี้เคยโดนพลิกไว้
                avatarImage.transform.localScale = Vector3.one; 
            }

            // 2. เปลี่ยนรูป P(X) ให้สีตรงกับตัวละคร
            if (avatarIdx >= 0 && avatarIdx < playerNumSprites.Length && playerNumSprites[avatarIdx] != null)
            {
                playerNumImage.sprite = playerNumSprites[avatarIdx];
                playerNumImage.color = Color.white;
            }

            _boundPlayer.Relationship.OnValueChanged += OnStatsChanged;
            _boundPlayer.Stress.OnValueChanged += OnStatsChanged;
            _boundPlayer.PersonalMoney.OnValueChanged += OnMoneyChanged;

            RefreshUI();
            rootPanel.SetActive(true);
        }

        private void OnStatsChanged(int oldVal, int newVal) => RefreshUI();
        private void OnMoneyChanged(float oldVal, float newVal) => RefreshUI();

        private void RefreshUI()
        {
            if (_boundPlayer == null) return;

            if (relationshipText != null) relationshipText.text = _boundPlayer.Relationship.Value.ToString();
            if (stressText != null) stressText.text = _boundPlayer.Stress.Value.ToString();
            if (moneyText != null) moneyText.text = _boundPlayer.PersonalMoney.Value.ToString("N0");
        }
    }
}