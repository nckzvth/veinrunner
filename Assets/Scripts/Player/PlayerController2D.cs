// File: Assets/Scripts/Player/PlayerController2D.cs
using UnityEngine;

namespace Game.Player
{
    [RequireComponent(typeof(Rigidbody2D))]
    public sealed class PlayerController2D : MonoBehaviour
    {
        [SerializeField] private float moveSpeed = 6f;
        [SerializeField] private float jumpForce = 8f;

        private Rigidbody2D _rb;

        private void Awake() => _rb = GetComponent<Rigidbody2D>();
        private void Update()
        {
            // TODO: basic run/jump/ladder; overweight slow.
        }
    }
}
