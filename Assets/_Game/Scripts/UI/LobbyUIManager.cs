using System.Collections.Generic;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;
using Managers;

namespace UI
{
    [System.Serializable]
    public class PlayerLobbySlot
    {
        public GameObject slotRoot;      
        public Image avatarImage;        
        public TextMeshProUGUI nameText; 
        public GameObject hostTag;       
    }

    /// <summary>
    /// จัดการ UI ทั้งหมดของหน้า Main Menu และ Lobby แบบ Reactive
    /// </summary>
    public class LobbyUIManager : MonoBehaviour
    {
        [Header("--- Panels (หน้าต่าง) ---")]
        public GameObject titlePanel;
        public GameObject connectionPanel;
        public GameObject createGamePanel;
        public GameObject lobbyPanel;

        [Header("--- Title & Connection ---")]
        public Button playBtn;
        public Button quitBtn;
        public Button hostGameBtn;
        public TMP_InputField joinCodeInput;
        public Button joinBtn;
        public Button backFromConnectionBtn;

        [Header("--- Create Game ---")]
        public Button players2Btn;
        public Button players4Btn;
        public Button createBtn;
        public Button backFromCreateBtn;
        private int _selectedMaxPlayers = 0; 

        [Header("--- Lobby: Status & Info ---")]
        public TextMeshProUGUI inviteCodeText;
        public TextMeshProUGUI statusText;
        public Button leaveLobbyBtn;

        [Header("--- Lobby: Players List (ซ้าย) ---")]
        public Sprite placeholderSprite;   
        public Sprite[] avatarSprites;      
        public PlayerLobbySlot[] playerSlots; 

        [Header("--- Lobby: Avatar Selection (ขวา) ---")]
        public Button[] avatarSelectButtons; 
        public GameObject[] mySelectionHighlights; 

        private bool _isSubscribed = false;

        private void Start()
        {
            playBtn.onClick.AddListener(() => SwitchPanel(connectionPanel));
            quitBtn.onClick.AddListener(() => Application.Quit());

            hostGameBtn.onClick.AddListener(() => 
            {
                _selectedMaxPlayers = 0;
                players2Btn.GetComponent<Image>().color = Color.white;
                players4Btn.GetComponent<Image>().color = Color.white;
                SwitchPanel(createGamePanel);
            });

            joinBtn.onClick.AddListener(OnJoinGameClicked);
            backFromConnectionBtn.onClick.AddListener(() => SwitchPanel(titlePanel));

            if (joinCodeInput != null)
            {
                joinCodeInput.onValueChanged.AddListener((val) => 
                {
                    joinCodeInput.SetTextWithoutNotify(val.ToUpper());
                });
            }

            players2Btn.onClick.AddListener(() => SetMaxPlayers(2));
            players4Btn.onClick.AddListener(() => SetMaxPlayers(4));
            createBtn.onClick.AddListener(OnCreateGameClicked);
            backFromCreateBtn.onClick.AddListener(() => SwitchPanel(connectionPanel));

            leaveLobbyBtn.onClick.AddListener(OnLeaveLobbyClicked);
            for (int i = 0; i < avatarSelectButtons.Length; i++)
            {
                int index = i;
                avatarSelectButtons[i].onClick.AddListener(() => OnAvatarSelectClicked(index));
            }

            SwitchPanel(titlePanel);

            NetworkManager.Singleton.OnClientConnectedCallback += OnNetworkConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback += OnNetworkDisconnected;
        }

        private void OnDestroy()
        {
            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.OnClientConnectedCallback -= OnNetworkConnected;
                NetworkManager.Singleton.OnClientDisconnectCallback -= OnNetworkDisconnected;
            }
            UnsubscribeLobbyEvents();
        }

        #region Flow & Panel Routing
        private void SwitchPanel(GameObject targetPanel)
        {
            titlePanel.SetActive(false);
            connectionPanel.SetActive(false);
            createGamePanel.SetActive(false);
            lobbyPanel.SetActive(false);

            targetPanel.SetActive(true);
        }

        private void SetMaxPlayers(int count)
        {
            _selectedMaxPlayers = count;
            
            players2Btn.GetComponent<Image>().color = (count == 2) ? Color.gray : Color.white;
            players4Btn.GetComponent<Image>().color = (count == 4) ? Color.gray : Color.white;
        }
        #endregion

        #region Actions (ส่งคำสั่งไป Manager)
        private async void OnCreateGameClicked()
        {
            if (_selectedMaxPlayers == 0) 
            {
                Debug.Log("Please select number of players first!");
                return; 
            }

            createBtn.interactable = false; 
            bool success = await LobbyNetworkManager.Instance.StartHostRelay(_selectedMaxPlayers);
            
            if (!success) 
            {
                createBtn.interactable = true;
                Debug.LogError("สร้างห้องไม่สำเร็จ");
            }
        }

        private async void OnJoinGameClicked()
        {
            if (string.IsNullOrEmpty(joinCodeInput.text)) return;

            joinBtn.interactable = false;

            string upperCaseCode = joinCodeInput.text.ToUpper();
            bool success = await LobbyNetworkManager.Instance.StartClientRelay(upperCaseCode);
            
            if (!success) 
            {
                joinBtn.interactable = true;
                Debug.LogError("Join ไม่สำเร็จ (Code ผิด หรือ ห้องเต็ม)");
            }
        }

        private void OnLeaveLobbyClicked()
        {
            if (LobbyNetworkManager.Instance != null)
            {
                LobbyNetworkManager.Instance.LeaveLobby();
            }
        }

        private void OnAvatarSelectClicked(int index)
        {
            if (LobbyNetworkManager.Instance != null)
            {
                LobbyNetworkManager.Instance.ToggleAvatarSelectionServerRpc(index);
            }
        }
        #endregion

        #region Lobby Reactive UI (อัปเดตหน้าจอตาม Server)
        private void OnNetworkConnected(ulong clientId)
        {
            if (clientId == NetworkManager.Singleton.LocalClientId)
            {
                SwitchPanel(lobbyPanel);
                if (LobbyNetworkManager.Instance != null)
                {
                    inviteCodeText.text = LobbyNetworkManager.Instance.CurrentJoinCode;
                }

                SubscribeLobbyEvents();
                RefreshLobbyUI(); 
            }
        }

        private void OnNetworkDisconnected(ulong clientId)
        {
            if (clientId == NetworkManager.Singleton.LocalClientId)
            {
                UnsubscribeLobbyEvents();
                SwitchPanel(connectionPanel); 
                joinBtn.interactable = true; 
            }
        }

        // สร้างฟังก์ชันรับ Event แบบเต็มตัว เพื่อกัน Event Leak
        private void OnPlayerListChanged(NetworkListEvent<ulong> changeEvent) => RefreshLobbyUI();
        private void OnAvatarListChanged(NetworkListEvent<ulong> changeEvent) => RefreshLobbyUI();

        private void SubscribeLobbyEvents()
        {
            if (_isSubscribed || LobbyNetworkManager.Instance == null) return;
            
            // ผูก Event ด้วยฟังก์ชันที่สร้างไว้
            LobbyNetworkManager.Instance.ConnectedPlayers.OnListChanged += OnPlayerListChanged;
            LobbyNetworkManager.Instance.AvatarSelections.OnListChanged += OnAvatarListChanged;
            LobbyNetworkManager.Instance.CountdownTimer.OnValueChanged += UpdateStatusText;
            
            _isSubscribed = true;
        }

        private void UnsubscribeLobbyEvents()
        {
            if (!_isSubscribed || LobbyNetworkManager.Instance == null) return;

            // ยกเลิกผูก Event จะได้ไม่ชี้มาที่ UI ที่โดนทำลายไปแล้ว
            LobbyNetworkManager.Instance.ConnectedPlayers.OnListChanged -= OnPlayerListChanged;
            LobbyNetworkManager.Instance.AvatarSelections.OnListChanged -= OnAvatarListChanged;
            LobbyNetworkManager.Instance.CountdownTimer.OnValueChanged -= UpdateStatusText;

            _isSubscribed = false;
        }

        private void UpdateStatusText(int oldValue, int newValue)
        {
            if (statusText == null) return; 

            if (newValue == -1)
            {
                statusText.text = "Waiting for more players";
            }
            else
            {
                statusText.text = $"Game starts in {newValue}";
            }
        }

        private void RefreshLobbyUI()
        {
            if (this == null || gameObject == null || !gameObject.activeInHierarchy) return;

            if (LobbyNetworkManager.Instance == null) return;
            if (NetworkManager.Singleton == null || (!NetworkManager.Singleton.IsClient && !NetworkManager.Singleton.IsServer)) return;

            var connectedPlayers = LobbyNetworkManager.Instance.ConnectedPlayers;
            var selections = LobbyNetworkManager.Instance.AvatarSelections;

            if (connectedPlayers == null || selections == null) return;

            ulong myId = NetworkManager.Singleton.LocalClientId;

            // 1. อัปเดตรายชื่อฝั่งซ้าย (Player Slots)
            for (int i = 0; i < playerSlots.Length; i++)
            {
                if (playerSlots[i].slotRoot == null) return; // กันพังตอนเปลี่ยน Scene แบบฉิวเฉียด

                if (i < connectedPlayers.Count)
                {
                    playerSlots[i].slotRoot.SetActive(true);
                    ulong playerId = connectedPlayers[i];

                    playerSlots[i].nameText.text = $"Player {i + 1}";
                    playerSlots[i].hostTag.SetActive(i == 0); 

                    int avatarIndex = LobbyNetworkManager.Instance.GetSelectedAvatarIndexForClient(playerId);
                    
                    if (avatarIndex != -1 && avatarIndex < avatarSprites.Length)
                    {
                        playerSlots[i].avatarImage.sprite = avatarSprites[avatarIndex];
                    }
                    else
                    {
                        playerSlots[i].avatarImage.sprite = placeholderSprite; 
                    }
                }
                else
                {
                    playerSlots[i].slotRoot.SetActive(false); 
                }
            }

            // 2. อัปเดตปุ่ม Avatar ฝั่งขวา
            for (int i = 0; i < avatarSelectButtons.Length; i++)
            {
                if (i >= selections.Count) continue;

                ulong ownerId = selections[i];
                bool isFree = (ownerId == ulong.MaxValue);
                bool isMine = (ownerId == myId);

                avatarSelectButtons[i].interactable = (isFree || isMine);

                if (mySelectionHighlights.Length > i && mySelectionHighlights[i] != null)
                {
                    mySelectionHighlights[i].SetActive(isMine);
                }
            }
        }
        #endregion
    }
}