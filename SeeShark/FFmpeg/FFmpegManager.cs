// Copyright (c) The Vignette Authors
// This file is part of SeeShark.
// SeeShark is licensed under the BSD 3-Clause License. See LICENSE for details.

using System.Runtime.InteropServices;
using FFmpeg.AutoGen;
using LF = SeeShark.FFmpeg.LibraryFlags;
using LibraryLoader = FFmpeg.AutoGen.Native.LibraryLoader;

namespace SeeShark.FFmpeg
{
    public static class FFmpegManager
    {
        /// <summary>
        /// Whether FFmpeg has been setup.
        /// </summary>
        public static bool IsFFmpegSetup { get; private set; } = false;

        /// <summary>
        /// Informative version string. It is usually the actual release version number or a git commit description. It has no fixed format and can change any time. It should never be parsed by code.
        /// <br/>
        /// Note: fetching this value will setup FFmpeg if it hasn't been done before.
        /// </summary>
        public static string FFmpegVersion
        {
            get
            {
                SetupFFmpeg();
                return ffmpeg.av_version_info();
            }
        }

        /// <summary>
        /// Root path for loading FFmpeg libraries.
        /// </summary>
        public static string FFmpegRootPath
        {
            get => ffmpeg.RootPath;
            private set => ffmpeg.RootPath = value;
        }

        private static av_log_set_callback_callback? logCallback;

        /// <summary>
        /// Setup FFmpeg: root path and logging.
        /// <br/>
        /// It will only setup FFmpeg once. Any non-first call will do nothing.
        /// <br/>
        /// SeeShark is designed such that this method is called whenever it is
        /// necessary to have FFmpeg ready.
        /// <br/>
        /// However, if you want to, you can still call it at the beginning of
        /// your program to customize your FFmpeg setup.
        /// </summary>
        /// <param name="rootPath">Root path for loading FFmpeg libraries.</param>
        /// <param name="logLevel">Log level for FFmpeg.</param>
        /// <param name="logColor">Color of the FFmpeg logs.</param>
        public static void SetupFFmpeg(FFmpegLogLevel logLevel, ConsoleColor logColor, params string[] paths)
        {
            if (IsFFmpegSetup)
                return;

            TrySetRootPath(LF.AVCodec | LF.AVDevice | LF.AVFormat | LF.SWScale, paths);
            SetupFFmpegLogging(logLevel, logColor);
            ffmpeg.avdevice_register_all();

            IsFFmpegSetup = true;
        }

        public static void SetupFFmpeg(params string[] paths) => SetupFFmpeg(FFmpegLogLevel.Panic, ConsoleColor.Yellow, paths);

        internal static unsafe void SetupFFmpegLogging(FFmpegLogLevel logLevel, ConsoleColor logColor)
        {
            ffmpeg.av_log_set_level((int)logLevel);

            // Do not convert to local function!
            logCallback = (p0, level, format, vl) =>
            {
                if (level > ffmpeg.av_log_get_level())
                    return;

                var lineSize = 1024;
                var lineBuffer = stackalloc byte[lineSize];
                var printPrefix = 1;

                ffmpeg.av_log_format_line(p0, level, format, vl, lineBuffer, lineSize, &printPrefix);
                var line = Marshal.PtrToStringAnsi((IntPtr)lineBuffer);

                // TODO: maybe make it possible to log this in any stream?
                Console.ForegroundColor = logColor;
                Console.Write(line);
                Console.ResetColor();
            };

            ffmpeg.av_log_set_callback(logCallback);
        }

        /// <summary>
        ///     Tries to load the native libraries from the set root path. <br/>
        ///     You can specify which libraries need to be loaded with LibraryFlags.
        ///     It will try to load all librares by default. <br/>
        ///     Ideally, you would want to only call this function once, before doing anything with FFmpeg.
        ///     If you try to do that later, it might unload all of your already loaded libraries and fail to provide them again.
        /// </summary>
        /// <returns>Whether it succeeded in loading all the requested libraries.</returns>
        public static bool CanLoadLibraries(LibraryFlags libraries = LibraryFlags.All, string path = "")
        {
            var validated = new List<string>();
            return libraries.ToStrings().All((lib) => canLoadLibrary(lib, path, validated));
        }

        // Note: recurses in a very similar way to the LoadLibrary() method.
        private static bool canLoadLibrary(string lib, string path, List<string> validated)
        {
            if (validated.Contains(lib))
                return true;

            var version = ffmpeg.LibraryVersionMap[lib];
            if (!canLoadNativeLibrary(path, lib, version))
                return false;

            validated.Add(lib);

            var dependencies = ffmpeg.LibraryDependenciesMap[lib];
            return dependencies.Except(validated).All((dep) => canLoadLibrary(dep, path, validated));
        }

        /// <summary>
        ///     Tries to set the RootPath to the first path in which it can find all the native libraries.
        ///     Ideally, you would want to only call this function once, before doing anything with FFmpeg.
        /// </summary>
        /// <remarks>
        ///     This function will not load the native libraries but merely check if they exist.
        /// </remarks>
        /// <param name="paths">Every path to try out. It will set the RootPath to the first one that works.</param>
        public static void TrySetRootPath(params string[] paths) => TrySetRootPath(paths);

        /// <summary>
        ///     Tries to set the RootPath to the first path in which it can find all the required native libraries.
        ///     Ideally, you would want to only call this function once, before doing anything with FFmpeg.
        /// </summary>
        /// <remarks>
        ///     This function will not load the native libraries but merely check if they exist.
        /// </remarks>
        /// <param name="requiredLibraries">The required libraries. If you don't need all of them, you can specify them here.</param>
        /// <param name="paths">Every path to try out. It will set the RootPath to the first one that works.</param>
        public static void TrySetRootPath(LibraryFlags requiredLibraries, params string[] paths)
        {
            try
            {
                ffmpeg.RootPath = paths.First((path) => CanLoadLibraries(requiredLibraries, path));
            }
            catch (InvalidOperationException)
            {
                var pathList = paths.Aggregate((a, b) => $"'{a}', '{b}'");
                throw new InvalidOperationException($"Couldn't find native libraries in the following paths: {pathList}");
            }
        }

        /// <summary>
        ///     Checks if it can load a native library using platform naming conventions.
        /// </summary>
        /// <param name="path">Path of the library.</param>
        /// <param name="libraryName">Name of the library.</param>
        /// <param name="version">Version of the library.</param>
        /// <returns>Whether it found the native library in the path.</returns>
        private static bool canLoadNativeLibrary(string path, string libraryName, int version)
        {
            var nativeLibraryName = LibraryLoader.GetNativeLibraryName(libraryName, version);
            var fullName = Path.Combine(path, nativeLibraryName);
            return File.Exists(fullName);
        }
    }
}
