using System.Collections.Generic;
using UnityEngine;
using System.Threading.Tasks;
using Fusion.Sockets;
using Fusion.Photon.Realtime;
using System;
using UnityEngine.SceneManagement;
using StarterAssets;
using Fusion;
using Cinemachine;

public enum GameState
{
  Idle,  // Initial non running state
  StartAuto, // Starting Peer
  Started // Peer is running
}

[Serializable] public class CustomDictionary<TKey, TValue> : SerializableDictionary<TKey, TValue> { }
public class GameController : MonoBehaviour, INetworkRunnerCallbacks
{
  public GameState CurrentGameState { get; private set; }
  public static GameController Instance
  {
    get
    {
      if (_instance == null)
      {
        _instance = FindObjectOfType<GameController>();
      }

      return _instance;
    }
  }

  public struct KeyValue<K, V>
  {
    public K key;
    public V value;
  }

  [Header("Prefabs")]
  [SerializeField] private NetworkRunner _runnerPrefab;
  [SerializeField] private NetworkObject _playerPrefab;
  [SerializeField] StarterAssetsInputs inputAsset;

  [Header("User Info")]
  public CustomDictionary<int, NetworkObject> _playersMap;
  [Space(20)]
  public CustomDictionary<PlayerRef, NetworkObject> _playerCharacterMap;
  [Space(20)]

  private static GameController _instance;
  private NetworkRunner _instanceRunner;
  private byte[] _connectionToken;

  List<int> _pendingTokens;
  System.Diagnostics.Stopwatch _watch = new System.Diagnostics.Stopwatch();
  float CLEANUP_TIMEOUT = 1000 * 10;

  public void ChangeGameState(GameState newGameState)
  {
    if (CurrentGameState == newGameState) { return; }

    Debug.Log($"Game State changed from {CurrentGameState} to {newGameState}");

    // update
    CurrentGameState = newGameState;

    switch (CurrentGameState)
    {
      case GameState.Idle:
        break;
      case GameState.StartAuto:
        Run_StartGame();
        break;
      default:
        break;
    }
  }

  void Awake()
  {
    Application.targetFrameRate = 60;

    if (_instance == null)
    {
      _instance = this;

      _instance._connectionToken = ConnectionTokenUtils.NewToken();
      _instance._pendingTokens = new List<int>();
    }

    if (_instance != this)
    {
      Destroy(gameObject);
    }
    else
    {
      DontDestroyOnLoad(gameObject);
    }
  }

  private void Start()
  {
    ChangeGameState(GameState.StartAuto);
  }

  void Update()
  {
    if (_instanceRunner == null) return;
    if (_instanceRunner.IsServer && _watch.IsRunning && _watch.ElapsedMilliseconds > CLEANUP_TIMEOUT)
    {
      _watch.Stop();
      lock (_pendingTokens)
      {
        foreach (var token in _pendingTokens)
        {
          if (_playersMap.TryGetValue(token, out var NetworkPlayer))
          {
            _instanceRunner.Despawn(NetworkPlayer);
            _playersMap.Remove(token);
          }
        }

        _pendingTokens.Clear();
      }
    }
  }

  async void Run_StartGame()
  {
    if (_instanceRunner == null)
    {
      Debug.Log("Run_StartGame");

      _instanceRunner = GetRunner("Runner");

      var result = await StartSimulation(_instanceRunner, GameMode.AutoHostOrClient, _connectionToken);

      if (result.Ok)
      {
        Debug.Log(GameState.Started);
        ChangeGameState(GameState.Started);
        Debug.Log("Done");
      }
      else
      {
        Debug.Log(GameState.Idle);
        ChangeGameState(GameState.Idle);

        Destroy(_instanceRunner);
        _instanceRunner = null;
      }
    }
  }

  Task<StartGameResult> StartSimulation(NetworkRunner runner, GameMode gameMode, byte[] connectionToken, HostMigrationToken migrationToken = null, Action<NetworkRunner> migrationResume = null)
  {

    return runner.StartGame(new StartGameArgs()
    {
      GameMode = gameMode,
      SceneManager = runner.gameObject.AddComponent<NetworkSceneManagerDefault>(),
      Scene = SceneManager.GetActiveScene().buildIndex,
      HostMigrationToken = migrationToken,
      HostMigrationResume = migrationResume,
      ConnectionToken = connectionToken
    });
  }

  NetworkRunner GetRunner(string name)
  {
    var runner = Instantiate(_runnerPrefab);
    runner.name = name;
    runner.ProvideInput = true;
    runner.AddCallbacks(this);

    return runner;
  }

  int GetPlayerToken(NetworkRunner runner, PlayerRef player)
  {
    if (runner.LocalPlayer == player)
    {
      return ConnectionTokenUtils.HashToken(_connectionToken);
    }
    else
    {
      var token = runner.GetPlayerConnectionToken(player);

      if (token != null)
      {
        return ConnectionTokenUtils.HashToken(token);
      }
    }

    return 0; // invalid
  }

  public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
  {
    Log.Warn($"{nameof(OnPlayerJoined)}: {player}");

    if (_playerPrefab != null && runner.IsServer)
    {
      int playerToken = GetPlayerToken(runner, player);

      Debug.Log($"Player Joined with token {playerToken}");

      Debug.Log("check token : " + _playersMap.ContainsKey(playerToken));

      if (_playersMap.TryGetValue(playerToken, out var NetworkPlayer))
      {
        Debug.Log("Taking control over pre-created Player");
        NetworkPlayer.AssignInputAuthority(player);

        _playerCharacterMap[player] = NetworkPlayer;

        lock (_pendingTokens)
        {
          if (_pendingTokens.Contains(playerToken))
          {
            _pendingTokens.Remove(playerToken);
          }
        }
      }
      else
      {
        Debug.Log("Creating new Player");

        // random position
        var pos = UnityEngine.Random.insideUnitSphere * 3;
        pos.y = 1;

        // Spawn a new Player
        var playerInstance = runner.Spawn(_playerPrefab, pos, Quaternion.identity, inputAuthority: player, onBeforeSpawned: (runner, obj) =>
        {
          obj.GetBehaviour<NetworkPlayer>().Token = playerToken;
          var inputPlayer = obj.GetComponent<StarterAssetsInputs>();

          if (inputPlayer != null) inputAsset = inputPlayer;
        });

        // Store player ref
        _playersMap[playerToken] = playerInstance;
        _playerCharacterMap[player] = playerInstance;
      }


      Log.Warn("Spawn in ClientServer Mode");
    }
  }

  public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
  {
    if (runner.IsServer)
    {
      int playerToken = 0; // invalid

      if (_playerCharacterMap.TryGetValue(player, out var playerCharacter))
      {
        playerToken = playerCharacter.GetComponent<NetworkPlayer>().Token;

        runner.Despawn(playerCharacter);
      }

      Debug.Log($"Recovered Token: {playerToken}");

      if (_playersMap.ContainsKey(playerToken))
      {
        _playersMap.Remove(playerToken);
      }
    }

    Log.Warn($"{nameof(OnPlayerLeft)}: {player}");
  }

  public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason)
  {
    Debug.Log(shutdownReason);
    Debug.Log(GameState.Idle);
    ChangeGameState(GameState.Idle);

    _playersMap.Clear();
    _playerCharacterMap.Clear();

    // Reload scene after shutdown
    if (Application.isPlaying && shutdownReason != ShutdownReason.HostMigration)
    {
      SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
  }
  public void OnDisconnectedFromServer(NetworkRunner runner)
  {
    runner.Shutdown();
  }

  public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason)
  {
    runner.Shutdown();
  }

  public async void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken)
  {
    Debug.Log($"Shutdown Old Runner: {runner.gameObject.name}");

    // Shutdown old Runner
    await runner.Shutdown(shutdownReason: ShutdownReason.HostMigration);

    Debug.Log("Shutdown old Runner DONE");

    // Continue with the restart
    // Create a new Custom Runner
    _instanceRunner = GetRunner(hostMigrationToken.GameMode.ToString());

    Debug.Log($"Created new Runner: {_instanceRunner.gameObject.name}");

    // Update Game State
    Debug.Log(GameState.Started);
    ChangeGameState(GameState.Started);

    Debug.Log("Start new RUNNER");

    // Start new Runner with the Host Migration Token
    var result = await StartSimulation(
      _instanceRunner,
      hostMigrationToken.GameMode,
      _connectionToken,
      migrationToken: hostMigrationToken,
      migrationResume: HostMigrationResume);

    if (result.Ok == false)
    {
      Debug.LogWarning(result.ShutdownReason);
      ChangeGameState(GameState.Idle);
    }
    else
    {
      Debug.Log("Done");
    }
  }

  void HostMigrationResume(NetworkRunner runner)
  {
    Debug.Log($"Resume Simulation on new Runner");

    foreach (var resumeNO in runner.GetResumeSnapshotNetworkObjects())
    {
      if ((resumeNO.TryGetBehaviour<NetworkPlayer>(out var test)) && resumeNO.TryGetBehaviour<NetworkPositionRotation>(out var posRot))
      {

        Debug.Log("prevSnapshot : " + test.Token);
        var newNO = runner.Spawn(resumeNO, position: posRot.ReadPosition(), rotation: posRot.ReadRotation(), onBeforeSpawned: (runner, newNO) =>
        {
          newNO.CopyStateFrom(resumeNO);

          // If only partial State is necessary, it is possible to copy it only from specific NetworkBehaviours
          if (resumeNO.TryGetBehaviour<NetworkPlayer>(out var NetworkPlayerComponentRef))
          {
            var newNetworkPlayer = newNO.GetComponent<NetworkPlayer>();
            // newNetworkPlayer.CopyStateFrom(NetworkPlayerComponentRef);
          }
        });

        if (newNO.TryGetBehaviour<NetworkPlayer>(out var NetworkPlayer))
        {
          // Store Player for reconnection
          _playersMap[NetworkPlayer.Token] = newNO;
          _pendingTokens.Add(NetworkPlayer.Token);
        }
      }
    }

    // Start clean up timeout
    _watch.Start();

    Debug.Log("Resume Simulation DONE");
  }

  public void OnInput(NetworkRunner runner, NetworkInput input) { }
  public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
  public void OnConnectedToServer(NetworkRunner runner) { }
  public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }
  public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
  public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList) { }
  public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }
  public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ArraySegment<byte> data) { }
  public void OnSceneLoadDone(NetworkRunner runner) { }
  public void OnSceneLoadStart(NetworkRunner runner) { }
}