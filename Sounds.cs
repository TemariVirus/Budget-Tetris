using System.Windows.Media;
using System.Windows.Threading;

namespace Tetris;

class Sound
{
    public const string SoundsFolder = @"Sounds\";
    
    public static readonly Sound BGM = new Sound("Korobeiniki Remix.wav"),
                                 SoftDrop = new Sound("bfall.wav"),
                                 HardDrop = new Sound("harddrop.wav"),
                                 TSpin = new Sound("tspin.wav"),
                                 PC = new Sound("pc.wav"),
                                 Hold = new Sound("hold.wav"),
                                 Slide = new Sound("move.wav"),
                                 Rotate = new Sound("rotate.wav"),
                                 LvlUp = new Sound("lvlup.wav"),
                                 Pause = new Sound("pause.wav");

    public static readonly Sound[] ClearSounds =
    {
        new Sound("single.wav"),
        new Sound("double.wav"),
        new Sound("triple.wav"),
        new Sound("tetris.wav")
    };

    public static bool IsMuted = false;
    public static Thread SoundThread { get; private set; }
    public static Dispatcher SoundDP { get => Dispatcher.FromThread(SoundThread); }

    public static double SFXVolume = 0.1;
    private static readonly Queue<MediaPlayer> MediaPlayerPool = new Queue<MediaPlayer>();

    private static double _BGMVolume = 0.04;
    public static double BGMVolume
    {
        get => _BGMVolume;
        set
        {
            _BGMVolume = value;
            SoundDP?.Invoke(() =>
            {
                BGMPlayer.Volume = value;
            });
        }
    }
    private static MediaPlayer BGMPlayer;

    
    public Uri Path { get; private set; }

    public Sound(string file_name)
    {
        if (SoundThread == null)
            InitSound();

        Path = new Uri(SoundsFolder + file_name, UriKind.Relative);
    }
    
    public static void InitSound()
    {
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
        if (IsMuted) return;

        MediaPlayer player = GetSFXPlayer();
        SoundDP.Invoke(() =>
        {
            player.Open(Path);
            player.Play();
        });
    }

    static MediaPlayer GetSFXPlayer()
    {
        // Create a new player if pool is dry
        if (!MediaPlayerPool.TryDequeue(out MediaPlayer player))
        {
            SoundDP.Invoke(() =>
                {
                    player = new MediaPlayer()
                    {
                        Volume = SFXVolume,
                    };
                    player.MediaEnded += (s, e) =>
                    {
                        player.Close();
                        MediaPlayerPool.Enqueue(player);
                    };
                });
        }

        return player;
    }
}
