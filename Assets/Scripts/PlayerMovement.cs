using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

public class PlayerMovement : MonoBehaviour
{
  // Base settings (kept for compatibility)
  public float _playerSpeed = 3f;
  // Jump impulse applied when player presses jump
  public float _jumpForce = 8f;
  // Rotation smoothing speed for Slerp in FixedUpdate
  public float _rotationSpeed = 5f;
  // If true, movement input is interpreted relative to the camera orientation
  public bool _useCameraRelativeMovement = false;
  // Running/walking configuration
  // _walkSpeed and _runSpeed define the base speeds for walking and running
  public float _walkSpeed = 2f;      // base walking speed (units/sec)
  public float _runSpeed = 6f;       // top running speed (units/sec)
  // Acceleration rate when increasing speed (units/sec^2)
  public float _acceleration = 8f;   // how fast we accelerate to target speed
  // Deceleration rate when input is released (large value for near-instant stop)
  public float _deceleration = 80f;  // how fast we decelerate when input released (very quick)
  // Key used to toggle running (inspector overrideable)
  public KeyCode _runKey = KeyCode.LeftShift; // key to hold for running
  // Internal current horizontal speed (units/sec)
  private float _currentSpeed = 0f;   // current horizontal speed (units/sec)
  // legacy/unused field kept for compatibility with earlier codepaths
  private float _playerMovementDirect;
  private bool _isJumping;
  private PlayerInput _playerInput;
  private Rigidbody _playerRigidBody;
  private Quaternion _targetRotation;
  // Desired horizontal movement velocity (world-space, units/sec). Set in Update, consumed in FixedUpdate.
  public Vector3 _movementVelocity;
  private InputAction _playerMovement;
  private InputAction _playerJump;
  public GameObject _camera;
  // Contact normal of the surface currently beneath the player (used to project movement on slopes)
  private Vector3 _localNormal;
  // Flag indicating whether player is currently standing on a surface
  private bool _isGrounded;
  // Raycast distances / air-control tuning
  public float _groundCheckDistance = 0.6f;   // vertical distance to consider player grounded
  public float _forwardGroundCheck = 0.6f;  // forward offset to test stepping down
  public float _airControlAcceleration = 10f; // responsiveness of horizontal control while airborne

  void Start()
  {
    // Initialize input and Rigidbody references
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
      // Improve visual smoothness by enabling Rigidbody interpolation
      _playerRigidBody.interpolation = RigidbodyInterpolation.Interpolate;
    }
  }

  // FixedUpdate is called on the physics timestep. Apply smoothed rotation here for Rigidbody.
  void FixedUpdate()
  {
    Debug.Log("FixedUpdate::Current velocity:  " + _currentSpeed);
    Debug.Log("FixedUpdate::movement velocity:  " + _movementVelocity);
    Debug.Log("FixedUpdate::linearVelocity:  " + _playerRigidBody.linearVelocity);
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

    // --- Grounded movement handling ---
    if (effectiveGrounded) {
      // If no input, stop immediately when grounded
      if (_movementVelocity.sqrMagnitude <= 0.000001f) {
        // stop horizontal motion instantly
        Vector3 v = _playerRigidBody.linearVelocity;
        v.x = 0f;
        v.z = 0f;
        _playerRigidBody.linearVelocity = v;
        Debug.Log("velocity is:  " + _playerRigidBody.linearVelocity);
        // Snap player upright (world up) when stopped on ground
        Vector3 forwardHoriz = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
        if (forwardHoriz.sqrMagnitude < 0.000001f)
          forwardHoriz = transform.forward;
        Quaternion upright = Quaternion.LookRotation(forwardHoriz.normalized, Vector3.up);
        _playerRigidBody.MoveRotation(upright);
        _targetRotation = upright;
      }
      else {
        // When grounded (or stepping down), set full movement velocity (allows moving down ramps)
        // assign horizontal velocity directly (movementVelocity is in units/sec)
        _playerRigidBody.linearVelocity = _movementVelocity;
      }
    }
    else {
      if (_movementVelocity.sqrMagnitude > 0.000001f) {
        Debug.Log("movement sqrt value is: " + _movementVelocity.sqrMagnitude);
        // In air: apply horizontal acceleration so gravity is not canceled
        Vector3 currentHorizontal = new Vector3(_playerRigidBody.linearVelocity.x, 0f, _playerRigidBody.linearVelocity.z);
        Vector3 desiredHorizontal = new Vector3(_movementVelocity.x, 0f, _movementVelocity.z);
        Vector3 accel = (desiredHorizontal - currentHorizontal) * _airControlAcceleration;
        _playerRigidBody.AddForce(accel, ForceMode.Acceleration);
      }
    }

    if (_movementVelocity.sqrMagnitude > 0.000001f &&
        Quaternion.Angle(_playerRigidBody.rotation, _targetRotation) > 0.01f) {
      Quaternion smooth = Quaternion.Slerp(_playerRigidBody.rotation, _targetRotation, _rotationSpeed * Time.fixedDeltaTime);
      _playerRigidBody.MoveRotation(smooth);
      Debug.Log("Smooth value is: " + smooth);
    }
    _movementVelocity = Vector3.zero;
  }

  void Update()
  {
    // Refresh grounding info each frame using a short downward ray to ensure immediate response
    if (_playerRigidBody != null) {
      _groundCheckDistance = 0.8f; // slight dynamic override for responsiveness
      RaycastHit hit;
      if (Physics.Raycast(_playerRigidBody.position + Vector3.up * 0.1f, Vector3.down, out hit, _groundCheckDistance)) {
        _isGrounded = true;
        _localNormal = hit.normal; // store surface normal for slope projection
      }
      else {
        _isGrounded = false;
        _localNormal = Vector3.up;
      }
    }

    // Calculating rotation and velocity outside the IsPressed guard
    // It's so that we can update the currentSpeed when IsPresses is false
    // else it'll always have a velocity attached to it
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
      if (projected.sqrMagnitude > 0.000001f)
        intended = projected;
    }

    // Determine target speed based on running or walking
    //Enable only run speed rught now,
    float targetBaseSpeed = _runSpeed;// Input.GetKey(_runKey) ? _runSpeed : _walkSpeed;

    float targetSpeed = targetBaseSpeed * inputMag;
    // Smoothly accelerate/decelerate current speed toward target
    if (targetSpeed > _currentSpeed) {
      _currentSpeed = Mathf.MoveTowards(_currentSpeed, targetSpeed, _acceleration * Time.deltaTime);
    }
    else {
      // decelerate quickly (large deceleration yields near-instant stop)
      _currentSpeed = Mathf.MoveTowards(_currentSpeed, targetSpeed, _deceleration * Time.deltaTime);
      // Clamp to 0 if close enough to avoid oscillation near the threshold
      if (_currentSpeed < 0.01f) {
        _currentSpeed = 0f;
      }
    }
    // Compute movement velocity in units/sec (will be applied in FixedUpdate)
    // Only compute if currentSpeed is meaningful to avoid issues with normalizing near-zero vectors
    if (_currentSpeed > 0.0001f && intended.sqrMagnitude > 0.000001f) {
      _movementVelocity = intended.normalized * _currentSpeed;
    }
    else {
      _movementVelocity = Vector3.zero;
    }

    /*
     * Calculation of the movement velocity ends here
     */

    if (_playerMovement.IsPressed()) {
      if (inputMag > 0.0001f && intended.sqrMagnitude > 0.000001f) {
        // Use ground-aligned forward when grounded so initial movement aligns with slope
        Vector3 forwardForRotation = intended.normalized;
        Vector3 upForRotation = Vector3.up;
        if (_isGrounded) {
          // Align forward to the slope and use slope normal as up for the rotation
          forwardForRotation = Vector3.ProjectOnPlane(intended, _localNormal).normalized;
          upForRotation = _localNormal;
          if (forwardForRotation.sqrMagnitude < 0.000001f)
            forwardForRotation = Vector3.ProjectOnPlane(transform.forward, _localNormal).normalized;
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
      foreach (ContactPoint c in collision.contacts)
        avg += c.normal;
      _localNormal = avg == Vector3.zero ? Vector3.up : (avg / collision.contacts.Length).normalized;
    }
  }

  /*private void OnCollisionStay(Collision collision)
  {
    if (collision.gameObject.CompareTag("Floor")) {
      Vector3 avg = Vector3.zero;
      foreach (ContactPoint c in collision.contacts)
        avg += c.normal;
      _localNormal = avg == Vector3.zero ? Vector3.up : (avg / collision.contacts.Length).normalized;
      _isGrounded = true;
    }
  }*/

  /*private void OnCollisionExit(Collision collision)
  {
    if (collision.gameObject.CompareTag("Floor")) {
      _isGrounded = false;
      _localNormal = Vector3.up;
    }
  }*/
}
