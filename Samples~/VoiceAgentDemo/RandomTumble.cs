// © 2025–2026, Stefan Webb. Some Rights Reserved.
// Licensed under CC BY-SA 4.0
//
// Continuously tumbles this GameObject toward new random orientations.
// Standalone — no dependency on any other part of this project.

using UnityEngine;

namespace GenerativeGamedev {

public class RandomTumble : MonoBehaviour
{
    [SerializeField] private float _tumbleDuration = 2f;

    private Quaternion _startRotation;
    private Quaternion _targetRotation;
    private float _tumbleStartTime;

    private void Start()
    {
        _startRotation = transform.rotation;
        PickNewTarget();
    }

    private void Update()
    {
        var t = (Time.time - _tumbleStartTime) / _tumbleDuration;
        if (t >= 1f)
        {
            transform.rotation = _targetRotation;
            PickNewTarget();
            return;
        }

        transform.rotation = Quaternion.Slerp(_startRotation, _targetRotation, t);
    }

    private void PickNewTarget()
    {
        _startRotation = transform.rotation;
        _targetRotation = Random.rotation;
        _tumbleStartTime = Time.time;
    }
}

}
