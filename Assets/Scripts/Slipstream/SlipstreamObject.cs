using UnityEngine;
using UnityEngine.Splines;
using System.Collections.Generic;

public class SlipstreamObject : MonoBehaviour
{
    [Header("Path Settings")]
    [SerializeField] private SplineContainer splineContainer;
    [SerializeField] private float travelSpeed = 20f;
    [SerializeField] private bool autoStart = false;

    [Header("Slipstream Zone")]
    [SerializeField] private float followDistance = 10f;
    [SerializeField] private float zoneLength = 15f;
    [SerializeField] private float zoneRadius = 5f; 
    [SerializeField] private float accelerationForce = 50f; 
    
    [Header("Slipstream Tuning")]
    [SerializeField] private float brakeForce = 100f;
    [SerializeField] private float speedBuffer = 0.5f;

    [Header("Visuals")]
    // [SerializeField] private ParticleSystem slipstreamParticles;
    [SerializeField] private Color gizmoColor = new Color(0, 1, 1, 0.3f);

    private float _currentSplinePos = 0f;
    private bool _isMoving = false;
    private KinematicBody _playerBody;
    private float _splineLength;
    
    private Vector3 _lastPosition;
    private float _currentObjectSpeed;

    private void Start()
    {
        if (splineContainer != null)
            _splineLength = splineContainer.CalculateLength();
        
        _isMoving = autoStart;

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
            _playerBody = player.GetComponent<KinematicBody>();
        
        _lastPosition = transform.position;

        // if (slipstreamParticles != null)
        //     slipstreamParticles.Stop();
    }

    private void FixedUpdate()
    {
        _currentObjectSpeed = (transform.position - _lastPosition).magnitude / Time.fixedDeltaTime;
        _lastPosition = transform.position;
        
        if (_playerBody == null) return;

        UpdateMovement();
        ApplySlipstreamLogic();
    }

    private void UpdateMovement()
    {
        if (!_isMoving)
        {
            float distToPlayer = Vector3.Distance(transform.position, _playerBody.transform.position);
            if (distToPlayer < zoneLength)
            {
                _isMoving = true;
                // if (slipstreamParticles != null) slipstreamParticles.Play();
            }
            return;
        }
        
        _currentSplinePos += (travelSpeed * Time.fixedDeltaTime) / _splineLength;
        if (_currentSplinePos > 1f) _currentSplinePos = 1f;

        transform.position = (Vector3)splineContainer.EvaluatePosition(_currentSplinePos);
        Vector3 forward = (Vector3)splineContainer.EvaluateTangent(_currentSplinePos);
        if (forward != Vector3.zero)
            transform.rotation = Quaternion.LookRotation(forward);
    }

    private void ApplySlipstreamLogic()
    {
        Vector3 relativePos = transform.InverseTransformPoint(_playerBody.transform.position);
        bool isInRangeZ = relativePos.z < 0 && relativePos.z > -zoneLength;
        bool isInRangeRadius = new Vector2(relativePos.x, relativePos.y).magnitude < zoneRadius;

        if (isInRangeZ && isInRangeRadius)
        {
            float currentDist = Mathf.Abs(relativePos.z);

            float targetSpeed = _currentObjectSpeed;
            
            if (currentDist < followDistance)
            {
                targetSpeed = Mathf.Lerp(0f, _currentObjectSpeed, currentDist / followDistance);
            }
            
            float speedError = targetSpeed - _playerBody.Speed;

            if (speedError > speedBuffer)
            {
                float boostFactor = Mathf.Clamp01((currentDist - followDistance) / 5f + 1f);
                _playerBody.AddAcceleration(transform.forward * accelerationForce * boostFactor);
            }
            else if (speedError < -speedBuffer)
            {
                Vector3 brakeDir = -_playerBody.Velocity.normalized;
            
                if (_playerBody.Speed > 0.1f)
                {
                    _playerBody.AddAcceleration(brakeDir * brakeForce);
                }
                else
                {
                    _playerBody.SetVelocity(Vector3.zero);
                }
            }
        }
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = gizmoColor;
        Gizmos.matrix = transform.localToWorldMatrix;
        
        Vector3 center = new Vector3(0, 0, -zoneLength / 2f);
        Vector3 size = new Vector3(zoneRadius * 2, zoneRadius * 2, zoneLength);
        Gizmos.DrawWireCube(center, size);

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(new Vector3(0, 0, -followDistance), 0.5f);
    }
}