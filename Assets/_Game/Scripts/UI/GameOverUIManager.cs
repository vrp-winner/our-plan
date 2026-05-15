using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Systems;
using Managers;
using Unity.Netcode;

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
                TurnManager.Instance.OnGameOverTriggered += ShowSummary;

            if (backToMenuButton != null)
            {
                backToMenuButton.onClick.AddListener(() =>
                {
                    backToMenuButton.interactable = false; // กันกดเบิ้ล
                    StartCoroutine(ShutdownAndLeaveCoroutine());
                });
            }
        }
        
        private System.Collections.IEnumerator ShutdownAndLeaveCoroutine()
        {
            var net = NetworkManager.Singleton;
            
            if (net != null)
            {
                // เริ่ม shutdown
                net.Shutdown();
                
                // รอจนระบบ “ไม่ active แล้วจริง ๆ”
                yield return new WaitUntil(() => net == null || (!net.IsListening && !net.ShutdownInProgress));
                
                // กัน object ค้างใน scene
                Destroy(net.gameObject);
            }

            // ทำลาย Manager ทิ้งกันข้ามซีน
            var gs = GameObject.Find("GameSystem");
            if (gs != null) Destroy(gs);

            var lnm = GameObject.Find("LobbyNetworkManager");
            if (lnm != null) Destroy(lnm);

            // buffer 1 frame กัน race condition ของ scene load
            yield return null; 
            UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenu");
        }

        private void OnDestroy()
        {
            if (TurnManager.Instance != null)
                TurnManager.Instance.OnGameOverTriggered -= ShowSummary;
        }
        
        public void ShowSummary(bool isWin, string reason)
        {
            StartCoroutine(ShowSummaryCoroutine(isWin, reason));
        }

        private System.Collections.IEnumerator ShowSummaryCoroutine(bool isWin, string reason)
        {
            // รอให้ NetworkVariable sync มาถึง client ก่อน
            yield return new WaitForSeconds(0.5f);
            
            summaryPanelRoot.SetActive(true);

            if (questText != null) 
                questText.text = GetObjectiveText(TurnManager.Instance.currentObjective.Value);
            
            if (reasonText != null) 
                reasonText.text = reason;

            PlayerStatus[] allPlayers = FindObjectsByType<PlayerStatus>(FindObjectsSortMode.None);

            foreach (var p in allPlayers)
            {
                PlayerSummarySlot targetSlot = (p.teamId.Value == 0) ? player1Slot : player2Slot; 
                
                targetSlot.rootPanel.SetActive(true);

                if (targetSlot.moneyText != null) targetSlot.moneyText.text = p.PersonalMoney.Value.ToString("N0");
                
                if (targetSlot.stressFillImage != null)
                    targetSlot.stressFillImage.fillAmount = p.Stress.Value / 100f; 

                if (sharedRelationshipFillImage != null)
                    sharedRelationshipFillImage.fillAmount = p.Relationship.Value / 100f; 

                int avatarIdx = p.avatarIndex.Value; 
                if (avatarIdx >= 0 && avatarIdx < avatarSprites.Length)
                    targetSlot.avatarImage.sprite = avatarSprites[avatarIdx];

                if (targetSlot.resultBannerImage != null)
                    targetSlot.resultBannerImage.sprite = isWin ? completedSprite : notCompletedSprite;
            }
        }
        
        private string GetObjectiveText(GameObjective obj)
        {
            switch (obj)
            {
                case GameObjective.GetMarried:
                    return "Get married";

                case GameObjective.BuyHouseAndCar:
                    return "Buy a house & a car";

                case GameObjective.RetireWealthy:
                    return "Retired with $800,000";

                case GameObjective.DateEveryPlaces:
                    return "Dating every places";

                default:
                    return obj.ToString();
            }
        }
    }
}