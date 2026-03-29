using UnityEngine;

public class CameraMovement : MonoBehaviour
{
  public PlayerMovement _player;
  private Vector3 _cameraOffset;

  private void Start()
  {
    _cameraOffset = _player.transform.position - transform.position;
  }

  // Update is called once per frame
  void LateUpdate()
  {
    //Vector3 velocity = _player._movementVelocity;
    //Transform _playerTransform = _player.transform;
    Vector3 newpos = _player.transform.position - _cameraOffset;
    transform.position = newpos;
  }
}
