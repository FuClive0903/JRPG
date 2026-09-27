using UnityEngine;

namespace Game.Battle
{
    public enum BattleState
    {
        Start,
        WaitingForCommand,
        SelectingTarget,
        ResolvingAction,
        EnemyTurn,
        Victory,
        Defeat,
        Paused,
    }
}
