using System;
using System.Timers;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Playnite.SDK;
using PlayniteSounds.Models;

namespace PlayniteSounds
{
    public class MusicFader
    {
        private static readonly ILogger Logger = LogManager.GetLogger();

        private readonly MediaPlayer player;
        private readonly PlayniteSoundsSettings settings;
        private Timer fadeTimer;

        private bool isFadingOut;
        private Action pauseAction;
        private Action stopAction;
        private Action playAction;

        private bool isPaused = false;

        DateTime lastCall;
        DateTime lastTickCall = default;

        public MusicFader(MediaPlayer player, PlayniteSoundsSettings settings)
        {
            this.player = player;
            this.settings = settings;
            fadeTimer = new Timer(50)
            {
                AutoReset = false
            };
            fadeTimer.Elapsed += (sender,e) => Application.Current?.Dispatcher?.Invoke(() => TimerTick());
        }

        public void Destroy()
        {
            fadeTimer?.Close();
            fadeTimer?.Dispose();
            fadeTimer = null;
        }


        void TimerTick()
        {
            double musicVolume = settings.MusicVolume / 100.0;
            double fadeFrequency = 20; // 10 steps per second

            if (lastTickCall != default)
            {
                double lastInterval = (DateTime.Now - lastTickCall).TotalMilliseconds;
                if (lastInterval != 0)
                {
                    fadeFrequency = 1000 / lastInterval;
                }
            }

            double fadeStep = musicVolume / (settings.FadeDuration * fadeFrequency);

            if (isFadingOut && player?.Clock?.CurrentState == ClockState.Active)
            {
                player.Volume = (player.Volume > fadeStep) ? player.Volume - fadeStep : 0;
                //Logger.Trace($"Fading Out: Volume: {player.Volume}");
            }
            else if (isFadingOut)
            {
                player.Volume = 0;
                //Logger.Trace($"Fading Out: Not playing");
            }
            else
            {
                player.Volume = (player.Volume + fadeStep < musicVolume ) ? player.Volume + fadeStep : musicVolume;
                //Logger.Trace($"Fading In: Volume: {player.Volume}");
            }

            if (player.Volume >= musicVolume)
            {
                Logger.Trace($"Fade In: Complete in {(DateTime.Now - lastCall).TotalMilliseconds} ms");
                return;
            }
            else if (player.Volume == 0 && pauseAction == null && playAction != null)
            {
                stopAction?.Invoke();
                playAction.Invoke();
                stopAction = playAction = null;
                isFadingOut = false;
                player.Volume = fadeStep;
                Logger.Trace($"Fade Out: done, in {(DateTime.Now - lastCall).TotalMilliseconds} ms, do fade In");
            }
            else if (player.Volume == 0 && (pauseAction != null || stopAction != null))
            {
                pauseAction?.Invoke();
                stopAction?.Invoke();
                isPaused = pauseAction != null;
                stopAction = pauseAction = null;
                Logger.Trace($"Fade Out: Complete in {(DateTime.Now - lastCall).TotalMilliseconds} ms");
                return;
            }
            fadeTimer?.Start();
            lastTickCall = DateTime.Now;
        }

        private void EnsureTimer()
        {
            lastCall = DateTime.Now;
            lastTickCall = default;
            fadeTimer?.Start();
        }

        public void Pause()
        {
            isFadingOut = true;
            pauseAction = player.Clock.Controller.Pause;
            EnsureTimer();
        }

        public void Switch(Action stopAction, Action playAction)
        {
            isFadingOut = true;
            this.playAction = playAction;
            this.stopAction = stopAction;
            EnsureTimer();
        }

        public void Resume()
        {
            if (!isPaused)
            {
                pauseAction = null;
                if (playAction == null && stopAction == null)
                {
                    isFadingOut = false;
                }
            }
            else
            {
                if (playAction != null || stopAction != null)
                {
                    stopAction?.Invoke();
                    playAction?.Invoke();
                    player.Volume = 0;
                    stopAction = playAction = null;
                }
                else
                {
                    player.Clock.Controller.Resume();
                }
                isPaused = false;
                isFadingOut = false;
            }
            EnsureTimer();
        }


    }
}
