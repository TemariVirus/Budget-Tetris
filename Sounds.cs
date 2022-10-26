namespace Tetris;

using System.Windows.Media;
using static Tetris.Game;

static class Sounds
{
    public const string SoundsFolder = @"Sounds\";

    public const string SoftDrop = SoundsFolder + "bfall.wav",
                        HardDrop = SoundsFolder + "harddrop.wav",
                        TSpin = SoundsFolder + "tspin.wav",
                        PC = SoundsFolder + "pc.wav",
                        Hold = SoundsFolder + "hold.wav",
                        Slide = SoundsFolder + "move.wav",
                        Rotate = SoundsFolder + "rotate.wav",
                        LvlUp = SoundsFolder + "lvlup.wav",
                        Pause = SoundsFolder + "pause.wav";

    public static readonly string[] ClearSounds = { "", SoundsFolder + "single.wav", SoundsFolder + "double.wav", SoundsFolder + "triple.wav", SoundsFolder + "tetris.wav" };


    static Thread SoundThread = new Thread(MediaPlayerThread);
    static MediaPlayer Player;
    static string FileToPlay = "";
    static bool IsPlaying = false;

    private static void MediaPlayerThread()
    {
        Thread.CurrentThread.Priority = ThreadPriority.Lowest;
        Player = new MediaPlayer();
        Player.Volume = 0.1;
        while (true)
        {
            Thread.Sleep(1);
            if (GameManager.IsMuted || IsDead) continue;
            if (FileToPlay.Length == 0) continue;

            IsPlaying = true;

            Player.Open(new Uri(FileToPlay, UriKind.Relative));
            Player.Play();
            FileToPlay = "";

            IsPlaying = false;
        }
    }

    public static void Playsfx(string filename)
    {
        if (IsPlaying || GameManager.IsMuted || IsDead) return;

        FileToPlay = filename;
    }
}
