using UnityEngine;
using Unity.Netcode;
using System;
using Configs;
using Managers;

namespace Systems
{
    public class PlayerTimeSystem : NetworkBehaviour
    {
        [SerializeField] private GameConfig gameConfig;
    
        // ใช้ float เพราะเวลานับถอยหลังมีจุดทศนิยม
        private readonly NetworkVariable<float> _timeRemaining = new NetworkVariable<float>(120f);
        
        private bool _isMyTurnActive = false;
    
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
                    TurnManager.Instance.OnPlayerTurnChanged += HandleTurnChange;
                }
            }
        }
    
        public override void OnNetworkDespawn()
        {
            if (IsServer && TurnManager.Instance != null)
            {
                TurnManager.Instance.OnPlayerTurnChanged -= HandleTurnChange;
            }
        }
    
        private void Update()
        {
            // ทำงานเฉพาะที่เครื่อง Server เท่านั้น (Server Authoritative)
            if (!IsServer) return;
    
            // เวลาจะลดก็ต่อเมื่อ _isMyTurnActive เป็นจริงเท่านั้น (ตาผู้เล่นคนไหน คนนั้นเวลาลด)
            if (_isMyTurnActive && _timeRemaining.Value > 0)
            {
                _timeRemaining.Value -= Time.deltaTime;
    
                // ถ้าเวลาหมดแล้ว
                if (_timeRemaining.Value <= 0)
                {
                    _timeRemaining.Value = 0;
                    Debug.Log("เวลาหมด! จบเทิร์นอัตโนมัติ");
                    TurnManager.Instance.RequestEndTurnRpc();
                }
            }
        }
    
        // ฟังก์ชันสำหรับจัดการเมื่อเปลี่ยนเทิร์น (รับ ID มาเช็ค)
        private void HandleTurnChange(ulong activePlayerId)
        {
            // ถ้า ID ที่ส่งมา ตรงกับ ID ของเจ้าของ Object นี้ -> แปลว่าเป็นตาผู้เล่นนั้นๆ
            if (OwnerClientId == activePlayerId)
            {
                _isMyTurnActive = true;
                
                // รีเซ็ตเวลาเต็ม
                if (gameConfig != null)
                {
                    _timeRemaining.Value = gameConfig.MaxTimePerTurn;
                }
                Debug.Log($"[PlayerTimeSystem] เริ่มเทิร์นผู้เล่น {OwnerClientId} รีเซ็ตเวลาเรียบร้อย");
            }
            else
            {
                // ไม่ใช่ตาผู้เล่น หยุดเวลา
                _isMyTurnActive = false;
            }
        }

        // ฟังก์ชันสำหรับหักเวลาเมื่อทำ Action
        public void DeductTime(float amount)
        {
            if (!IsServer) return; // ต้องให้ Server หักให้
    
            // ถ้าเวลาหมดแล้ว ไม่ให้ทำอะไรต่อ
            if (_timeRemaining.Value <= 0) return;
    
            // เช็คว่ามีเวลาพอไหม? (ถ้าเวลาไม่พอ ห้ามทำ Action)
            // ใช้ + 0.01f เพื่อกัน Floating Point Error
            if (_timeRemaining.Value + 0.01f < amount) 
            {
                Debug.Log($"เวลาไม่พอ! ต้องการ {amount} วินาที แต่เหลือ {_timeRemaining.Value:0.00} วินาที");
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
                TurnManager.Instance.RequestEndTurnRpc();
            }
        }
        
        public void RequestAction(float timeCost)
        {
            if (IsOwner)
            {
                RequestActionServerRpc(timeCost);
            }
        }

        [Rpc(SendTo.Server)]
        private void RequestActionServerRpc(float timeCost)
        {
            // เรียกใช้ Logic DeductTime (แบบ Strict Check)
            DeductTime(timeCost);
        }

        // ฟังก์ชันสำหรับ Sleep
        public void SleepAndEndTurn()
        {
            if (IsOwner)
            {
                SleepServerRpc();
            }
        }

        [Rpc(SendTo.Server)]
        private void SleepServerRpc()
        {
            // ไม่ต้องเช็คเวลา (DeductTime) แต่สั่งให้เวลาเป็น 0 แล้วจบเลย
            _timeRemaining.Value = 0f;
            Debug.Log("ผู้เล่นกด Sleep -> จบเทิร์นทันที");
            TurnManager.Instance.RequestEndTurnRpc();
        }
    }
}
