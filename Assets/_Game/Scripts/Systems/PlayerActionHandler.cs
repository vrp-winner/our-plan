using UnityEngine;
using Unity.Netcode;
using Unity.Collections;
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
        
        // เก็บ Index ของ Action ที่ถูกกดไปแล้วในเทิร์นนี้ และเป็นแบบ Once Per Turn
        // จำชื่อสถานที่ + ตัวเลข (เช่น "LOC_Beach_0") ป้องกัน Index ชนกัน
        public NetworkList<FixedString32Bytes> UsedOncePerTurnActions;
        
        // ประเภทของ Action สำหรับธนาคาร
        public enum BankActionType { Deposit, Withdraw, PayRent, BuyHouse }

        private void Awake()
        {
            _pointSystem = GetComponent<PlayerPointSystem>();
            _status = GetComponent<PlayerStatus>();
            UsedOncePerTurnActions = new NetworkList<FixedString32Bytes>();
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
            FixedString32Bytes actionKey = new FixedString32Bytes($"{locationId}_{actionIndex}");
            
            // เช็ค Once Per Turn ด้วย Key ใหม่
            if (action.IsOncePerTurn && UsedOncePerTurnActions.Contains(actionKey)) return;

            // เช็คว่ามี Point พอไหม
            if (!action.IsConsumeAllPoints && !_pointSystem.HasEnoughPoints(action.PointCost)) return;
            
            // เช็คว่าถ้า Effect เงินติดลบ (แปลว่าต้องซื้อ/จ่าย) ผู้เล่นมีเงินพอไหม?
            if (action.MoneyEffect < 0 && _status.PersonalMoney.Value < Mathf.Abs(action.MoneyEffect)) return;
            
            // เช็คเงื่อนไขแต่งงานก่อน
            if (action.ActionName == "Buy (Ring)" && _status.Relationship.Value < 100)
            {
                Debug.Log("[Action] ความสัมพันธ์ยังไม่ถึง 100");
                return;
            }

            // 1. หักแต้ม
            if (action.IsConsumeAllPoints) _pointSystem.ConsumeAllPoints();
            else _pointSystem.ConsumePoints(action.PointCost);

            // 2. Apply สถานะตัวละคร
            _status.ApplyStats_ServerOnly(action.RelationshipEffect, action.StressEffect, action.MoneyEffect);
            
            // ดักจับการแต่งงาน
            if (action.ActionName == "Buy (Ring)")
            {
                _status.IsMarried.Value = true;
                Debug.Log("[Action] ซื้อแหวนสำเร็จ! สถานะ: แต่งงานแล้ว");
            }
            
            // ดักจับการไปเดท
            if (action.ActionName == "Date")
            {
                FixedString32Bytes loc32 = new FixedString32Bytes(locationId);
                if (!_status.VisitedDatingSpots.Contains(loc32))
                {
                    _status.VisitedDatingSpots.Add(loc32);
                    Debug.Log($"[Action] ไปเดทที่ {locationId} (รวมไปเดทมาแล้ว {_status.VisitedDatingSpots.Count} ที่)");
                }
            }

            // 3. มาร์คสถานะสถานที่นี้ว่าได้ทำ Action ไปแล้ว เพื่อให้ UI รู้
            _pointSystem.MarkLocationAsInteracted(locationId);
            
            // ถ้า Action นี้เป็นแบบ กดได้ครั้งเดียวต่อตา ให้บันทึกไว้ใน List
            if (action.IsOncePerTurn) UsedOncePerTurnActions.Add(actionKey);

            // ดักจับการซื้อรถจาก Shopping Mall
            if (locationId == "LOC_ShoppingMall" && action.ActionName == "Buy (Car)")
            {
                if (EconomyManager.Instance != null && !EconomyManager.Instance.isCarBought.Value)
                {
                    EconomyManager.Instance.isCarBought.Value = true;
                    Debug.Log("[Shopping] ซื้อรถสำเร็จ! ค่าเดินทางลดเหลือ 1 Point");
                }
            }

            if (action.EndsTurn && _pointSystem.PointsRemaining > 0)
            {
                TurnManager.Instance.RequestEndTurnRpc();
            }
        }
        
        // คำสั่งแยกเฉพาะกิจสำหรับ Bank
        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        public void RequestBankActionServerRpc(BankActionType actionType, float amount, RpcParams rpcParams = default)
        {
            if (!IsServer) return;
            if (rpcParams.Receive.SenderClientId != OwnerClientId) return;
            if (TurnManager.Instance.activeActorNetworkId.Value != this.NetworkObjectId) return;
            if (_pointSystem.CurrentLocationId != "LOC_Bank") return;

            int pointCost = 2; // กำหนดให้ทุก Action ในธนาคารใช้ 2 Point (12 วินาที)
            if (!_pointSystem.HasEnoughPoints(pointCost)) return;

            bool success = false;

            switch (actionType)
            {
                case BankActionType.Deposit:
                    if (_status.PersonalMoney.Value >= amount)
                    {
                        _status.PersonalMoney.Value -= amount;
                        EconomyManager.Instance.jointMoney.Value += amount;
                        success = true;
                        _status.ApplyStats_ServerOnly(5, -5, 0); // ฝาก: Relationship +5, Stress -5
                    }
                    break;

                case BankActionType.Withdraw:
                    if (EconomyManager.Instance.jointMoney.Value >= amount)
                    {
                        EconomyManager.Instance.jointMoney.Value -= amount;
                        _status.PersonalMoney.Value += amount;
                        success = true;
                        _status.ApplyStats_ServerOnly(-5, 5, 0); // ถอน: Relationship -5, Stress +5
                    }
                    break;

                case BankActionType.PayRent:
                    if (EconomyManager.Instance.pendingBills.Value > 0 && EconomyManager.Instance.jointMoney.Value >= EconomyManager.Instance.GetCurrentRentPrice())
                    {
                        EconomyManager.Instance.ExecutePayRent_ServerOnly();
                        success = true;
                        _status.ApplyStats_ServerOnly(5, -5, 0); // จ่ายค่าเช่า: Relationship +5, Stress -5
                    }
                    break;

                case BankActionType.BuyHouse:
                    if (!EconomyManager.Instance.isHouseBought.Value && EconomyManager.Instance.jointMoney.Value >= 500000f)
                    {
                        EconomyManager.Instance.jointMoney.Value -= 500000f;
                        EconomyManager.Instance.isHouseBought.Value = true; 
                        success = true;
                        Debug.Log("[Bank] ซื้อบ้านสำเร็จ!");
                        _status.ApplyStats_ServerOnly(0, -10, 0); // ซื้อบ้าน: Stress -10
                    }
                    break;
            }

            if (success)
            {
                _pointSystem.ConsumePoints(pointCost);
                _pointSystem.MarkLocationAsInteracted("LOC_Bank");
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