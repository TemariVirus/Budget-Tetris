using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;

namespace Tetris
{
    public class Sound
    {
        private class SoundFile
        {
            const int MaxStreams = 6;

            private readonly string Path;
            private readonly List<int> Streams;

            public SoundFile(string path)
            {
                Path = path;
                Streams = new List<int>();
            }

            public SoundFile(Uri path) : this(path.ToString()) { }

            public int GetFreeStream()
            {
                // Check for free stream
                foreach (int stream in Streams)
                    if (BASS_ChannelIsActive(stream) != 1)
                        return stream;

                // Make new stream if none are free
                if (Streams.Count < MaxStreams)
                {
                    int stream = BASS_StreamCreateFileUnicode(false, Path, 0, 0, int.MinValue);
                    Streams.Add(stream);
                    return stream;
                }

                // Else return 0 (no handle)
                return 0;
            }
        }

        public const string SoundsFolder = @"Sounds\";

        private static bool _IsMuted = false;
        public static bool IsMuted
        {
            get => _IsMuted;
            set
            {
                _IsMuted = value;

                BASS_ChannelSetAttribute(BGMHandle,
                    2,
                    value ? 0 : _BGMVolume);
            }
        }

        private static float _SFXVolume;
        public static float SFXVolume
        {
            get => _SFXVolume;
            set => _SFXVolume = Math.Min(Math.Max(value, 0), 1);
        }

        private static float _BGMVolume;
        public static float BGMVolume
        {
            get => _BGMVolume;
            set
            {
                value = Math.Min(Math.Max(value, 0), 1);
                _BGMVolume = value;

                if (BGMHandle == 0) return;
                BASS_ChannelSetAttribute(BGMHandle,
                    2,
                    IsMuted ? 0 : BGMVolume);
            }
        }

        private static readonly int BGMHandle = 0;

        public static readonly Sound SoftDrop = new Sound("bfall.mp3"),
                                     HardDrop = new Sound("harddrop.mp3"),
                                     TSpin = new Sound("tspin.mp3"),
                                     PC = new Sound("pc.mp3"),
                                     GarbageSmall = new Sound("garbagesmall.mp3"),
                                     GarbageLarge = new Sound("garbagelarge.mp3"),
                                     Hold = new Sound("hold.mp3"),
                                     Slide = new Sound("move.mp3"),
                                     Rotate = new Sound("rotate.mp3"),
                                     Pause = new Sound("pause.mp3");

        public static readonly Sound[] ClearSounds =
        {
            new Sound("single.mp3"),
            new Sound("double.mp3"),
            new Sound("triple.mp3"),
            new Sound("tetris.mp3")
        };

        public bool HasSource { get; private set; }
        private readonly SoundFile Src;

        static Sound()
        {
            BASS_Init(-1, 44100, 0, IntPtr.Zero, IntPtr.Zero);

            // Play BGM looping
            string path = new Uri(SoundsFolder + "Korobeiniki Remix.mp3", UriKind.Relative).ToString();
            if (!File.Exists(path)) return;

            BGMHandle = BASS_StreamCreateFileUnicode(false, path, 0, 0, int.MinValue);
            if (BGMHandle == 0) return;

            BASS_ChannelSetAttribute(BGMHandle, 2, BGMVolume);
            BASS_ChannelFlags(BGMHandle, 4, 4);
            BASS_ChannelPlay(BGMHandle, true);
        }

        private Sound(string fileName)
        {
            string path = new Uri(SoundsFolder + fileName, UriKind.Relative).ToString();
            HasSource = File.Exists(path);
            Src = new SoundFile(path);
        }

        public void Play()
        {
            if (!HasSource || IsMuted) return;

            int handle = Src.GetFreeStream();
            // Return if can't get handle
            if (handle == 0) return;

            BASS_ChannelSetAttribute(handle, 2, SFXVolume);
            BASS_ChannelPlay(handle, true);
        }

        [DllImport("bass")]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool BASS_Init(int device, int freq, int flags, IntPtr win, IntPtr clsid);

        [DllImport("bass", EntryPoint = "BASS_StreamCreateFile")]
        static extern int BASS_StreamCreateFileUnicode([MarshalAs(UnmanagedType.Bool)] bool mem, [In][MarshalAs(UnmanagedType.LPWStr)] string file, long offset, long length, int flags);

        [DllImport("bass")]
        static extern int BASS_ChannelFlags(int handle, int flags, int mask);

        [DllImport("bass")]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool BASS_ChannelSetAttribute(int handle, int attrib, float value);

        [DllImport("bass")]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool BASS_ChannelPlay(int handle, [MarshalAs(UnmanagedType.Bool)] bool restart);

        [DllImport("bass")]
        static extern int BASS_ChannelIsActive(int handle);
    }
}
