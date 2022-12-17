using System.IO;

namespace Tetris
{
    class Sound
    {
        public const string SoundsFolder = @"Sounds\";

        public static bool Initialised { get; private set; } = false;
        public static Thread SoundThread { get; private set; }

        public static bool IsMuted { get; set; }
        public static double SFXVolume { get; set; }
        public static double BGMVolume { get; set; }

        public static readonly Sound BGM = new Sound("Korobeiniki Remix.wav"),
                                     SoftDrop = new Sound("bfall.wav"),
                                     HardDrop = new Sound("harddrop.wav"),
                                     TSpin = new Sound("tspin.wav"),
                                     PC = new Sound("pc.wav"),
                                     GarbageSmall = new Sound("garbagesmall.wav"),
                                     GarbageLarge = new Sound("garbagelarge.wav"),
                                     Hold = new Sound("hold.wav"),
                                     Slide = new Sound("move.wav"),
                                     Rotate = new Sound("rotate.wav"),
                                     Pause = new Sound("pause.wav");

        public static readonly Sound[] ClearSounds =
        {
            new Sound("single.wav"),
            new Sound("double.wav"),
            new Sound("triple.wav"),
            new Sound("tetris.wav")
        };


        private readonly int Handle = 0;
        public bool Loaded { get => Handle != 0; }

        public Sound(string file_name)
        {
            if (!Initialised) return;
        }

        public static void InitSound()
        {
            return;
        }

        public void Play()
        {
            return;

            //if (!Initialised || IsMuted || !Loaded) return;
        }
    }
}
