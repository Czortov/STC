using System;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class ShipHullHealth : MonoBehaviour
{
    [Header("Hull Health")]

    [Min(1)]
    [SerializeField] private int maxHealth = 300;

    public int MaxHealth => maxHealth;

    public int CurrentHealth { get; private set; }

    public bool IsDestroyed =>
        CurrentHealth <= 0;

    public float HealthRatio =>
        MaxHealth > 0
            ? Mathf.Clamp01(
                (float)CurrentHealth / MaxHealth
            )
            : 0f;

    public event Action<
        ShipHullHealth,
        int,
        int> HealthChanged;

    public event Action<ShipHullHealth> Destroyed;

    private void Awake()
    {
        maxHealth = Mathf.Max(1, maxHealth);
        CurrentHealth = maxHealth;
    }

    public void TakeDamage(int damage)
    {
        if (damage <= 0 || IsDestroyed)
        {
            return;
        }

        int previousHealth =
            CurrentHealth;

        CurrentHealth = Mathf.Max(
            0,
            CurrentHealth - damage
        );

        HealthChanged?.Invoke(
            this,
            CurrentHealth,
            MaxHealth
        );

        Debug.Log(
            $"{name} получил {damage} урона корпусу. " +
            $"Общее здоровье: {CurrentHealth}/{MaxHealth}.",
            this
        );

        if (previousHealth > 0 &&
            CurrentHealth <= 0)
        {
            Debug.Log(
                $"{name}: корабль уничтожен.",
                this
            );

            Destroyed?.Invoke(this);
        }
    }

    public void Repair(int amount)
    {
        if (amount <= 0 ||
            IsDestroyed ||
            CurrentHealth >= MaxHealth)
        {
            return;
        }

        CurrentHealth = Mathf.Min(
            MaxHealth,
            CurrentHealth + amount
        );

        HealthChanged?.Invoke(
            this,
            CurrentHealth,
            MaxHealth
        );

        Debug.Log(
            $"{name} восстановил {amount} здоровья корпуса. " +
            $"Общее здоровье: {CurrentHealth}/{MaxHealth}.",
            this
        );
    }

    public void RestoreFullHealth()
    {
        CurrentHealth = MaxHealth;

        HealthChanged?.Invoke(
            this,
            CurrentHealth,
            MaxHealth
        );
    }

    [ContextMenu("Test/Take 10 Hull Damage")]
    private void TestTakeDamage()
    {
        TakeDamage(10);
    }

    [ContextMenu("Test/Repair 10 Hull")]
    private void TestRepair()
    {
        Repair(10);
    }

    [ContextMenu("Test/Destroy Ship")]
    private void TestDestroyShip()
    {
        TakeDamage(CurrentHealth);
    }

    private void OnValidate()
    {
        maxHealth = Mathf.Max(1, maxHealth);
    }
}