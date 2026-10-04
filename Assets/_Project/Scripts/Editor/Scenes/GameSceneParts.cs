using UnityEngine;

namespace CatapultGames.Editor
{
    // What the GameScene build steps hand to each other while the scene is built.
    internal sealed class GameSceneParts
    {
        public MaterialSet          Materials;
        public Camera               Camera;
        public GridCameraController CameraController;
        public GridRenderer         Grid;
        public GridBoard            Board;
        public BallQueue            Queue;
        public BallQueueView        QueueView;
        public Transform            LaunchArea;
        public Transform            LaunchOrigin;
        public AimPreview           Aim;
        public BallLauncher         Launcher;
        public GameManager          GameManager;
        public Transform            Canvas;
        public BoosterSystem        Boosters;
        public ProgressHUD          Progress;
        public TutorialHint         Tutorial;
    }
}
