// File: Assets/Scripts/Abilities/IAbility.cs
namespace Game.Abilities
{
    public interface IAbility
    {
        void OnEquip();
        void OnUnequip();
        void Tick(float dt);
    }
}
