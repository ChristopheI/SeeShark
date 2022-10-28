// Copyright (c) The Vignette Authors
// This file is part of SeeShark.
// SeeShark is licensed under the BSD 3-Clause License. See LICENSE for details.

using System.Collections.Generic;
using FFmpeg.AutoGen;
using SeeShark.Device;

namespace SeeShark
{
    /// <summary>
    /// Options to give to a <see cref="VideoStreamDecoder" /> for it to feed them to FFmpeg when opening a video input stream.
    /// We can indeed open an input in different ways. For example, you might want to open your camera at a different resolution, or change the input format.
    /// </summary>
    /// <remarks>
    /// Some examples of input options are:
    /// <list type="bullet">
    /// <item>https://ffmpeg.org/ffmpeg-devices.html#video4linux2_002c-v4l2</item>
    /// <item>https://ffmpeg.org/ffmpeg-devices.html#dshow</item>
    /// <item>https://ffmpeg.org/ffmpeg-devices.html#avfoundation</item>
    /// </list>
    /// </remarks>
    public class VideoInputOptions
    {
        /// <summary>
        /// To request a specific resolution of the video stream.
        /// </summary>
        /// <remarks>
        /// The underlying driver will change it back to a compatible resolution.
        /// </remarks>
        /// <value>(width, height)</value>
        public (int, int)? VideoSize { get; set; }
        /// <summary>
        /// To request a specific framerate for the video stream.
        /// </summary>
        /// <remarks>
        /// The underlying driver will change it back to a compatible framerate.
        /// </remarks>
        public AVRational? Framerate { get; set; }
        /// <summary>
        /// To request a specific input format for the video stream.
        /// On all platform (except Windows), if the video stream is raw, it is the name of its pixel format, otherwise it is the name of its codec.
        /// On Windows, contains only the pixel format. (can be null/empty)
        /// </summary>
        public string? InputFormat { get; set; }
        /// <summary>
        /// Used on Windows only, contains the codec (can be null/empty)
        /// If VCodec is not null/empty, InputFormat is null/empty (and the opposite)
        /// </summary>
        public string? VCodec { get; set; }

        /// <summary>
        /// Combines all properties into a dictionary of options that FFmpeg can use.
        /// </summary>
        public virtual IDictionary<string, string> ToAVDictOptions(DeviceInputFormat deviceFormat)
        {
            Dictionary<string, string> dict = new();

            if (VideoSize != null)
            {
                (int width, int height) = VideoSize.Value;
                dict.Add("video_size", $"{width}x{height}");
            }

            if (Framerate != null)
                dict.Add("framerate", $"{Framerate.Value.num}/{Framerate.Value.den}");

            // I have no idea why "YUYV" specifically is like this...
            if (InputFormat != null)
            {
                string key = deviceFormat == DeviceInputFormat.DShow ? "pixel_format" : "input_format";
                dict.Add(key, InputFormat == "YUYV" ? "yuv422p" : InputFormat.ToLower());
            }

            if ((VCodec != null) && (deviceFormat == DeviceInputFormat.DShow))
            {
                dict.Add("vcodec", VCodec);
            }

            return dict;
        }

        public override string ToString()
        {
            string s;
            if (VCodec != null)
            {
                s = $"VCodec:{VCodec} {VideoSize}";
            }
            else
            {
                s = $"InputFormat:{InputFormat} {VideoSize}";
            }

            if (Framerate != null)
            {
                double fps = ffmpeg.av_q2d(Framerate.Value);
                s += $" - {fps:0.000} fps";
            }
            return s;
        }
    }
}
