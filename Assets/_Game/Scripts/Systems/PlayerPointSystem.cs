using UnityEngine;
using Unity.Netcode;
using Managers;
using System;
using Configs;
using UI;
using Domain;

namespace Systems
{
    /// <summary>
    /// ระบบจัดการแต้ม (Time/Action Points) ของตัวละคร
    /// ลดความซับซ้อนของ RPC กลับไปใช้ SendTo.Owner และให้การปิด UI ทำงานแบบ Local
    /// </summary>
    public class PlayerPointSystem : NetworkBehaviour
    {
        #region Variables (ตัวแปรตั้งค่า)
        [Header("Configuration")]
        [SerializeField] private GameConfig gameConfig;

        // เปลี่ยนมาใช้ int ทั้งหมดเพื่อความแม่นยำของ Point
        private readonly NetworkVariable<int> _pointsRemaining = new NetworkVariable<int>(0);
        private bool _isSick = false; // สำหรับสถานะป่วย

        public event Action<int> OnVirtualTimeChanged;
        #endregion

        #region Lifecycle (การแจกเวลา)
        public override void OnNetworkSpawn()
        {
            // ทุกครั้งที่แต้มเปลี่ยน ส่งค่า (แต้ม * วินาทีต่อแต้ม) ไปให้ UI แสดงผล
            _pointsRemaining.OnValueChanged += (oldVal, newVal) => {
                OnVirtualTimeChanged?.Invoke(newVal * gameConfig.SecondsPerPoint);
            };

            if (IsServer)
            {
                TurnManager.Instance.ActiveActorNetworkId.OnValueChanged += HandleActiveActorChanged;
                if (TurnManager.Instance.ActiveActorNetworkId.Value == this.NetworkObjectId)
                {
                    _pointsRemaining.Value = gameConfig.MaxPointsPerTurn;
                }
            }

            RefreshTimeUI();
        }

        public override void OnNetworkDespawn()
        {
            if (IsServer && TurnManager.Instance != null)
            {
                TurnManager.Instance.ActiveActorNetworkId.OnValueChanged -= HandleActiveActorChanged;
            }
        }
        #endregion

        #region Server Logic (ระบบใช้แต้มและสลับเทิร์น)
        private void HandleActiveActorChanged(ulong oldId, ulong newId)
        {
            if (this.NetworkObjectId == newId)
            {
                _pointsRemaining.Value = gameConfig.MaxPointsPerTurn;
            }
        }
        
        [Rpc(SendTo.Server)]
        public void UsePointsServerRpc(int pointAmount)
        {
            if (!IsServer) return;
            if (TurnManager.Instance.ActiveActorNetworkId.Value != this.NetworkObjectId) return;
            
            bool canUsePoints = GameActionProcessor.TryProcessPointUsage(
                _pointsRemaining.Value, 
                pointAmount, 
                _isSick, 
                gameConfig.CostSickPenalty, 
                out int newPointsRemaining
            );

            if (canUsePoints)
            {
                _pointsRemaining.Value = newPointsRemaining;
                if (_pointsRemaining.Value <= 0)
                {
                    TurnManager.Instance.RequestEndTurnRpc();
                }
            }
        }
        #endregion

        #region Client Input
        public void RequestAction(int pointCost)
        {
            if (!IsOwner) return;
            if (TurnManager.Instance.ActiveActorNetworkId.Value != this.NetworkObjectId) return;

            UsePointsServerRpc(pointCost);
        }

        public void SleepAndEndTurn()
        {
            if (!IsOwner) return;
            if (TurnManager.Instance.ActiveActorNetworkId.Value != this.NetworkObjectId) return;

            // กดนอน = ใช้แต้มที่เหลือทั้งหมดจนเป็น 0
            UsePointsServerRpc(_pointsRemaining.Value);
            Debug.Log("ผู้เล่นกด Sleep -> ใช้แต้มที่เหลือทั้งหมด");
        }
        #endregion

        #region Location Interactions (Simplified UI Flow)
        /// <summary>
        /// แจ้ง Owner Client เพื่อเปิดหน้าต่าง UI
        /// </summary>
        /// <param name="locationId">รหัสอ้างอิงของสถานที่</param>
        public void NotifyLocationEnter(string locationId)
        {
            if (IsServer)
            {
                // STEP 1: ใช้ Rpc แบบอ่านง่าย ส่งตรงเข้า Owner
                EnterLocationClientRpc(locationId);
            }
        }
        
        /// <summary>
        /// แจ้ง Owner Client เพื่อปิดหน้าต่าง UI (กรณีเดินออกด้วยคำสั่งอื่น)
        /// </summary>
        public void NotifyLocationExit()
        {
            if (IsServer)
            {
                ExitLocationClientRpc();
            }
        }

        /// <summary>
        /// RPC: สั่งเปิด UI ไปที่ Owner
        /// </summary>
        [Rpc(SendTo.Owner)]
        private void EnterLocationClientRpc(string locationId)
        {
            InteractableLocation loc = LocationRegistry.GetLocation(locationId);
            if (loc != null)
            {
                InteractionUIManager.Instance.ShowLocation(loc.Config, this);
            }
        }

        /// <summary>
        /// RPC: สั่งปิด UI ไปที่ Owner
        /// </summary>
        [Rpc(SendTo.Owner)]
        private void ExitLocationClientRpc()
        {
            if (InteractionUIManager.Instance != null)
            {
                InteractionUIManager.Instance.HideUI();
            }
        }
        
        /// <summary>
        /// ร้องขอปิดหน้าต่าง UI (ทำงานแบบ Local ล้วน ไม่เปลือง Network)
        /// </summary>
        public void RequestCloseInteractionUI()
        {
            // STEP 1: Guard Check
            if (!IsOwner) return;
            
            // STEP 2: ปิดฝั่ง Client ไปเลย ไม่ต้องเรียก RPC ไปหา Server อีกแล้ว
            if (InteractionUIManager.Instance != null)
            {
                InteractionUIManager.Instance.HideUI();
            }
        }
        #endregion

        #region Utilities (อื่นๆ)
        public void OnEnterHome()
        {
            if (IsServer && TryGetComponent(out PlayerStatus status))
            {
                StatusManager.Instance.ProcessHomeEntry(status);
            }
        }
        
        public void RefreshTimeUI()
        {
            OnVirtualTimeChanged?.Invoke(_pointsRemaining.Value * gameConfig.SecondsPerPoint);
        }
        #endregion
    }
}