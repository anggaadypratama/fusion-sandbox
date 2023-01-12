using UnityEngine;
using Fusion;
using StarterAssets;
using Cinemachine;

public class NetworkPlayer : NetworkBehaviour
{
  public StarterAssetsInputs input;
  public Transform camTarget;
  public CinemachineVirtualCamera cam;
  public UICanvasControllerInput inputMobile;
  [Networked] public int Token { get; set; }
  [Networked] public PlayerRef Player { get; set; }

  private void Awake()
  {
    cam = GameObject.FindObjectOfType<CinemachineVirtualCamera>();
    inputMobile = GameObject.FindObjectOfType<UICanvasControllerInput>();
  }

  private void Start()
  {
    OnAssignObject();
  }

  public void OnResetObject()
  {
    cam = null;
    inputMobile = null;

    Debug.Log("Object reset");
  }

  public void OnAssignObject()
  {
    cam = GameObject.FindObjectOfType<CinemachineVirtualCamera>();
    inputMobile = GameObject.FindObjectOfType<UICanvasControllerInput>();

    if (Object.HasInputAuthority)
    {
      cam.Follow = camTarget;
      cam.LookAt = camTarget;

      inputMobile.starterAssetsInputs = input;

      Debug.Log("Object assigned");

    }
  }
}