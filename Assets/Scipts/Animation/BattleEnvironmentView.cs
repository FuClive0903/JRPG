using UnityEngine;

namespace Game.Battle
{
    public class BattleEnvironmentView : MonoBehaviour
    {
        [SerializeField] private GameObject defaultEnvironmentPrefab;

        private void Awake()
        {
            GameObject prefab = BattleSession.EnvironmentPrefab != null
                ? BattleSession.EnvironmentPrefab
                : defaultEnvironmentPrefab;
            if (prefab != null)
                Instantiate(prefab, transform, false);
        }
    }
}
