using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

public class PlayerMovement : MonoBehaviour
{
  public  float       _playerSpeed = 3f;
  public  float       _jumpForce = 8f;
  public  float       _rotationSpeed = 5f;
  public  bool        _useCameraRelativeMovement = false;
  private float       _playerMovementDirect;
  private bool        _isJumping;
  private PlayerInput _playerInput;
  private Rigidbody   _playerRigidBody;
  private Quaternion  _targetRotation;
  public  Vector3     _movementVelocity;
  private InputAction _playerMovement;
  private InputAction _playerJump;
  public  GameObject  _camera;
  private Vector3     _localNormal;
  private bool        _isGrounded;
  public  float       _groundCheckDistance = 0.6f;
  public  float       _forwardGroundCheck = 0.6f;
  public  float       _airControlAcceleration = 10f;

  void Start()
  {
    _playerInput = new PlayerInput();
    _playerRigidBody = GetComponent<Rigidbody>();
    _playerMovement = _playerInput.FindAction("MovePlayer");
    if (_playerMovement != null) {
      _playerMovement.Enable();
    }
    _playerJump = _playerInput.FindAction("JumpPlayer");
    if (_playerJump != null) {
      _playerJump.Enable();
    }
    _isJumping = false;
    _targetRotation = transform.rotation;
    _movementVelocity = Vector3.zero;
    _localNormal = Vector3.up;
    _isGrounded = false;
    if (_playerRigidBody != null) {
      _playerRigidBody.interpolation = RigidbodyInterpolation.Interpolate;
    }
  }

  // FixedUpdate is called on the physics timestep. Apply smoothed rotation here for Rigidbody.
  void FixedUpdate()
  {
    if (_playerRigidBody == null) {
      return;
    }

    // Determine if there's ground slightly ahead to allow stepping down ramps
    bool forwardGround = false;
    RaycastHit forwardHit;
    Vector3 forwardDir = _movementVelocity.sqrMagnitude > 0.000001f ? _movementVelocity.normalized : transform.forward;
    if (_movementVelocity.sqrMagnitude > 0.000001f) {
      Vector3 rayStart = _playerRigidBody.position + forwardDir * _forwardGroundCheck + Vector3.up * 0.1f;
      if (Physics.Raycast(rayStart, Vector3.down, out forwardHit, _groundCheckDistance)) {
        forwardGround = true;
      }
    }

    bool effectiveGrounded = _isGrounded || forwardGround;

    if (effectiveGrounded) {
      // If no input, stop immediately when grounded
      if (_movementVelocity.sqrMagnitude <= 0.000001f) {
        Vector3 v = _playerRigidBody.linearVelocity;
        v.x = 0f; v.z = 0f;
        _playerRigidBody.linearVelocity = v;
        // Snap player upright (world up) when stopped on ground
        Vector3 forwardHoriz = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
        if (forwardHoriz.sqrMagnitude < 0.000001f) forwardHoriz = transform.forward;
        Quaternion upright = Quaternion.LookRotation(forwardHoriz.normalized, Vector3.up);
        _playerRigidBody.MoveRotation(upright);
        _targetRotation = upright;
      }
      else {
        // When grounded (or stepping down), set full movement velocity (allows moving down ramps)
        _playerRigidBody.linearVelocity = _movementVelocity;
      }
    }
    else {
      if (_movementVelocity.sqrMagnitude > 0.000001f) {
        // In air: apply horizontal acceleration so gravity is not canceled
        Vector3 currentHorizontal = new Vector3(_playerRigidBody.linearVelocity.x, 0f, _playerRigidBody.linearVelocity.z);
        Vector3 desiredHorizontal = new Vector3(_movementVelocity.x, 0f, _movementVelocity.z);
        Vector3 accel = (desiredHorizontal - currentHorizontal) * _airControlAcceleration;
        _playerRigidBody.AddForce(accel, ForceMode.Acceleration);
      }
    }

    if (Quaternion.Angle(_playerRigidBody.rotation, _targetRotation) > 0.01f) {
      Quaternion smooth = Quaternion.Slerp(_playerRigidBody.rotation, _targetRotation, _rotationSpeed * Time.fixedDeltaTime);
      _playerRigidBody.MoveRotation(smooth);
    }
    _movementVelocity = Vector3.zero;
  }

    void Update()
  {
    // Refresh grounding info each frame using a short downward ray to ensure immediate response
    if (_playerRigidBody != null) {
      Debug.Log("histting Raycast :  ");
      _groundCheckDistance = 0.8f;
      RaycastHit hit;
      if (Physics.Raycast(_playerRigidBody.position + Vector3.up * 0.1f, Vector3.down, out hit, _groundCheckDistance)) {
        Debug.Log("hit value is:  " + hit);
        _isGrounded = true;
        _localNormal = hit.normal;
      }
      else {
        Debug.Log("No hit happened");
        _isGrounded = false;
        _localNormal = Vector3.up;
      }
    }
    if (_playerMovement.IsPressed()) {
      Vector2 playerMoveVec = _playerMovement.ReadValue<Vector2>();
      // Player Rotation
      Vector3 move = new Vector3(playerMoveVec.x, 0f, playerMoveVec.y);
      Vector3 worldMove = (_useCameraRelativeMovement && _camera != null)
                          ? Vector3.ProjectOnPlane(_camera.transform.TransformDirection(move), Vector3.up)
                          : move;
      float inputMag = move.magnitude;

      Vector3 intended = worldMove;
      if (_isGrounded) {
        Vector3 projected = Vector3.ProjectOnPlane(worldMove, _localNormal);
        if (projected.sqrMagnitude > 0.000001f) intended = projected;
      }

      _movementVelocity = inputMag > 0.0001f ? intended.normalized * inputMag * _playerSpeed : Vector3.zero;
      if (inputMag > 0.0001f && intended.sqrMagnitude > 0.000001f) {
        // Use ground-aligned forward when grounded so initial movement aligns with slope
        Vector3 forwardForRotation = intended.normalized;
        Vector3 upForRotation = Vector3.up;
        if (_isGrounded) {
          // Align forward to the slope and use slope normal as up for the rotation
          forwardForRotation = Vector3.ProjectOnPlane(intended, _localNormal).normalized;
          upForRotation = _localNormal;
          if (forwardForRotation.sqrMagnitude < 0.000001f) forwardForRotation = Vector3.ProjectOnPlane(transform.forward, _localNormal).normalized;
        }
        Quaternion targetRotation = Quaternion.LookRotation(forwardForRotation, upForRotation);
        _targetRotation = targetRotation;
      }
    }
    if (_playerJump.IsPressed() && !_isJumping) {
      _playerRigidBody.AddForce(Vector3.up * _jumpForce, ForceMode.Impulse);
      _isJumping = true;
    }
  }

  private void OnCollisionEnter(Collision collision)
  {
    if (collision.gameObject.CompareTag("Floor")) {
      _isJumping = false;
      _isGrounded = true;
      Vector3 avg = Vector3.zero;
      foreach (ContactPoint c in collision.contacts) avg += c.normal;
      _localNormal = avg == Vector3.zero ? Vector3.up : (avg / collision.contacts.Length).normalized;
    }
  }
  
  private void OnCollisionStay(Collision collision)
  {
    if (collision.gameObject.CompareTag("Floor")) {
      Vector3 avg = Vector3.zero;
      foreach (ContactPoint c in collision.contacts) avg += c.normal;
      _localNormal = avg == Vector3.zero ? Vector3.up : (avg / collision.contacts.Length).normalized;
      _isGrounded = true;
    }
  }

  private void OnCollisionExit(Collision collision)
  {
    if (collision.gameObject.CompareTag("Floor")) {
      _isGrounded = false;
      _localNormal = Vector3.up;
    }
  }
}
