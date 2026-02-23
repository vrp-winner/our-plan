using UnityEngine;
using Unity.Netcode;
using System;
using Configs;
using Managers;
using UI;

namespace Systems
{
    public class PlayerPointSystem : NetworkBehaviour
    {
        [SerializeField] private GameConfig gameConfig;

        // เปลี่ยนมาใช้ int ทั้งหมดเพื่อความแม่นยำของ Point
        private readonly NetworkVariable<int> _pointsRemaining = new NetworkVariable<int>(0);
        private bool _isSick = false; // สำหรับสถานะป่วย

        public event Action<int> OnVirtualTimeChanged;

        public override void OnNetworkSpawn()
        {
            // ทุกครั้งที่แต้มเปลี่ยน ส่งค่า (แต้ม * วินาทีต่อแต้ม) ไปให้ UI แสดงผล
            _pointsRemaining.OnValueChanged += (oldVal, newVal) => {
                OnVirtualTimeChanged?.Invoke(newVal * gameConfig.SecondsPerPoint);
            };

            if (IsServer)
            {
                TurnManager.Instance.OnPlayerTurnChanged += HandleTurnChange;
            }
        }

        public override void OnNetworkDespawn()
        {
            if (IsServer && TurnManager.Instance != null)
            {
                TurnManager.Instance.OnPlayerTurnChanged -= HandleTurnChange;
            }
        }

        // --- ระบบจัดการแต้ม (Server Authoritative) ---

        [Rpc(SendTo.Server)]
        public void UsePointsServerRpc(int pointAmount)
        {
            if (_pointsRemaining.Value <= 0) return;

            int finalCost = pointAmount;

            // Mechanic: ถ้าป่วย จะเสียแต้มเพิ่มขึ้นตามที่ตั้งไว้ใน Config
            if (_isSick) finalCost += gameConfig.CostSickPenalty;

            // หักแต้ม
            _pointsRemaining.Value = Mathf.Max(0, _pointsRemaining.Value - finalCost);

            Debug.Log($"[Player {OwnerClientId}] ใช้ไป {finalCost} แต้ม เหลือ {_pointsRemaining.Value} แต้ม");

            // ถ้าแต้มหมด ให้สั่งจบเทิร์นผ่าน TurnManager
            if (_pointsRemaining.Value <= 0)
            {
                TurnManager.Instance.RequestEndTurnRpc();
            }
        }

        private void HandleTurnChange(ulong activePlayerId)
        {
            if (OwnerClientId == activePlayerId)
            {
                // เริ่มเทิร์นใหม่ รีเซ็ตแต้มให้เต็มตาม Config
                _pointsRemaining.Value = gameConfig.MaxPointsPerTurn;
                Debug.Log($"[PlayerPointSystem] เริ่มเทิร์นผู้เล่น {OwnerClientId} รีเซ็ตเป็น {gameConfig.MaxPointsPerTurn} แต้ม");
            }
        }

        // --- ระบบ Interaction (ใช้ร่วมกับ UI) ---

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

        // --- ระบบ Location (เหมือนเดิมแต่ปรับ Parameter) ---

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
        // ฟังก์ชันอื่นๆ (NotifyLocationExit, CloseUI) คงเดิมไว้ได้เลย
    }
}