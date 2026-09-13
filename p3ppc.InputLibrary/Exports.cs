using System;
using p3ppc.InputLibrary.Interfaces;
using Reloaded.Mod.Interfaces;

namespace p3ppc.InputLibrary
{
    public sealed class Exports : IExports
    {
        public Type[] GetTypes()
        {
            return new[]
            {
                typeof(IInputHook)
            };
        }
    }
}
