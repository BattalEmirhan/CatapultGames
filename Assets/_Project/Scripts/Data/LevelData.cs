using System;

namespace CatapultGames
{
    [Serializable]
    public class LevelData
    {
        public LevelMetadata metadata = new LevelMetadata();
        public GridConfig    grid     = new GridConfig();
        public CameraConfig  camera   = new CameraConfig();
        public CellData[]    cells    = new CellData[0];
        public BallData[]    balls    = new BallData[0];
    }
}
