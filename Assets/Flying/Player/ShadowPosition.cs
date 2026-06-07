using UnityEngine;
using UnityEngine.Serialization;

namespace Crease.Flying.Player
{
    public class ShadowPosition : MonoBehaviour
    {
        [FormerlySerializedAs("player")]
        [SerializeField] private GameObject _player;
        [FormerlySerializedAs("shadowDistance")]
        [SerializeField] private float _shadowDistance = 1f;

        void Start()
        {
            transform.position = new Vector3(_player.transform.position.x, _player.transform.position.y - _shadowDistance, _player.transform.position.z);
        }

        void Update()
        {
            transform.position = new Vector3(_player.transform.position.x, _player.transform.position.y - _shadowDistance, _player.transform.position.z);
        }
    }
}
