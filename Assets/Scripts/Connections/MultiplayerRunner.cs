using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Fusion;
using Fusion.Sockets;
using UnityEngine.SceneManagement;
using System;
using System.Linq;
using System.Threading.Tasks;

public class MultiplayerRunner : MonoBehaviour
{
  [SerializeField] NetworkRunner networkRunnerPrefab;
  [SerializeField] GameMode gameMode;
  NetworkRunner networkRunner;

  private void Start()
  {
    networkRunner = Instantiate(networkRunnerPrefab);
    networkRunner.name = "NetworkRunner";

    var client = InitializeRunner(networkRunner, gameMode, NetAddress.Any(), SceneManager.GetActiveScene().buildIndex, null);

    Debug.Log($"Server Started {client.Status}");
  }

  protected virtual Task InitializeRunner(NetworkRunner netRun, GameMode mode, NetAddress address, SceneRef scene, Action<NetworkRunner> init)
  {
    var sceneObjectProvider = netRun.GetComponents(typeof(MonoBehaviour)).OfType<INetworkSceneManager>().FirstOrDefault();

    if (sceneObjectProvider == null)
      sceneObjectProvider = netRun.gameObject.AddComponent<NetworkSceneManagerDefault>();

    netRun.ProvideInput = true;

    return netRun.StartGame(new StartGameArgs
    {
      GameMode = mode,
      Address = address,
      Scene = scene,
      Initialized = init
    });
  }
}
