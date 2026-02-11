using UnityEngine;
using TMPro;
using UnityEngine.UI;
using Configs;
using Managers;
using Systems;

namespace UI
{
    public class InteractionUIManager : MonoBehaviour
    {
        public static InteractionUIManager Instance { get; private set; }

        [Header("UI References")]
        [SerializeField] private GameObject panelRoot; // ตัวกล่อง UI ทั้งหมด
        [SerializeField] private TextMeshProUGUI locationNameText;
        [SerializeField] private TextMeshProUGUI descriptionText;
        [SerializeField] private Transform actionButtonsContainer; // จุดที่จะเสกปุ่ม
        [SerializeField] private Button closeButton;
        [SerializeField] private Button actionButtonPrefab; // ปุ่มต้นแบบ (Prefab)

        // เก็บ Reference ของผู้เล่นคนที่เปิดหน้าต่างนี้อยู่
        private PlayerTimeSystem _currentUser;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);

            // สั่ง HideUI ผ่าน Network
            closeButton.onClick.AddListener(OnCloseButtonClicked);
            HideUI(); // ซ่อนตอนเริ่มเกม
        }
        
        private void Start()
        {
            // Safety Net: ถ้ามีการเปลี่ยนเทิร์น ให้ปิด UI ทิ้งทันที (กันค้าง)
            if (TurnManager.Instance != null)
            {
                TurnManager.Instance.OnPlayerTurnChanged += OnTurnChanged;
            }
        }

        private void OnDestroy()
        {
            if (TurnManager.Instance != null)
            {
                TurnManager.Instance.OnPlayerTurnChanged -= OnTurnChanged;
            }
        }
        
        private void OnTurnChanged(ulong playerId)
        {
            // ไม่ว่าใครจะเล่น ถ้าเทิร์นเปลี่ยน ให้ปิดหน้าต่าง Interaction ทิ้งเลย
            HideUI();
        }
        
        // ฟังก์ชันกดปุ่ม Close (เรียกผ่าน Network)
        private void OnCloseButtonClicked()
        {
            // Cache ตัวแปรไว้กัน Null
            var playerRef = _currentUser;
            if (playerRef != null && playerRef.IsOwner)
            {
                playerRef.RequestCloseInteractionUI();
            }
            
            HideUI();
        }

        public void ShowLocation(LocationConfig config, PlayerTimeSystem player)
        {
            _currentUser = player;
            
            // เช็คว่าผู้ที่เปิดเมนูนี้ คือตัวผู้เล่นเองมั้ย
            // ถ้าใช่ -> กดปุ่มได้ (Interactive)
            // ถ้าไม่ใช่ -> ดูได้อย่างเดียว (Spectator)
            bool isMyCharacter = player.IsOwner;
            
            // ตั้งค่าข้อความ
            locationNameText.text = config.LocationName;
            
            // เปลี่ยนคำอธิบาย ให้รู้ว่าใครกำลังทำอะไร
            if (isMyCharacter)
            {
                descriptionText.text = config.Description;
                closeButton.gameObject.SetActive(true); // ปิดเองได้
            }
            else
            {
                descriptionText.text = $"[Player {player.OwnerClientId}] is visiting here...";
                closeButton.gameObject.SetActive(false); // คนอื่นกดปิดไม่ได้ (ต้องรอผู้เล่นเจ้าของตาเดินออกเอง)
            }

            // ล้างปุ่มเก่าทิ้ง (Clear old buttons)
            foreach (Transform child in actionButtonsContainer)
            {
                Destroy(child.gameObject);
            }

            // สร้างปุ่มใหม่ตาม Config (Dynamic Buttons)
            foreach (var action in config.AvailableActions)
            {
                CreateActionButton(action, isMyCharacter);
            }

            panelRoot.SetActive(true);
        }

        private void CreateActionButton(LocationAction action, bool isInteractive)
        {
            Button btn = Instantiate(actionButtonPrefab, actionButtonsContainer);
            
            // ตั้งชื่อปุ่ม
            var btnText = btn.GetComponentInChildren<TextMeshProUGUI>();
            if (btnText != null)
            {
                btnText.text = $"{action.ActionName} (-{action.TimeCost}s)";
            }
            
            // ถ้าเป็นจอผู้เล่นอื่นจะดูเฉยๆ ให้ปิดปุ่ม (สีเทา)
            btn.interactable = isInteractive;

            // ผูกฟังก์ชันเมื่อกดปุ่ม
            if (isInteractive)
            {
                btn.onClick.AddListener(() => 
                {
                    OnActionClicked(action);
                });
            }
        }

        private void OnActionClicked(LocationAction action)
        {
            // เก็บตัวแปรใส่ Local Variable ไว้ก่อน
            // เพราะถ้าเกิดการจบเทิร์น _currentUser อาจถูกเซ็ตเป็น null ได้
            var playerRef = _currentUser;
            
            // ปิด UI ทันทีในฝั่ง Local เพื่อกันการกดซ้ำ และเคลียร์ State
            HideUI();
            
            if (playerRef != null && playerRef.IsOwner)
            {
                // สั่งปิด UI ผ่าน Network
                playerRef.RequestCloseInteractionUI();
                
                // เช็คชื่อ Action เพื่อเรียกฟังก์ชันให้ถูกตัว
                if (action.ActionName == "Sleep")
                {
                    // ถ้าเป็น Sleep ให้เรียกฟังก์ชันพิเศษ (จบเทิร์นทันที)
                    playerRef.SleepAndEndTurn();
                }
                else
                {
                    // ถ้าเป็น Action อื่นๆ (เช่น Work) ให้เช็คเวลาตามปกติ
                    playerRef.RequestAction(action.TimeCost);
                }
            }
        }

        public void HideUI()
        {
            panelRoot.SetActive(false);
            _currentUser = null;
        }
    }
}