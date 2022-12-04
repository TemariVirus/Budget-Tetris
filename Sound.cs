namespace Tetris;

using System.IO;
using System.Windows.Media;
using System.Windows.Threading;

class Sound
{
    public const string SoundsFolder = @"Sounds\";
    public const int MaxSounds = 16;

    public static Thread SoundThread { get; private set; }
    public static Dispatcher SoundDP { get => Dispatcher.FromThread(SoundThread); }

    private static bool _IsMuted = false;
    public static bool IsMuted
    {
        get => _IsMuted;
        set
        {
            _IsMuted = value;
            SoundDP?.Invoke(() => BGMPlayer.IsMuted = value);
        }
    }

    public static double SFXVolume;
    private static readonly HashSet<MediaPlayer> MediaPlayers = new HashSet<MediaPlayer>();
    private static readonly Queue<MediaPlayer> MediaPlayerPool = new Queue<MediaPlayer>();

    private static double _BGMVolume;
    public static double BGMVolume
    {
        get => _BGMVolume;
        set
        {
            _BGMVolume = value;
            if (BGMPlayer != null)
                SoundDP.Invoke(() => BGMPlayer.Volume = value);
        }
    }
    private static MediaPlayer BGMPlayer;

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


    public Uri Path { get; }
    public bool HasSource { get; }

    public Sound(string file_name)
    {
        Path = new Uri(SoundsFolder + file_name, UriKind.Relative);
        HasSource = File.Exists(Path.ToString());
    }

    public static void InitSound()
    {
        if (SoundThread != null) return;

        // Create Sound thread
        SoundThread = new Thread(() =>
        {
            BGMPlayer = new MediaPlayer
            {
                Volume = BGMVolume,
            };
            // Loop delegate
            BGMPlayer.MediaEnded += (s, e) =>
            {
                BGMPlayer.Position = TimeSpan.Zero;
                BGMPlayer.Play();
            };
            // Play BGM looping
            BGMPlayer.Open(BGM.Path);
            BGMPlayer.Play();
            // Run the dispatcher
            Dispatcher.Run();
        });
        SoundThread.Start();
        SoundThread.Priority = ThreadPriority.Lowest;
    }

    public void Play()
    {
        if (!HasSource || IsMuted) return;

        SoundDP.Invoke(() =>
        {
            MediaPlayer player = GetSFXPlayer();
            if (player == null) return;

            player.Volume = SFXVolume;
            player.Open(Path);
            player.Play();
        });
    }

    private static MediaPlayer GetSFXPlayer()
    {
        // Create a new player if pool is dry
        if (MediaPlayerPool.Count == 0)
        {
            if (MediaPlayers.Count >= MaxSounds)
                return null;

            MediaPlayer player = new MediaPlayer()
            {
                Volume = SFXVolume,
            };
            MediaPlayers.Add(player);
            player.MediaEnded += (s, e) =>
            {
                player.Close();
                MediaPlayerPool.Enqueue(player);
            };

            return player;
        }

        return MediaPlayerPool.Dequeue();
    }
}
