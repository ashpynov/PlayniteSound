using System;
using System.Runtime.InteropServices;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Playnite.SDK;

namespace PlayniteSounds.Players
{
    public class WMPMusicPlayer : IMusicPlayer
    {
        private static readonly ILogger Logger = LogManager.GetLogger();

        private MediaPlayer _mediaPlayer;
        private readonly MediaTimeline _timeLine;

        private void OnMediaEnded(object sender, EventArgs e)
        {
            MediaEnded?.Invoke(sender, e);
        }

        private void OnMediaFailed(object sender, ExceptionEventArgs e)
        {

            if (e.ErrorException.GetType() == typeof(System.Windows.Media.InvalidWmpVersionException))
            {
                Logger.Info($"Windows Media Player Legacy App is not installed. Fallback to SDL");
                throw e.ErrorException;
            }

            MediaFailed?.Invoke(sender, e);
        }

        public event EventHandler MediaEnded;
        public event EventHandler<ExceptionEventArgs> MediaFailed;
        public double Volume
        {
            get => _mediaPlayer?.Volume ?? 0;
            set
            {
                if (_mediaPlayer != null)
                {
                    _mediaPlayer.Volume = value;
                }
            }
        }

        public bool IsLoaded { get => _mediaPlayer?.Clock != null; }

        public bool IsActive { get => _mediaPlayer?.Clock?.CurrentState == ClockState.Active; }

        public string Source { get => _mediaPlayer?.Source?.LocalPath ?? string.Empty; }

        public TimeSpan? CurrentTime { get => TimeSpan.Zero; }

        private bool WMPIsInstalled()
        {
            try
            {
                Type wmpType = Type.GetTypeFromProgID("MediaPlayer.MediaPlayer.1");
                if (wmpType != null)
                {
                    dynamic wmpObject = Activator.CreateInstance(wmpType);
                    Marshal.ReleaseComObject(wmpObject);
                    return true;
                }
            }
            catch  { }
            return false;
        }
        public WMPMusicPlayer()
        {
            if (!WMPIsInstalled())
            {
                string error = $"Windows Media Player Legacy (App) is not installed.";
                Logger.Trace(error);
                throw new Exception(error);
            }
            _mediaPlayer = new MediaPlayer();
            _mediaPlayer.MediaEnded += OnMediaEnded;
            _mediaPlayer.MediaFailed += OnMediaFailed;
            _timeLine = new MediaTimeline();

            Logger.Trace("Using Windows Media Player Legacy (App)");
        }

        public void Destroy()
        {
            _mediaPlayer?.Close();
            _mediaPlayer = null;
        }

        public void Stop()
        {
            _mediaPlayer?.Clock?.Controller?.Stop();
        }

        public void Play()
        {
            _mediaPlayer?.Clock?.Controller?.Begin();
        }

        public void Load(string filePath)
        {
            _timeLine.Source = new Uri(filePath);
            _mediaPlayer.Clock = _timeLine.CreateClock();
        }

        public void Close()
        {
            if (_mediaPlayer?.Clock != null)
            {
                _mediaPlayer.Clock = null;
            }
            _mediaPlayer?.Close();
        }

        public void Seek(TimeSpan startFrom)
        {
            _mediaPlayer.Clock.Controller.Seek(startFrom, TimeSeekOrigin.BeginTime);
        }

        public void Pause()
        {
            _mediaPlayer?.Clock?.Controller?.Pause();
        }

        public void Resume()
        {
            _mediaPlayer?.Clock?.Controller?.Resume();
        }
    }
}
