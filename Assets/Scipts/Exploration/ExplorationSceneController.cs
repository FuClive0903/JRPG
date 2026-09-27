using Game.Battle;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Exploration
{
    public class ExplorationSceneController : MonoBehaviour
    {
        [SerializeField] private Transform player;
        [SerializeField] private Transform defaultSpawn;

        private void Awake()
        {
            if (player == null || defaultSpawn == null)
            {
                Debug.LogError("Exploration scene requires Player and PlayerSpawn references.", this);
                enabled = false;
                return;
            }

            Vector3 destination = defaultSpawn.position;
            try
            {
                if (!GameRuntimeState.IsInitialized)
                {
                    BattleData data = BattleData.Load();
                    if (GameSaveSystem.HasAutoSave)
                        GameRuntimeState.Initialize(GameSaveSystem.LoadAutoSave(data));
                    else
                        GameSaveSystem.SaveNewGame(data.CreateParty(data.defaultPlayerParty, true), data.defaultInventory);
                }
                ExplorationProgress exploration = GameRuntimeState.Snapshot().exploration;
                if (exploration.hasSavedPosition &&
                    exploration.sceneName == SceneManager.GetActiveScene().name)
                {
                    destination = exploration.position;
                    // Village saves made on the old XZ map keep the former ground height in Y.
                    if (Mathf.Abs(destination.z) > 0.01f)
                        destination = new Vector3(destination.x, destination.z, 0f);
                }
            }
            catch (System.Exception exception)
            {
                Debug.LogError("Cannot load exploration progress: " + exception.Message, this);
                enabled = false;
                return;
            }

            MovePlayer(destination);
        }

        public bool SaveCurrentPosition()
        {
            try
            {
                GameSaveSystem.SaveAutoExplorationPosition(BattleData.Load(),
                    SceneManager.GetActiveScene().name, defaultSpawn.name, player.position);
                return true;
            }
            catch (System.Exception exception)
            {
                Debug.LogError("Cannot save exploration progress: " + exception.Message, this);
                return false;
            }
        }

        public bool SaveToManualSlot(int slotNumber)
        {
            try
            {
                GameSaveSystem.SaveManualSlot(BattleData.Load(), slotNumber,
                    SceneManager.GetActiveScene().name, defaultSpawn.name, player.position);
                return true;
            }
            catch (System.Exception exception)
            {
                Debug.LogError("Cannot save manual slot " + slotNumber + ": " +
                    exception.Message, this);
                return false;
            }
        }

        private void OnApplicationQuit()
        {
            if (enabled && player != null && defaultSpawn != null && GameRuntimeState.IsInitialized)
                SaveCurrentPosition();
        }

        private void MovePlayer(Vector3 destination)
        {
            player.position = destination;
            Rigidbody2D body = player.GetComponent<Rigidbody2D>();
            if (body != null)
            {
                body.position = destination;
                body.linearVelocity = Vector2.zero;
            }
            ExplorationPlayerController controller = player.GetComponent<ExplorationPlayerController>();
            if (controller != null)
                controller.ResetTrackedPosition();
        }
    }
}
