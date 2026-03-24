using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Unity.Netcode;
using Managers;
using System.Collections;

namespace UI
{
    public class MainMenuUIManager : MonoBehaviour
    {
        public static MainMenuUIManager Instance { get; private set; }

        #region หน้าต่าง UI (Panels)
        [Header("Panels (หน้าต่างต่างๆ)")]
        [SerializeField] private GameObject mainMenuPanel;
        [SerializeField] private GameObject hostSetupPanel;
        [SerializeField] private GameObject timeSetupPanel;
        [SerializeField] private GameObject characterSetupPanel; 
        [SerializeField] private GameObject waitingLobbyPanel;
        #endregion

        #region ปุ่ม (Buttons)

        [Header("Main Menu Buttons")]
        [SerializeField] private Button playBtn;
        [SerializeField] private Button playOnlineBtn;
        [SerializeField] private Button exitBtn;

        [Header("Host Setup Buttons")]
        [SerializeField] private Button twoPlayersBtn;
        [SerializeField] private Button fourPlayersBtn;
        [SerializeField] private Button backFromHostBtn;

        [Header("Time Setup Buttons")]
        [SerializeField] private Button shortTimeBtn;  
        [SerializeField] private Button mediumTimeBtn;  
        [SerializeField] private Button longTimeBtn;    
        [SerializeField] private Button unlimitedTimeBtn; 
        [SerializeField] private Button backFromTimeBtn;

        [Header("Character Selection Buttons")]
        [SerializeField] private Button[] avatarButtons; 
        [SerializeField] private Button confirmAvatarBtn;
        [SerializeField] private Button backFromAvatarBtn;
        [SerializeField] private TextMeshProUGUI selectedAvatarText; 

        [Header("Waiting Lobby UI")]
        [SerializeField] private TextMeshProUGUI statusText;
        [SerializeField] private Button startGameBtn;

        #endregion

        #region ตัวแปรเก็บสถานะ (State Variables)
        private int _tempPlayerCount = 2;
        private int _tempMaxCycles = 15;
        private bool _isHostMode = false;
        private int _selectedAvatarIndex = 0; 
        private int _lastPlayerCount = -1;
        #endregion

        #region Lifecycle
        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
        }

        private void Update()
        {
            if (waitingLobbyPanel.activeInHierarchy && NetworkManager.Singleton.IsServer)
            {
                int currentPlayers = NetworkManager.Singleton.ConnectedClients.Count;

                if (currentPlayers != _lastPlayerCount)
                {
                    _lastPlayerCount = currentPlayers;
                    UpdateStatusText();
                }
            }
        }
        private void Start()
        {
            playBtn.onClick.AddListener(() => SwitchPanel(hostSetupPanel));
            playOnlineBtn.onClick.AddListener(() => GoToCharacterSelection(false, 0));
            exitBtn.onClick.AddListener(() => Application.Quit());

            twoPlayersBtn.onClick.AddListener(() => GoToTimeSetup(2));
            fourPlayersBtn.onClick.AddListener(() => GoToTimeSetup(4));
            backFromHostBtn.onClick.AddListener(() => SwitchPanel(mainMenuPanel));

            shortTimeBtn.onClick.AddListener(() => GoToCharacterSelection(true, 15));
            mediumTimeBtn.onClick.AddListener(() => GoToCharacterSelection(true, 30));
            longTimeBtn.onClick.AddListener(() => GoToCharacterSelection(true, 45));
            unlimitedTimeBtn.onClick.AddListener(() => GoToCharacterSelection(true, 9999)); 
            backFromTimeBtn.onClick.AddListener(() => SwitchPanel(hostSetupPanel));

            for (int i = 0; i < avatarButtons.Length; i++)
            {
                int index = i; 
                avatarButtons[i].onClick.AddListener(() => SelectAvatar(index));
            }
            confirmAvatarBtn.onClick.AddListener(ConfirmCharacterAndConnect);
            backFromAvatarBtn.onClick.AddListener(() => {
                if (_isHostMode) SwitchPanel(timeSetupPanel);
                else SwitchPanel(mainMenuPanel);
            });

            startGameBtn.onClick.AddListener(StartMatch);

            SwitchPanel(mainMenuPanel);
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;

            if (TurnManager.Instance != null)
                TurnManager.Instance.OnGameStateChanged += HandleGameStateChanged;
        }

        private void OnDestroy()
        {
            if (NetworkManager.Singleton != null)
                NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
            if (TurnManager.Instance != null)
                TurnManager.Instance.OnGameStateChanged -= HandleGameStateChanged;
        }
        #endregion

        #region Flow Logic (การเปลี่ยนหน้าต่าง)
        private void SwitchPanel(GameObject panelToShow)
        {
            mainMenuPanel.SetActive(false);
            hostSetupPanel.SetActive(false);
            timeSetupPanel.SetActive(false);
            characterSetupPanel.SetActive(false);
            waitingLobbyPanel.SetActive(false);

            panelToShow.SetActive(true);
        }
        private void GoToTimeSetup(int playerCount)
        {
            _tempPlayerCount = playerCount;
            SwitchPanel(timeSetupPanel);
        }

        private void GoToCharacterSelection(bool isHost, int maxCycles)
        {
            _isHostMode = isHost;
            _tempMaxCycles = maxCycles;

            SelectAvatar(0);
            SwitchPanel(characterSetupPanel);
        }

        private void SelectAvatar(int index)
        {
            _selectedAvatarIndex = index;
            if (selectedAvatarText != null)
            {
                selectedAvatarText.text = $"เลือกตัวละครแบบที่: {index + 1}";
            }
        }
        #endregion

        #region Network Events (การจัดการผู้เล่น)
        private void ConfirmCharacterAndConnect()
        {
            

            if (_isHostMode)
            {
                TurnManager.Instance.SetPlayerRequired(_tempPlayerCount);
                TurnManager.Instance.SetMaxCycles(_tempMaxCycles);
                NetworkManager.Singleton.StartHost();

                TurnManager.Instance.RegisterPlayerAvatar(NetworkManager.Singleton.LocalClientId, _selectedAvatarIndex);
            }
            else
            {
                NetworkManager.Singleton.StartClient();
            }

            SwitchPanel(waitingLobbyPanel);
            startGameBtn.gameObject.SetActive(false);
            UpdateStatusText();
        }
        #endregion

        #region Network Events
        private void OnClientConnected(ulong clientId)
        {
            if (!_isHostMode && clientId == NetworkManager.Singleton.LocalClientId)
            {
                TurnManager.Instance.SubmitAvatarSelectionServerRpc(_selectedAvatarIndex);
            }

            if (NetworkManager.Singleton.IsServer) UpdateStatusText();
        }

        private void UpdateStatusText()
        {
            int currentPlayers = NetworkManager.Singleton.ConnectedClients.Count;
            int required = TurnManager.Instance.GetPlayerRequired();

            if (_isHostMode)
            {
                statusText.text = $"รอผู้เล่นเข้าร่วม... ({currentPlayers}/{required})";
                if (currentPlayers >= required)
                {
                    statusText.text = "ผู้เล่นครบแล้ว! พร้อมลุย!";
                    startGameBtn.gameObject.SetActive(true);
                    startGameBtn.interactable = true;
                }
            }
            else
            {
                statusText.text = "กำลังเชื่อมต่อ... รอ Host กดเริ่มเกม";
            }
        }

        private void StartMatch()
        {
            TurnManager.Instance.StartGameServerRpc();
        }

        private void HandleGameStateChanged(bool isStarted)
        {
            if (isStarted && gameObject.activeSelf)
            {
                gameObject.SetActive(false);
            }
        }
        #endregion

    }
}