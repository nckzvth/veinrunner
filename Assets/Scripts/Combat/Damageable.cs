// File: Assets/Scripts/Combat/Damageable.cs
using UnityEngine;

namespace Game.Combat
{
    public sealed class Damageable : MonoBehaviour
    {
        [SerializeField] private float maxHP = 100f;
        private float _hp;

        private void Awake() => _hp = maxHP;
        // TODO: TakeDamage, DR, events, knockback hooks.
    }
}
