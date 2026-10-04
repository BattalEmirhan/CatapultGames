using UnityEngine;

namespace CatapultGames
{
    // BootScene: app-wide settings that must hold before gameplay starts.
    public sealed class Bootstrap : MonoBehaviour
    {
        [SerializeField] private int targetFrameRate = 60;

        private void Awake()
        {
            QualitySettings.vSyncCount  = 0;
            Application.targetFrameRate = targetFrameRate;
        }
    }
}
