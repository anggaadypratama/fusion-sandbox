using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Fusion;
using Fusion.Sockets;
using System;
using StarterAssets;
using System.Linq;
using Cinemachine;

public struct NetworkInputData : INetworkInput
{
  public Vector2 Direction;
  public bool isJump;
  public bool isSprint;
}

public class SpawnPlayer : MonoBehaviour, INetworkRunnerCallbacks
{
  public NetworkRunner networkRunnerPrefab;
  public NetworkPlayer networkPlayer;
  public Transform[] spawnPosition;
  public StarterAssetsInputs inputAsset;
  private Dictionary<PlayerRef, NetworkObject> spawnedCharacters = new Dictionary<PlayerRef, NetworkObject>();

  public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
  {
    if (runner.IsServer)
    {
      var length = spawnPosition.Length - 1;

      var netPlayer = runner.Spawn(networkPlayer.gameObject, spawnPosition[UnityEngine.Random.Range(0, spawnPosition.Length - 1)].position, Quaternion.identity, player);
      var inputPlayer = netPlayer.gameObject.GetComponent<StarterAssetsInputs>();

      if (inputPlayer != null) inputAsset = inputPlayer;
      spawnedCharacters.Add(player, netPlayer);
    }
  }
  private void Update()
  {
  }
  public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
  {
    if (runner.IsPlayer && spawnedCharacters.TryGetValue(player, out NetworkObject networkObject))
    {
      runner.Despawn(networkObject);
      spawnedCharacters.Remove(player);
    }
  }
  public void OnInput(NetworkRunner runner, NetworkInput input)
  {
    var data = new NetworkInputData();

    if (inputAsset != null)
    {
      data.Direction = inputAsset.move;
      data.isJump = inputAsset.jump;
      data.isSprint = inputAsset.sprint;

      Debug.Log("spawn : " + data.Direction);

      input.Set(data);
    }
  }

  public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input)
  {
    Debug.Log(player.PlayerId);
  }

  public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason)
  {
    if (shutdownReason == ShutdownReason.HostMigration)
    {
      // ...
    }
    else
    {
      // Or a normal Shutdown
    }
  }
  public void OnConnectedToServer(NetworkRunner runner) { }
  public void OnDisconnectedFromServer(NetworkRunner runner) { }
  public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }
  public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason) { }
  public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
  public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList) { }
  public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }
  public async void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken)
  {
    Debug.Log("eass");
    await runner.Shutdown(shutdownReason: ShutdownReason.HostMigration);
    var newRunner = Instantiate(networkRunnerPrefab);

    StartGameResult result = await newRunner.StartGame(new StartGameArgs()
    {
      // SessionName = SessionName,              // ignored, peer never disconnects from the Photon Cloud
      // GameMode = gameMode,                    // ignored, Game Mode comes with the HostMigrationToken
      HostMigrationToken = hostMigrationToken,   // contains all necessary info to restart the Runner
      HostMigrationResume = HostMigrationResume, // this will be invoked to resume the simulation
    });

    Debug.Log(ShutdownReason.HostMigration);

    Debug.Log(result.Ok);

    if (result.Ok == false)
    {
      Debug.LogWarning(result.ShutdownReason);
    }
    else
    {
      Debug.Log("Done");
    }
  }

  void HostMigrationResume(NetworkRunner runner)
  {

    // Get a temporary reference for each NO from the old Host
    foreach (var resumeNO in runner.GetResumeSnapshotNetworkObjects())

      if (
          // Extract any NetworkBehavior used to represent the position/rotation of the NetworkObject
          // this can be either a NetworkTransform or a NetworkRigidBody, for example
          resumeNO.TryGetBehaviour<NetworkPositionRotation>(out var posRot))
      {

        runner.Spawn(resumeNO, position: posRot.ReadPosition(), rotation: posRot.ReadRotation(), onBeforeSpawned: (runner, newNO) =>
        {
          // One key aspects of the Host Migration is to have a simple way of restoring the old NetworkObjects state
          // If all state of the old NetworkObject is all what is necessary, just call the NetworkObject.CopyStateFrom
          newNO.CopyStateFrom(resumeNO);

          // and/or

          // If only partial State is necessary, it is possible to copy it only from specific NetworkBehaviours
          if (resumeNO.TryGetBehaviour<NetworkBehaviour>(out var myCustomNetworkBehaviour))
          {
            newNO.GetComponent<NetworkBehaviour>().CopyStateFrom(myCustomNetworkBehaviour);
          }
        });
      }
  }

  public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ArraySegment<byte> data) { }
  public void OnSceneLoadDone(NetworkRunner runner) { }
  public void OnSceneLoadStart(NetworkRunner runner) { }
}

