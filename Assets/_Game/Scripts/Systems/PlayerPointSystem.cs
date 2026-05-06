using UnityEngine;
using Unity.Netcode;
using Unity.Collections;
using Managers;
using Configs;
using Domain;

namespace Systems
{
    /// <summary>
    /// ตัวจัดการเวลาของผู้เล่น
    /// แยกสถานะการยืน (CurrentLocation) ออกจากสถานะการทำ Action
    /// </summary>
    public class PlayerPointSystem : NetworkBehaviour
    {
        #region Variables
        [Header("Configuration")]
        [SerializeField] private GameConfig gameConfig;

        private readonly NetworkVariable<int> _pointsRemaining = new NetworkVariable<int>(0);
        private const bool IsSick = false;

        // จำว่าผู้เล่นพึ่งทำ Action ที่ Location ไหน
        private string _lastInteractedLocation = string.Empty;
        
        // Location ปัจจุบันที่ผู้เล่นยืนอยู่
        public string CurrentLocationId { get; private set; } = string.Empty;
        public int PointsRemaining => _pointsRemaining.Value;
        
        private PlayerActionHandler _actionHandler;
        #endregion

        #region Lifecycle
        private void Awake()
        {
            _actionHandler = GetComponent<PlayerActionHandler>();
        }
        
        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                TurnManager.Instance.activeActorNetworkId.OnValueChanged += HandleActiveActorChanged;
                if (TurnManager.Instance.activeActorNetworkId.Value == this.NetworkObjectId)
                {
                    ResetTurnData();
                }
            }
        }

        public override void OnNetworkDespawn()
        {
            if (IsServer && TurnManager.Instance != null)
            {
                TurnManager.Instance.activeActorNetworkId.OnValueChanged -= HandleActiveActorChanged;
            }
        }
        #endregion

        #region Turn logic
        private void HandleActiveActorChanged(ulong oldId, ulong newId)
        {
            if (this.NetworkObjectId == newId && IsServer)
            {
                ResetTurnData();
            }
        }
        
        private void ResetTurnData()
        {
            // รีเซ็ต Point
            _pointsRemaining.Value = gameConfig.MaxPointsPerTurn;
            TurnManager.Instance.currentTime.Value = _pointsRemaining.Value * gameConfig.SecondsPerPoint;
            
            // รีเซ็ตการล็อกสถานที่
            _lastInteractedLocation = string.Empty;

            // รีเซ็ตความจำว่าเคยกดปุ่ม Once Per Turn อะไรไปบ้าง
            if (_actionHandler != null)
            {
                _actionHandler.UsedOncePerTurnActions.Clear();
            }
        }
        #endregion

        #region Point Management (Server Only)
        public bool HasEnoughPoints(int pointAmount)
        {
            return GameActionProcessor.TryProcessPointUsage(
                _pointsRemaining.Value, pointAmount, IsSick, gameConfig.CostSickPenalty, out _);
        }

        public void ConsumePoints(int pointAmount)
        {
            if (!IsServer) return;
            
            if (GameActionProcessor.TryProcessPointUsage(_pointsRemaining.Value, pointAmount, IsSick, gameConfig.CostSickPenalty, out int newPointsRemaining))
            {
                _pointsRemaining.Value = newPointsRemaining;
                TurnManager.Instance.currentTime.Value = _pointsRemaining.Value * gameConfig.SecondsPerPoint;

                if (_pointsRemaining.Value <= 0)
                {
                    TurnManager.Instance.RequestEndTurnRpc();
                }
            }
        }

        public void ConsumeAllPoints()
        {
            if (!IsServer) return;
            _pointsRemaining.Value = 0;
            TurnManager.Instance.currentTime.Value = 0;
            TurnManager.Instance.RequestEndTurnRpc();
        }

        public void MarkLocationAsInteracted(string locationId)
        {
            if (IsServer) 
            {
                _lastInteractedLocation = locationId;
            }
        }
        #endregion

        #region Location Interactions (Server Controlled)
        public void NotifyLocationEnter(string locationId)
        {
            if (!IsServer) return;

            // เปลี่ยน Location -> รีเซ็ตสถานะการกด Action
            if (CurrentLocationId != locationId)
            {
                _lastInteractedLocation = string.Empty;
            }

            // อัปเดตตำแหน่งปัจจุบัน
            CurrentLocationId = locationId;

            // ปรับให้เปิด UI เสมอ ไม่ว่าเคยกด Action ไปแล้วหรือยัง
            // เพื่อให้ผู้เล่นซื้อของต่อ หรือกดปุ่มอื่นๆ ได้ จนกว่าจะจบเทิร์น
            TurnManager.Instance.currentInteractionLocationId.Value = new FixedString64Bytes(locationId);
            TurnManager.Instance.isInteractionOpen.Value = true;
        }
        
        public void NotifyLocationExit()
        {
            if (!IsServer) return;

            // ล้างสถานะเมื่อออกจาก Location
            _lastInteractedLocation = string.Empty;
            CurrentLocationId = string.Empty;

            TurnManager.Instance.currentInteractionLocationId.Value = new FixedString64Bytes("");
            TurnManager.Instance.isInteractionOpen.Value = false;
        }
        #endregion
    }
}