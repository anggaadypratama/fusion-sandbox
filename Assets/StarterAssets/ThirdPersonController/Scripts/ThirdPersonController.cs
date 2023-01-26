﻿using UnityEngine;
using Fusion;
#if ENABLE_INPUT_SYSTEM && STARTER_ASSETS_PACKAGES_CHECKED
using UnityEngine.InputSystem;
#endif

/* Note: animations are called via the controller for both the character and capsule using animator null checks
 */

namespace StarterAssets
{
  [RequireComponent(typeof(CharacterController))]
  [OrderBefore(typeof(NetworkTransform))]
  [DisallowMultipleComponent]
#if ENABLE_INPUT_SYSTEM && STARTER_ASSETS_PACKAGES_CHECKED
  [RequireComponent(typeof(PlayerInput))]
#endif
  public class ThirdPersonController : NetworkBehaviour
  {
    [Header("Player")]
    [Tooltip("Move speed of the character in m/s")]
    public float MoveSpeed = 2.0f;

    [Tooltip("Sprint speed of the character in m/s")]
    public float SprintSpeed = 5.335f;

    [Tooltip("How fast the character turns to face movement direction")]
    [Range(0.0f, 0.3f)]
    public float RotationSmoothTime = 0.12f;

    [Tooltip("Acceleration and deceleration")]
    public float SpeedChangeRate = 10.0f;

    public AudioClip LandingAudioClip;
    public AudioClip[] FootstepAudioClips;
    [Range(0, 1)] public float FootstepAudioVolume = 0.5f;

    [Space(10)]
    [Tooltip("The height the player can jump")]
    public float JumpHeight = 1.2f;

    [Tooltip("The character uses its own gravity value. The engine default is -9.81f")]
    public float Gravity = -15.0f;

    [Space(10)]
    [Tooltip("Time required to pass before being able to jump again. Set to 0f to instantly jump again")]
    public float JumpTimeout = 0.50f;

    [Tooltip("Time required to pass before entering the fall state. Useful for walking down stairs")]
    public float FallTimeout = 0.15f;

    [Tooltip("If the character is grounded or not. Not part of the CharacterController built in grounded check")]
    [Networked] public bool Grounded { get; set; }

    [Tooltip("Useful for rough ground")]
    public float GroundedOffset = -0.02f;

    [Tooltip("The radius of the grounded check. Should match the radius of the CharacterController")]
    public float GroundedRadius = 0.28f;

    [Tooltip("What layers the character uses as ground")]
    public LayerMask GroundLayers;

    [Header("Cinemachine")]
    [Tooltip("The follow target set in the Cinemachine Virtual Camera that the camera will follow")]
    public GameObject CinemachineCameraTarget;

    [Tooltip("How far in degrees can you move the camera up")]
    public float TopClamp = 70.0f;

    [Tooltip("How far in degrees can you move the camera down")]
    public float BottomClamp = -30.0f;

    [Tooltip("Additional degress to override the camera. Useful for fine tuning camera position when locked")]
    public float CameraAngleOverride = 0.0f;

    [Tooltip("For locking the camera position on all axis")]
    public bool LockCameraPosition = false;

    // cinemachine
    private float _cinemachineTargetYaw;
    private float _cinemachineTargetPitch;

    // player
    private float _speed;
    [Networked] public float animationBlend { get; set; }
    [Networked] public float inputMagnitude { get; set; }
    [Networked] public NetworkBool isJump { get; set; }
    [Networked] public NetworkBool isAnimJump { get; set; }
    [Networked] public NetworkBool isAnimFreeFall { get; set; }
    // [Networked] public Vector3 moving { get; set; }

    public Interpolator<Vector3> NetMoveInterpolator;
    private float _targetRotation = 0.0f;
    private float _rotationVelocity;
    private float _verticalVelocity;
    private float _terminalVelocity = 53.0f;

    // timeout deltatime
    private float _jumpTimeoutDelta;
    private float _fallTimeoutDelta;

    // animation IDs
    [Header("Animation Id")]
    public int _animIDSpeed;
    public int _animIDGrounded;
    public int _animIDJump;
    public int _animIDFreeFall;
    public int _animIDMotionSpeed;

#if ENABLE_INPUT_SYSTEM && STARTER_ASSETS_PACKAGES_CHECKED
    private PlayerInput _playerInput;
#endif
    // private Animator _networkMecanimAnimator;
    private NetworkMecanimAnimator _networkMecanimAnimator;
    private CharacterController _controller;
    // private StarterAssetsInputs _input;
    private GameObject _mainCamera;

    private const float _threshold = 0.01f;

    private bool _hasAnimator;

    private NetworkInputData networkData = new NetworkInputData();

    private bool IsCurrentDeviceMouse
    {
      get
      {
#if ENABLE_INPUT_SYSTEM && STARTER_ASSETS_PACKAGES_CHECKED
        return _playerInput.currentControlScheme == "KeyboardMouse";
#else
        return false;
#endif
      }
    }
    protected void Awake()
    {
      // get a reference to our main camera
      if (_mainCamera == null)
      {
        _mainCamera = GameObject.FindGameObjectWithTag("MainCamera");
      }
    }

    private void Start()
    {
      _cinemachineTargetYaw = CinemachineCameraTarget.transform.rotation.eulerAngles.y;

      _networkMecanimAnimator = GetComponent<NetworkMecanimAnimator>();
      _hasAnimator = TryGetComponent(out _networkMecanimAnimator.Animator);
      _controller = GetComponent<CharacterController>();
#if ENABLE_INPUT_SYSTEM && STARTER_ASSETS_PACKAGES_CHECKED
      _playerInput = GetComponent<PlayerInput>();
#else
      Debug.LogError("Starter Assets package is missing dependencies. Please use Tools/Starter Assets/Reinstall Dependencies to fix it");
#endif

      AssignAnimationIDs();

      // reset our timeouts on start
      _jumpTimeoutDelta = JumpTimeout;
      _fallTimeoutDelta = FallTimeout;
    }

    private void Update()
    {
      _hasAnimator = TryGetComponent(out _networkMecanimAnimator.Animator);
    }

    public override void FixedUpdateNetwork()
    {
      if (GetInput<NetworkInputData>(out var data))
      {
        if (HasStateAuthority || (HasStateAuthority && Runner.IsForward))
        {
          networkData = data;

          JumpAndGravity(data);
          Move(data);
        }
      }
    }

    public override void Spawned()
    {
      base.Spawned();
      Grounded = true;
      // NetMoveInterpolator = GetInterpolator<Vector3>(nameof(moving));
    }

    public override void Render()
    {
      GroundedCheck();
      // _controller.Move(NetMoveInterpolator.Value);

      if (_hasAnimator)
      {
        _networkMecanimAnimator.Animator.SetBool(_animIDGrounded, Grounded);
        _networkMecanimAnimator.Animator.SetFloat(_animIDSpeed, animationBlend);
        _networkMecanimAnimator.Animator.SetFloat(_animIDMotionSpeed, inputMagnitude);
      }

      if (Grounded)
      {
        if (isJump)
        {
          Debug.Log("Jumping");
          if (_hasAnimator)
          {
            isAnimJump = true;
            _networkMecanimAnimator.Animator.SetBool(_animIDJump, isAnimJump);
          }
        }
        else
        {
          if (_hasAnimator)
          {

            isAnimJump = false;
            isAnimFreeFall = false;



            _networkMecanimAnimator.Animator.SetBool(_animIDJump, isAnimJump);
            _networkMecanimAnimator.Animator.SetBool(_animIDFreeFall, isAnimFreeFall);
          }


        }
      }
      else
      {
        if (_fallTimeoutDelta >= 0.0f)
        {
          _fallTimeoutDelta -= Runner.DeltaTime;
        }
        else
        {
          // update animator if using character
          if (_hasAnimator)
          {
            Debug.Log("Falling");

            isAnimFreeFall = true;
            _networkMecanimAnimator.Animator.SetBool(_animIDFreeFall, isAnimFreeFall);
          }
        }
      }
    }

    private void LateUpdate()
    {
      CameraRotation(networkData);
    }

    private void AssignAnimationIDs()
    {
      _animIDSpeed = Animator.StringToHash("Speed");
      _animIDGrounded = Animator.StringToHash("Grounded");
      _animIDJump = Animator.StringToHash("Jump");
      _animIDFreeFall = Animator.StringToHash("FreeFall");
      _animIDMotionSpeed = Animator.StringToHash("MotionSpeed");
    }

    private void GroundedCheck()
    {
      Grounded = _controller.isGrounded;
    }

    private void CameraRotation(NetworkInputData _input)
    {
      // if there is an input and camera position is not fixed
      if (_input.look.sqrMagnitude >= _threshold && !LockCameraPosition)
      {
        //Don't multiply mouse input by Runner.DeltaTime;
        float deltaTimeMultiplier = IsCurrentDeviceMouse ? 1.0f : Runner.DeltaTime;

        _cinemachineTargetYaw += _input.look.x * deltaTimeMultiplier;
        _cinemachineTargetPitch += _input.look.y * deltaTimeMultiplier;
      }

      // clamp our rotations so our values are limited 360 degrees
      _cinemachineTargetYaw = ClampAngle(_cinemachineTargetYaw, float.MinValue, float.MaxValue);
      _cinemachineTargetPitch = ClampAngle(_cinemachineTargetPitch, BottomClamp, TopClamp);

      // Cinemachine will follow this target
      CinemachineCameraTarget.transform.rotation = Quaternion.Euler(_cinemachineTargetPitch + CameraAngleOverride,
          _cinemachineTargetYaw, 0.0f);
    }

    private void Move(NetworkInputData _input)
    {
      // set target speed based on move speed, sprint speed and if sprint is pressed
      float targetSpeed = _input.sprint ? SprintSpeed : MoveSpeed;

      // a simplistic acceleration and deceleration designed to be easy to remove, replace, or iterate upon

      // note: Vector2's == operator uses approximation so is not floating point error prone, and is cheaper than magnitude
      // if there is no input, set the target speed to 0
      if (_input.move == Vector2.zero) targetSpeed = 0.0f;

      // a reference to the players current horizontal velocity
      float currentHorizontalSpeed = new Vector3(_controller.velocity.x, 0.0f, _controller.velocity.z).magnitude;

      float speedOffset = 0.1f;
      inputMagnitude = _input.analogMovement ? _input.move.magnitude : 1f;

      // accelerate or decelerate to target speed
      if (currentHorizontalSpeed < targetSpeed - speedOffset ||
          currentHorizontalSpeed > targetSpeed + speedOffset)
      {
        // creates curved result rather than a linear one giving a more organic speed change
        // note T in Lerp is clamped, so we don't need to clamp our speed
        _speed = Mathf.Lerp(currentHorizontalSpeed, targetSpeed * inputMagnitude,
            Runner.DeltaTime * SpeedChangeRate);

        // round speed to 3 decimal places
        _speed = Mathf.Round(_speed * 1000f) / 1000f;
      }
      else
      {
        _speed = targetSpeed;
      }

      animationBlend = Mathf.Lerp(animationBlend, targetSpeed, Runner.DeltaTime * SpeedChangeRate);
      if (animationBlend < 0.01f) animationBlend = 0f;

      // normalise input direction
      Vector3 inputDirection = new Vector3(_input.move.x, 0.0f, _input.move.y).normalized;

      // note: Vector2's != operator uses approximation so is not floating point error prone, and is cheaper than magnitude
      // if there is a move input rotate player when the player is moving
      if (_input.move != Vector2.zero)
      {
        _targetRotation = Mathf.Atan2(inputDirection.x, inputDirection.z) * Mathf.Rad2Deg +
                          _mainCamera.transform.eulerAngles.y;
        float rotation = Mathf.SmoothDampAngle(transform.eulerAngles.y, _targetRotation, ref _rotationVelocity,
            RotationSmoothTime);

        // rotate to face input direction relative to camera position
        transform.rotation = Quaternion.Euler(0.0f, rotation, 0.0f);
      }


      Vector3 targetDirection = Quaternion.Euler(0.0f, _targetRotation, 0.0f) * Vector3.forward;

      // move the player
      _controller.Move(targetDirection.normalized * (_speed * Runner.DeltaTime) +
                       new Vector3(0.0f, _verticalVelocity, 0.0f) * Runner.DeltaTime);

      // moving = targetDirection.normalized * (_speed * Runner.DeltaTime) + new Vector3(0.0f, _verticalVelocity, 0.0f) * Runner.DeltaTime;
    }

    private void JumpAndGravity(NetworkInputData _input)
    {
      if (Grounded)
      {
        // reset the fall timeout timer
        _fallTimeoutDelta = FallTimeout;

        // stop our velocity dropping infinitely when grounded
        if (_verticalVelocity < 0.0f)
        {
          _verticalVelocity = -2f;
        }

        isJump = networkData.jump && _jumpTimeoutDelta <= 0.0f;
        // Jump
        if (isJump)
        {
          // the square root of H * -2 * G = how much velocity needed to reach desired height
          _verticalVelocity = Mathf.Sqrt(JumpHeight * -2f * Gravity);
        }

        // jump timeout
        if (_jumpTimeoutDelta >= 0.0f)
        {
          _jumpTimeoutDelta -= Runner.DeltaTime;
        }
      }
      else
      {
        // reset the jump timeout timer
        _jumpTimeoutDelta = JumpTimeout;

        // fall timeout
        if (_fallTimeoutDelta >= 0.0f)
        {
          _fallTimeoutDelta -= Runner.DeltaTime;
        }

        // if we are not grounded, do not jump
        networkData.jump = false;
      }

      // apply gravity over time if under terminal (multiply by delta time twice to linearly speed up over time)
      if (_verticalVelocity < _terminalVelocity)
      {
        _verticalVelocity += Gravity * Runner.DeltaTime;
      }
    }

    private static float ClampAngle(float lfAngle, float lfMin, float lfMax)
    {
      if (lfAngle < -360f) lfAngle += 360f;
      if (lfAngle > 360f) lfAngle -= 360f;
      return Mathf.Clamp(lfAngle, lfMin, lfMax);
    }

    private void OnDrawGizmosSelected()
    {
      Color transparentGreen = new Color(0.0f, 1.0f, 0.0f, 0.35f);
      Color transparentRed = new Color(1.0f, 0.0f, 0.0f, 0.35f);

      if (Grounded) Gizmos.color = transparentGreen;
      else Gizmos.color = transparentRed;

      // when selected, draw a gizmo in the position of, and matching radius of, the grounded collider
      Gizmos.DrawSphere(
          new Vector3(transform.position.x, transform.position.y - GroundedOffset, transform.position.z),
          GroundedRadius);
    }

    private void OnFootstep(AnimationEvent animationEvent)
    {
      if (animationEvent.animatorClipInfo.weight > 0.5f)
      {
        if (FootstepAudioClips.Length > 0)
        {
          var index = Random.Range(0, FootstepAudioClips.Length);
          AudioSource.PlayClipAtPoint(FootstepAudioClips[index], transform.TransformPoint(_controller.center), FootstepAudioVolume);
        }
      }
    }

    private void OnLand(AnimationEvent animationEvent)
    {
      if (animationEvent.animatorClipInfo.weight > 0.5f)
      {
        AudioSource.PlayClipAtPoint(LandingAudioClip, transform.TransformPoint(_controller.center), FootstepAudioVolume);
      }
    }
  }
}