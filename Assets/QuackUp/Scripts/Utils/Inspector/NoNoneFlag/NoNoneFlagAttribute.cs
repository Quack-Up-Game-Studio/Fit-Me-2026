using System;

namespace QuackUp.Utils
{
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public class NoNoneFlagAttribute : Attribute
    {
        
    }
}