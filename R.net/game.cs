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
    }
}
