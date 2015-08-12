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

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Audio;
using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;

namespace R.net
{

    /// <summary>
    /// This is the main type for your game.
    /// </summary>
    public class game : Game
    {
        GraphicsDeviceManager graphics;
        SpriteBatch spriteBatch;

        private const string _core = "snes9x_libretro.dll";
        int scale = 2;
        Wrapper _libretro;
        int _frames = 0;
        float _time = 0.0f;
        int _fps = 0;
        Texture2D _output;
        SystemAVInfo _avinfo;

        private int SampleRate = 44100;
        private int Channels = 2;
        private DynamicSoundEffectInstance _instance;

        private int SamplesPerBuffer = 3000;
        private float[,] _workingBuffer;
        private byte[] _xnaBuffer;

        public unsafe game()
        {
            graphics = new GraphicsDeviceManager(this);         
            Content.RootDirectory = "Content";
        }

        /// <summary>
        /// Allows the game to perform any initialization it needs to before starting to run.
        /// This is where it can query for any required services and load any non-graphic
        /// related content.  Calling base.Initialize will enumerate through any components
        /// and initialize them as well.
        /// </summary>
        protected override void Initialize()
        {
            // TODO: Add your initialization logic here
            base.Initialize();
        }

        /// <summary>
        /// LoadContent will be called once per game and is the place to load
        /// all of your content.
        /// </summary>
        protected override void LoadContent()
        {
            // Create a new SpriteBatch, which can be used to draw textures.
            spriteBatch = new SpriteBatch(GraphicsDevice);
            // TODO: use this.Content to load your game content here
            _libretro = new Wrapper(_core);
            _libretro.Init();
            _libretro.LoadGame("smw.sfc");
            _avinfo = _libretro.GetAVInfo();
            _output = new Texture2D(GraphicsDevice, (int)_avinfo.geometry.base_width, (int)_avinfo.geometry.base_height);


            SampleRate = (int)_libretro.GetAVInfo().timing.sample_rate;
            // Create DynamicSoundEffectInstance object and start it
            _instance = new DynamicSoundEffectInstance(SampleRate, Channels == 2 ? AudioChannels.Stereo : AudioChannels.Mono);
            _instance.Play();

            // Create buffers
            const int bytesPerSample = 2;
            _xnaBuffer = new byte[Channels * SamplesPerBuffer * bytesPerSample];
            _workingBuffer = new float[Channels, SamplesPerBuffer];

        }

        /// <summary>
        /// UnloadContent will be called once per game and is the place to unload
        /// game-specific content.
        /// </summary>
        protected override void UnloadContent()
        {
            // TODO: Unload any non ContentManager content here
        }

        /// <summary>
        /// Allows the game to run logic such as updating the world,
        /// checking for collisions, gathering input, and playing audio.
        /// </summary>
        /// <param name="gameTime">Provides a snapshot of timing values.</param>
        protected override void Update(GameTime gameTime)
        {
            if (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed || Keyboard.GetState().IsKeyDown(Keys.Escape))
                Exit();

            // TODO: Add your update logic here
            _libretro.Update();
            _time += (float)gameTime.ElapsedGameTime.TotalMilliseconds;

            // 1 Second has passed
            if (_time >= 1000.0f)
            {
                _fps = _frames;
                _frames = 0;
                _time = 0;
            }
            while (_instance.PendingBufferCount < 3)
                SubmitBuffer();

            base.Update(gameTime);
        }

        /// <summary>
        /// This is called when the game should draw itself.
        /// </summary>
        /// <param name="gameTime">Provides a snapshot of timing values.</param>
        protected override void Draw(GameTime gameTime)
        {

            if (graphics.PreferredBackBufferHeight != _avinfo.geometry.base_height * scale || graphics.PreferredBackBufferWidth != _avinfo.geometry.base_width * scale)
            {
                graphics.PreferredBackBufferHeight = (int)_avinfo.geometry.base_height * scale;
                graphics.PreferredBackBufferWidth = (int)_avinfo.geometry.base_width * scale;
                //graphics.SynchronizeWithVerticalRetrace = false;
                //this.IsFixedTimeStep = false;
                graphics.ApplyChanges();
            }
            GraphicsDevice.Clear(Color.CornflowerBlue);
            _frames++;
            // TODO: Add your drawing code here
            spriteBatch.Begin();

            this.Window.Title = _fps.ToString();
            _output.SetData<Color>(ProcessFramebuffer(_libretro.GetFramebuffer(), (uint)_libretro.GetAVInfo().geometry.base_width, (uint)_libretro.GetAVInfo().geometry.base_height));
            spriteBatch.Draw(_output, new Rectangle(0, 0, (int)_avinfo.geometry.base_width * scale, (int)_avinfo.geometry.base_height * scale), Color.White);
            spriteBatch.End();
            base.Draw(gameTime);
        }

        protected Color[] ProcessFramebuffer(Pixel[] frameBuffer, uint width, uint height)
        {
            Color[] image = new Color[width * height];
            if (frameBuffer != null)
            {
                image = new Color[width * height];

                for (int i = 0; i < width * height; i++)
                {
                    image[i] = new Color(frameBuffer[i].Red, frameBuffer[i].Green, frameBuffer[i].Blue);
                    frameBuffer[i] = null;
                }
            }
            return image;
        }

        private void FillWorkingBuffer()
        {

            IntPtr data; data = _libretro.GetSoundBuffer().data;
            for (int i = 0; i < SamplesPerBuffer; i++)
            {
                Int16 chunk = Marshal.ReadInt16(data);
                _workingBuffer[0, i] = chunk;
                data = data + (sizeof(Int16));

                chunk = Marshal.ReadInt16(data);
                _workingBuffer[1, i] = chunk;
                data = data + (sizeof(Int16));
            }
        }

        private void SubmitBuffer()
        {
            FillWorkingBuffer();
            ConvertBuffer(_workingBuffer, _xnaBuffer);
            _instance.SubmitBuffer(_xnaBuffer);
        }

        /// <summary>
        /// Converts a bi-dimensional (Channel, Samples) floating point buffer to a PCM (byte) buffer with interleaved channels
        /// </summary>
        private static void ConvertBuffer(float[,] from, byte[] to)
        {
            const int bytesPerSample = 2;
            int channels = from.GetLength(0);
            int bufferSize = from.GetLength(1);

            // Make sure the buffer sizes are correct
            System.Diagnostics.Debug.Assert(to.Length == bufferSize * channels * bytesPerSample, "Buffer sizes are mismatched.");

            for (int i = 0; i < bufferSize; i++)
            {
                for (int c = 0; c < channels; c++)
                {
                    // First clamp the value to the [-1.0..1.0] range
                    float floatSample = MathHelper.Clamp(from[c, i], -1.0f, 1.0f);

                    // Convert it to the 16 bit [short.MinValue..short.MaxValue] range
                    short shortSample = (short)(floatSample >= 0.0f ? floatSample * short.MaxValue : floatSample * short.MinValue * -1);

                    // Calculate the right index based on the PCM format of interleaved samples per channel [L-R-L-R]
                    int index = i * channels * bytesPerSample + c * bytesPerSample;

                    // Store the 16 bit sample as two consecutive 8 bit values in the buffer with regard to endian-ness
                    if (!BitConverter.IsLittleEndian)
                    {
                        to[index] = (byte)(shortSample >> 8);
                        to[index + 1] = (byte)shortSample;
                    }
                    else
                    {
                        to[index] = (byte)shortSample;
                        to[index + 1] = (byte)(shortSample >> 8);
                    }
                }
            }
        }


    }
}
