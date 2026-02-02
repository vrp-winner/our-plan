using UnityEngine;
using TMPro;
using UnityEngine.UI;
using Configs;
using Systems;
using System.Collections.Generic;

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

            closeButton.onClick.AddListener(HideUI);
            HideUI(); // ซ่อนตอนเริ่มเกม
        }

        public void ShowLocation(LocationConfig config, PlayerTimeSystem player)
        {
            _currentUser = player;
            
            // ตั้งค่าข้อความ
            locationNameText.text = config.LocationName;
            descriptionText.text = config.Description;

            // ล้างปุ่มเก่าทิ้ง (Clear old buttons)
            foreach (Transform child in actionButtonsContainer)
            {
                Destroy(child.gameObject);
            }

            // สร้างปุ่มใหม่ตาม Config (Dynamic Buttons)
            foreach (var action in config.AvailableActions)
            {
                CreateActionButton(action);
            }

            panelRoot.SetActive(true);
        }

        private void CreateActionButton(LocationAction action)
        {
            Button btn = Instantiate(actionButtonPrefab, actionButtonsContainer);
            
            // ตั้งชื่อปุ่ม
            var btnText = btn.GetComponentInChildren<TextMeshProUGUI>();
            if (btnText != null)
            {
                btnText.text = $"{action.ActionName} (-{action.TimeCost}s)";
            }

            // ผูกฟังก์ชันเมื่อกดปุ่ม
            btn.onClick.AddListener(() => 
            {
                OnActionClicked(action);
            });
        }

        private void OnActionClicked(LocationAction action)
        {
            if (_currentUser != null)
            {
                // เช็คชื่อ Action เพื่อเรียกฟังก์ชันให้ถูกตัว
                if (action.ActionName == "Sleep")
                {
                    // ถ้าเป็น Sleep ให้เรียกฟังก์ชันพิเศษ (จบเทิร์นทันที)
                    _currentUser.SleepAndEndTurn();
                }
                else
                {
                    // ถ้าเป็น Action อื่นๆ (เช่น Work) ให้เช็คเวลาตามปกติ
                    _currentUser.RequestAction(action.TimeCost);
                }
                
                // กดแล้วปิดหน้าต่าง
                HideUI();
            }
        }

        public void HideUI()
        {
            panelRoot.SetActive(false);
            _currentUser = null;
        }
    }
}