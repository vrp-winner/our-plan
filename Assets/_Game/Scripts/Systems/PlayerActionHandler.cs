using UnityEngine;
using Unity.Netcode;
using Configs;
using Managers;

namespace Systems
{
    /// <summary>
    /// ตัวจัดการคำสั่ง Action ของผู้เล่น
    /// รับ Request จาก Client และตรวจสอบความถูกต้อง
    /// </summary>
    [RequireComponent(typeof(PlayerPointSystem), typeof(PlayerStatus))]
    public class PlayerActionHandler : NetworkBehaviour
    {
        private PlayerPointSystem _pointSystem;
        private PlayerStatus _status;

        private void Awake()
        {
            _pointSystem = GetComponent<PlayerPointSystem>();
            _status = GetComponent<PlayerStatus>();
        }

        /// <summary>
        /// รับคำสั่งทำ Action จากปุ่ม (Server Only)
        /// </summary>
        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        public void RequestExecuteLocationActionServerRpc(string locationId, int actionIndex, RpcParams rpcParams = default)
        {
            if (!IsServer) return;

            if (rpcParams.Receive.SenderClientId != OwnerClientId) return;
            if (TurnManager.Instance.activeActorNetworkId.Value != this.NetworkObjectId) return;
            if (_pointSystem.CurrentLocationId != locationId) return;

            InteractableLocation loc = LocationRegistry.GetLocation(locationId);
            if (loc == null || loc.Config == null) return;
            if (actionIndex < 0 || actionIndex >= loc.Config.AvailableActions.Count) return;

            LocationAction action = loc.Config.AvailableActions[actionIndex];

            if (!action.IsConsumeAllPoints && !_pointSystem.HasEnoughPoints(action.PointCost)) return;

            // 1. หักแต้ม
            if (action.IsConsumeAllPoints) _pointSystem.ConsumeAllPoints();
            else _pointSystem.ConsumePoints(action.PointCost);

            // 2. Apply สถานะตัวละคร
            _status.ApplyStats_ServerOnly(action.RelationshipEffect, action.StressEffect, action.MoneyEffect);

            // [TASK] STEP 3: มาร์คสถานะสถานที่นี้ว่าได้ทำ Action ไปแล้ว ป้องกัน UI เด้งซ้ำในเทิร์นเดิม
            _pointSystem.MarkLocationAsInteracted(locationId);

            // 4. บังคับปิด Interaction UI ทันที
            TurnManager.Instance.isInteractionOpen.Value = false;

            // 5. เช็คเงื่อนไขบังคับจบเทิร์น
            if (action.EndsTurn && _pointSystem.PointsRemaining > 0)
            {
                TurnManager.Instance.RequestEndTurnRpc();
            }
        }

        /// <summary>
        /// ปิด UI จาก Server
        /// </summary>
        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        public void RequestCloseUI_ServerRpc(RpcParams rpcParams = default)
        {
            if (!IsServer) return;
            if (rpcParams.Receive.SenderClientId != OwnerClientId) return;
            if (TurnManager.Instance.activeActorNetworkId.Value != this.NetworkObjectId) return;
            
            TurnManager.Instance.isInteractionOpen.Value = false;
        }
    }
}