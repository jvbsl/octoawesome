﻿using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using OctoAwesome.Components;
using System;
using System.Linq;

namespace OctoAwesomeDX
{
    /// <summary>
    /// This is the main type for your game
    /// </summary>
    public class OctoGame : Game
    {

        GraphicsDeviceManager graphics;

        CameraComponent egoCamera;
        InputComponent input;
        SceneComponent render3d;
        WorldComponent world;
        HudComponent hud;

        public OctoGame()
            : base()
        {
            graphics = new GraphicsDeviceManager(this);
            Content.RootDirectory = "Content";
            this.Window.Title = "OctoAwesome";
            graphics.PreferredBackBufferWidth = 1280;
            graphics.PreferredBackBufferHeight = 720;
            this.IsMouseVisible = false;

            this.TargetElapsedTime = new TimeSpan(0, 0, 0, 0, 15);

            input = new InputComponent(this);
            input.UpdateOrder = 1;
            Components.Add(input);

            world = new WorldComponent(this, input);
            world.UpdateOrder = 2;
            Components.Add(world);

            egoCamera = new CameraComponent(this, world);
            egoCamera.UpdateOrder = 3;
            Components.Add(egoCamera);

            render3d = new SceneComponent(this, world, egoCamera);
            render3d.DrawOrder = 1;
            Components.Add(render3d);

            hud = new HudComponent(this, world);
            hud.DrawOrder = 2;
            Components.Add(hud);
        }
    }
}