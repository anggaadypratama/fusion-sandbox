using UnityEngine;
using Fusion;

public struct NetworkInputData : INetworkInput
{
  public Vector2 move;
  public bool jump;
  public bool sprint;
  public bool analogMovement;
  public Vector2 look;
}