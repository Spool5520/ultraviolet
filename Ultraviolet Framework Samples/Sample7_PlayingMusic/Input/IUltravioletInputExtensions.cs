﻿using TwistedLogik.Ultraviolet;
using TwistedLogik.Ultraviolet.Input;

namespace UltravioletSample.Sample7_PlayingMusic.Input
{
    public static class IUltravioletInputExtensions
    {
        public static GameInputActions GetActions(this IUltravioletInput @this)
        {
            return actions;
        }

        private static readonly UltravioletSingleton<GameInputActions> actions = 
            InputActionCollection.CreateSingleton<GameInputActions>();
    }
}