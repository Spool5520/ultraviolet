﻿using System;
using System.IO;
using TwistedLogik.Nucleus;
using TwistedLogik.Ultraviolet;
using TwistedLogik.Ultraviolet.Content;
using TwistedLogik.Ultraviolet.Graphics;
using TwistedLogik.Ultraviolet.Graphics.Graphics2D;
using TwistedLogik.Ultraviolet.Graphics.Graphics2D.Text;
using TwistedLogik.Ultraviolet.OpenGL;
using UltravioletSample.Assets;
using UltravioletSample.Input;

namespace UltravioletSample
{
#if ANDROID
    [Android.App.Activity(Label = "Ultraviolet Sample 6", MainLauncher = true, ConfigurationChanges = 
        Android.Content.PM.ConfigChanges.Orientation | 
        Android.Content.PM.ConfigChanges.ScreenSize | 
        Android.Content.PM.ConfigChanges.KeyboardHidden)]
    public class Game : UltravioletActivity
#else
    public class Game : UltravioletApplication
#endif
    {
        public Game()
            : base("TwistedLogik", "Ultraviolet Sample 6")
        {

        }

        public static void Main(string[] args)
        {
            using (var game = new Game())
            {
                game.Run();
            }
        }

        protected override UltravioletContext OnCreatingUltravioletContext()
        {
            return new OpenGLUltravioletContext(this);
        }

        protected override void OnInitialized()
        {
            const String Archive = "UltravioletSample.Content.uvarc";

            if (GetType().Assembly.GetManifestResourceStream(Archive) != null)
            {
                SetFileSourceFromManifest(Archive);
            }
            else
            {
                if (Ultraviolet.Platform == UltravioletPlatform.Android)
                {
                    throw new InvalidOperationException("Unable to set the file system source archive.");
                }
            }

            base.OnInitialized();
        }

        protected override void OnUpdating(UltravioletTime time)
        {
            if (Ultraviolet.GetInput().GetActions().ExitApplication.IsPressed())
            {
                Exit();
            }
            base.OnUpdating(time);
        }

        protected override void OnDrawing(UltravioletTime time)
        {
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend);

            DrawAlignedText();
            DrawColoredAndStyledText();

            spriteBatch.End();

            base.OnDrawing(time);
        }

        private void DrawAlignedText()
        {
            var window = Ultraviolet.GetPlatform().Windows.GetPrimary();
            var width  = window.ClientSize.Width;
            var height = window.ClientSize.Height;

            var settingsTopLeft = new TextLayoutSettings(spriteFontSegoe, width, height, TextFlags.AlignTop | TextFlags.AlignLeft);
            textRenderer.Draw(spriteBatch, "Aligned top left", Vector2.Zero, Color.White, settingsTopLeft);

            var settingsTopCenter = new TextLayoutSettings(spriteFontSegoe, width, height, TextFlags.AlignTop | TextFlags.AlignCenter);
            textRenderer.Draw(spriteBatch, "Aligned top center", Vector2.Zero, Color.White, settingsTopCenter);

            var settingsTopRight = new TextLayoutSettings(spriteFontSegoe, width, height, TextFlags.AlignTop | TextFlags.AlignRight);
            textRenderer.Draw(spriteBatch, "Aligned top right", Vector2.Zero, Color.White, settingsTopRight);

            var settingsBottomLeft = new TextLayoutSettings(spriteFontSegoe, width, height, TextFlags.AlignBottom | TextFlags.AlignLeft);
            textRenderer.Draw(spriteBatch, "Aligned bottom left", Vector2.Zero, Color.White, settingsBottomLeft);

            var settingsBottomCenter = new TextLayoutSettings(spriteFontSegoe, width, height, TextFlags.AlignBottom | TextFlags.AlignCenter);
            textRenderer.Draw(spriteBatch, "Aligned bottom center", Vector2.Zero, Color.White, settingsBottomCenter);

            var settingsBottomRight = new TextLayoutSettings(spriteFontSegoe, width, height, TextFlags.AlignBottom | TextFlags.AlignRight);
            textRenderer.Draw(spriteBatch, "Aligned bottom right", Vector2.Zero, Color.White, settingsBottomRight);
        }

        private void DrawColoredAndStyledText()
        {
            var window = Ultraviolet.GetPlatform().Windows.GetPrimary();
            var width  = window.ClientSize.Width;
            var height = window.ClientSize.Height;

            if (textLayoutResult.Settings.Width != width || textLayoutResult.Settings.Height != height)
            {
                const string text = 
                    "Ultraviolet Formatting Commands\n" +
                    "\n" +
                    "||c:AARRGGBB| - Changes the color of text.\n" + 
                    "|c:FFFF0000|red|c| |c:FFFF8000|orange|c| |c:FFFFFF00|yellow|c| |c:FF00FF00|green|c| |c:FF0000FF|blue|c| |c:FF6F00FF|indigo|c| |c:FFFF00FF|magenta|c|\n" +
                    "\n" +
                    "||font:name| - Changes the current font.\n" +
                    "We can |font:segoe|transition to a completely different font|font| within a single line\n" +
                    "\n" +
                    "||b| and ||i| - Changes the current font style.\n" +
                    "|b|bold|b| |i|italic|i| |b||i|bold italic|i||b|\n" +
                    "\n" +
                    "||style:name| - Changes to a preset style.\n" +
                    "|style:preset1|this is preset1|style| |style:preset2|this is preset2|style|\n" +
                    "\n" +
                    "||icon:name| - Draws an icon in the text.\n" +
                    "[|icon:ok| OK] [|icon:cancel| Cancel]";

                var settings = new TextLayoutSettings(spriteFontGaramond, width, height, TextFlags.AlignMiddle | TextFlags.AlignCenter);
                textRenderer.CalculateLayout(text, textLayoutResult, settings);
            }

            textRenderer.Draw(spriteBatch, textLayoutResult, Vector2.Zero, Color.White);
        }

        protected override void OnLoadingContent()
        {
            this.content = ContentManager.Create("Content");

            LoadInputBindings();
            LoadContentManifests();

            this.spriteFontGaramond = this.content.Load<SpriteFont>(GlobalFontID.Garamond);
            this.spriteFontSegoe    = this.content.Load<SpriteFont>(GlobalFontID.SegoeUI);
            this.spriteBatch        = SpriteBatch.Create();
            this.textRenderer       = new TextRenderer();
            
            this.textRenderer.RegisterFont("segoe", this.spriteFontSegoe);
            this.textRenderer.RegisterFont("garamond", this.spriteFontGaramond);

            this.textRenderer.RegisterStyle("preset1", new TextStyle() { Color = Color.Lime, Font = "garamond", Bold = true });
            this.textRenderer.RegisterStyle("preset2", new TextStyle() { Color = Color.Red, Font = "garamond", Italic = true });

            var spriteOK     = this.content.Load<Sprite>(GlobalSpriteID.OK);
            var spriteCancel = this.content.Load<Sprite>(GlobalSpriteID.Cancel);

            this.textRenderer.RegisterIcon("ok", spriteOK[0]);
            this.textRenderer.RegisterIcon("cancel", spriteCancel[0]);

            base.OnLoadingContent();
        }

        protected override void OnShutdown()
        {
            SaveInputBindings();

            base.OnShutdown();
        }

        protected override void Dispose(Boolean disposing)
        {
            if (disposing)
            {
                SafeDispose.Dispose(content);
            }
            base.Dispose(disposing);
        }

        private String GetInputBindingsPath()
        {
            return Path.Combine(GetRoamingApplicationSettingsDirectory(), "InputBindings.xml");
        }

        private void LoadInputBindings()
        {
            Ultraviolet.GetInput().GetActions().Load(GetInputBindingsPath(), throwIfNotFound: false);
        }

        private void SaveInputBindings()
        {
            Ultraviolet.GetInput().GetActions().Save(GetInputBindingsPath());
        }

        private void LoadContentManifests()
        {
            var uvContent = Ultraviolet.GetContent();

            var contentManifestFiles = this.content.GetAssetFilePathsInDirectory("Manifests");
            uvContent.Manifests.Load(contentManifestFiles);

            uvContent.Manifests["Global"]["Fonts"].PopulateAssetLibrary(typeof(GlobalFontID));
            uvContent.Manifests["Global"]["Sprites"].PopulateAssetLibrary(typeof(GlobalSpriteID));
        }

        private ContentManager content;
        private SpriteFont spriteFontSegoe;
        private SpriteFont spriteFontGaramond;
        private SpriteBatch spriteBatch;
        private TextRenderer textRenderer;
        private readonly TextLayoutResult textLayoutResult = new TextLayoutResult();
    }
}
