using p3ppc.InputLibrary.Configuration;
using Reloaded.Mod.Interfaces;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace p3ppc.InputLibrary

{
    public class Logging
    {
        public Config Configuration;
        private ILogger _logger;
        public Logging(Config configuration, ILogger logger)
        {
            // Initialise fields
            Configuration = configuration;
            _logger = logger;
        }
        public enum Input
        {
            Select = 0x1,
            Start = 0x8,
            Up = 0x10,
            Right = 0x20,
            Down = 0x40,
            Left = 0x80,
            LB = 0x400,
            RB = 0x800,
            Triangle = 0x1000,
            Circle = 0x2000,
            Cross = 0x4000,
            Square = 0x8000
        };

        public void LogDebug(string message)
        {
            if (Configuration.DebugEnabled)
                _logger.WriteLine($"[InputLibrary] {message}");
        }


        // Pushes an item to the beginning of the array, pushing everything else forward and removing the last element
        public void ArrayPush<T>(T[] array, T newItem)
        {
            for (int i = array.Length - 1; i > 0; i--)
            {
                array[i] = array[i - 1];
            }
            array[0] = newItem;
        }
    }
}
