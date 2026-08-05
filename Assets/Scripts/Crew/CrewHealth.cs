using System;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(CrewUnit))]
public sealed class CrewHealth : MonoBehaviour
{
    public event Action<CrewHealth> HealthChanged;
    public event Action<CrewHealth> Died;

    [Header("Health")]

    [Min(1)]
    [SerializeField] private int maxHealth = 100;

    [Header("Bunk Healing")]

    [Tooltip(
        "Сколько здоровья восстанавливается за секунду, " +
        "пока пират находится на клетке с койкой.")]
    [Min(0.1f)]
    [SerializeField] private float bunkHealingPerSecond = 10f;

    [SerializeField] private bool healInBunks = true;

    [Header("Health Bar")]

    [SerializeField] private bool createHealthBar = true;

    private CrewUnit crewUnit;
    private CrewHealthBarView healthBarView;

    private float healingAccumulator;
    private bool wasHealing;
    private bool isDead;

    public int MaxHealth => maxHealth;
    public int CurrentHealth { get; private set; }

    public bool IsDead => isDead;

    public bool IsDamaged =>
        CurrentHealth < MaxHealth;

    public float HealthRatio =>
        MaxHealth > 0
            ? Mathf.Clamp01(
                (float)CurrentHealth / MaxHealth
            )
            : 0f;

    public bool IsHealingInBunk =>
        !isDead &&
        IsDamaged &&
        IsStandingInBunk();

    private void Awake()
    {
        crewUnit = GetComponent<CrewUnit>();

        maxHealth = Mathf.Max(1, maxHealth);
        CurrentHealth = maxHealth;

        if (createHealthBar)
        {
            EnsureHealthBar();
        }
    }

    private void Update()
    {
        UpdateBunkHealing();
    }

    public void TakeDamage(int damage)
    {
        if (damage <= 0 || isDead)
        {
            return;
        }

        CurrentHealth = Mathf.Max(
            0,
            CurrentHealth - damage
        );

        healthBarView?.Refresh();
        HealthChanged?.Invoke(this);

        Debug.Log(
            $"{name} получил {damage} урона. " +
            $"Здоровье: {CurrentHealth}/{MaxHealth}.",
            this
        );

        if (CurrentHealth <= 0)
        {
            Die();
        }
    }

    public void Heal(int amount)
    {
        ApplyHealing(
            amount,
            writeLog: true
        );
    }

    public void Kill()
    {
        if (isDead)
        {
            return;
        }

        CurrentHealth = 0;
        healthBarView?.Refresh();
        HealthChanged?.Invoke(this);

        Die();
    }

    private void UpdateBunkHealing()
    {
        if (!healInBunks ||
            isDead ||
            !IsDamaged ||
            !IsStandingInBunk())
        {
            StopBunkHealing();
            return;
        }

        if (!wasHealing)
        {
            wasHealing = true;

            Debug.Log(
                $"{name} начал восстанавливать здоровье в койке. " +
                $"Здоровье: {CurrentHealth}/{MaxHealth}.",
                this
            );
        }

        healingAccumulator +=
            bunkHealingPerSecond * Time.deltaTime;

        int healingPoints =
            Mathf.FloorToInt(healingAccumulator);

        if (healingPoints <= 0)
        {
            return;
        }

        healingAccumulator -= healingPoints;

        ApplyHealing(
            healingPoints,
            writeLog: false
        );

        if (CurrentHealth < MaxHealth)
        {
            return;
        }

        Debug.Log(
            $"{name} полностью восстановил здоровье.",
            this
        );

        StopBunkHealing();
    }

    private bool IsStandingInBunk()
    {
        if (crewUnit == null ||
            crewUnit.IsMoving ||
            crewUnit.CurrentCell == null)
        {
            return false;
        }

        return crewUnit.CurrentCell.ModuleType ==
               ShipModuleType.Bunks;
    }

    private void ApplyHealing(
        int amount,
        bool writeLog)
    {
        if (amount <= 0 ||
            isDead ||
            CurrentHealth >= MaxHealth)
        {
            return;
        }

        CurrentHealth = Mathf.Min(
            MaxHealth,
            CurrentHealth + amount
        );

        healthBarView?.Refresh();
        HealthChanged?.Invoke(this);

        if (writeLog)
        {
            Debug.Log(
                $"{name} восстановил {amount} здоровья. " +
                $"Здоровье: {CurrentHealth}/{MaxHealth}.",
                this
            );
        }
    }

    private void StopBunkHealing()
    {
        healingAccumulator = 0f;
        wasHealing = false;
    }

    private void Die()
    {
        if (isDead)
        {
            return;
        }

        isDead = true;
        StopBunkHealing();
        Died?.Invoke(this);

        Debug.Log(
            $"{name} погиб.",
            this
        );

        Collider2D[] colliders =
            GetComponentsInChildren<Collider2D>();

        foreach (Collider2D unitCollider in colliders)
        {
            if (unitCollider != null)
            {
                unitCollider.enabled = false;
            }
        }

        Destroy(gameObject);
    }

    private void EnsureHealthBar()
    {
        healthBarView =
            GetComponent<CrewHealthBarView>();

        if (healthBarView == null)
        {
            healthBarView =
                gameObject.AddComponent<CrewHealthBarView>();
        }

        healthBarView.Initialize(this);
    }

    [ContextMenu("Test/Take 20 Damage")]
    private void TestTakeDamage()
    {
        TakeDamage(20);
    }

    [ContextMenu("Test/Heal 20")]
    private void TestHeal()
    {
        Heal(20);
    }

    [ContextMenu("Test/Kill")]
    private void TestKill()
    {
        Kill();
    }
}
