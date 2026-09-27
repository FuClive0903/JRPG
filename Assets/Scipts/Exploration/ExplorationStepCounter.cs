using System;
using UnityEngine;

namespace Game.Exploration
{
    public class ExplorationStepCounter : MonoBehaviour
    {
        private const float MinimumStepDistance = 0.01f;

        [SerializeField] private ExplorationPlayerController playerController;
        [SerializeField, Min(MinimumStepDistance)] private float distancePerStep = 1f;
        [SerializeField, Min(0)] private int completedSteps;
        [SerializeField, Min(0f)] private float distanceRemainder;

        public int CompletedSteps { get { return completedSteps; } }
        public event Action<int> StepCompleted;

        private void OnEnable()
        {
            if (playerController == null)
            {
                Debug.LogError("Exploration step counter requires a player controller.", this);
                enabled = false;
                return;
            }

            playerController.HorizontalDistanceMoved += AddDistance;
        }

        private void OnDisable()
        {
            if (playerController != null)
                playerController.HorizontalDistanceMoved -= AddDistance;
        }

        public void ResetSteps()
        {
            completedSteps = 0;
            distanceRemainder = 0f;
        }

        public void SetDistancePerStep(float distance)
        {
            if (distance < MinimumStepDistance)
                throw new ArgumentOutOfRangeException(nameof(distance));

            distancePerStep = distance;
        }

        private void AddDistance(float distance)
        {
            if (distance <= 0f)
                return;

            float requiredDistance = Mathf.Max(MinimumStepDistance, distancePerStep);
            distanceRemainder += distance;
            while (distanceRemainder >= requiredDistance)
            {
                distanceRemainder -= requiredDistance;
                completedSteps++;
                StepCompleted?.Invoke(completedSteps);
            }
        }

        private void OnValidate()
        {
            distancePerStep = Mathf.Max(MinimumStepDistance, distancePerStep);
            completedSteps = Mathf.Max(0, completedSteps);
            distanceRemainder = Mathf.Max(0f, distanceRemainder);
        }
    }
}
