using SpellFlinger.Enum;
using SpellSlinger.Networking;
using UnityEngine;

namespace SpellFlinger.PlayScene
{
    public class CustomProjectile : Projectile
    {
        [SerializeField] private float _range = 0f;
        private Rigidbody _rigidbody;
        private Vector3 _direction;
        private float _throwForce = 5f;
        private new int _damage = 15;
        public override void Throw(Vector3 direction, PlayerStats ownerPlayerStats)
        {
            Direction = direction.normalized * _movementSpeed;
            _direction = direction.normalized;
            OwnerPlayerStats = ownerPlayerStats;
            transform.rotation = Quaternion.FromToRotation(transform.forward, Direction.normalized);
            _rigidbody = GetComponent<Rigidbody>();
        }
        
        public override void FixedUpdateNetwork()
        {
            //Debug.LogFormat("custom projectile direction: {0}, {1}, {2}", _direction.x, _direction.y, _direction.z);
            _rigidbody.AddForce(_direction * _throwForce, ForceMode.Impulse);
        }
        
        private void OnTriggerEnter(Collider other)
        {
            Debug.LogFormat("other.tag: {0}", other.tag);
            if (other.CompareTag("Player"))
            {
                Debug.LogFormat("udarili smo playera {0}", other.GetComponent<PlayerStats>().PlayerName);
                PlayerStats player = other.GetComponent<PlayerStats>();
                if (player.Object.InputAuthority == OwnerPlayerStats.Object.InputAuthority) 
                    return;
                // player can't hit teammates
                if (FusionConnection.GameModeType == GameModeType.TDM && player.Team == OwnerPlayerStats.Team)
                    return;
                player.DealDamage(_damage, OwnerPlayerStats);
            }
            
            Destroy(gameObject);
        }
    }
}