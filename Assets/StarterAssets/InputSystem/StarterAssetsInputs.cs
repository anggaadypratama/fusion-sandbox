using UnityEngine;
using Fusion;
#if ENABLE_INPUT_SYSTEM && STARTER_ASSETS_PACKAGES_CHECKED
using UnityEngine.InputSystem;
#endif

namespace StarterAssets
{
  public class StarterAssetsInputs : NetworkBehaviour
  {
    [Header("Character Input Values")]
    public Vector2 move;
    public Vector2 look;
    public NetworkBool jump;
    public NetworkBool sprint;

    [Header("Movement Settings")]
    public NetworkBool analogMovement;

    [Header("Mouse Cursor Settings")]
    public bool cursorLocked = true;
    public bool cursorInputForLook = true;

#if ENABLE_INPUT_SYSTEM && STARTER_ASSETS_PACKAGES_CHECKED
    public void OnMove(InputValue value)
    {
      if (Object.HasInputAuthority)
      {
        MoveInput(value.Get<Vector2>());
      }
    }

    public void OnLook(InputValue value)
    {
      if (cursorInputForLook)
      {
        LookInput(value.Get<Vector2>());
      }
    }

    public void OnJump(InputValue value)
    {
      JumpInput(value.isPressed);
    }

    public void OnSprint(InputValue value)
    {
      SprintInput(value.isPressed);
    }
#endif


    public void MoveInput(Vector2 newMoveDirection)
    {
      move = newMoveDirection;
    }

    public void LookInput(Vector2 newLookDirection)
    {
      look = newLookDirection;
    }

    public void JumpInput(bool newJumpState)
    {
      jump = newJumpState;
    }

    public void SprintInput(bool newSprintState)
    {
      Debug.Log(newSprintState + " OnSprint StarterInput");

      sprint = newSprintState;
    }

    private void OnApplicationFocus(bool hasFocus)
    {
      SetCursorState(cursorLocked);
    }

    private void SetCursorState(bool newState)
    {
      Cursor.lockState = newState ? CursorLockMode.Locked : CursorLockMode.None;
    }
  }

}