using System;
using System.Diagnostics;

namespace GifMaker
{
    class FFMpegHandler
    {
        public static string ExecuteFFMpeg(string arguments)
        {
            try
            {
                Process process = Process.Start("cmd.exe", $@"/k ffmpeg.exe {arguments}");
            }
            catch (Exception e)
            {
                return e.ToString();
            }
            return "Success";
        }
    }
}
