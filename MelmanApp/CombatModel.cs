namespace MelmanApp
{
    public record GameRequest(
    int GameId,
    int Turn,
    PlayerTower PlayerTower,
    List<EnemyTower> EnemyTowers,
    List<Diplomacy> Diplomacy,
    List<PreviousAttack> PreviousAttacks
);

    public record PlayerTower(
        int PlayerId,
        int Hp,
        int Armor,
        int Resources,
        int Level
    );

    public record EnemyTower(
        int PlayerId,
        int Hp,
        int Armor,
        int Level
    );

    public record Diplomacy(
        int PlayerId,
        DiplomacyAction Action
    );

    public record DiplomacyAction(
        int AllyId,
        int AttackTargetId
    );

    public record PreviousAttack(
        int PlayerId,
        AttackActionRequest Action
    );

    public record AttackActionRequest(
        int TargetId,
        int TroopCount
    );
}
