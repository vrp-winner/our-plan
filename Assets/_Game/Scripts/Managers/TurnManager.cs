using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;
using System;

namespace Managers
{
    /// <summary>
    /// Class นี้ทำหน้าที่จัดการระบบเทิร์นและเวลาของเกม (Server Authoritative)
    /// </summary>
    public class TurnManager : NetworkBehaviour
    {
        // Singleton Instance เพื่อให้เข้าถึงได้ง่ายจาก Class อื่น (เช่น TurnUIManager)
        public static TurnManager Instance { get; private set; }

        [Header("Settings")]
        [SerializeField] private int turnsPerCycle = 4; // ครบทุกคนเล่นคนละ 4 รอบ = 1 Cycle
        
        // ตัวแปร Network
        // NetworkVariable ใช้ Sync ข้อมูลระหว่าง Server และ Client โดยอัตโนมัติ
        private NetworkList<ulong> _playerIds; // เก็บ ID ของผู้เล่นเรียงตามลำดับที่เข้าเกม
        private NetworkVariable<bool> _isGameStarted = new NetworkVariable<bool>(false); // เช็คว่าเกมเริ่มหรือยัง
        // ตั้งค่าเริ่มต้นเป็น 0 เพื่อสื่อว่า "ยังไม่มีข้อมูล"
        private NetworkVariable<int> _currentPlayerIndex = new NetworkVariable<int>(0); // ชี้ว่าตอนนี้ตาใคร (0, 1, 2...)
        private NetworkVariable<int> _currentRound = new NetworkVariable<int>(0); // รอบการเดิน (Turn 1, 2, 3...)
        private NetworkVariable<int> _currentCycle = new NetworkVariable<int>(0); // เดือน (Cycle)

        // Events สำหรับให้ UI หรือ System อื่นๆ มา subscribe (Observer Pattern)
        public event Action<ulong> OnPlayerTurnChanged; // ส่ง ID คนที่เป็นเจ้าของตาปัจจุบันไปบอก UI/Player
        public event Action<int> OnRoundChanged;
        public event Action<int> OnCycleChanged;
        public event Action<bool> OnGameStateChanged; // Event ใหม่สำหรับตอนเริ่มเกม
        
        // เพิ่ม Properties ให้คนอื่นดึงค่าปัจจุบันไปใช้ได้ทันที (แก้ปัญหา UI ว่างตอนเริ่ม)
        public bool IsGameStarted => _isGameStarted.Value;
        public int CurrentRound => _currentRound.Value;
        public int CurrentCycle => _currentCycle.Value;
        
        public ulong CurrentActivePlayerId 
        {
            get 
            {
                if (_playerIds == null || _playerIds.Count == 0) return 99999; // คืนค่ามั่วๆไปก่อนถ้ายังไม่มีคน
                if (_currentPlayerIndex.Value >= _playerIds.Count) return _playerIds[0];
                return _playerIds[_currentPlayerIndex.Value];
            }
        }

        private void Awake()
        {
            // Setup Singleton
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
            
            _playerIds = new NetworkList<ulong>();
        }

        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                // เมื่อ Server เริ่ม ให้รอคน connect
                NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
            }
            // เมื่อ Object นี้เกิดในระบบ Network ให้ Client ทุกคนคอยฟังค่าที่เปลี่ยนไป
            // ใช้ _ (Discard) แทน oldValue เพื่อบอกว่าไม่ใช้ค่านี้ (แก้ Warning unused)
            // Client คอยฟังค่าเปลี่ยน
            _currentPlayerIndex.OnValueChanged += (_, newValue) => NotifyTurnChange();
            _currentRound.OnValueChanged += (_, newValue) => OnRoundChanged?.Invoke(newValue);
            _currentCycle.OnValueChanged += (_, newValue) => OnCycleChanged?.Invoke(newValue);
            _isGameStarted.OnValueChanged += (_, newVal) => OnGameStateChanged?.Invoke(newVal); // ฟังค่าสถานะเกม
        }
        
        private void OnClientConnected(ulong clientId)
        {
            if (IsServer)
            {
                // เก็บ ID เข้า List
                _playerIds.Add(clientId);
            }
        }
        
        // ฟังก์ชันเริ่มเกม (ต้องกดปุ่ม Start Game หรือเรียกเมื่อคนครบ)
        public void StartGame()
        {
            if (!IsServer) return;
            
            Debug.Log("[TurnManager] Host กด Start Game!");
            
            // ลำดับการเซ็ตค่า
            _isGameStarted.Value = true; // บอกว่าเกมเริ่มแล้ว
            _currentPlayerIndex.Value = 0;
            _currentRound.Value = 1;     // เซ็ตเลขรอบเป็น 1
            _currentCycle.Value = 1;
            
            // Notify ทันทีเพื่อให้ Client ทุกคนรู้ว่าเริ่มแล้ว
            NotifyTurnChange();
        }

        private void NotifyTurnChange()
        {
            // ถ้าเกมยังไม่เริ่ม หรือไม่มีคน ห้ามส่ง Event มั่ว
            if (!_isGameStarted.Value || _playerIds.Count == 0) return;
            
            // ดึง ID ของคนที่เป็นตาปัจจุบัน
            ulong activePlayerId = _playerIds[_currentPlayerIndex.Value];
            OnPlayerTurnChanged?.Invoke(activePlayerId);
            
            Debug.Log($"[TurnManager] Active Player ID: {activePlayerId}");
        }
        
        // Syntax แบบใหม่สำหรับ Unity 6 / NGO 2.0+
        // [Rpc(SendTo.Server)] บอกว่าให้ส่งคำสั่งนี้ไปหา Server
        // ฟังก์ชันจบเทิร์น (Player เรียก)
        [Rpc(SendTo.Server)]
        public void RequestEndTurnRpc()
        {
            if (!_isGameStarted.Value) return;
            
            // คำนวณ Index คนต่อไป
            int nextIndex = _currentPlayerIndex.Value + 1;

            // ถ้าเกินจำนวนคนเล่น แปลว่าครบรอบวงแล้ว (ทุกคนเล่นครบ 1 ครั้ง)
            if (nextIndex >= _playerIds.Count)
            {
                nextIndex = 0; // วนกลับมาคนแรก
                AdvanceRound(); // ขึ้นรอบใหม่ (Round 2)
            }

            _currentPlayerIndex.Value = nextIndex;
        }
        
        private void AdvanceRound()
        {
            _currentRound.Value++;

            // คำนวณ Cycle: ทุกๆ 4 Round = 1 Cycle
            int calculatedCycle = ((_currentRound.Value - 1) / turnsPerCycle) + 1;
            if (calculatedCycle > _currentCycle.Value)
            {
                _currentCycle.Value = calculatedCycle;
            }
        }
    }
}