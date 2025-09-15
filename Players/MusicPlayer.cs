namespace PlayniteSounds.Players
{
    public class MusicPlayer
    {
        static public IMusicPlayer Create()
        {
            try
            {
                return new WMPMusicPlayer();
            }
            catch
            {
                return new SDL2MusicPlayer();
            }
        }
    }
}
