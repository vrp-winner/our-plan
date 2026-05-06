using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Systems;
using Managers;

namespace UI
{
    [System.Serializable]
    public class PlayerSummarySlot
    {
        [Tooltip("เปิด/ปิด ฝั่งนี้ ")]
        public GameObject rootPanel;

        [Tooltip("รูปตัวละคร")]
        public Image avatarImage;

        [Tooltip("ป้ายผลลัพธ์ (Completed / Not Completed)")]
        public Image resultBannerImage;

        [Header("Stats")]
        public TextMeshProUGUI moneyText;

        [Tooltip("หลอดความเครียด ")]
        public Image stressFillImage; 
    }

    public class GameOverUIManager : MonoBehaviour
    {
        [Header("Main UI")]
        public GameObject summaryPanelRoot;
        public TextMeshProUGUI questText;
        public TextMeshProUGUI reasonText;

        [Tooltip("หลอดความสัมพันธ์รวมตรงกลาง")]
        public Image sharedRelationshipFillImage; 

        public Button backToMenuButton;

        [Header("Player Layouts")]
        public PlayerSummarySlot player1Slot;
        public PlayerSummarySlot player2Slot; 

        [Header("Assets")]
        public Sprite completedSprite;
        public Sprite notCompletedSprite;
        public Sprite[] avatarSprites;

        private void Start()
        {
            summaryPanelRoot.SetActive(false);

            if (TurnManager.Instance != null)
            {
                TurnManager.Instance.OnGameOverTriggered += ShowSummary;
            }

            if (backToMenuButton != null)
            {
                backToMenuButton.onClick.AddListener(() =>
                {
                    if (Unity.Netcode.NetworkManager.Singleton != null)
                        Unity.Netcode.NetworkManager.Singleton.Shutdown();

                    UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenu");
                });
            }
        }

        private void OnDestroy()
        {
            if (TurnManager.Instance != null)
            {
                TurnManager.Instance.OnGameOverTriggered -= ShowSummary;
            }
        }

        public void ShowSummary(bool isWin, string reason)
        {
            summaryPanelRoot.SetActive(true);

            if (questText != null) questText.text = TurnManager.Instance.currentObjective.Value.ToString();
            if (reasonText != null) reasonText.text = reason;

            PlayerStatus[] allPlayers = FindObjectsByType<PlayerStatus>(FindObjectsSortMode.None);

            foreach (var p in allPlayers)
            {
                PlayerSummarySlot targetSlot = (p.teamId.Value == 0) ? player1Slot : player2Slot; 
                
                targetSlot.rootPanel.SetActive(true);

                if (targetSlot.moneyText != null) targetSlot.moneyText.text = p.PersonalMoney.Value.ToString("N0");
                
                if (targetSlot.stressFillImage != null)
                {
                    targetSlot.stressFillImage.fillAmount = p.Stress.Value / 100f; 
                }

                if (sharedRelationshipFillImage != null)
                {
                    sharedRelationshipFillImage.fillAmount = p.Relationship.Value / 100f; 
                }

                int avatarIdx = p.avatarIndex.Value; 
                if (avatarIdx >= 0 && avatarIdx < avatarSprites.Length)
                {
                    targetSlot.avatarImage.sprite = avatarSprites[avatarIdx];
                }

                if (targetSlot.resultBannerImage != null)
                {
                    targetSlot.resultBannerImage.sprite = isWin ? completedSprite : notCompletedSprite;
                }
            }
        }
    }
}