using UnityEngine;
using UnityEngine.UI;
using Systems;
using Managers;
using Unity.Netcode;

namespace UI
{
    public class StatusUIManager : MonoBehaviour
    {
        [Header("UI Rings")]
        [SerializeField] private Image relationshipRing;
        [SerializeField] private Image stressRing;

        private PlayerStatus _activeStatus;

        private void Start()
        {
            if (TurnManager.Instance != null)
                TurnManager.Instance.OnPlayerTurnChanged += UpdateActivePlayerUI;
        }

        private void UpdateActivePlayerUI(ulong activePlayerId)
        {
            if (_activeStatus != null)
            {
                _activeStatus.Relationship.OnValueChanged -= (oldV, newV) => SyncFill(relationshipRing, newV);
                _activeStatus.Stress.OnValueChanged -= (oldV, newV) => SyncFill(stressRing, newV);
            }

            foreach (var netObj in NetworkManager.Singleton.SpawnManager.SpawnedObjects.Values)
            {
                if (netObj.OwnerClientId == activePlayerId)
                {
                    _activeStatus = netObj.GetComponent<PlayerStatus>();
                    if (_activeStatus != null)
                    {
                        _activeStatus.Relationship.OnValueChanged += (oldV, newV) => SyncFill(relationshipRing, newV);
                        _activeStatus.Stress.OnValueChanged += (oldV, newV) => SyncFill(stressRing, newV);
                        SyncFill(relationshipRing, _activeStatus.Relationship.Value);
                        SyncFill(stressRing, _activeStatus.Stress.Value);
                    }
                    break;
                }
            }
        }

        private void SyncFill(Image img, int value) => img.fillAmount = value / 100f;
    }
}