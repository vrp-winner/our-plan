using UnityEngine;
using Unity.Netcode;
using System;
using Configs;
using Managers;
using UnityEngine.InputSystem;

namespace Systems
{
    public class PlayerTimeSystem : NetworkBehaviour
    {
        [SerializeField] private GameConfig gameConfig;
    
        // ใช้ float เพราะเวลานับถอยหลังมีจุดทศนิยม
        private readonly NetworkVariable<float> _timeRemaining = new NetworkVariable<float>(120f);
    
        public event Action<float> OnTimeChanged;
    
        public override void OnNetworkSpawn()
        {
            // ใช้ _ แทน oldVal
            _timeRemaining.OnValueChanged += (_, newVal) => OnTimeChanged?.Invoke(newVal);
            
            if (IsServer)
            {
                // เมื่อเริ่มเทิร์นใหม่ ให้รีเซ็ตเวลา
                if (TurnManager.Instance != null)
                {
                    TurnManager.Instance.OnTurnChanged += ResetTime;
                }
                ResetTime(0); // รีเซ็ตครั้งแรก
            }
        }
    
        public override void OnNetworkDespawn()
        {
            if (IsServer && TurnManager.Instance != null)
            {
                TurnManager.Instance.OnTurnChanged -= ResetTime;
            }
        }
    
        private void Update()
        {
            // ทำงานเฉพาะที่เครื่อง Server เท่านั้น (Server Authoritative)
            if (!IsServer) return;
    
            // ถ้ายิ่งมีเวลาเหลือ ให้ลดลงเรื่อยๆ (Real-time Countdown)
            if (_timeRemaining.Value > 0)
            {
                _timeRemaining.Value -= Time.deltaTime;
    
                // ถ้าเวลาหมดแล้ว
                if (_timeRemaining.Value <= 0)
                {
                    _timeRemaining.Value = 0;
                    Debug.Log("เวลาหมด! จบเทิร์นอัตโนมัติ");
                    TurnManager.Instance.ForceEndTurn();
                }
            }
        }
    
        private void ResetTime(int turnIndex)
        {
            // เช็ค gameConfig != null เผื่อลืมลากใส่
            if (gameConfig != null)
            {
                _timeRemaining.Value = gameConfig.MaxTimePerTurn;
            }
        }
    
        // ฟังก์ชันสำหรับหักเวลาเมื่อทำ Action (เดิน, ทำงาน)
        public void DeductTime(float amount)
        {
            if (!IsServer) return; // ต้องให้ Server หักให้
    
            // ถ้าเวลาหมดแล้ว ไม่ให้ทำอะไรต่อ
            if (_timeRemaining.Value <= 0) return;
    
            // เช็คว่ามีเวลาพอไหม? (ถ้าเวลาไม่พอ ห้ามทำ Action)
            // แต่ถ้ายอมให้ทำจนหมดแม็ก ก็ลบ if นี้ทิ้งได้เลย
            if (_timeRemaining.Value < amount) 
            {
                Debug.Log($"เวลาไม่พอ! ต้องการ {amount} วินาที แต่เหลือ {_timeRemaining.Value:0} วินาที");
                return; // ยกเลิกการกระทำ
            }
    
            // หักเวลา
            float newTime = _timeRemaining.Value - amount;
    
            // Clamp: ป้องกันค่าติดลบ (ต่ำสุดคือ 0)
            _timeRemaining.Value = Mathf.Max(0f, newTime);
            
            Debug.Log($"หักเวลาไป {amount} วินาที เหลือ {_timeRemaining.Value:0} วินาที");
    
            // ถ้าหักแล้วเหลือ 0 พอดี ให้จบเทิร์นเลยทันที
            if (_timeRemaining.Value == 0)
            {
                Debug.Log("ใช้เวลาจนหมดเกลี้ยง! จบเทิร์นทันที");
                TurnManager.Instance.ForceEndTurn();
            }
        }
        
        // Test: ลองกดปุ่ม Spacebar เพื่อจำลองการทำงาน (เอาออกทีหลัง)
        private void LateUpdate() {
            if (IsOwner && Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame) {
                TestWorkActionRpc();
            }
        }
    
        [Rpc(SendTo.Server)]
        private void TestWorkActionRpc() {
            // สมมติว่าถ้ากดทำงาน จะหักเวลาตาม Config
            DeductTime(gameConfig.CostWork);
        }
    }
}
