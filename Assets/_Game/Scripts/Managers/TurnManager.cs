using UnityEngine;
using Unity.Netcode;
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
        [SerializeField] private int turnsPerCycle = 4; // 4 เทิร์น = 1 รอบการจ่ายค่าเช่า

        // NetworkVariable ใช้ Sync ข้อมูลระหว่าง Server และ Client โดยอัตโนมัติ
        // WritePermission.Server แปลว่ามีแค่ Server เท่านั้นที่แก้ค่านี้ได้ (กันมีคนโกง)
        private readonly NetworkVariable<int> _currentTurn = new NetworkVariable<int>(1);
        private readonly NetworkVariable<int> _currentCycle = new NetworkVariable<int>(1);

        // Events สำหรับให้ UI หรือ System อื่นๆ มา subscribe (Observer Pattern)
        public event Action<int> OnTurnChanged;
        public event Action<int> OnCycleChanged;

        private void Awake()
        {
            // Setup Singleton
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
        }

        public override void OnNetworkSpawn()
        {
            // เมื่อ Object นี้เกิดในระบบ Network ให้ Client ทุกคนคอยฟังค่าที่เปลี่ยนไป
            // ใช้ _ (Discard) แทน oldValue เพื่อบอกว่าไม่ใช้ค่านี้ (แก้ Warning unused)
            _currentTurn.OnValueChanged += (_, newValue) => OnTurnChanged?.Invoke(newValue);
            _currentCycle.OnValueChanged += (_, newValue) => OnCycleChanged?.Invoke(newValue);

            // อัปเดต UI ครั้งแรกทันที
            OnTurnChanged?.Invoke(_currentTurn.Value);
            OnCycleChanged?.Invoke(_currentCycle.Value);
        }

        public override void OnNetworkDespawn()
        {
            // ล้าง Event เมื่อ Object ถูกทำลาย (กัน Memory Leak)
            _currentTurn.OnValueChanged -= (_, newValue) => OnTurnChanged?.Invoke(newValue);
            _currentCycle.OnValueChanged -= (_, newValue) => OnCycleChanged?.Invoke(newValue);
        }

        /// <summary>
        /// ฟังก์ชันนี้จะถูกเรียกอัตโนมัติเมื่อเวลาหมด
        /// </summary>
        public void ForceEndTurn()
        {
            if (!IsServer) return; // ให้ Server เป็นคนสั่งจบเท่านั้น
            EndTurnRpc();
        }
        
        // Syntax แบบใหม่สำหรับ Unity 6 / NGO 2.0+
        // [Rpc(SendTo.Server)] บอกว่าให้ส่งคำสั่งนี้ไปหา Server
        [Rpc(SendTo.Server)]
        public void RequestEndTurnRpc()
        {
            EndTurnRpc();
        }
        
        [Rpc(SendTo.Server)]
        private void EndTurnRpc()
        {
            // เพิ่มจำนวนเทิร์น
            _currentTurn.Value++;

            // คำนวณ Cycle (เช่น เทิร์น 5 คือเริ่ม Cycle 2)
            // สูตร: (Turn - 1) / 4 + 1
            int calculatedCycle = ((_currentTurn.Value - 1) / turnsPerCycle) + 1;
            if (calculatedCycle > _currentCycle.Value)
            {
                _currentCycle.Value = calculatedCycle;
                Debug.Log($"[Server] New Cycle: {_currentCycle.Value}");
            }

            Debug.Log($"[Server] Turn Ended. New Turn: {_currentTurn.Value}");
        }
    }
}