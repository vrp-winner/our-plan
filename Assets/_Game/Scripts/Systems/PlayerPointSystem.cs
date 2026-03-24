using UnityEngine;
using Unity.Netcode;
using Managers;
using System;
using Configs;
using UI;

namespace Systems
{
    public class PlayerPointSystem : NetworkBehaviour
    {
        #region ตัวแปรตั้งค่า (Variables)
        [SerializeField] private GameConfig gameConfig;

        // เปลี่ยนมาใช้ int ทั้งหมดเพื่อความแม่นยำของ Point
        private readonly NetworkVariable<int> _pointsRemaining = new NetworkVariable<int>(0);
        private bool _isSick = false; // สำหรับสถานะป่วย

        public event Action<int> OnVirtualTimeChanged;
        #endregion

        #region การแจกเวลา(Timecycle )
        public override void OnNetworkSpawn()
        {
            // ทุกครั้งที่แต้มเปลี่ยน ส่งค่า (แต้ม * วินาทีต่อแต้ม) ไปให้ UI แสดงผล
            _pointsRemaining.OnValueChanged += (oldVal, newVal) => {
                OnVirtualTimeChanged?.Invoke(newVal * gameConfig.SecondsPerPoint);
            };

            if (IsServer)
            {
                TurnManager.Instance.ActiveActorNetworkId.OnValueChanged += (oldId, newId) => {
                    if (this.NetworkObjectId == newId)
                    {
                        _pointsRemaining.Value = gameConfig.MaxPointsPerTurn;
                        Debug.Log($"[Server] แจกแต้มให้ Client {OwnerClientId} สำเร็จ!");
                    }
                };

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
                TurnManager.Instance.OnPlayerTurnChanged -= HandleTurnChange;
            }
        }
        #endregion

        #region ระบบใช้แต้มและสลับเทิร์น (Point Usage)
        [Rpc(SendTo.Server)]
        public void UsePointsServerRpc(int pointAmount)
        {
            if (_pointsRemaining.Value < pointAmount) return;

            int finalCost = pointAmount;

            if (_isSick) finalCost += gameConfig.CostSickPenalty;

            _pointsRemaining.Value = Mathf.Max(0, _pointsRemaining.Value - finalCost);

            if (_pointsRemaining.Value <= 0)
            {
                TurnManager.Instance.RequestEndTurnRpc();
            }
        }

        private void HandleTurnChange(ulong activeActorId)
        {
            if (this.NetworkObjectId == activeActorId)
            {
                _pointsRemaining.Value = gameConfig.MaxPointsPerTurn;
                Debug.Log($"[PlayerPointSystem] เริ่มเทิร์นตัวละคร {this.NetworkObjectId} รีเซ็ตเป็น {gameConfig.MaxPointsPerTurn} แต้ม");
            }
        }


        public void RequestAction(int pointCost)
        {
            if (IsOwner)
            {
                // ส่งคำสั่งหักแต้มไปที่ Server
                UsePointsServerRpc(pointCost);
            }
        }

        public void SleepAndEndTurn()
        {
            if (IsOwner)
            {
                // กดนอน = ใช้แต้มที่เหลือทั้งหมดจนเป็น 0
                UsePointsServerRpc(_pointsRemaining.Value);
                Debug.Log("ผู้เล่นกด Sleep -> ใช้แต้มที่เหลือทั้งหมด");
            }
        }
        #endregion

        // --- ระบบ Location (เหมือนเดิมแต่ปรับ Parameter) ---
        #region เชื่อมต่อกับตึก (Location Sync)
        public void NotifyLocationEnter(string locationObjectName)
        {
            EnterLocationClientRpc(locationObjectName);
        }
        public void NotifyLocationExit()
        {
            if (IsServer)
            {
                ExitLocationClientRpc();
            }
        }

        [Rpc(SendTo.ClientsAndHost)]
        private void ExitLocationClientRpc()
        {
            if (UI.InteractionUIManager.Instance != null)
            {
                UI.InteractionUIManager.Instance.HideUI();
            }
        }

        [Rpc(SendTo.ClientsAndHost)]
        private void EnterLocationClientRpc(string locationObjectName)
        {
            GameObject locationObj = GameObject.Find(locationObjectName);
            if (locationObj != null && locationObj.TryGetComponent(out InteractableLocation loc))
            {
                // ส่งสคริปต์ตัวเองเข้า UI เพื่อให้ UI เรียก RequestAction ได้
                InteractionUIManager.Instance.ShowLocation(loc.Config, this);
            }
        }
        #endregion

        #region Utilities (อื่นๆ)
        public void RequestCloseInteractionUI()
        {
            if (IsOwner)
            {
                CloseInteractionUIServerRpc();
            }
        }

        [Rpc(SendTo.Server)]
        private void CloseInteractionUIServerRpc()
        {
            CloseInteractionUIClientRpc();
        }

        [Rpc(SendTo.ClientsAndHost)]
        private void CloseInteractionUIClientRpc()
        {
            if (UI.InteractionUIManager.Instance != null)
            {
                UI.InteractionUIManager.Instance.HideUI();
            }
        }
        public void OnEnterHome()
        {
            if (IsServer)
            {
                StatusManager.Instance.ProcessHomeEntry(this.GetComponent<PlayerStatus>());
            }
        }
        public void RefreshTimeUI()
        {
            OnVirtualTimeChanged?.Invoke(_pointsRemaining.Value * gameConfig.SecondsPerPoint);
        }
        #endregion
    }
}