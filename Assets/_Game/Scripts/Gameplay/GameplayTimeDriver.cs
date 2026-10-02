using System;
using LittleCastle.World;
using UnityEngine;

namespace LittleCastle.Gameplay
{
    /// <summary>
    /// Thin Unity adapter that forwards authoritative WorldTimeSystem progress
    /// into the pure gameplay simulation.
    ///
    /// The server/host must call Initialize with the session authority. Clients
    /// should not create their own competing authoritative simulation.
    /// </summary>
    public sealed class GameplayTimeDriver : MonoBehaviour
    {
        [SerializeField]
        private WorldTimeSystem timeSystem;

        private GameplaySimulationAuthority authority;

        public GameplaySimulationAuthority Authority =>
            authority;

        public void Initialize(
            GameplaySimulationAuthority authority)
        {
            this.authority =
                authority ??
                throw new ArgumentNullException(nameof(authority));
        }

        private void OnEnable()
        {
            if (timeSystem == null)
            {
                timeSystem =
                    FindFirstObjectByType<WorldTimeSystem>();
            }

            if (timeSystem != null)
            {
                timeSystem.TimeAdvanced +=
                    HandleTimeAdvanced;
            }
        }

        private void OnDisable()
        {
            if (timeSystem != null)
            {
                timeSystem.TimeAdvanced -=
                    HandleTimeAdvanced;
            }
        }

        private void HandleTimeAdvanced(
            WorldTimeState state,
            double gameHoursAdvanced)
        {
            if (authority == null ||
                timeSystem == null)
            {
                return;
            }

            authority.Advance(
                state,
                gameHoursAdvanced,
                timeSystem.IsNight);
        }
    }
}
