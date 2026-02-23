using UnityEngine;
using System.Collections.Generic;

namespace Configs
{
    [System.Serializable]
    public class LocationAction
    {
        [field: SerializeField] public string ActionName { get; private set; }
        // เปลี่ยนจาก float เป็น int เพื่อใช้เป็นแต้ม (Point)
        [field: SerializeField] public int PointCost { get; private set; }
    }

    [CreateAssetMenu(fileName = "LocationConfig", menuName = "Game/LocationConfig")]
    public class LocationConfig : ScriptableObject
    {
        [Header("General Info")]
        [field: SerializeField] public string LocationName { get; private set; }
        [field: TextArea]
        [field: SerializeField] public string Description { get; private set; }

        [Header("Available Actions")]
        [field: SerializeField] public List<LocationAction> AvailableActions { get; private set; }
    }
}