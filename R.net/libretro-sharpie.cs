/*  R.net project
 *  Copyright (C) 2010-2015 - Andrés Suárez
 *  Copyright (C) 2010-2011 - Iván Fernandez
 *
 *  libretro.net is free software: you can redistribute it and/or modify it under the terms
 *  of the GNU General Public License as published by the Free Software Found-
 *  ation, either version 3 of the License, or (at your option) any later version.
 *
 *  libretro.net is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY;
 *  without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR
 *  PURPOSE.  See the GNU General Public License for more details.
 *
 *  You should have received a copy of the GNU General Public License along with libretro.net.
 *  If not, see <http://www.gnu.org/licenses/>.
 */


using System;
using System.IO;
using System.Runtime.InteropServices;


namespace R.net
{

    public enum PixelFormat
    {
        // 0RGB1555, native endian. 0 bit must be set to 0.
        // This pixel format is default for compatibility concerns only.
        // If a 15/16-bit pixel format is desired, consider using RGB565.
        RETRO_PIXEL_FORMAT_0RGB1555 = 0,

        // XRGB8888, native endian. X bits are ignored.
        RETRO_PIXEL_FORMAT_XRGB8888 = 1,

        // RGB565, native endian. This pixel format is the recommended format to use if a 15/16-bit format is desired
        // as it is the pixel format that is typically available on a wide range of low-power devices.
        // It is also natively supported in APIs like OpenGL ES.
        RETRO_PIXEL_FORMAT_RGB565 = 2,

        // Ensure sizeof() == sizeof(int).
        RETRO_PIXEL_FORMAT_UNKNOWN = int.MaxValue
    }

    //Shouldn't be part of the wrapper, will remove later
    [StructLayout(LayoutKind.Sequential)]
    public class Pixel
    {
        public float Alpha;
        public float Red;
        public float Green;
        public float Blue;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct SystemAVInfo
    {
        public Geometry geometry;
        public Timing timing;
    }

    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct GameInfo
    {
        public char* path;
        public void* data;
        public uint size;
        public char* meta;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct Geometry
    {
        public uint base_width;
        public uint base_height;
        public uint max_width;
        public uint max_height;
        public float aspect_ratio;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct Timing
    {
        public double fps;
        public double sample_rate;
    }

    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct SystemInfo
    {

        public char* library_name;
        public char* library_version;
        public char* valid_extensions;

        [MarshalAs(UnmanagedType.U1)]
        public bool need_fullpath;

        [MarshalAs(UnmanagedType.U1)]
        public bool block_extract;
    }

    public unsafe class DLLHandler
    {
        [DllImport("kernel32.dll")]
        private static extern IntPtr LoadLibrary(string dllToLoad);

        [DllImport("kernel32.dll")]
        private static extern IntPtr GetProcAddress(IntPtr hModule, string procedureName);

        [DllImport("kernel32.dll")]
        private static extern bool FreeLibrary(IntPtr hModule);

        private static IntPtr dllPointer = IntPtr.Zero;

        public static bool LoadCore(string dllName)
        {
            dllPointer = LoadLibrary(dllName);
            //oh dear, error handling here
            if (dllPointer == IntPtr.Zero)
            {
                return false;
            }

            return true;
        }

        public static T GetMethod<T>(string functionName) where T : class
        {
            if (dllPointer == IntPtr.Zero)
            {
                return default(T);
            }

            IntPtr pAddressOfFunctionToCall = GetProcAddress(dllPointer, functionName);

            if (pAddressOfFunctionToCall == IntPtr.Zero)
            {
                return default(T);
            }

            return Marshal.GetDelegateForFunctionPointer(pAddressOfFunctionToCall, typeof(T)) as T;
        }
    }

    public class Environment
    {
        public const uint RETRO_ENVIRONMENT_SET_ROTATION = 1;
        public const uint RETRO_ENVIRONMENT_GET_OVERSCAN = 2;
        public const uint RETRO_ENVIRONMENT_GET_CAN_DUPE = 3;
        public const uint RETRO_ENVIRONMENT_GET_VARIABLE = 4;
        public const uint RETRO_ENVIRONMENT_SET_VARIABLES = 5;
        public const uint RETRO_ENVIRONMENT_SET_MESSAGE = 6;
        public const uint RETRO_ENVIRONMENT_SHUTDOWN = 7;
        public const uint RETRO_ENVIRONMENT_SET_PERFORMANCE_LEVEL = 8;
        public const uint RETRO_ENVIRONMENT_GET_SYSTEM_DIRECTORY = 9;
        public const uint RETRO_ENVIRONMENT_SET_PIXEL_FORMAT = 10;
        public const uint RETRO_ENVIRONMENT_SET_INPUT_DESCRIPTORS = 11;
        public const uint RETRO_ENVIRONMENT_SET_KEYBOARD_CALLBACK = 12;
    }

    public class Wrapper
    {
        private PixelFormat pixelFormat;
        private bool requiresFullPath;
        private SystemAVInfo av;
        Pixel[] frameBuffer;

        //Prevent GC on delegates as long as the wrapper is running
        private Libretro.RetroEnvironmentDelegate _environment;
        private Libretro.RetroVideoRefreshDelegate _videoRefresh;
        private Libretro.RetroAudioSampleDelegate _audioSample;
        private Libretro.RetroAudioSampleBatchDelegate _audioSampleBatch;
        private Libretro.RetroInputPollDelegate _inputPoll;
        private Libretro.RetroInputStateDelegate _inputState;

        public Wrapper(string coreToLoad)
        {
            Libretro.InitializeLibrary(coreToLoad);
        }

        public unsafe void Init()
        {
            int _apiVersion = Libretro.RetroApiVersion();
            SystemInfo info = new SystemInfo();
            Libretro.RetroGetSystemInfo(ref info);

            string _coreName = Marshal.PtrToStringAnsi((IntPtr)info.library_name);
            string _coreVersion = Marshal.PtrToStringAnsi((IntPtr)info.library_version);
            string _validExtensions = Marshal.PtrToStringAnsi((IntPtr)info.valid_extensions);
            requiresFullPath = info.need_fullpath;
            bool _blockExtract = info.block_extract;

            Console.WriteLine("Core information:");
            Console.WriteLine("API Version: " + _apiVersion);
            Console.WriteLine("Core Name: " + _coreName);
            Console.WriteLine("Core Version: " + _coreVersion);
            Console.WriteLine("Valid Extensions: " + _validExtensions);
            Console.WriteLine("Block Extraction: " + _blockExtract);
            Console.WriteLine("Requires Full Path: " + requiresFullPath);

            _environment = new Libretro.RetroEnvironmentDelegate(RetroEnvironment);
            _videoRefresh = new Libretro.RetroVideoRefreshDelegate(RetroVideoRefresh);
            _audioSample = new Libretro.RetroAudioSampleDelegate(RetroAudioSample);
            _audioSampleBatch = new Libretro.RetroAudioSampleBatchDelegate(RetroAudioSampleBatch);
            _inputPoll = new Libretro.RetroInputPollDelegate(RetroInputPoll);
            _inputState = new Libretro.RetroInputStateDelegate(RetroInputState);

            Console.WriteLine("\nSetting up environment:");

            Libretro.RetroSetEnvironment(_environment);
            Libretro.RetroSetVideoRefresh(_videoRefresh);
            Libretro.RetroSetAudioSample(_audioSample);
            Libretro.RetroSetAudioSampleBatch(_audioSampleBatch);
            Libretro.RetroSetInputPoll(_inputPoll);
            Libretro.RetroSetInputState(_inputState);

            Libretro.RetroInit();
        }

        public bool Update()
        {
            Libretro.RetroRun();
            return true;
        }

        public SystemAVInfo GetAVInfo()
        {
            return av;
        }

        public Pixel[] GetFramebuffer()
        {
            return frameBuffer;
        }

        private unsafe void RetroVideoRefresh(void* data, uint width, uint height, uint pitch)
        {

            // Process Pixels one by one for now...this is not the best way to do it 
            // should be using memory streams or something

            //Declare the pixel buffer to pass on to the renderer
            frameBuffer = new Pixel[width * height];

            //Get the array from unmanaged memory as a pointer
            IntPtr pixels = (IntPtr)data;
            //Gets The pointer to the row start to use with the pitch
            IntPtr rowStart = pixels;

            //Get the size to move the pointer
            Int32 size = 0;

            uint i = 0;
            uint j = 0;

            switch (pixelFormat)
            {
                case PixelFormat.RETRO_PIXEL_FORMAT_0RGB1555:
                    size = Marshal.SizeOf(typeof(Int16));
                    for (i = 0; i < height; i++)
                    {
                        for (j = 0; j < width; j++)
                        {
                            Int16 packed = Marshal.ReadInt16(pixels);
                            frameBuffer[i * width + j] = new Pixel()
                            {
                                Alpha = 1
                                ,
                                Red = ((packed >> 10) & 0x001F) / 31.0f
                                ,
                                Green = ((packed >> 5) & 0x001F) / 31.0f
                                ,
                                Blue = (packed & 0x001F) / 31.0f
                            };

                            pixels = (IntPtr)((int)pixels + size);
                        }
                        pixels = (IntPtr)((int)rowStart + pitch);
                        rowStart = pixels;
                    }
                    break;
                case PixelFormat.RETRO_PIXEL_FORMAT_XRGB8888:
                    size = Marshal.SizeOf(typeof(Int32));
                    for (i = 0; i < height; i++)
                    {
                        for (j = 0; j < width; j++)
                        {
                            Int32 packed = Marshal.ReadInt32(pixels);
                            frameBuffer[i * width + j] = new Pixel()
                            {
                                Alpha = 1
                                ,
                                Red = ((packed >> 16) & 0x00FF) / 255.0f
                                ,
                                Green = ((packed >> 8) & 0x00FF) / 255.0f
                                ,
                                Blue = (packed & 0x00FF) / 255.0f

                            };

                            pixels = (IntPtr)((int)pixels + size);
                        }
                        pixels = (IntPtr)((int)rowStart + pitch);
                        rowStart = pixels;

                    }
                    break;
                case PixelFormat.RETRO_PIXEL_FORMAT_RGB565:
                    size = Marshal.SizeOf(typeof(Int16));
                    for (i = 0; i < height; i++)
                    {
                        for (j = 0; j < width; j++)
                        {
                            Int16 packed = Marshal.ReadInt16(pixels);
                            frameBuffer[i * width + j] = new Pixel()
                            {
                                Alpha = 1
                                ,
                                Red = ((packed >> 11) & 0x001F) / 31.0f
                                ,
                                Green = ((packed >> 5) & 0x003F) / 63.0f
                                ,
                                Blue = (packed & 0x001F) / 31.0f
                            };
                            packed = 0;
                            pixels = (IntPtr)(pixels + size);
                        }
                        pixels = (IntPtr)(rowStart.ToInt64() + pitch);
                        rowStart = pixels;
                    }
                    break;
                case PixelFormat.RETRO_PIXEL_FORMAT_UNKNOWN:
                    frameBuffer = null;
                    break;
            }            
        }

        private unsafe void RetroAudioSample(Int16 left, Int16 right)
        {
            return;
        }

        private unsafe void RetroAudioSampleBatch(Int16* data, uint frames)
        {
            return;
        }

        private unsafe void RetroInputPoll()
        {
            return;
        }

        private unsafe Int16 RetroInputState(uint port, uint device, uint index, uint id)
        {
            return 0;
        }

        private unsafe bool RetroEnvironment(uint cmd, void* data)
        {

            switch (cmd)
            {
                case Environment.RETRO_ENVIRONMENT_GET_OVERSCAN:
                    break;
                case Environment.RETRO_ENVIRONMENT_GET_VARIABLE:
                    break;
                case Environment.RETRO_ENVIRONMENT_SET_VARIABLES:
                    break;
                case Environment.RETRO_ENVIRONMENT_SET_MESSAGE:
                    break;
                case Environment.RETRO_ENVIRONMENT_SET_ROTATION:
                    break;
                case Environment.RETRO_ENVIRONMENT_SHUTDOWN:
                    break;
                case Environment.RETRO_ENVIRONMENT_SET_PERFORMANCE_LEVEL:
                    break;
                case Environment.RETRO_ENVIRONMENT_GET_SYSTEM_DIRECTORY:
                    break;
                case Environment.RETRO_ENVIRONMENT_SET_PIXEL_FORMAT:
                    pixelFormat = *(PixelFormat*)data;
                    switch (pixelFormat)
                    {
                        case PixelFormat.RETRO_PIXEL_FORMAT_0RGB1555:

                            break;
                        case PixelFormat.RETRO_PIXEL_FORMAT_RGB565:

                            break;
                        case PixelFormat.RETRO_PIXEL_FORMAT_XRGB8888:

                            break;
                        default:
                            return false;
                    }
                    break;
                case Environment.RETRO_ENVIRONMENT_SET_INPUT_DESCRIPTORS:
                    break;
                case Environment.RETRO_ENVIRONMENT_SET_KEYBOARD_CALLBACK:
                    break;
                default:
                    return false;
            }
            return true;
        }

        private unsafe char* StringToChar(string s)
        {
            IntPtr p = Marshal.StringToHGlobalUni(s);
            return (char*)(p.ToPointer());
        }

        private unsafe GameInfo LoadGameInfo(string file)
        {
            GameInfo gameInfo = new GameInfo();


            {
                FileStream stream = new FileStream(file, FileMode.Open);

                byte[] data = new byte[stream.Length];
                stream.Read(data, 0, (int)stream.Length);
                IntPtr arrayPointer = Marshal.AllocHGlobal(data.Length * Marshal.SizeOf(typeof(byte)));
                Marshal.Copy(data, 0, arrayPointer, data.Length);


                gameInfo.path = StringToChar(file);
                gameInfo.size = (uint)data.Length;
                gameInfo.data = arrayPointer.ToPointer();

                stream.Close();
            }
            return gameInfo;
        }

        public unsafe bool LoadGame(string gamePath)
        {
            GameInfo gameInfo = LoadGameInfo(gamePath);
            bool ret = Libretro.RetroLoadGame(ref gameInfo);

            Console.WriteLine("\nSystem information:");

            av = new SystemAVInfo();
            Libretro.RetroGetSystemAVInfo(ref av);

            Console.WriteLine("Geometry:");
            Console.WriteLine("Base width: " + av.geometry.base_width);
            Console.WriteLine("Base height: " + av.geometry.base_height);
            Console.WriteLine("Max width: " + av.geometry.max_width);
            Console.WriteLine("Max height: " + av.geometry.max_height);
            Console.WriteLine("Aspect ratio: " + av.geometry.aspect_ratio);
            Console.WriteLine("Geometry:");
            Console.WriteLine("Target fps: " + av.timing.fps);
            Console.WriteLine("Sample rate " + av.timing.sample_rate);

            return true;
        }
    }

    public unsafe class Libretro
    {

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate int RetroApiVersionDelegate();
        public static RetroApiVersionDelegate RetroApiVersion;

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate void RetroInitDelegate();
        public static RetroInitDelegate RetroInit;

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate void RetroGetSystemInfoDelegate(ref SystemInfo info);
        public static RetroGetSystemInfoDelegate RetroGetSystemInfo;

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate void RetroGetSystemAVInfoDelegate(ref SystemAVInfo info);
        public static RetroGetSystemAVInfoDelegate RetroGetSystemAVInfo;

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate bool RetroLoadGameDelegate(ref GameInfo game);
        public static RetroLoadGameDelegate RetroLoadGame;

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate void RetroSetVideoRefreshDelegate(RetroVideoRefreshDelegate r);
        public static RetroSetVideoRefreshDelegate RetroSetVideoRefresh;

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate void RetroSetAudioSampleDelegate(RetroAudioSampleDelegate r);
        public static RetroSetAudioSampleDelegate RetroSetAudioSample;

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate void RetroSetAudioSampleBatchDelegate(RetroAudioSampleBatchDelegate r);
        public static RetroSetAudioSampleBatchDelegate RetroSetAudioSampleBatch;

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate void RetroSetInputPollDelegate(RetroInputPollDelegate r);
        public static RetroSetInputPollDelegate RetroSetInputPoll;

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate void RetroSetInputStateDelegate(RetroInputStateDelegate r);
        public static RetroSetInputStateDelegate RetroSetInputState;

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate bool RetroSetEnvironmentDelegate(RetroEnvironmentDelegate r);
        public static RetroSetEnvironmentDelegate RetroSetEnvironment;

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate void RetroRunDelegate();
        public static RetroRunDelegate RetroRun;

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate void RetroDeInitDelegate();
        public static RetroDeInitDelegate RetroDeInit;

        //typedef void (*retro_video_refresh_t)(const void *data, unsigned width, unsigned height, size_t pitch);
        public unsafe delegate void RetroVideoRefreshDelegate(void* data, uint width, uint height, uint pitch);

        //typedef void (*retro_audio_sample_t)(int16_t left, int16_t right);
        public unsafe delegate void RetroAudioSampleDelegate(Int16 left, Int16 right);

        //typedef size_t (*retro_audio_sample_batch_t)(const int16_t *data, size_t frames);
        public unsafe delegate void RetroAudioSampleBatchDelegate(Int16* data, uint frames);

        //typedef void (*retro_input_poll_t)(void);
        public delegate void RetroInputPollDelegate();

        //typedef int16_t (*retro_input_state_t)(unsigned port, unsigned device, unsigned index, unsigned id);
        public delegate Int16 RetroInputStateDelegate(uint port, uint device, uint index, uint id);

        //typedef bool (*retro_environment_t)(unsigned cmd, void *data);
        public unsafe delegate bool RetroEnvironmentDelegate(uint cmd, void* data);

        public static void InitializeLibrary(string dllName)
        {
            DLLHandler.LoadCore(dllName);

            RetroApiVersion = DLLHandler.GetMethod<RetroApiVersionDelegate>("retro_api_version");
            RetroInit = DLLHandler.GetMethod<RetroInitDelegate>("retro_init");
            RetroGetSystemInfo = DLLHandler.GetMethod<RetroGetSystemInfoDelegate>("retro_get_system_info");
            RetroGetSystemAVInfo = DLLHandler.GetMethod<RetroGetSystemAVInfoDelegate>("retro_get_system_av_info");
            RetroLoadGame = DLLHandler.GetMethod<RetroLoadGameDelegate>("retro_load_game");
            RetroSetVideoRefresh = DLLHandler.GetMethod<RetroSetVideoRefreshDelegate>("retro_set_video_refresh");
            RetroSetAudioSample = DLLHandler.GetMethod<RetroSetAudioSampleDelegate>("retro_set_audio_sample");
            RetroSetAudioSampleBatch = DLLHandler.GetMethod<RetroSetAudioSampleBatchDelegate>("retro_set_audio_sample_batch");
            RetroSetInputPoll = DLLHandler.GetMethod<RetroSetInputPollDelegate>("retro_set_input_poll");
            RetroSetInputState = DLLHandler.GetMethod<RetroSetInputStateDelegate>("retro_set_input_state");
            RetroSetEnvironment = DLLHandler.GetMethod<RetroSetEnvironmentDelegate>("retro_set_environment");
            RetroRun = DLLHandler.GetMethod<RetroRunDelegate>("retro_run");
            RetroDeInit = DLLHandler.GetMethod<RetroDeInitDelegate>("retro_deinit");
        }
    }

}
