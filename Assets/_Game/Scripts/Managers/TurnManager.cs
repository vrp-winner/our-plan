using UnityEngine;
using Unity.Netcode;
using System;

namespace Managers
{
    public class TurnManager : SingletonNetwork<TurnManager>
    {
        [Header("Settings")]
        [SerializeField] private int playerRequired = 2; // บังคับ 2 คนตามโหมดปกติ
        [SerializeField] private int turnsPerCycle = 4;

        // Network Variables
        private NetworkList<ulong> _playerIds;
        private NetworkVariable<bool> _isGameStarted = new NetworkVariable<bool>(false);
        private NetworkVariable<int> _currentPlayerIndex = new NetworkVariable<int>(0);
        private NetworkVariable<int> _currentRound = new NetworkVariable<int>(0);
        private NetworkVariable<int> _currentCycle = new NetworkVariable<int>(0);

        public event Action<ulong> OnPlayerTurnChanged;
        public event Action<int> OnRoundChanged;
        public event Action<int> OnCycleChanged;
        public event Action<ulong, bool, string> OnGameOver;
        public event Action<bool> OnGameStateChanged;

        public bool IsGameStarted => _isGameStarted.Value;
        public ulong CurrentActivePlayerId
        {
            get
            {
                if (!IsSpawned || _playerIds == null || _playerIds.Count == 0)
                    return 99999;

                if (_currentPlayerIndex.Value >= _playerIds.Count)
                    return _playerIds[0];

                return _playerIds[_currentPlayerIndex.Value];
            }
        }

        protected override void Awake()
        {
            base.Awake();
            _playerIds = new NetworkList<ulong>();
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            if (IsServer) NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
            _isGameStarted.OnValueChanged += (_, newValue) => OnGameStateChanged?.Invoke(newValue);

            _currentPlayerIndex.OnValueChanged += (_, newValue) => NotifyTurnChange();
            _currentRound.OnValueChanged += (_, newValue) => OnRoundChanged?.Invoke(newValue);
            _currentCycle.OnValueChanged += (_, newValue) => OnCycleChanged?.Invoke(newValue);
        }
        public override void OnNetworkDespawn()
        {
            base.OnNetworkDespawn();
            if (IsServer && NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
            }

            if (_playerIds != null)
            {
                _playerIds.Dispose();
            }
        }

        private void OnClientConnected(ulong clientId)
        {
            if (IsServer)
            {
                if (!_playerIds.Contains(clientId)) // ป้องกันการเพิ่ม ID ซ้ำ
                {
                    _playerIds.Add(clientId);
                    Debug.Log($"[TurnManager] Player {clientId} Connected. Total: {_playerIds.Count}");
                }

                if (_playerIds.Count >= playerRequired && !IsGameStarted)
                {
                    StartGame();
                }
            }
        }

        public void StartGame()
        {
            if (!IsServer) return;
            _isGameStarted.Value = true;
            _currentRound.Value = 1;
            _currentCycle.Value = 1;
            _currentPlayerIndex.Value = 0;
            NotifyTurnChange();
        }

        private void NotifyTurnChange()
        {
            if (_playerIds.Count == 0) return;
            OnPlayerTurnChanged?.Invoke(_playerIds[_currentPlayerIndex.Value]);
        }

        [Rpc(SendTo.Server)]
        public void RequestEndTurnRpc()
        {
            int nextIndex = _currentPlayerIndex.Value + 1;
            if (nextIndex >= _playerIds.Count)
            {
                nextIndex = 0;
                _currentRound.Value++;
                int calcCycle = ((_currentRound.Value - 1) / turnsPerCycle) + 1;
                if (calcCycle > _currentCycle.Value) _currentCycle.Value = calcCycle;
            }
            _currentPlayerIndex.Value = nextIndex;
        }

        [Rpc(SendTo.ClientsAndHost)]
        public void NotifyEndGameRpc(ulong playerId, bool isWin, string reason)
        {
            _isGameStarted.Value = false;
            OnGameOver?.Invoke(playerId, isWin, reason);
        }
    }
}