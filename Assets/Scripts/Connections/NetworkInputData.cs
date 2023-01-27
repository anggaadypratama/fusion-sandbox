using UnityEngine;
using Fusion;

public struct NetworkInputData : INetworkInput
{
  public Vector2 move;
  public NetworkBool jump;
  public NetworkBool sprint;
  public NetworkBool analogMovement;
  public Vector2 look;
}